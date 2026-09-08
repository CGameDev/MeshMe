<?php
declare(strict_types=1);
$home=getenv('MESHME_ACTIVATION_HOME') ?: dirname(__DIR__);
require $home.'/src/bootstrap.php';require $home.'/src/Admin/Portal.php';
$requestId=bin2hex(random_bytes(16));$newKeys=[];$message='';
function h(mixed $value): string{return Portal::escape($value);}
function csrfField(): string{return '<input type="hidden" name="csrf" value="'.h($_SESSION['csrf']).'">';}
function table(array $rows): void {
    if(!$rows){echo '<p>No matching records.</p>';return;}echo '<div class="scroll"><table><thead><tr>';foreach(array_keys($rows[0]) as $key)echo '<th>'.h($key).'</th>';echo '</tr></thead><tbody>';
    foreach($rows as $row){echo '<tr>';foreach($row as $value)echo '<td>'.h($value??'—').'</td>';echo '</tr>';}echo '</tbody></table></div>';
}
try{
    httpsOnly();header("Content-Security-Policy: default-src 'none'; style-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'");header('Referrer-Policy: no-referrer');
    $config=configuration();if(!in_array($_SERVER['REMOTE_ADDR']??'', $config['admin_allowed_ips']??[],true))throw new ApiError('ADMIN_NETWORK_REQUIRED',403);
    if((int)($_SERVER['CONTENT_LENGTH']??0)>8192)throw new ApiError('INVALID_REQUEST',413);
    ini_set('session.use_strict_mode','1');ini_set('session.use_only_cookies','1');session_name('meshme_admin');
    session_set_cookie_params(['secure'=>true,'httponly'=>true,'samesite'=>'Strict','path'=>rtrim(dirname($_SERVER['SCRIPT_NAME']),'/').'/']);session_start();
    if(isset($_SESSION['last'])&&time()-$_SESSION['last']>1800){$_SESSION=[];session_regenerate_id(true);}$_SESSION['last']=time();$_SESSION['csrf']??=bin2hex(random_bytes(32));
    $db=database($config);
    if($_SERVER['REQUEST_METHOD']==='POST'){
        Portal::csrf((string)($_POST['csrf']??''),$_SESSION['csrf']);$action=$_POST['action']??'';
        if($action==='login'){
            RateLimit::consume($db,['admin-ip:'.$_SERVER['REMOTE_ADDR'],'admin-name:'.(string)($_POST['username']??'')]);
            $admin=query($db,'SELECT * FROM admins WHERE username=?',[(string)($_POST['username']??'')])->fetch();
            if(!$admin||!password_verify((string)($_POST['password']??''),$admin['password_hash']))throw new ApiError('LOGIN_FAILED',403);
            session_regenerate_id(true);$_SESSION['admin']=$admin['id'];$_SESSION['csrf']=bin2hex(random_bytes(32));query($db,'UPDATE admins SET last_login_utc=? WHERE id=?',[time(),$admin['id']]);event($db,'admin.login',null,$admin['id'],$requestId);
        }elseif(!isset($_SESSION['admin']))throw new ApiError('LOGIN_REQUIRED',401);
        elseif($action==='logout'){$_SESSION=[];session_destroy();header('Location: ./',true,303);exit;}
        elseif($action==='export'){
            $rows=query($db,'SELECT id,key_hint,status,max_devices,reference,created_utc,updated_utc FROM license_keys ORDER BY created_utc DESC LIMIT 10000')->fetchAll();event($db,'admin.csv_export',null,$_SESSION['admin'],$requestId);
            header('Content-Type: text/csv; charset=utf-8');header('Content-Disposition: attachment; filename="meshme-administration.csv"');$out=fopen('php://output','w');fputcsv($out,['id','key_hint','status','max_devices','reference','created_utc','updated_utc'],',','"','');foreach($rows as $row)fputcsv($out,array_map([Portal::class,'csvCell'],$row),',','"','');exit;
        }else{
            $newKeys=Portal::mutate($db,$_POST,$_SESSION['admin'],$requestId);$_SESSION['csrf']=bin2hex(random_bytes(32));$message='Change saved.';
            if($newKeys&&($_POST['delivery']??'')==='download'){
                header('Content-Type: text/csv; charset=utf-8');header('Content-Disposition: attachment; filename="meshme-new-keys-once.csv"');$out=fopen('php://output','w');fputcsv($out,['activation_key'],',','"','');foreach($newKeys as $key)fputcsv($out,[$key],',','"','');exit;
            }
        }
    }
}catch(Throwable $e){$code=$e instanceof ApiError?$e->stableCode:'SERVICE_UNAVAILABLE';http_response_code($e instanceof ApiError?$e->httpStatus:503);error_log(json_encode(['requestId'=>$requestId,'code'=>$code]));echo '<p>'.h($code).'</p>';exit;}
?><!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>Mesh Me360 Licensing Administration</title><link rel="stylesheet" href="portal.css"><main>
<h1>Mesh Me360 Licensing Administration</h1><p>Computer Universal Technology Systems</p>
<?php if(!isset($_SESSION['admin'])): ?>
<form method="post"><?=csrfField()?><input type="hidden" name="action" value="login"><label>Administrator <input name="username" required autocomplete="username" maxlength="80"></label><label>Password <input type="password" name="password" required autocomplete="current-password"></label><button>Sign in</button></form>
<?php else: ?>
<p role="status"><?=h($message)?></p><form method="post"><?=csrfField()?><button name="action" value="logout">Sign out</button> <button name="action" value="export">Export administrative metadata</button></form>
<?php
$counts=query($db,"SELECT (SELECT COUNT(*) FROM license_keys WHERE status='active') AS active_keys,(SELECT COUNT(*) FROM activations WHERE status='active') AS active_activations,(SELECT COUNT(*) FROM trials) AS trials,(SELECT COUNT(*) FROM activation_events WHERE event LIKE 'request.rejected.%' AND occurred_utc>UNIX_TIMESTAMP()-86400) AS recent_rejects")->fetchAll();table($counts);
if($newKeys){echo '<h2>New keys — shown only once</h2><p>Copy these now. Reloading this form generates another batch; the existing keys cannot be recovered.</p><textarea readonly rows="8" aria-label="New activation keys">'.h(implode("\n",$newKeys)).'</textarea>';}
?>
<h2>Generate keys</h2><form method="post"><?=csrfField()?><input type="hidden" name="action" value="generate"><label>Count <input name="count" type="number" min="1" max="100" value="1" required></label><label>Customer/reference <input name="reference" maxlength="200"></label><label>One-time delivery <select name="delivery"><option value="show">Reveal for copy</option><option value="download">Download CSV</option></select></label><button>Generate</button></form>
<h2>Keys and active devices</h2><form method="get"><label>Search hint, reference, activation ID or device hash prefix <input name="q" maxlength="200" value="<?=h($_GET['q']??'')?>"></label><button>Search</button></form>
<?php $search='%'.str_replace(['!','%','_'],['!!','!%','!_'],substr((string)($_GET['q']??''),0,200)).'%';
$keys=query($db,"SELECT DISTINCT k.id,k.key_hint,k.status,k.max_devices,k.reference,k.expires_utc FROM license_keys k LEFT JOIN activations a ON a.license_key_id=k.id LEFT JOIN devices d ON d.id=a.device_id WHERE k.key_hint LIKE ? ESCAPE '!' OR k.reference LIKE ? ESCAPE '!' OR a.id LIKE ? ESCAPE '!' OR d.combined LIKE ? ESCAPE '!' ORDER BY k.created_utc DESC LIMIT 100",[$search,$search,$search,$search])->fetchAll();table($keys); ?>
<form method="post"><?=csrfField()?><label>Key ID <input name="id" required pattern="[0-9a-f]{32}"></label><label>Action <select name="action"><option value="active">Activate key</option><option value="suspended">Suspend key</option><option value="revoked">Revoke key</option><option value="reset">Reset/deactivate active devices</option><option value="max_devices">Override device limit</option></select></label><label>Maximum devices <input type="number" name="max_devices" min="1" max="100" value="1"></label><label><input type="checkbox" name="confirm" value="yes">Confirm suspend, revoke or device reset. Existing signed leases can remain valid offline until expiry.</label><button>Apply change</button></form>
<h2>Activation and validation history</h2><?php table(query($db,'SELECT a.id,a.license_key_id,a.status,a.app_version,a.last_validated_utc,d.combined AS device_hash,a.policy_version FROM activations a JOIN devices d ON d.id=a.device_id ORDER BY a.last_validated_utc DESC LIMIT 100')->fetchAll()); ?>
<h2>Trials — UTC Unix seconds</h2><?php table(query($db,'SELECT t.id,t.started_utc,t.expires_utc,t.status,t.last_validated_utc,d.combined AS device_hash FROM trials t JOIN devices d ON d.id=t.device_id ORDER BY t.started_utc DESC LIMIT 100')->fetchAll()); ?>
<h2>Append-only audit events</h2><?php table(query($db,'SELECT * FROM activation_events ORDER BY id DESC LIMIT 200')->fetchAll()); ?>
<?php endif; ?></main></html>
