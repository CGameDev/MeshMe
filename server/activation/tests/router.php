<?php
declare(strict_types=1);
// Test transport only. Never deploy this directory or route to production.
if(PHP_SAPI!=='cli-server')exit(1);
if(!str_starts_with($_SERVER['REQUEST_URI'],'/insecure/'))$_SERVER['HTTPS']='on';
else $_SERVER['REQUEST_URI']=substr($_SERVER['REQUEST_URI'],9);
if(str_starts_with($_SERVER['REQUEST_URI'],'/admin/')){
    $_SERVER['SCRIPT_NAME']='/admin/index.php';require __DIR__.'/../admin-public/index.php';
}else{$_SERVER['SCRIPT_NAME']='/index.php';require __DIR__.'/../public/index.php';}
