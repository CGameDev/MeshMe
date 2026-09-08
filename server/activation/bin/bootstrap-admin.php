<?php
declare(strict_types=1);
require __DIR__.'/../src/bootstrap.php';
if(PHP_SAPI!=='cli')exit(1);
// Supply the password through a secret environment variable; never a command-line argument or log.
$username=$argv[1]??'';$password=getenv('MESHME_ADMIN_PASSWORD');
if(!preg_match('/^[A-Za-z0-9._-]{3,80}$/D',$username)||!$password||strlen($password)<16){fwrite(STDERR,"Set MESHME_ADMIN_PASSWORD (16+ characters) and supply an administrator username.\n");exit(1);}
$db=database(configuration());$id=bin2hex(random_bytes(16));
$hash=password_hash($password,defined('PASSWORD_ARGON2ID')?PASSWORD_ARGON2ID:PASSWORD_DEFAULT);
query($db,'INSERT INTO admins(id,username,password_hash) VALUES(?,?,?)',[$id,$username,$hash]);
event($db,'admin.bootstrapped',$id,'bootstrap',bin2hex(random_bytes(16)));echo "Administrator created. Remove the password environment variable. Use LAN/VPN with MFA.\n";
