# Mesh Me360 — Owner Milestone: UX Simplification, Graphics Controls, Trial & PC Activation

**Status:** REQUIRED OWNER ENHANCEMENT  
**Date:** 2026-09-07  
**Repository:** `CGameDev/MeshMe`  
**Product owner/publisher credit:** **Computer Universal Technology Systems**

---

# 1. Goal

Improve Mesh Me360 without making the approved interface complicated or overwhelming, and add a professional 7-day trial plus online license activation service hosted on the owner's Synology NAS.

This is an additive owner milestone. It does **not** authorize redesigning the locked Mesh Me360 interface, changing toolbar order, removing current functionality, rewriting geometry algorithms, or renumbering the existing M4–M11 implementation plan.

The approved locked UI remains the visual authority. New features must use progressive disclosure, contextual assistance, Preferences, existing status/diagnostics surfaces, compact dialogs and context menus rather than adding permanent clutter.

This owner milestone also creates a narrowly scoped network exception to the older offline-only wording: model editing/rendering/export must remain offline-capable after a valid cached entitlement exists, but starting a trial, first license activation, periodic entitlement validation, deactivation and official update checks may use the network.

---

# 2. Non-negotiable rules

1. Preserve `docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png` and existing command order.
2. Do not add another permanent workspace, ribbon, sidebar or dashboard.
3. Do not move existing visible commands merely to simplify implementation.
4. Keep the central viewport the dominant region.
5. Heavy geometry remains owned by the existing web/worker architecture; licensing belongs in the native host/service layer.
6. No geometry, project file, texture content or user model data is ever sent to the activation server.
7. No server secret, database credential or private signing key may be embedded in the Windows client or committed to Git.
8. Activation security must not depend on obscurity. Assume the client protocol and source can be inspected.
9. Never store raw motherboard/system serial numbers in MariaDB. Normalize and hash hardware components locally before transmission.
10. One normal license key permits one active physical/virtual PC at a time unless the database record explicitly changes `max_devices`.
11. Reinstalling Mesh Me360 on the same recognized PC must not consume an additional activation.
12. A normal Windows reinstall or storage replacement should not unnecessarily invalidate a license when the strong motherboard/system identity is unchanged.
13. A motherboard/system replacement should normally count as a new PC and require deactivation/reset or administrator override.
14. All API traffic must use HTTPS. Production HTTP activation is forbidden.
15. Never silently start the trial on installation or first launch. The user explicitly chooses **Start 7-Day Trial**.
16. Trial timing is server-authoritative UTC time. Reinstalling/deleting AppData must not restart a trial on the same recognized hardware.
17. Trial expiration must never delete or corrupt user projects.
18. Existing AGPLv3 source/license/attribution obligations remain intact.

---

# 3. AGPL distribution boundary — mandatory

Mesh Me360 contains/adapts the pinned AGPLv3 BumpMesh/stlTexturizer engine. This activation system is therefore an entitlement system for **official Computer Universal Technology Systems builds/services**, not a claim that AGPL recipients lose their rights to inspect, modify or redistribute covered source/code under the AGPL.

Do not add legal wording that claims users are forbidden from exercising AGPL rights. Do not remove the Source Code / License access already required by the repository.

The activation design may make casual copying of the official binary ineffective by binding the official activation token to a recognized PC, but contributors must not claim it can make modification of AGPL-covered source impossible.

If the owner later requires a truly proprietary non-redistributable product, that is a separate licensing/business decision requiring an upstream commercial license or replacement of AGPL-covered components. It is outside this milestone.

---

# 4. UX simplification scope

Implement the following without changing the locked visual hierarchy.

## 4.1 Contextual Suggested Action

Use the existing status/context region to offer at most one primary suggested next action and optionally one secondary action.

Examples:
- imported mesh has open edges → `Recommended: Repair Mesh`;
- repaired watertight mesh → `Next: Texture Model` / `Prepare for Print`;
- two suitable objects selected → `Boolean` / `Combine`;
- active texture preview → `Apply Texture` / `Emboss` / `Deboss`.

Rules:
- suggestions are based on real current state/diagnostics;
- never block normal workflow;
- never create fake AI/cloud dependency;
- no persistent tutorial overlay.

## 4.2 Progressive Basic / Advanced controls

Use existing expanders to keep common controls visible and technical parameters collapsed by default.

Examples:
- Texture basic: Source, Scale, Depth, Strength, Apply;
- Texture advanced: Direction, Blend Mode, projection/mapping and specialist controls;
- Hollow basic: Wall Thickness, Preview, Apply;
- Hollow advanced: drains, quality and diagnostics options;
- Repair basic: Scan, Repair;
- Repair advanced: safe tolerances and individual repair choices.

Do not remove advanced capability.

## 4.3 Ctrl+K Command Search

Add a non-permanent command palette opened by `Ctrl+K`.

Search:
- existing commands/tools;
- Preferences entries;
- recent projects;
- texture/library presets;
- export formats;
- local documentation topics.

Every result must invoke the same authoritative command handler used by menus/toolbars. Do not create parallel implementations.

## 4.4 Clickable Model Health

The existing issue indicator opens a compact Model Health view showing truthful current checks such as:
- watertight/manifold state;
- open/non-manifold edges;
- normals;
- degenerate geometry;
- triangle-count concern;
- sampled/local wall warning where supported;
- placement on build plane.

Offer safe existing repair actions when applicable. Do not claim a local sampled thickness check certifies global minimum wall thickness.

## 4.5 Quality presets

Where a tool has several technical quality parameters, expose:
- Draft;
- Standard — Recommended;
- High Quality;
- Advanced / Custom.

Presets must map to documented actual parameters and remain deterministic. They must not silently override a user's explicit custom values.

## 4.6 Context menus

Add context-sensitive right-click actions without adding permanent UI.

Single model examples:
- Rename, Duplicate, Hide/Show, Lock/Unlock;
- Center Model, Place on Bed;
- Repair Mesh, Make Solid;
- Export Selected;
- Delete.

Multi-selection examples:
- Boolean when valid;
- Combine;
- applicable transform/visibility operations.

Texture/library examples:
- Apply;
- Favorite;
- Rename custom item;
- duplicate/custom export where implemented;
- Delete only for user-owned items.

## 4.7 Context-sensitive F1 documentation

Pressing F1 while a tool/dialog/control has context should open the corresponding section of the bundled offline documentation. Generic F1 opens the guide index.

## 4.8 Improved tooltips

Tooltips explain when/why to use a command, not merely repeat the command name. Keep them concise and accessible.

## 4.9 Smart drag and drop

- STL/OBJ/3MF/STEP/STP → import model;
- `.mm360` → open project;
- legacy `.bumpmesh` → legacy conversion/open flow;
- supported image dropped with an applicable model selected → offer `Use as Texture` or `Use as Displacement Source` without guessing.

## 4.10 Goal-based empty state

When no document is active, keep the viewport simple:
- Open Model;
- drop supported model/project here;
- recent projects;
- optional compact goals: `Texture a Model`, `Repair a Model`, `Prepare for 3D Printing`.

These goals only route to existing workflows. Do not build duplicate wizard engines.

---

# 5. Graphics / GPU preferences

Add under **Preferences → Performance → Graphics**. Do not add GPU controls to the main toolbar.

## 5.1 Rendering mode

Default: **Automatic (Recommended)**.

Required states:
- `Automatic / Windows Default`;
- `Prefer Power Saving GPU` where a supported Windows process/GPU preference can be requested;
- `Prefer High Performance GPU` where a supported Windows process/GPU preference can be requested;
- `Software Compatibility Mode` as a troubleshooting fallback.

The UI wording must say **Prefer**, not **Force**, unless the active renderer can prove a specific adapter was selected.

If the Windows/WebView2 stack cannot support a programmatic adapter preference reliably, expose an `Open Windows Graphics Settings` action rather than writing undocumented registry values or pretending a physical adapter was forced.

## 5.2 Hardware acceleration

WebView2 hardware acceleration remains enabled by default.

Software Compatibility Mode may create WebView2 with the documented GPU-disabled troubleshooting path. It must:
- require restart;
- display a performance warning;
- never become the default after a temporary startup failure unless the user accepts the change;
- be reversible.

Do not change geometry algorithms when switching rendering mode.

## 5.3 GPU failure recovery

If WebGL/GPU startup fails with an actionable graphics initialization error, the existing native fatal-startup surface may offer:

`Restart in Compatibility Mode`

This must not hide unrelated startup errors.

## 5.4 Application Diagnostics

Report, where available:
- Mesh Me360 version;
- WebView2 version;
- WebGL version;
- renderer/vendor string;
- hardware acceleration active/inactive;
- requested GPU preference;
- actual renderer information exposed by WebGL/WebView2;
- compatibility-mode state.

Never claim the actual GPU equals the requested preference unless verified.

---

# 6. Licensing UX

Licensing must be professional but visually quiet.

## 6.1 First-run entitlement screen

When no valid trial/license entitlement exists, show a compact branded native/host-owned entitlement surface with two primary choices:

- **Start 7-Day Trial**
- **Activate License**

Also provide:
- Source Code / License access required by AGPL;
- Privacy/activation-data summary;
- Exit.

Do not display this as part of the permanent modeling workspace.

## 6.2 Trial

- Full normal editing functionality for 7 calendar days.
- Trial begins only after explicit user action and a successful server response.
- Internet is required to start the trial.
- Server stores the original start/expiry for the recognized hardware.
- Reinstalling the app does not reset the trial.
- Trial state is represented by a server-signed token cached locally.
- Display remaining time unobtrusively, e.g. in Help > License and a restrained status message near expiry.
- At expiry, do not delete or damage projects/recovery files.
- Expired mode may permit project viewing/recovery and safe export of the user's existing state, but must not silently provide a new editable trial.

## 6.3 Activation key format

Human format:

`XXXXX-XXXXX-XXXXX-XXXXX-XXXXX`

Requirements:
- exactly 25 key symbols excluding hyphens;
- uppercase, case-insensitive input;
- use an ambiguity-resistant 32-symbol alphabet (do not use visually confusing `I`, `L`, `O`, `U`);
- 24 cryptographically random symbols + one checksum/check symbol is acceptable and provides approximately 120 bits of random key entropy;
- generated only server-side using a cryptographically secure random generator;
- no customer/license metadata encoded into the key;
- never use sequential/predictable keys.

Normalize by removing spaces/hyphens and uppercasing before checksum/lookup.

## 6.4 License behavior

Initial product license type: **one active PC**.

Rules:
- same key + same recognized PC = reactivation, not a second seat;
- same key + different PC while one active PC exists = reject with a clear message;
- user can deactivate the current PC while online;
- administrator can revoke/reset an activation from the Synology portal;
- losing/replacing a motherboard may require reset;
- database supports future `max_devices` > 1, but do not expose unrequested multi-seat UI now.

## 6.5 Offline licensed use

First activation is online.

After activation, cache a server-signed hardware-bound entitlement so normal model work can continue offline.

Recommended balance:
- attempt background validation no more often than every 7 days;
- signed licensed lease/grace allows up to 30 days since the last successful validation before requiring the server again;
- validation failure because the NAS/network is temporarily unavailable must not immediately block a paid user;
- explicit server revocation takes effect on the next successful validation or when the signed lease/grace expires.

The exact interval may be made owner-configurable on the server, but must have safe defaults and tests.

---

# 7. Hardware identity design

Do **not** lock the license to a single brittle identifier such as MAC address or disk volume serial.

Create a versioned multi-component hardware fingerprint in the native .NET host.

Recommended normalized components:
- SMBIOS/System UUID (`Win32_ComputerSystemProduct.UUID`) — strong component;
- baseboard serial — strong component when non-placeholder;
- BIOS/system serial — secondary component when valid;
- Windows MachineGuid — weak/fallback component because a Windows reinstall can change it.

Reject known invalid placeholder values such as all-zero UUIDs, empty serials and common OEM placeholders.

Do not use MAC address as a strong identity component.

## 7.1 Privacy-preserving hashing

For each valid component:
1. trim/normalize/case-normalize locally;
2. namespace it by component type and fingerprint schema version;
3. hash locally with SHA-256;
4. send only component hashes plus a combined fingerprint/version to the API.

Never transmit raw serial numbers if the hashed form is sufficient.

## 7.2 Same-PC matching

The server must use a documented versioned matching policy that tolerates a minor legitimate change while treating motherboard/system replacement as a new PC.

Suggested weighted policy to test, not silently change:
- System UUID: 50;
- Baseboard serial hash: 25;
- BIOS/system serial hash: 15;
- MachineGuid hash: 10;
- recognized same-PC threshold: 60.

Missing/invalid components must be handled explicitly; do not fabricate values. Record the fingerprint schema/policy version with each activation so a future policy can migrate safely.

A Windows reinstall with the same strong hardware should normally remain the same device. A motherboard replacement should normally fail the threshold and require transfer/reset.

## 7.3 Local token protection

Cache the signed entitlement under Mesh Me360 application data and protect the local copy with Windows DPAPI in addition to the server signature.

Because the installer is currently per-user, a second Windows account on the same PC may need to reactivate with the same key; the server must recognize the same hardware and not count another seat.

Do not rely on DPAPI as the license signature. The server signature remains authoritative.

---

# 8. Signed entitlement protocol

Never put a symmetric master secret in MeshMe360.exe.

Use asymmetric signatures:
- server keeps the private signing key;
- client embeds only the public verification key;
- server signs trial/license entitlement payloads;
- client verifies before accepting any cached entitlement.

Use a well-supported cross-platform scheme available in .NET 8 and PHP/OpenSSL, for example RSA-3072 + SHA-256, unless implementation verification establishes a better compatible choice.

Private key requirements:
- generated outside the application repository;
- stored outside the Web Station document root;
- readable only by the activation service account;
- never logged;
- never returned by an API;
- backed up securely;
- support signing-key ID/version for future rotation.

Signed payload fields should include at minimum:
- protocol/token version;
- product ID (`meshme360`);
- entitlement type (`trial` or `license`);
- server-side license/trial ID;
- fingerprint schema version;
- hashed hardware component set or signed device-binding representation needed for offline comparison;
- issued-at UTC;
- trial expiry or lease expiry;
- next-validation recommendation;
- signing-key ID;
- random token ID/nonce.

The client must verify signature, product, time bounds and current hardware binding before enabling entitlement.

---

# 9. Synology activation service

Target owner deployment:
- Synology DSM Web Station;
- PHP 8.2;
- MariaDB 10;
- HTTPS only;
- HSTS on the production activation hostname where appropriate.

Keep the activation API isolated from Mesh Me360 project/model storage.

Suggested repository/deployment separation:

```text
server/activation/
├─ public/                 # only public web root
│  └─ index.php
├─ src/
│  ├─ Api/
│  ├─ Licensing/
│  ├─ Hardware/
│  ├─ Security/
│  └─ Admin/
├─ config/
│  └─ config.example.php   # NO real secrets
├─ migrations/
├─ tests/
└─ README_DEPLOY_SYNOLOGY.md
```

Production secrets/database passwords/private keys must live outside Git and preferably outside the public document root.

---

# 10. Database contract

Use InnoDB, UTC timestamps and prepared statements.

Minimum logical tables:

## `products`
- `id`;
- `code` unique (`meshme360`);
- `name`;
- `active`;
- validation/grace defaults;
- created/updated UTC.

## `license_keys`
- `id` UUID/binary identifier;
- `product_id`;
- `key_hash` unique (SHA-256 of normalized full key; do not store plaintext key by default);
- `key_hint`/masked prefix+suffix for administration;
- `status` (`active`, `suspended`, `revoked`);
- `max_devices` default 1;
- optional customer/reference metadata;
- optional license expiry for future use, nullable for perpetual;
- created/updated UTC.

The admin portal displays the full key only at generation time unless a separately reviewed encrypted-at-rest recovery design is implemented. Lost keys can be replaced/revoked rather than storing them casually in plaintext.

## `devices`
- `id`;
- fingerprint schema version;
- combined fingerprint hash;
- individual component hashes required for tolerant matching;
- first_seen/last_seen UTC;
- optional OS/app metadata;
- no raw serials.

## `activations`
- `id`;
- `license_key_id`;
- `device_id`;
- status (`active`, `deactivated`, `revoked`);
- activated_at;
- last_validated_at;
- deactivated_at/revoked_at nullable;
- last app version;
- signing/token metadata where useful.

Enforce active-device count transactionally; concurrent activation requests must not allow two active PCs when `max_devices=1`.

## `trials`
- `id`;
- device binding/fingerprint reference;
- started_at server UTC;
- expires_at server UTC;
- status;
- last_seen/validated time.

One recognized PC gets one trial for this product. Reinstall does not create another record.

## `activation_events`
Append-only audit records for:
- key generated;
- trial started;
- activation accepted/rejected;
- validation;
- deactivation;
- reset/revoke/suspend;
- admin changes;
- rate-limit/security event.

Never log full activation keys, raw hardware serials, private keys or model paths/content.

---

# 11. Public API contract

Version all endpoints under `/api/v1/` and return JSON with stable error codes plus safe user-facing messages.

Minimum endpoints:

- `POST /api/v1/trial/start`
- `POST /api/v1/activate`
- `POST /api/v1/validate`
- `POST /api/v1/deactivate`
- `GET /api/v1/health` — minimal non-sensitive service health only

Requests include protocol version, app version, product code, fingerprint schema and hashed device components as applicable.

Responses must never reveal whether arbitrary guessed keys are close/valid beyond the minimum needed for the submitted activation attempt.

Use generic stable errors such as:
- `INVALID_KEY`;
- `KEY_REVOKED`;
- `DEVICE_LIMIT_REACHED`;
- `TRIAL_ALREADY_USED`;
- `TRIAL_EXPIRED`;
- `DEVICE_CHANGED`;
- `UPDATE_REQUIRED` only if protocol compatibility truly requires it;
- `RATE_LIMITED`;
- `SERVICE_UNAVAILABLE`.

---

# 12. API and server security

Mandatory:
- HTTPS only in production;
- HSTS where deployment allows;
- PDO prepared statements; no SQL string concatenation from user input;
- least-privilege MariaDB service account;
- separate administrative credentials from DB credentials;
- rate limiting by IP plus submitted key/device dimensions;
- exponential/backoff behavior after repeated failed activation attempts;
- request size limits;
- strict JSON/content-type validation;
- constant-time comparison where secrets/hashes require it;
- no verbose PHP/database exceptions returned to clients;
- structured server logs with request correlation IDs;
- secrets outside web root and Git;
- backups for DB and private signing key;
- schema migrations, not manual production-only edits;
- CSRF protection on browser admin actions;
- `Secure`, `HttpOnly`, `SameSite=Strict` admin session cookies;
- password hashing using PHP's current strong password API (Argon2id when available);
- strongly recommend TOTP/second factor for the admin portal;
- strongly prefer limiting the admin portal to trusted LAN/VPN/identity-proxy access even though the activation API itself is public.

Do not add certificate pinning unless there is a documented key/certificate rotation strategy; normal trusted HTTPS plus signed entitlements is the default.

---

# 13. Synology admin portal

Create a simple professional admin UI separate from the public activation API.

Required capabilities:
- dashboard counts: active keys, active activations, trials, recent rejects;
- generate one or batch activation keys;
- one-time copy/download of newly generated keys;
- search by key hint, customer/reference, activation ID or device hash prefix;
- view masked key status and activation history;
- activate/suspend/revoke key;
- reset/deactivate a device;
- change `max_devices` as an administrative override;
- inspect trial start/expiry;
- see app version/last validation and hashed device identity only;
- audit-log viewer;
- CSV export of administrative metadata without secrets/raw serials;
- confirmation for destructive revoke/reset actions.

No dependency on phpMyAdmin for routine licensing operations.

---

# 14. Privacy and user messaging

Activation UI must state, concisely, that Mesh Me360 sends only licensing information needed for activation, including hashed device identifiers, app version and service/security metadata; no model/project/texture contents are uploaded.

Provide an easy way to copy the non-secret Device ID/fingerprint prefix for support.

Logs and diagnostics shown to users must not expose the full activation key.

---

# 15. Failure behavior

## Server unavailable
- trial not yet started: explain that an internet connection is required to begin;
- first activation: explain activation service cannot be reached and preserve entered key only for the current dialog/session if appropriate;
- already activated and within signed offline grace: continue normal operation and retry later without nagging every launch;
- grace exhausted: show activation/validation recovery dialog, not a crash.

## License revoked/suspended
Show a clear non-destructive entitlement message. Do not delete user data.

## Hardware mismatch
Explain that the license appears activated to another PC or the computer hardware changed. Offer Deactivate/Retry/Support guidance as applicable.

## Clock rollback
Use server-signed times and stored trusted validation state to detect material rollback. Do not extend a trial because the local clock was moved backward. Allow a small tolerance for normal clock correction.

---

# 16. Required client architecture

Add native host services/contracts rather than putting license secrets in JavaScript:

Suggested boundaries:
- `ILicenseService` / `LicenseService`;
- `IHardwareFingerprintService`;
- `IActivationApiClient`;
- `IEntitlementTokenVerifier`;
- `LicenseState` domain model;
- native activation/trial UI bridge;
- Preferences/Help license status view;
- tests with a fake/local activation transport.

The WebView/engine receives only the minimum effective entitlement state needed to enable normal UI, not activation keys or private protocol secrets.

Do not block or alter geometry algorithms based on hidden JS flags that can become inconsistent with native entitlement state.

---

# 17. Testing requirements

## UX
- locked UI layout remains structurally unchanged;
- Ctrl+K keyboard navigation and command execution;
- progressive Advanced controls;
- contextual suggestions based on real state;
- context menus;
- F1 context help;
- smart drag/drop;
- Model Health actions;
- light/dark themes and DPI.

## Graphics
- Automatic default;
- preference persistence/restart behavior;
- software compatibility restart;
- hardware acceleration diagnostics;
- startup recovery offered only for relevant GPU/WebGL failure;
- no geometry/output difference caused solely by renderer preference.

## Trial
- first trial starts from server UTC;
- exactly seven calendar days;
- reinstall/local-data deletion does not reset server trial;
- same recognized hardware returns same trial record;
- expired trial cannot restart;
- local clock rollback does not extend trial;
- user projects survive expiry.

## Activation
- valid 25-symbol key activates;
- lower/uppercase and hyphen/space normalization;
- checksum typo rejection before unnecessary server lookup where appropriate;
- invalid/revoked/suspended key errors;
- one active PC enforced under concurrency;
- same-PC reactivation does not consume another slot;
- different PC is rejected when slot occupied;
- deactivation frees the slot;
- admin reset works;
- copied local entitlement fails hardware binding on another machine;
- tampered entitlement/signature fails closed;
- expired signed lease follows grace/validation policy;
- private key never appears in client/repo/build output.

## Hardware tolerance
Fixtures for:
- same machine same values;
- Windows reinstall changing only MachineGuid;
- SSD/storage change;
- missing OEM serial placeholder;
- motherboard/system identity change;
- VM/duplicate identifier edge cases documented.

## API/security
- prepared statement/injection tests;
- malformed/oversized JSON;
- rate limiting;
- replay/repeated request idempotency where needed;
- concurrent activation transaction;
- admin CSRF/session tests;
- no raw key/hardware serial in logs;
- database migration up/down or clean install test;
- backup/restore procedure documented.

---

# 18. Deployment deliverables

Codex must produce:
- client implementation and tests;
- server activation service source;
- MariaDB migration/schema files;
- Synology deployment guide for Web Station + PHP 8.2 + MariaDB 10;
- `.env`/config example with placeholders only;
- signing-key generation/rotation instructions;
- admin bootstrap instructions;
- HTTPS/HSTS deployment checklist;
- backup/restore instructions;
- API protocol documentation;
- threat-model/security notes;
- owner acceptance checklist.

Never commit real NAS hostnames if the owner has not explicitly approved them, database passwords, API/admin passwords, activation keys, customer data or private signing keys.

---

# 19. Definition of done

This owner milestone is complete only when:

- [ ] Existing approved Mesh Me360 layout is not redesigned.
- [ ] All ten UX simplification features are implemented and tested.
- [ ] Graphics preferences live only in Preferences/Diagnostics/recovery surfaces.
- [ ] Hardware acceleration defaults on; compatibility fallback works.
- [ ] GPU preference is truthful and does not claim an unverified physical adapter.
- [ ] Application Diagnostics reports real renderer state.
- [ ] 7-day trial is server-authoritative and cannot be reset by normal reinstall/AppData deletion on the same recognized PC.
- [ ] 25-symbol activation keys are cryptographically generated and checked.
- [ ] One-active-PC limit is enforced transactionally on the Synology service.
- [ ] Hardware identity is multi-component, versioned and privacy-preserving.
- [ ] Raw hardware serials are not stored in the DB.
- [ ] Trial/license tokens are asymmetrically signed and hardware-bound.
- [ ] No private signing key or server credential exists in the Windows client/repository.
- [ ] Paid activation can tolerate temporary server outage within documented grace.
- [ ] Same-PC reinstall/reactivation does not consume another activation.
- [ ] Deactivation and administrator reset/revocation work.
- [ ] Synology admin portal manages keys, trials, activations and audit history.
- [ ] Public API is HTTPS-only in production and rate-limited.
- [ ] User model/project content never goes to the licensing service.
- [ ] Expiry/revocation never deletes user projects.
- [ ] AGPL source/attribution rights remain visible and intact.
- [ ] Server/client tests and deployment documentation pass owner review.

Do not declare this complete because an activation dialog appears or because a key validates once. The full client, signed-token, hardware-binding, server database, admin, failure handling, privacy, security and regression gates are required.