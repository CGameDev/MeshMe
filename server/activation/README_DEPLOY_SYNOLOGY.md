# Mesh Me360 activation — Synology installation

**DSM 7.3 dedicated-account correction:** for the DS1821+ installation, use
[`synology/README.md`](synology/README.md) and its SPK builder. A manually created
PHP profile was verified to run as the shared `http` user. Do not grant production
signing-key/configuration access to that shared account or group. The SPK registers
dedicated-account services without publishing portals or touching existing data;
verify the real package account on the NAS before the guided cutover. The manual
layout below remains reference material, not evidence of account isolation.

Owner-selected public base URL: **https://consolecrate.xyz/meshme-activation/**.
This package is prepared for installation; it has not been installed on the live NAS.
Keep the existing ConsoleCrate `/api/` and all its data/configuration unchanged.

## 1. Required layout and Web Station setup

Use PHP **8.2** (current security patch) with PDO MySQL, OpenSSL, sessions and JSON,
and MariaDB **10** with InnoDB. Enable HTTPS with the certificate for consolecrate.xyz.
Do not place the entire ZIP under the website root.

Recommended NAS layout (verify actual volume/share names in DSM):

```text
/volume1/meshme-services/activation/       # package source, NOT publicly served
  src/ config/ migrations/ tests/ bin/
/volume1/meshme-private/                  # separate private folder, NOT in Git/web root
  config.php signing-key.pem public-key.pem
<existing ConsoleCrate web root>/meshme-activation/
  index.php .htaccess                    # copy ONLY package public/ contents
<private admin web root>/
  index.php portal.css                   # copy ONLY admin-public/ contents
```

The local `Y:\` mapping was inspected and maps to `\\PlexFlixServer\consolecrate`.
It does **not** establish the NAS filesystem path or prove Web Station's document root.
Confirm that mapping in DSM before copying `public/` into `Y:\meshme-activation\`.
Do not store signing keys, passwords or the full service tree on that public share.

Create a separate PHP 8.2 service profile with environment variables:

```text
MESHME_ACTIVATION_HOME=/volume1/meshme-services/activation
MESHME_ACTIVATION_CONFIG=/volume1/meshme-private/config.php
```

Give the service account read access to service code/config/private key and write access
only to its protected session/log directories. Restrict private directory permissions
to the administrator and that service account. Configure PHP `display_errors=Off`,
`log_errors=On`, `expose_php=Off`; logs must stay outside public roots. Configure PHP
`open_basedir` to include these paths, session storage and required runtime files.

For Apache, enable rewrite support and allow the provided `.htaccess`. For Nginx,
configure the equivalent route in the managed site configuration: requests below
`/meshme-activation/api/v1/` must reach `/meshme-activation/index.php` through the
PHP 8.2 profile, preserving the original request URI and setting SCRIPT_NAME to
`/meshme-activation/index.php`. Do not route these requests to ConsoleCrate's API.
Do not edit generated Synology configuration files that DSM will overwrite.

TLS must be visible to PHP as `HTTPS=on`. The service deliberately ignores
untrusted `X-Forwarded-Proto`/`X-Forwarded-For`. If using a trusted reverse proxy,
configure Web Station/FastCGI's verified TLS and real-client-IP parameters at that
boundary. Verify HTTP is rejected and HTTPS succeeds before activating clients.
Set a web-server body limit of 24 KiB for the public API (admin: 8 KiB) and an
additional front-proxy per-IP rate limit. Never enable request-body logging.

## 2. Database and least privilege

Create a **new** `meshme_activation` database. Import `migrations/001_initial.sql`
using a separate migration administrator. Runtime code never creates/drops schemas.
All times are authoritative server Unix seconds in UTC; keep NAS time synchronized.

Create a distinct MariaDB runtime user restricted to localhost. Using the database
administrator, grant SELECT/INSERT/UPDATE on `products`, `license_keys`, `devices`,
`activations`, `trials`, `rate_limits`, and `admins` in this database only. Grant
**SELECT and INSERT only** on `activation_events`. Do not grant UPDATE/DELETE/TRUNCATE
on audit records, schema DDL, FILE, SUPER or access to ConsoleCrate databases.
The admin portal uses separately hashed administrator credentials, never the DB login.
Backups and migrations use separate, non-runtime accounts.

Copy `config/config.example.php` to `/volume1/meshme-private/config.php` and fill in
the private database settings. Never commit this populated file. Use the actual
MariaDB port chosen in DSM; Synology installations may not use 3306.

## 3. Signing keys and rotation

On the NAS/admin machine, outside Git and every web root:

```sh
umask 077
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out signing-key.pem
openssl pkey -in signing-key.pem -pubout -out public-key.pem
```

Set `signing_key_path`, `signing_key_id`, and `public_keys` in private configuration.
The private key must be readable only by the signing service account and the owner.
Back up this key encrypted separately from the database. Never copy it into a
Windows client, repository, public directory or installation ZIP.

To rotate: generate a new key/id; ship a client containing both public keys first;
keep both verification keys on the server; switch the server signing id/key only
after compatible clients are deployed. Keep the previous verification key until
all prior 30-day leases expire and renewal/deactivation migration is complete.
If a signing key is compromised, plan a client update that distrusts it; deleting
a key on the server cannot immediately revoke already cached offline leases.

## 4. Administrator bootstrap and protected portal

Publish `admin-public/` under a **separate private HTTPS site**, reachable through
trusted LAN/VPN or an identity proxy with MFA/TOTP. Do not expose it under the public
ConsoleCrate path. `admin_allowed_ips` must list the exact trusted ingress/client
addresses after reviewing the proxy boundary; the default allows loopback only.
IP restrictions supplement login, not replace it. Strongly enable TOTP in the VPN
or identity proxy; this package does not implement its own TOTP enrollment system.

Set `MESHME_ADMIN_PASSWORD` through a protected terminal/DSM secret environment
(at least 16 characters), then run with the service configuration environment:

```sh
php bin/bootstrap-admin.php owner-admin
unset MESHME_ADMIN_PASSWORD
```

Do not put the password in command-line arguments, scripts, shell history or chat.
The password uses Argon2id when PHP supports it, otherwise PHP's password default.
Portal sessions are Secure/HttpOnly/SameSite=Strict with CSRF tokens, session-id
rotation at login and a 30-minute inactivity limit. All destructive suspend/revoke/
reset changes require a checked confirmation. Normal administration needs no phpMyAdmin.

The portal provides counts, single/batch generation, one-time reveal or CSV download,
masked-key/reference/device search, statuses, device limits, reset, validation and
trial timestamps, audit history and metadata CSV export. Full keys are not recoverable
after generation. Store the one-time export securely or replace/revoke a lost key.
Current tables show the most recent 100 records (audit 200); metadata export caps at
10,000 records. Larger operational history remains in MariaDB and backups.

## 5. Configure the official Windows build

Copy `src/MeshMe360.Desktop/activation.example.json` to adjacent `activation.json`.
Keep the approved endpoint `https://consolecrate.xyz/meshme-activation/` and replace
the placeholder with the **public** PEM string, keyed by the same signing-key id.
The build copies this public configuration into the output. Never insert private PEM.
No configured endpoint/key means the official client fails closed with a clear
publisher-configuration message; it never silently grants a trial.

Build and test the configured client. First trial/activation is online; successful
paid activation provides a signed lease of up to 30 days and recommends validation
after 7 days. Outage retries back off for at least 24 hours; normal successful
background validation is no more frequent than once per 7 days. Manual Retry is explicit.
Source/License remains accessible from the native entitlement surface.

## 6. Verification before owner acceptance

Run the tests against a **new empty test database**, never production:

```sh
export MESHME_TEST_DSN='mysql:host=127.0.0.1;dbname=meshme_test_acceptance;charset=utf8mb4'
export MESHME_TEST_USER='meshme_test_admin'
# Supply MESHME_TEST_PASSWORD privately.
php tests/run.php
```

Tests refuse non-`meshme_test_*` or nonempty databases. They generate and delete
temporary test signing keys outside the source tree. Test users require DDL on their
test database; runtime production users do not. The test harness starts concurrent
PHP processes to prove the one-device transaction.

Deployment checks:

- `GET https://consolecrate.xyz/meshme-activation/api/v1/health` returns only status/protocol.
- HTTP public API is rejected; TLS hostname/certificate and HSTS are correct.
- `/meshme-activation/src/`, `/config/`, private-key/config paths are inaccessible.
- Public access to the administrator site is denied; trusted MFA access succeeds.
- Start trial explicitly, reinstall/delete local cache, and verify original expiry.
- Activate on PC A, reject PC B, deactivate A, activate B; repeat with admin reset.
- Disconnect network within grace and verify normal editing/save/export.
- Verify wrong hardware, signature tampering, clock rollback and expired grace fail closed.
- Expire/revoke while a project is dirty; verify native Save Current Project and recovery safety.
- Run physical Windows 10/11 tests, light/dark/DPI and both rendering modes.
- Confirm all existing ConsoleCrate endpoints continue unchanged.

## 7. Backup, restore and rollback

Schedule transaction-consistent MariaDB dumps with a backup-only account and encrypted
off-NAS copies. Include devices, original trials, activations and append-only events;
losing these can reset trial history or device-seat decisions. Back up private signing
keys/configuration encrypted separately with access logging and restricted ownership.

Restore drill: restore to a new isolated database/private directory, configure a private
staging endpoint, verify original trial expiry, same-PC reactivation and signed-token
validation, and verify audit continuity before switching production. Never test a restore
by replacing the live ConsoleCrate database. Record backup time and recovery outcomes.

Rollback code by pointing the activation PHP profile back to the prior version; keep
the database and signing keys intact. For this initial install, disabling the isolated
route is the safe rollback. Do not drop trials/activations or delete users' local projects.
The initial migration intentionally has no destructive automatic down migration.
