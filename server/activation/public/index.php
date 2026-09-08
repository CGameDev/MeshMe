<?php
declare(strict_types=1);
require (getenv('MESHME_ACTIVATION_HOME') ?: dirname(__DIR__)).'/src/bootstrap.php';
$requestId=bin2hex(random_bytes(16));$db=null;
header('Content-Type: application/json; charset=utf-8');header('X-Request-ID: '.$requestId);
try {
    httpsOnly();$path=parse_url($_SERVER['REQUEST_URI'],PHP_URL_PATH);
    // Supports an isolated /meshme-activation/ mount without taking over ConsoleCrate /api/v1.
    $prefix=rtrim(dirname($_SERVER['SCRIPT_NAME']),'/');if($prefix!==''&&str_starts_with($path,$prefix.'/'))$path=substr($path,strlen($prefix));
    if($path==='/api/v1/health'&&$_SERVER['REQUEST_METHOD']==='GET'){echo '{"status":"ok","protocol":1}';exit;}
    if($_SERVER['REQUEST_METHOD']!=='POST')throw new ApiError('METHOD_NOT_ALLOWED',405);
    if(!in_array($path,['/api/v1/trial/start','/api/v1/activate','/api/v1/validate','/api/v1/deactivate'],true))throw new ApiError('NOT_FOUND',404);
    if(strtolower(trim(explode(';',$_SERVER['CONTENT_TYPE']??'')[0]))!=='application/json')throw new ApiError('INVALID_REQUEST',415);
    if((int)($_SERVER['CONTENT_LENGTH']??0)>24576)throw new ApiError('INVALID_REQUEST',413);
    $raw=file_get_contents('php://input',false,null,0,24577);if(strlen($raw)>24576)throw new ApiError('INVALID_REQUEST',413);
    $r=ApiRequest::decode($raw,$_SERVER['CONTENT_TYPE']??'');
    $config=configuration();$db=database($config);
    $dimensions=['ip:'.($_SERVER['REMOTE_ADDR']??'unknown')];
    if(is_string($r['key']??null))$dimensions[]='key:'.hash('sha256',Keys::normalize($r['key']));
    if(is_string($r['hardware']['combined']??null))$dimensions[]='device:'.$r['hardware']['combined'];
    RateLimit::consume($db,$dimensions);
    echo json_encode((new LicenseService($db,new Tokens($config)))->handle(substr($path,8),$r,$requestId),JSON_THROW_ON_ERROR);
}catch(Throwable $e){
    $code=$e instanceof ApiError?$e->stableCode:'SERVICE_UNAVAILABLE';http_response_code($e instanceof ApiError?$e->httpStatus:503);
    if($code==='RATE_LIMITED')header('Retry-After: 3600');
    if($db)try{event($db,'request.rejected.'.$code,null,'api',$requestId);}catch(Throwable){}
    error_log(json_encode(['requestId'=>$requestId,'code'=>$code]));
    echo json_encode(['code'=>$code,'message'=>'The request could not be completed.','requestId'=>$requestId]);
}
