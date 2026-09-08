<?php
declare(strict_types=1);
require __DIR__.'/../src/bootstrap.php';require __DIR__.'/../src/Admin/Portal.php';
$checks=0;
function check(bool $value,string $name): void {global $checks;if(!$value)throw new RuntimeException('FAIL: '.$name);$checks++;echo 'PASS: '.$name."\n";}
function reject(callable $fn,string $code): void {try{$fn();}catch(ApiError $e){check($e->stableCode===$code,'Reject '.$code);return;}throw new RuntimeException('Expected '.$code);}
function hardware(string $name): array {$c=[];foreach(Fingerprint::WEIGHTS as $key=>$weight)$c[$key]=hash('sha256','fixture:'.$name.':'.$key);ksort($c);$parts=[];foreach($c as $k=>$v)$parts[]=$k.':'.$v;return ['version'=>1,'components'=>$c,'combined'=>hash('sha256',implode("\n",$parts))];}
function recombine(array $h): array {$c=$h['components'];ksort($c);$p=[];foreach($c as $k=>$v)$p[]=$k.':'.$v;$h['combined']=hash('sha256',implode("\n",$p));return $h;}
for($i=0;$i<1000;$i++){$key=Keys::generate();if(!Keys::check($key)||strlen(Keys::normalize($key))!==25)throw new RuntimeException('Key generation failed');}
check(true,'1000 cryptographically generated keys validate');
check(Keys::check(strtolower($key))&&Keys::check(str_replace('-',' ',$key)),'Key normalization');
check(!Keys::check(substr($key,0,-1).'I'),'Ambiguous symbol rejected');
$pc=hardware('alpha');Fingerprint::validate($pc);$reinstall=$pc;$reinstall['components']['machineGuid']=hash('sha256','replacement');$reinstall=recombine($reinstall);
check(Fingerprint::matches($pc,$reinstall),'Windows reinstall matches same hardware');
check(!Fingerprint::matches($pc,hardware('beta')),'Motherboard replacement differs');
reject(fn()=>Fingerprint::validate(['version'=>1,'components'=>['systemUuid'=>'raw serial'],'combined'=>'invalid']),'DEVICE_CHANGED');
check(Portal::csvCell('=HYPERLINK("bad")')[0]==="'"&&Portal::csvCell("\t=1")[0]==="'",'CSV formulas neutralized');
reject(fn()=>ApiRequest::decode('{broken','application/json'),'INVALID_REQUEST');
reject(fn()=>ApiRequest::decode(str_repeat('x',24577),'application/json'),'INVALID_REQUEST');
reject(fn()=>ApiRequest::decode('{}','text/plain'),'INVALID_REQUEST');
reject(fn()=>ApiRequest::decode('{"model":"private mesh"}','application/json'),'INVALID_REQUEST');
reject(fn()=>ApiRequest::decode('{"key":[]}','application/json'),'INVALID_REQUEST');
reject(fn()=>Portal::csrf('bad','expected'),'CSRF_REJECTED');Portal::csrf('expected','expected');check(true,'Valid CSRF accepted');

// Ephemeral test private keys live outside the source tree and are removed on exit.
$temp=sys_get_temp_dir().'/meshme-activation-tests-'.bin2hex(random_bytes(8));mkdir($temp,0700);
try{
    $rsa=openssl_pkey_new(['private_key_bits'=>3072,'private_key_type'=>OPENSSL_KEYTYPE_RSA]);openssl_pkey_export($rsa,$private);$public=openssl_pkey_get_details($rsa)['key'];file_put_contents($temp.'/private.pem',$private);file_put_contents($temp.'/public.pem',$public);unset($private);
    $config=['signing_key_path'=>$temp.'/private.pem','signing_key_id'=>'cross','public_keys'=>['cross'=>$temp.'/public.pem']];$tokens=new Tokens($config);
    $payload=['version'=>1,'product'=>'meshme360','type'=>'license','id'=>bin2hex(random_bytes(16)),'hardware'=>$pc,'issuedAt'=>time(),'expiresAt'=>time()+2592000,'nextValidation'=>time()+604800,'nonce'=>bin2hex(random_bytes(16))];
    $token=$tokens->sign($payload);check($tokens->verify($token)['id']===$payload['id'],'RSA signed entitlement verifies');
    reject(fn()=>$tokens->verify(array_merge($token,['payload'=>base64_encode('tampered')])),'INVALID_KEY');
    if(getenv('MESHME_CROSS_TOKEN'))file_put_contents(getenv('MESHME_CROSS_TOKEN'),json_encode(['publicKey'=>$public,'token'=>$token,'hardware'=>$pc],JSON_THROW_ON_ERROR));
    $dsn=getenv('MESHME_TEST_DSN');
    if(!$dsn){echo "SKIP: database integration requires MESHME_TEST_DSN with dbname=meshme_test_*\n";exit(2);}
    if(!preg_match('/dbname=meshme_test_[A-Za-z0-9_]+(?:;|$)/',$dsn))throw new RuntimeException('Only a dedicated meshme_test_* database is allowed');
    $config+=['dsn'=>$dsn,'db_user'=>getenv('MESHME_TEST_USER')?:'root','db_password'=>getenv('MESHME_TEST_PASSWORD')?:''];$db=database($config);
    if((int)query($db,'SELECT COUNT(*) FROM information_schema.tables WHERE table_schema=DATABASE()')->fetchColumn()!==0)throw new RuntimeException('Tests require an empty database; no existing tables are dropped');
    $db->exec(file_get_contents(__DIR__.'/../migrations/001_initial.sql'));check(true,'MariaDB clean migration installed');
    $service=new LicenseService($db,$tokens);$correlation=bin2hex(random_bytes(16));$base=['version'=>1,'product'=>'meshme360','appVersion'=>'test-1','hardware'=>$pc];
    $trial=$service->handle('trial/start',$base,$correlation)['token'];$t=$tokens->verify($trial);check($t['expiresAt']-$t['issuedAt']===604800&&abs($t['issuedAt']-time())<5,'Trial starts from server UTC for seven days');
    $same=$tokens->verify($service->handle('trial/start',array_merge($base,['hardware'=>$reinstall]),$correlation)['token']);check($same['id']===$t['id']&&$same['expiresAt']===$t['expiresAt'],'Reinstall returns original trial and expiry');
    query($db,'UPDATE trials SET expires_utc=? WHERE id=?',[time()-1,$t['id']]);reject(fn()=>$service->handle('trial/start',$base,$correlation),'TRIAL_EXPIRED');
    $keys=Portal::mutate($db,['action'=>'generate','count'=>1,'reference'=>"test' OR 1=1 --"],'test',$correlation);$key=$keys[0];$keyId=query($db,'SELECT id FROM license_keys')->fetchColumn();
    check(query($db,'SELECT reference FROM license_keys WHERE id=?',[$keyId])->fetchColumn()==="test' OR 1=1 --",'Prepared statement preserves injection string as data');
    $activation=$service->handle('activate',$base+['key'=>$key],$correlation)['token'];$a=$tokens->verify($activation);check($a['expiresAt']-$a['issuedAt']===2592000,'Paid lease defaults to 30 days');
    $again=$tokens->verify($service->handle('activate',array_merge($base,['hardware'=>$reinstall,'key'=>strtolower($key)]),$correlation)['token']);check($again['id']===$a['id']&&(int)query($db,"SELECT COUNT(*) FROM activations WHERE status='active'")->fetchColumn()===1,'Same-PC reactivation consumes one seat');
    reject(fn()=>$service->handle('activate',array_merge($base,['hardware'=>hardware('beta'),'key'=>$key]),$correlation),'DEVICE_LIMIT_REACHED');
    reject(fn()=>$service->handle('validate',array_merge($base,['hardware'=>hardware('beta'),'token'=>$activation]),$correlation),'DEVICE_CHANGED');
    $service->handle('deactivate',$base+['token'=>$activation],$correlation);$service->handle('deactivate',$base+['token'=>$activation],$correlation);check(true,'Deactivation is repeatable and frees slot');
    $b=$service->handle('activate',array_merge($base,['hardware'=>hardware('beta'),'key'=>$key]),$correlation)['token'];
    Portal::mutate($db,['action'=>'reset','id'=>$keyId,'confirm'=>'yes'],'test',$correlation);check((int)query($db,"SELECT COUNT(*) FROM activations WHERE status='active'")->fetchColumn()===0,'Admin reset frees active device');
    Portal::mutate($db,['action'=>'revoked','id'=>$keyId,'confirm'=>'yes'],'test',$correlation);reject(fn()=>$service->handle('activate',$base+['key'=>$key],$correlation),'KEY_REVOKED');
    Portal::mutate($db,['action'=>'active','id'=>$keyId],'test',$correlation);
    $configPath=$temp.'/config.php';file_put_contents($configPath,'<?php return '.var_export($config,true).';');
    $requests=[];foreach(['gamma','delta'] as $name){$path=$temp.'/'.$name.'.json';file_put_contents($path,json_encode(array_merge($base,['hardware'=>hardware($name),'key'=>$key]),JSON_THROW_ON_ERROR));$requests[]=$path;}
    $processes=[];foreach($requests as $i=>$path){$pipes=[];$process=proc_open([PHP_BINARY,__DIR__.'/race.php',$configPath,$path],[0=>['pipe','r'],1=>['pipe','w'],2=>['pipe','w']],$pipes);fclose($pipes[0]);$processes[]=[$process,$pipes];}
    $outcomes=[];foreach($processes as [$process,$pipes]){$outcomes[]=trim(stream_get_contents($pipes[1]));$errors=stream_get_contents($pipes[2]);fclose($pipes[1]);fclose($pipes[2]);if(proc_close($process)!==0)throw new RuntimeException('Race worker failed: '.$errors);}
    sort($outcomes);check($outcomes===['DEVICE_LIMIT_REACHED','OK'],'Concurrent different PCs cannot exceed one seat');
    for($i=0;$i<20;$i++)RateLimit::consume($db,['test-ip','test-device']);reject(fn()=>RateLimit::consume($db,['test-ip','test-device']),'RATE_LIMITED');
    $events=json_encode(query($db,'SELECT * FROM activation_events')->fetchAll());check(!str_contains($events,$key)&&!str_contains($events,$pc['components']['systemUuid']),'Audit events exclude full keys and hardware components');
    check(!query($db,"SELECT COLUMN_NAME FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='license_keys' AND COLUMN_NAME='key_plaintext'")->fetchColumn(),'Database stores key hash and hint only');
    $config['admin_allowed_ips']=['127.0.0.1'];file_put_contents($configPath,'<?php return '.var_export($config,true).';');
    putenv('MESHME_ACTIVATION_CONFIG='.$configPath);
    $socket=stream_socket_server('tcp://127.0.0.1:0');$address=stream_socket_get_name($socket,false);fclose($socket);
    $server=proc_open([PHP_BINARY,'-S',$address,__DIR__.'/router.php'],[0=>['pipe','r'],1=>['file',$temp.'/http.log','a'],2=>['file',$temp.'/http.log','a']],$serverPipes);
    fclose($serverPipes[0]);$cookie='';
    $http=function(string $path,?string $body=null,string $type='application/json')use($address,&$cookie):array{
        $headers="Content-Type: $type\r\n".($cookie!==''?"Cookie: $cookie\r\n":'');
        $ctx=stream_context_create(['http'=>['method'=>$body===null?'GET':'POST','content'=>$body??'','header'=>$headers,'ignore_errors'=>true,'timeout'=>5]]);
        $content=file_get_contents('http://'.$address.$path,false,$ctx);$responseHeaders=$http_response_header??[];
        foreach($responseHeaders as $header)if(preg_match('/^Set-Cookie:\s*([^;]+)/i',$header,$m))$cookie=$m[1];
        return [$content,$responseHeaders];
    };
    try{
        for($i=0;$i<30;$i++){$probe=@stream_socket_client('tcp://'.$address,$errno,$error,.1);if($probe){fclose($probe);break;}usleep(100000);}
        [$body,$headers]=$http('/api/v1/health');check(str_contains($headers[0],'200')&&json_decode($body,true)['status']==='ok','Actual public health route returns minimal JSON');
        [$body,$headers]=$http('/insecure/api/v1/health');check(str_contains($headers[0],'403'),'Actual public entry rejects non-HTTPS transport');
        foreach([['{broken','application/json',400],[str_repeat('x',24577),'application/json',413],['{}','text/plain',415]] as [$input,$type,$status]){[$body,$headers]=$http('/api/v1/activate',$input,$type);check(str_contains($headers[0],(string)$status)&&json_decode($body,true)['code']==='INVALID_REQUEST','Actual request boundary rejects '.$status);}
        [$body,$headers]=$http('/api/v1/trial/start',json_encode(array_merge($base,['hardware'=>hardware('http-test')])));check(str_contains($headers[0],'200')&&$tokens->verify(json_decode($body,true)['token'])['type']==='trial','Actual public trial endpoint signs a valid response');
        $adminPassword=bin2hex(random_bytes(16));$adminId=bin2hex(random_bytes(16));query($db,'INSERT INTO admins(id,username,password_hash) VALUES(?,?,?)',[$adminId,'fixture-admin',password_hash($adminPassword,PASSWORD_DEFAULT)]);
        [$body,$headers]=$http('/admin/');preg_match('/name="csrf" value="([a-f0-9]+)"/',$body,$match);$csrf=$match[1];$oldCookie=$cookie;
        $setCookie=implode("\n",$headers);check(str_contains(strtolower($setCookie),'secure')&&str_contains(strtolower($setCookie),'httponly')&&str_contains($setCookie,'SameSite=Strict'),'Admin response sets secure session cookies');
        [$body,$headers]=$http('/admin/',http_build_query(['action'=>'login','csrf'=>$csrf,'username'=>'fixture-admin','password'=>$adminPassword]),'application/x-www-form-urlencoded');check(str_contains($headers[0],'200')&&str_contains($body,'Sign out')&&$cookie!==$oldCookie,'Admin login verifies password and rotates session id');
        [$body,$headers]=$http('/admin/',http_build_query(['action'=>'revoked','csrf'=>'forged','id'=>$keyId,'confirm'=>'yes']),'application/x-www-form-urlencoded');check(str_contains($headers[0],'403')&&str_contains($body,'CSRF_REJECTED'),'Actual admin mutation rejects forged CSRF');
    }finally{proc_terminate($server);proc_close($server);putenv('MESHME_ACTIVATION_CONFIG');}
    echo 'All '.$checks." PHP/MariaDB checks passed.\n";
}finally{foreach(glob($temp.'/*') as $file)unlink($file);rmdir($temp);}
