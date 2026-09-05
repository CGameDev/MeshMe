# Mesh Me360 — Decisions and Non-Goals

This file exists to prevent Codex from “helpfully” changing product intent while implementing it.

## Locked v1.0 decisions

1. **Product name is `Mesh Me360`.**
   - Internal namespace: `MeshMe360`.
   - `360` is branding, not an Xbox platform target.

2. **Target is Windows 10/11 x64 desktop.**
   - No macOS/Linux release is required for v1.0.
   - No Xbox/XDK/UWP console target.

3. **Host architecture is .NET 8 WPF + WebView2.**
   - Do not replace with Electron, Tauri, WinUI 3, Avalonia, Qt, MAUI, or a browser-only PWA merely because another framework seems easier.

4. **The BumpMesh JavaScript/Three.js geometry engine is preserved.**
   - Do not port the geometry engine to C# for v1.0.
   - Do not replace Three.js with another renderer.

5. **Pinned upstream baseline is exact.**
   - `CNCKitchen/stlTexturizer`
   - `a6ac179149b8a17c71a9469dd4cb6f866c0c01d1`
   - v1.2.0 baseline

6. **Runtime is offline-first.**
   - No required CDN.
   - No required local HTTP server.
   - No required Node/Python/PHP/Docker install for end users.

7. **UI is Fusion 360-inspired in spatial organization only.**
   - Use Mesh Me360 branding/assets.
   - No Autodesk proprietary assets.

8. **Single active document is sufficient for v1.0.**
   - Do not build multi-document tabs unless required later.

9. **Every feature in `docs/FEATURE_MATRIX.md` is mandatory.**
   - Auto update, autosave, recovery, installer, file associations, recent files, and Texture Library are not deferred “nice to haves.”

10. **`.bumpmesh` is the owned project association.**
    - Shared 3D formats receive Open With integration only.

11. **Autosave interval is 120 seconds in v1.0.**
    - UI may expose enable/disable, but do not invent a complex interval editor for v1.0.

12. **Update checks default to enabled, max once per 24 hours.**
    - Update install still requires user action.

13. **Updater/packager is Velopack unless a documented blocker is proven.**

14. **Release strategy includes an offline WebView2 runtime.**
    - Preferred: pinned Fixed Version Runtime x64 staged in release artifacts.

15. **Web/native binary transfer must not use base64 for large model files.**
    - Use the internal WebView2 resource bridge architecture.

16. **Recovery data remains separate from explicit user project files.**
    - Recovery never silently overwrites originals.

17. **No account/login/cloud requirement.**

18. **No telemetry/analytics upload in v1.0.**
    - Logs stay local unless the user manually shares them.

19. **No advertising or online content panels in the editing workspace.**

20. **Mesh Me360 versioning is independent of BumpMesh versioning.**

## Non-goals for v1.0

The following are intentionally outside scope. Do not add them unless the repository owner explicitly expands the project.

- full CAD solid modeling / B-Rep editing
- sketching/constraints system
- parametric Fusion-style feature timeline
- multi-body boolean CAD operations unless already present upstream (they are not implied by this project)
- slicer/G-code generation
- printer control
- cloud project storage
- user accounts
- online asset marketplace
- AI texture generation
- automatic web model upload
- collaborative editing
- multi-document tabbed workspace
- macOS/Linux packages
- Xbox 360 application/runtime
- Autodesk/Fusion file formats or proprietary integrations
- measurement tools not already supported by the engine
- arbitrary plug-in architecture
- rewriting BumpMesh geometry in C++/C#/Rust

## UI interpretation guardrails

The labels `DESIGN`, `TEXTURE`, `MESH`, and `INSPECT` are workspace/navigation concepts.

`DESIGN` does **not** imply that Codex should create:
- sketches,
- extrude,
- revolve,
- loft,
- fillet,
- shell,
- CAD component assemblies,
- parametric constraints.

The Browser panel is a logical project tree, not a claim that Mesh Me360 has Fusion's CAD document model.

The bottom History strip exposes real Mesh Me360/upstream undo-history information only. Do not fabricate parametric operations.

## If a conflict is found

Priority order:

1. `AGENTS.md`
2. this file
3. `docs/PRODUCT_REQUIREMENTS.md`
4. `docs/ARCHITECTURE.md`
5. `docs/UI_SPEC.md`
6. `docs/CODEX_MASTER_PLAN.md`
7. mockup SVGs
8. other notes/comments

If two higher-priority documents genuinely conflict, document the conflict rather than guessing a hidden intention.
