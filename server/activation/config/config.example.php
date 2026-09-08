<?php
declare(strict_types=1);
// Copy OUTSIDE both Git and the Web Station document root. No real secrets here.
return [
    'dsn' => 'mysql:host=127.0.0.1;port=3306;dbname=meshme_activation;charset=utf8mb4',
    'db_user' => 'meshme_service', 'db_password' => 'SET_OUTSIDE_GIT',
    'signing_key_path' => '/private/meshme/signing-key.pem',
    'signing_key_id' => 'owner-key-1',
    'public_keys' => ['owner-key-1' => '/private/meshme/public-key.pem'],
    // Deploy a separate admin root reachable only over trusted LAN/VPN or an MFA identity proxy.
    'admin_allowed_ips' => ['127.0.0.1', '::1'],
];
