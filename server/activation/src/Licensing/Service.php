<?php
declare(strict_types=1);
final class LicenseService {
    public function __construct(private readonly PDO $db,private readonly Tokens $tokens){}
    private function device(array $hardware,int $now): array {
        $c=$hardware['components'];
        $rows=query($this->db,'SELECT * FROM devices WHERE system_uuid=? OR baseboard=? OR combined=?',[$c['systemUuid']??null,$c['baseboard']??null,$hardware['combined']])->fetchAll();
        $matches=array_values(array_filter($rows,fn($r)=>Fingerprint::matches(json_decode($r['components_json'],true,32,JSON_THROW_ON_ERROR),$hardware)));
        if(count($matches)>1)throw new ApiError('DEVICE_CHANGED',409);
        if($matches){$r=$matches[0];query($this->db,'UPDATE devices SET last_seen_utc=? WHERE id=?',[$now,$r['id']]);return $r;}
        $id=bin2hex(random_bytes(16));
        query($this->db,'INSERT INTO devices(id,fingerprint_version,combined,system_uuid,baseboard,bios,machine_guid,components_json,first_seen_utc,last_seen_utc) VALUES(?,1,?,?,?,?,?,?,?,?)',[$id,$hardware['combined'],$c['systemUuid']??null,$c['baseboard']??null,$c['bios']??null,$c['machineGuid']??null,json_encode($hardware,JSON_THROW_ON_ERROR),$now,$now]);
        return ['id'=>$id,'components_json'=>json_encode($hardware,JSON_THROW_ON_ERROR)];
    }
    public function handle(string $action,array $request,string $correlation): array {
        if(($request['version']??null)!==1||($request['product']??'')!=='meshme360')throw new ApiError('UPDATE_REQUIRED',400);
        if(!is_array($request['hardware']??null))throw new ApiError('DEVICE_CHANGED',422);
        Fingerprint::validate($request['hardware']);$h=$request['hardware'];
        $version=$request['appVersion']??'';if(!is_string($version)||!preg_match('/^[0-9A-Za-z.+_-]{1,40}$/D',$version))throw new ApiError('UPDATE_REQUIRED');
        $now=time();$this->db->beginTransaction();
        try {
            // All mutations, including admin changes, lock this product first. This also serializes tolerant device recognition/trial creation.
            $product=query($this->db,"SELECT * FROM products WHERE code='meshme360' FOR UPDATE")->fetch();
            if(!$product||!$product['active'])throw new ApiError('SERVICE_UNAVAILABLE',503);
            if($action==='trial/start'){
                $device=$this->device($h,$now);
                $trial=query($this->db,'SELECT * FROM trials WHERE product_id=? AND device_id=?',[$product['id'],$device['id']])->fetch();
                if(!$trial){$id=bin2hex(random_bytes(16));query($this->db,'INSERT INTO trials(id,product_id,device_id,started_utc,expires_utc,last_validated_utc) VALUES(?,?,?,?,?,?)',[$id,$product['id'],$device['id'],$now,$now+604800,$now]);$trial=['id'=>$id,'started_utc'=>$now,'expires_utc'=>$now+604800,'status'=>'active'];event($this->db,'trial.started',$id,'api',$correlation);}
                if($trial['status']!=='active'||(int)$trial['expires_utc']<=$now)throw new ApiError('TRIAL_EXPIRED',403);
                $result=$this->entitlement('trial',$trial['id'],$h,(int)$trial['started_utc'],(int)$trial['expires_utc'],(int)$trial['expires_utc']);
            }elseif($action==='activate'){
                if(!is_string($request['key']??null)||!Keys::check($request['key']))throw new ApiError('INVALID_KEY');
                $key=query($this->db,'SELECT * FROM license_keys WHERE product_id=? AND key_hash=? FOR UPDATE',[$product['id'],hash('sha256',Keys::normalize($request['key']))])->fetch();
                $this->checkKey($key,$now);$device=$this->device($h,$now);
                $activation=query($this->db,'SELECT * FROM activations WHERE license_key_id=? AND device_id=?',[$key['id'],$device['id']])->fetch();
                $count=(int)query($this->db,"SELECT COUNT(*) FROM activations WHERE license_key_id=? AND status='active'",[$key['id']])->fetchColumn();
                if((!$activation||$activation['status']!=='active')&&$count>=(int)$key['max_devices'])throw new ApiError('DEVICE_LIMIT_REACHED',409);
                $id=$activation['id']??bin2hex(random_bytes(16));
                query($this->db,"INSERT INTO activations(id,license_key_id,device_id,activated_utc,last_validated_utc,app_version) VALUES(?,?,?,?,?,?) ON DUPLICATE KEY UPDATE status='active',last_validated_utc=VALUES(last_validated_utc),deactivated_utc=NULL,app_version=VALUES(app_version)",[$id,$key['id'],$device['id'],$now,$now,$version]);
                $result=$this->paid($id,$h,$product,$key,$now);event($this->db,'activation.accepted',$id,'api',$correlation);
            }elseif(in_array($action,['validate','deactivate'],true)){
                if(!is_array($request['token']??null))throw new ApiError('INVALID_KEY');
                $p=$this->tokens->verify($request['token']);if(!Fingerprint::matches($p['hardware'],$h))throw new ApiError('DEVICE_CHANGED',403);
                if($p['type']==='trial'){
                    if($action==='deactivate')throw new ApiError('INVALID_KEY');
                    $trial=query($this->db,'SELECT t.*,d.components_json FROM trials t JOIN devices d ON d.id=t.device_id WHERE t.id=? AND t.product_id=?',[$p['id'],$product['id']])->fetch();
                    if(!$trial||$trial['status']!=='active'||(int)$trial['expires_utc']<=$now)throw new ApiError('TRIAL_EXPIRED',403);
                    if(!Fingerprint::matches(json_decode($trial['components_json'],true),$h))throw new ApiError('DEVICE_CHANGED',403);
                    query($this->db,'UPDATE trials SET last_validated_utc=? WHERE id=?',[$now,$p['id']]);
                    $result=$this->entitlement('trial',$p['id'],$h,(int)$trial['started_utc'],(int)$trial['expires_utc'],(int)$trial['expires_utc']);
                }else{
                    $a=query($this->db,'SELECT a.*,d.components_json FROM activations a JOIN devices d ON d.id=a.device_id WHERE a.id=?',[$p['id']])->fetch();
                    if(!$a||!Fingerprint::matches(json_decode($a['components_json'],true),$h))throw new ApiError('DEVICE_CHANGED',403);
                    $key=query($this->db,'SELECT * FROM license_keys WHERE id=? AND product_id=? FOR UPDATE',[$a['license_key_id'],$product['id']])->fetch();
                    if($action==='deactivate'){
                        query($this->db,"UPDATE activations SET status='deactivated',deactivated_utc=? WHERE id=?",[$now,$p['id']]);$result=['deactivated'=>true];event($this->db,'activation.deactivated',$p['id'],'api',$correlation);
                    }else{
                        $this->checkKey($key,$now);if($a['status']!=='active')throw new ApiError('KEY_REVOKED',403);
                        query($this->db,'UPDATE activations SET last_validated_utc=?,app_version=? WHERE id=?',[$now,$version,$p['id']]);$result=$this->paid($p['id'],$h,$product,$key,$now);
                    }
                }
                event($this->db,'entitlement.'.$action,$p['id'],'api',$correlation);
            }else throw new ApiError('NOT_FOUND',404);
            $this->db->commit();return $result;
        }catch(Throwable $e){if($this->db->inTransaction())$this->db->rollBack();throw $e;}
    }
    private function checkKey(array|false $key,int $now): void {
        if(!$key)throw new ApiError('INVALID_KEY');
        if($key['status']!=='active'||($key['expires_utc']!==null&&(int)$key['expires_utc']<=$now))throw new ApiError($key['status']==='suspended'?'KEY_SUSPENDED':'KEY_REVOKED',403);
    }
    private function paid(string $id,array $h,array $p,array $key,int $now): array {
        $end=min($now+min(30,max(1,(int)$p['grace_days']))*86400,$key['expires_utc']===null?PHP_INT_MAX:(int)$key['expires_utc']);
        return $this->entitlement('license',$id,$h,$now,$end,min($end,$now+max(7,(int)$p['validation_days'])*86400));
    }
    private function entitlement(string $type,string $id,array $h,int $issued,int $expires,int $next): array {
        return ['token'=>$this->tokens->sign(['version'=>1,'product'=>'meshme360','type'=>$type,'id'=>$id,'hardware'=>$h,'issuedAt'=>$issued,'expiresAt'=>$expires,'nextValidation'=>$next,'nonce'=>bin2hex(random_bytes(16))])];
    }
}
