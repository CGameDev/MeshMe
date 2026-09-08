<?php
declare(strict_types=1);
require_once __DIR__.'/Hardware/Fingerprint.php';
require_once __DIR__.'/Security/Protocol.php';
require_once __DIR__.'/Licensing/Service.php';
require_once __DIR__.'/Security/RateLimit.php';
require_once __DIR__.'/Api/Request.php';

function configuration(): array {
    $path = getenv('MESHME_ACTIVATION_CONFIG');
    if (!$path || !is_file($path)) throw new RuntimeException('Configuration unavailable');
    $real = realpath($path);
    $roots = [realpath(__DIR__.'/..'), realpath($_SERVER['DOCUMENT_ROOT'] ?? __DIR__.'/../public')];
    foreach ($roots as $root) if ($root && str_starts_with($real, $root.DIRECTORY_SEPARATOR)) throw new RuntimeException('Configuration must be outside web root and source');
    return require $path;
}
function database(array $config): PDO {
    $db = new PDO($config['dsn'], $config['db_user'], $config['db_password'], [PDO::ATTR_ERRMODE=>PDO::ERRMODE_EXCEPTION, PDO::ATTR_EMULATE_PREPARES=>false, PDO::ATTR_DEFAULT_FETCH_MODE=>PDO::FETCH_ASSOC]);
    $db->exec("SET time_zone = '+00:00'"); return $db;
}
function query(PDO $db, string $sql, array $values=[]): PDOStatement { $s=$db->prepare($sql); $s->execute($values); return $s; }
function event(PDO $db, string $name, ?string $subject, string $actor, string $request): void {
    query($db,'INSERT INTO activation_events(occurred_utc,event,subject_id,actor,request_id) VALUES(?,?,?,?,?)',[time(),$name,$subject,$actor,$request]);
}
function httpsOnly(): void {
    // Proxy headers are deliberately not trusted. Configure HTTPS at the PHP/Web Station boundary.
    if (($_SERVER['HTTPS'] ?? '') !== 'on' && ($_SERVER['HTTPS'] ?? '') !== '1') throw new ApiError('SERVICE_UNAVAILABLE',403);
    header('Strict-Transport-Security: max-age=31536000');
    header('Cache-Control: no-store'); header('X-Content-Type-Options: nosniff');
}
