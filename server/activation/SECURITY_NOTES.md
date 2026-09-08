# Entitlement threat model and limits

Trust boundaries: WPF native host ↔ local WebView, host ↔ HTTPS public API,
public API ↔ MariaDB/private signing key, private administrator site ↔ administrator.
Geometry, files and textures never cross into the activation transport. JavaScript
retains geometry ownership and receives no secret/licensing protocol data.

Defenses implemented: asymmetric token verification plus DPAPI cache protection;
versioned hashed multi-component binding; server UTC trial state; original device
anchors; transactional one-seat enforcement; prepared SQL; strict request shape/
size/content type; IP/key/device rate limits; generic correlated errors; separate
admin login, CSRF, secure session cookies, trusted-network ACL and safe CSV output.
Production audit append-only protection depends on the documented database grants.

Threats and residual limits:

- Copying a normal official token/cache to another PC fails native hardware binding
  (and DPAPI usually fails across Windows accounts). Hashes are pseudonymous identifiers,
  not anonymous data or secrets. Hardware spoofing and deliberate source modification
  remain possible on a client controlled by an attacker.
- Identical/cloned VM hardware identifiers cannot be distinguished reliably without an
  additional owner-approved identity/enrollment policy. Missing strong identities fail
  closed. A conflicting UUID or baseboard hash requires support even if weak IDs match.
- Local clock rollback is compared with a DPAPI-protected high-water mark (five-minute
  tolerance) and requires online validation. No purely local system can reliably detect
  a full disk/VM snapshot rollback with its clock/state restored. The server never resets
  the original trial when a normal reinstall or AppData deletion requests it again.
- Revocation is visible at successful validation or lease expiry; a disconnected client
  can use its already signed lease for up to 30 days. NAS outages do not shorten grace.
- Server compromise/signing-key theft can mint entitlements. Separate least-privilege
  accounts, private key permissions, patched PHP/MariaDB, MFA at the private admin ingress
  and encrypted tested backups remain operational requirements.
- TLS termination and real-client-IP handling must be configured at a trusted boundary.
  The app does not trust arbitrary forwarding headers. Rate limits also need web-server
  enforcement to protect PHP from malformed-request floods and resource exhaustion.
- The private admin portal strongly recommends external TOTP/MFA. It has no built-in
  TOTP provisioning/recovery, customer billing, email delivery or password-reset service.
- Public configuration carries only verification keys. A user able to alter the client
  or replace its trust configuration can modify AGPL-covered behavior; this system does
  not pretend otherwise. Preserve AGPL source access, notices and redistribution rights.

No certificate pinning is used. Normal trusted HTTPS plus signed payloads supports
certificate renewal. Signing-key rotation requires the staged public-key rollout
documented in the deployment guide. Never export production secrets in diagnostics.
