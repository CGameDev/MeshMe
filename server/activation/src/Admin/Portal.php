<?php
declare(strict_types=1);
final class Portal {
    public static function escape(mixed $value): string {return htmlspecialchars((string)$value,ENT_QUOTES|ENT_SUBSTITUTE,'UTF-8');}
    public static function csvCell(mixed $value): string {
        $value=(string)$value;return preg_match('/^[\s]*[=+@-]/u',$value)||preg_match('/^[\t\r\n]/',$value)?"'".$value:$value;
    }
    public static function csrf(string $submitted,string $expected): void {if($expected===''||!hash_equals($expected,$submitted))throw new ApiError('CSRF_REJECTED',403);}
    public static function mutate(PDO $db,array $p,string $actor,string $request): array {
        $db->beginTransaction();
        try{
            $product=query($db,"SELECT id FROM products WHERE code='meshme360' FOR UPDATE")->fetchColumn();$action=$p['action']??'';$output=[];
            if($action==='generate'){
                $count=filter_var($p['count']??1,FILTER_VALIDATE_INT);if($count===false||$count<1||$count>100)throw new ApiError('INVALID_REQUEST');
                $reference=trim((string)($p['reference']??''));if(strlen($reference)>200)throw new ApiError('INVALID_REQUEST');
                for($i=0;$i<$count;$i++){$key=Keys::generate();$normalized=Keys::normalize($key);$id=bin2hex(random_bytes(16));query($db,'INSERT INTO license_keys(id,product_id,key_hash,key_hint,reference,created_utc,updated_utc) VALUES(?,?,?,?,?,?,?)',[$id,$product,hash('sha256',$normalized),substr($normalized,0,5).'…'.substr($normalized,-5),$reference,time(),time()]);event($db,'key.generated',$id,$actor,$request);$output[]=$key;}
            }elseif(in_array($action,['active','suspended','revoked','reset','max_devices'],true)){
                $id=(string)($p['id']??'');if(!preg_match('/^[0-9a-f]{32}$/D',$id))throw new ApiError('INVALID_REQUEST');
                $key=query($db,'SELECT * FROM license_keys WHERE id=? AND product_id=? FOR UPDATE',[$id,$product])->fetch();if(!$key)throw new ApiError('NOT_FOUND',404);
                if(in_array($action,['suspended','revoked','reset'],true)&&($p['confirm']??'')!=='yes')throw new ApiError('CONFIRM_REQUIRED');
                if($action==='reset'){query($db,"UPDATE activations SET status='deactivated',deactivated_utc=? WHERE license_key_id=? AND status='active'",[time(),$id]);}
                elseif($action==='max_devices'){
                    $max=filter_var($p['max_devices']??null,FILTER_VALIDATE_INT);$count=(int)query($db,"SELECT COUNT(*) FROM activations WHERE license_key_id=? AND status='active'",[$id])->fetchColumn();
                    if($max===false||$max<max(1,$count)||$max>100)throw new ApiError('INVALID_REQUEST');query($db,'UPDATE license_keys SET max_devices=?,updated_utc=? WHERE id=?',[$max,time(),$id]);
                }else query($db,'UPDATE license_keys SET status=?,updated_utc=? WHERE id=?',[$action,time(),$id]);
                event($db,'admin.'.$action,$id,$actor,$request);
            }else throw new ApiError('INVALID_REQUEST');
            $db->commit();return $output;
        }catch(Throwable $e){if($db->inTransaction())$db->rollBack();throw $e;}
    }
}
