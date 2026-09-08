<?php
declare(strict_types=1);
require __DIR__.'/../src/bootstrap.php';
$config=require $argv[1];$request=json_decode(file_get_contents($argv[2]),true,32,JSON_THROW_ON_ERROR);
try{(new LicenseService(database($config),new Tokens($config)))->handle('activate',$request,bin2hex(random_bytes(16)));echo 'OK';}
catch(ApiError $e){echo $e->stableCode;}
