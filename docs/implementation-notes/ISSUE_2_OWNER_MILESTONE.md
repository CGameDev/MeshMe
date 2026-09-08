# Issue #2 implementation and acceptance record

Baseline: `3c949d6` (main). Read `AGENTS.md` and the complete
`docs/milestones/OWNER_MILESTONE_UX_GRAPHICS_AND_ACTIVATION.md` before editing.
The owner milestone governs this additive work; the locked PNG remains the UI authority.
The owner selected `https://consolecrate.xyz/meshme-activation/` and requested a
**Synology installation package**, not live deployment. Existing `Y:\` contents were
inspected read-only; no ConsoleCrate service/configuration/data was changed.

## Requirement map

| Milestone | Existing integration point | Implementation |
|---|---|---|
| 4.1 Suggested Action | Existing status span; revision-aware diagnostics | At most primary/secondary actions; no fabricated diagnostic result; busy state suppresses actions |
| 4.2 Basic/Advanced | Texture expander and Repair parameters | Advanced collapsed; original controls retained; Apply position preserved |
| 4.3 Ctrl+K | CommandRegistry | Keyboard search of commands, preferences, recent entries, presets, export formats and bundled topics; shared recent handlers |
| 4.4 Model Health | Existing issue indicator / Mesh Diagnostics | Mouse/keyboard entry, real geometry/wall/bed checks, triangle warning, existing reviewable repair route |
| 4.5 Quality | Existing Make Solid/Refine/Simplify inputs | Explicit deterministic Draft/Standard/High assignments; manual input selects Custom |
| 4.6 Context menus | Project rows, viewport, library source cards | Transient context actions, selected model handling, no permanent toolbar changes |
| 4.7 F1 | Help command registration | Bundled contextual topics, generic Documentation fallback |
| 4.8 Tooltips | Existing command buttons and fields | Explanatory purpose/when-to-use text and real unavailable reasons |
| 4.9 Drop | Existing engine/project loaders | Model/project/legacy dispatch, explicit image-intent choice, incompatible mixed-drop rejection |
| 4.10 Empty state | Existing viewport | Open/drop/recent/goals route into existing workflows |
| 5 Graphics | AppSettings, MainWindow startup, Preferences and Diagnostics | Automatic default, documented GPU-disabled compatibility mode, persisted restart requirement, Windows preference shortcut, truthful renderer info and narrowly classified recovery |
| 6 Trial/license | New native Core/Desktop licensing services | Explicit native start/activate, signed seven-day trial, 7-day validation/30-day lease defaults, native effective entitlement gating, non-destructive saved-project recovery |
| 7 Hardware | Native WMI/registry | Versioned local SHA-256 component hashing; no raw serial transmission/storage; weighted matching and strong-conflict veto |
| 8 Signing | Core verifier / PHP Tokens | RSA-3072 SHA-256 signatures; public-only client configuration; DPAPI protected cached token/trusted time |
| 9–12 API/database/security | New server/activation | Separate API, schema, locked transactions, strict boundaries, key/device/IP limits, generic correlated errors and append-only audit grants |
| 13 Administration | Separate private admin-public root | Counts, batch keys, one-time reveal/download, search, statuses/device reset/override, trials/history/audit and safe CSV; network ACL, sessions, CSRF |
| 14–16 Privacy/failure/architecture | Native service and entitlement UI | Keys remain native; model data never enters transport; retry/grace/rollback behavior; AGPL access preserved |
| 17–19 Validation/deployment | Existing test runners plus new fixtures | Native/Core/web/PHP/MariaDB tests, protocol/security/deployment guides, explicit pending acceptance gates |

## Exact defaults and implementation decisions

- Quality: Make Solid uses longest dimension / 16, / 32, / 64 mm for Draft,
  Standard, High. Refine maximum edge length is 10/5/2.5 mm. Simplify target is
  floor(25/50/75% of input triangles), minimum four. Selecting a preset changes the
  actual input; editing it selects Custom. Existing geometry/resource-limit logic is unchanged.
- Graphics uses Automatic or `--disable-gpu` Software Compatibility. Physical adapter
  preference is delegated to Windows Graphics Settings; no undocumented registry writes
  or unverified “Force GPU” claims. Reported renderer strings remain authoritative observations.
- Trial has exact 604,800-second server bounds. Paid leases cap at 2,592,000 seconds.
  Normal successful validation is recommended after seven days; outage retry is throttled
  to 24 hours. Explicit manual retries can bypass that throttle. Five-minute local clock
  correction tolerance does not change server expiry.
- Matching policy v1 retains original device anchors, requires 60 weighted points,
  and rejects conflicting UUID/baseboard values. Insufficient hardware identity fails
  closed with support guidance; it does not invent a weak identity. Clone/VM and full
  snapshot rollback limits are documented in the security notes.
- The service serializes product mutations before key rows. This is intentionally
  conservative for NAS traffic and makes tolerant hardware recognition/trial uniqueness
  and concurrent seat enforcement share one transaction boundary.
- Native expiry hides/disables the workspace and offers the native license surface.
  A loaded document can still be saved through its existing authoritative Save handler.
  No licensing path deletes a project, recovery file or texture. Safe export/view-only
  expiry mode is optional in the owner specification and is not a separate new editor.
- The source-only client is unconfigured until the owner supplies the public verification
  key. Endpoint/private-key generation and NAS deployment remain explicit owner operations.
- The separate administrator ingress requires trusted LAN/VPN or an MFA identity proxy;
  TOTP is strongly recommended there. No new billing/customer-email service was introduced.

## Validation and limits

- .NET 8 solution builds; Core licensing/graphics tests plus existing Core checks pass.
- PHP 8.2.33 / MariaDB 10.11.16 integration covers clean migration, original trial expiry,
  reactivation, concurrency, deactivation/reset/revoke, injection handling, real API
  request boundaries, admin login/cookies/CSRF and correlated secret-free audit records.
- A PHP-signed token verifies using the .NET verifier, with only the public key crossing.
- Node tests and offline-asset checks pass; 117 pinned upstream blobs remain identical.
- Actual WebView issue #2 tests cover command search/arrows/Escape, F1, progressive
  controls, Model Health, quality/custom input, context menus, image choices, Preferences,
  native WMI hashing and Windows DPAPI. Native first-run trial uses a fake service.
- Existing native suite: 27 of 29 modes pass, including light/dark display, emulated
  100–200% density, geometry and 100k/~1M/2.4M stress. `--locked` produces evidence,
  not a full acceptance pass. Software Compatibility starts actual WebView and reports diagnostics.
- `--exports` and `--print` time out on the candidate **and on untouched main-branch
  web assets**. Both fail after the compact Export presentation is used. These pre-existing
  failures are recorded rather than changing unrelated export/menu behavior in issue #2.
- Structural layout is checked against the PNG's existing measured panel/control targets.
  Basic/Advanced keeps the Apply command at its approved position. Human visual acceptance,
  physical Windows 10/11 adapter/restart/expiry testing, configured NAS HTTPS/MFA checks,
  and packaged release qualification remain pending.

Do not close issue #2 or declare the owner milestone/release complete until the pending
gates and baseline export/print blockers have been resolved and the owner accepts the result.
