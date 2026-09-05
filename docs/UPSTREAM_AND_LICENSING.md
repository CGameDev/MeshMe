# Mesh Me360 — Upstream and Licensing Rules

## 1. Upstream project

Mesh Me360 is based on:

- Project: **BumpMesh / stlTexturizer**
- Author: **CNC Kitchen (Stefan Hermann) and contributors**
- Repository: `https://github.com/CNCKitchen/stlTexturizer`
- Pinned baseline commit: `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1`
- Baseline release represented by that commit: `v1.2.0`
- Upstream license: **GNU Affero General Public License v3.0**

The baseline must be imported before later upstream changes are considered. Do not start from an arbitrary moving `main` branch and then claim the pinned SHA as the base.

## 2. Why the baseline is pinned

Pinning gives Mesh Me360:
- a reproducible source baseline;
- deterministic regression comparison;
- clear provenance;
- a controlled point for future upstream merges.

Any later upstream sync must be a deliberate separate change and must record the new source commit(s).

## 3. Import location

Import the upstream web application under:

`src/MeshMe360.EngineWeb/upstream/`

Add:

`src/MeshMe360.EngineWeb/UPSTREAM_BASELINE.md`

with:

```text
Upstream: https://github.com/CNCKitchen/stlTexturizer
Baseline SHA: a6ac179149b8a17c71a9469dd4cb6f866c0c01d1
Baseline version: v1.2.0
Imported date: <UTC date>
Imported by: <developer/automation>
Local product: Mesh Me360
```

## 4. Preserve notices

When upstream files contain:
- copyright notices,
- author notices,
- SPDX identifiers,
- license comments,

preserve them in modified files.

Do not replace an upstream copyright line with CGameDev attribution. Mesh Me360 additions can carry their own copyright in addition to upstream notices where appropriate.

## 5. Mesh Me360 modification notices

For files modified from upstream, include a clear modification notice where practical, for example:

```text
Modified for Mesh Me360 by CGameDev / Brian Hinds, 2026.
Original work: BumpMesh / stlTexturizer by CNC Kitchen and contributors.
SPDX-License-Identifier: AGPL-3.0-only
```

Do not remove the original author/license notice to add this.

## 6. Repository license

Because Mesh Me360 is intended to distribute a modified/combined version of the AGPL-covered upstream program, Codex must treat the combined application source as **AGPL-3.0-only** unless the repository owner obtains a different compatible permission/license from the relevant copyright holders.

Milestone 0 must add the complete GNU AGPL v3 license text to top-level `LICENSE` before imported upstream code is committed.

## 7. Corresponding source

A distributed Mesh Me360 build must make the corresponding source available as required by the license.

At minimum, release documentation/UI should make it straightforward for a recipient to find:
- the Mesh Me360 source repository;
- build scripts/configuration;
- modified upstream source;
- dependency/version information needed to build the covered work;
- license text.

Do not ship an executable-only release with no source-code path/notice.

## 8. Interactive legal notices

Help > About must include:
- `Mesh Me360 is free software licensed under GNU AGPL v3.`
- upstream attribution to BumpMesh / CNC Kitchen;
- `View License` action;
- `View Source Code` action;
- `Third-Party Notices` action;
- warranty/no-warranty wording appropriate to the included AGPL notice.

## 9. Third-party dependencies

The pinned upstream README identifies browser dependencies including:
- Three.js — MIT
- fflate — MIT
- Three.js addons/modules under the Three.js project/license
- meshStep for STEP import — document its exact source/version/license when vendored

Mesh Me360 will also add desktop dependencies such as:
- Microsoft WebView2 SDK/runtime
- Velopack
- any selected logging/test packages

Codex must inspect each dependency's actual license before adding it and update `THIRD_PARTY_NOTICES.md`.

Do not assume a dependency is redistributable merely because it is on NuGet/npm/GitHub.

## 10. Autodesk/Fusion UI reference

Fusion 360 is a design-familiarity reference only.

Do not include:
- Autodesk logos;
- Fusion 360 logos;
- Autodesk screenshots;
- copied proprietary toolbar art;
- proprietary Autodesk fonts/assets;
- claims that Mesh Me360 is affiliated with or endorsed by Autodesk.

Use original Mesh Me360 UI art and system/open-source icons.

## 11. Product naming

User-visible product name: **Mesh Me360**.

The application may say it is `based on the BumpMesh mesh-processing engine` or similar factual attribution.

Do not relabel upstream authorship as if CGameDev created the original engine.

## 12. Future upstream synchronization

When updating from upstream after the pinned baseline:

1. record old and new upstream SHAs;
2. review upstream license/notices;
3. compare algorithmic changes;
4. merge upstream behavior before reapplying Mesh Me360 UI-specific adaptations where possible;
5. run the full engine regression suite;
6. update `UPSTREAM_BASELINE.md` only when the project intentionally adopts a new baseline;
7. update About/diagnostics if the displayed upstream version changes.

## 13. What Codex must not do

- Do not delete `LICENSE`/notice files to make packaging simpler.
- Do not remove upstream headers during formatting.
- Do not copy code from a differently licensed fork without checking compatibility.
- Do not replace AGPL with MIT, proprietary, or “all rights reserved” notices for the combined derivative work.
- Do not download random texture packs/icons from the web into releases without recording licenses.
- Do not use Fusion 360 assets as placeholders in production source.

This document is an implementation/compliance instruction for the project, not a substitute for legal advice when release/licensing questions require counsel.
