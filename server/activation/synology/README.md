# Dedicated-account Synology package

Owner installation target: DS1821+, DSM 7.3.2-86009 Update 4. The SPK contains PHP
and shell source only (`noarch`); it uses the NAS's PHP 8.2, Apache 2.4, MariaDB 10
and Web Station packages. No Windows UI, licensing protocol or geometry changes.

## Why this replaces the manual PHP profile

The installation reports show the manual MeshMe and ConsoleCrate PHP profiles both
use `http`. A separate profile does not establish a separate operating-system identity.
The milestone requires the signing key to be readable only by the activation service
account (with normal root/owner administrative control). Do not grant the key or DB
configuration to `http` or the `http` group.

Synology's `conf/privilege` creates the package identity `MeshMeActivation`. The
Web Station worker declares two PHP profiles using that identity. Membership of
the frontend `http` group permits serving public files; it must never be used to
grant group-read permission to private secrets. Both entry points check the actual
effective account name and PHP 8.2 before loading the unchanged application.

## Package layout and installation effects

- `/var/packages/MeshMeActivation/target/service`: bundled API/admin source and schema.
- `/var/services/web_packages/meshme-activation-api`: public API wrapper and rewrite rules only.
- `/var/services/web_packages/meshme-activation-admin`: admin wrapper and stylesheet only.
- `/var/packages/MeshMeActivation/var/sessions`: private session storage.
- `/volume1/meshme-private/config.php`: existing owner-managed configuration, outside package and public roots.
- Existing signing-key paths in that configuration remain unchanged.

Installing registers **MeshMe Activation API (Dedicated)** and **MeshMe Activation
Admin (Private)** in Web Station. It creates no portal, opens no new public route,
does not alter Nginx configuration or Cloudflare, and never connects to MariaDB,
runs migrations, generates keys or changes existing file permissions. Installation
and runtime scripts run as the package account, not root. Only a private sessions
directory is created during installation/upgrade.

## First installation checkpoint

1. Keep the existing working manual service/alias and Cloudflare routes in place.
2. In Package Center, use Manual Install and select the supplied SPK. It is an
   unsigned owner development package, not a Synology-certified release.
3. Review the package name and dependencies, then complete installation. If DSM
   rejects the package, retain its exact error; do not bypass system policy or edit
   generated service files.
4. Verify both dedicated services and PHP 8.2 profiles appear in Web Station.
5. Read the generated FPM configuration and confirm `user = MeshMeActivation`
   before granting any secret-file access. Verify existing ConsoleCrate downloads.

Package Center installation and these effective-account checks require the actual
NAS. Local archive/schema/script tests do not prove DSM acceptance.

## Guided cutover after account verification

Do these as separate reviewed steps, not as an automatic installation side effect.

1. Back up the existing private configuration and signing keys securely. Never
   regenerate them during this migration. Record ACLs and keep the database intact.
2. Grant `MeshMeActivation` traversal of `/volume1/meshme-private` and read-only
   access to exactly `config.php`, the configured private key and verification key.
   Inspect effective ACLs, including inherited rules. Keep shared `http` denied
   secret-file reading; remove its now-unneeded traversal entry after cutover.
3. Verify database connectivity and a local sign/verify round trip while running
   as the package account. Do not manufacture a production trial merely to test
   connectivity. Existing database grants remain sufficient.
4. Retarget the existing MeshMe alias portal to **MeshMe Activation API (Dedicated)**
   if DSM permits changing its service. Check the generated alias include referenced
   by ConsoleCrate's custom configuration hook; update that hook only if its name
   changes, using syntax-test/rollback before reload. If service selection is locked,
   plan a replacement alias and rollback before changing the existing portal.
5. Retest the exact HTTPS `/meshme-activation/api/v1/health` and an existing
   ConsoleCrate download. Test an actual signed activation only after the account,
   database, key, HTTPS and HTTP-rejection checks pass.
6. Retire the old manual MeshMe service/profile after successful validation. Its
   `/volume1/meshme-services/activation` source copy and `Y:` files can remain for
   rollback; do not grant them access to the key through shared PHP.

The Cloudflare Tunnel's MeshMe path rule must remain before the catch-all rule,
using `https://10.0.0.113:443`, Origin Server Name `consolecrate.xyz`, certificate
verification enabled. Keep the Let's Encrypt certificate assigned to that host.
The installer does not manage certificate renewal or tunnel settings.

**HTTP guard:** the tunnel can upgrade a visitor's HTTP request into HTTPS to the
NAS. Before production, configure a Cloudflare rule for this hostname/path that
rejects incoming HTTP, or verify the owner's chosen HTTPS enforcement behavior.
Test actual HTTP POST rejection without sending a real activation key. The PHP
HTTPS check alone proves transport at the NAS, not the visitor-to-Cloudflare hop.

## Administrator service

The package does not publish the administrator site. Create a separate private
HTTPS portal only after deciding its LAN/VPN/MFA ingress and configuring the exact
trusted IPs. The existing default configuration allows loopback only. Do not attach
the administrator service to the public MeshMe alias or a general tunnel route.
Bootstrap the administrator using `service/bin/bootstrap-admin.php` with the
package-account configuration environment and a privately supplied password.

## Rollback, upgrade and acceptance

Until cutover, uninstalling the new package only removes its registered services
and package-owned public roots. Owner-held keys/configuration, database, old source
and ConsoleCrate routes are outside the package and are never deleted by its scripts.
After cutover, restore the previous alias configuration before uninstalling. The old
shared-account service must remain unlicensed/fail-closed; rollback must not grant
its account access to the signing key. Back up package logs/session state as needed
before upgrades. The package account is an application security boundary, not a
defense against NAS root or a compromised privileged frontend/FPM administrator.

Production acceptance still requires: key-read denial for other accounts; correct
effective account after restart/upgrade; DB least privilege; real signed round trip;
public/private ingress checks; HTTP rejection; certificate renewal; backup/restore;
and all remaining owner milestone gates. This SPK does not close issue #2.

## Build and validation

```text
python server/activation/synology/build_package.py <output.spk>
python -m unittest discover -s server/activation/synology/tests -v
```

Sources for packaging metadata:
- https://help.synology.com/developer-guide/examples/compile_web_package.html
- https://help.synology.com/developer-guide/resource_acquisition/web_service.html
- https://help.synology.com/developer-guide/synology_package/INFO_necessary_fields.html
- https://raw.githubusercontent.com/SynoCommunity/spksrc/master/spk/demowebservice/src/conf_php8.2/resource

PHP backend `11` is explicitly PHP 8.2 in the maintained demo resource; Apache
backend `2` is Apache 2.4 in Synology's specification. No third-party application
source or downloaded reference SPK is redistributed in this package.
