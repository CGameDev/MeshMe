<?php
declare(strict_types=1);

// Package entry points must never fall back to a shared Web Station identity.
$identity = function_exists('posix_geteuid') && function_exists('posix_getpwuid')
    ? posix_getpwuid(posix_geteuid()) : false;
if (PHP_VERSION_ID < 80200 || PHP_VERSION_ID >= 80300
    || !is_array($identity) || ($identity['name'] ?? '') !== 'MeshMeActivation') {
    http_response_code(503);
    header('Content-Type: application/json');
    header('Cache-Control: no-store');
    echo '{"code":"SERVICE_UNAVAILABLE","message":"Activation package runtime is not configured."}';
    exit;
}

// Paths only; the package never contains a production password or signing key.
putenv('MESHME_ACTIVATION_HOME=/var/packages/MeshMeActivation/target/service');
putenv('MESHME_ACTIVATION_CONFIG=/volume1/meshme-private/config.php');
