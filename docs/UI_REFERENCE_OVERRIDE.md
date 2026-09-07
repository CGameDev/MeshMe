# Mesh Me360 — UI Reference Override

This document is a repository-owner scope override for the existing Mesh Me360 UI specification.

## 1. Authority order for UI work

For the UI/tooling milestone, use this authority order:

1. `docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png`
2. `docs/milestones/MILESTONE_03_LOCKED_UI_AND_FUNCTIONAL_TOOLSET.md`
3. `docs/UI_CONTROL_CONTRACT.md`
4. This override
5. Existing `docs/UI_SPEC.md`
6. Existing SVG mockups

If an older document says the prose wins over the image, that statement is superseded by this override.

## 2. Meaning of “identical”

“Identical” means the PNG is the implementation target, not inspiration.

At the 1536×1024 reference client size:

- major panel edges: target within ±3 px;
- toolbar/control centers: target within ±4 px;
- repeated gaps/padding: target within ±2–3 px;
- font size: target within ±1 px where platform rendering permits;
- control order and grouping: exact;
- visible labels: exact unless this milestone explicitly corrects dynamic text;
- dark surfaces, borders, selected-state blue accent, hover/focus treatment and hierarchy: visually matched;
- panel/viewport proportions: visually matched;
- no substitute ribbon, sidebar arrangement, tab order or command grouping.

Normal text antialiasing, GPU rendering, DPI differences and OS window chrome are allowed to vary. The reference image describes the **application client area**. Native outer Windows frame/caption behavior may sit outside that client area.

## 3. Third-party brand exception

Do not copy Autodesk/Fusion artwork, logos or proprietary icon files.

The reference image is an original Mesh Me360 design. Recreate its layout and use original/Open-source/system icons that visually fit the reference.

The small Windows-like mark shown at the extreme lower-left of the reference must be replaced by the Mesh Me360 app glyph or a neutral four-pane application/home glyph so the product does not misuse a third-party trademark. Keep its location and footprint the same.

## 4. Native project identity

The locked reference shows a `.mm360` project. Adopt:

- primary native project extension: `.mm360`
- ProgID: `MeshMe360.Project`
- display type: `Mesh Me360 Project`

Maintain backward open/import compatibility for `.bumpmesh` where technically possible. Opening an older `.bumpmesh` project must not silently overwrite it; saving after conversion should default to `.mm360`.

## 5. Product credit

Use exactly:

**Done by Computer Universal Technology Systems**

Recommended About block:

Mesh Me360  
Create. Texture. Print.  
Done by Computer Universal Technology Systems

Then show version, source-code link, GNU AGPLv3 notice and upstream/third-party notices separately.

Do not erase upstream authorship from covered upstream files or legal notices.


## 7. Owner-requested dropdown menus and app icon (2026-09-07)

The owner reviewed the running UI, described the rest as perfect, and requested
replacing the large command-menu overlays with compact Fusion-familiar vertical
menus using the attached example as a style reference. This explicitly updates
the menu presentation requirement: anchor menus below their menu-bar triggers,
use aligned original icons, command rows, group separators, supported shortcut
labels, hover/focus states and dismissal/keyboard navigation. Keep existing
command behavior and light/dark themes. Do not copy Autodesk artwork or add its
unrelated modeling commands. Tool parameter dialogs remain distinct from menus.

The owner also requested a Mesh Me360 application icon. Use the original blue M
branding in a multi-resolution Windows icon for the executable and WPF window.
This request does not constitute blanket functional, physical Windows or final
milestone acceptance.


## 8. Compact About window (2026-09-07)

The owner requested a centered About window matching the supplied banner/section
layout. Use a 684 × 508 client panel where space permits, a white product banner,
a publisher/version information strip, scrollable legal credits and a bottom-right
Close button. Retain the current product identity, source link and required
third-party notices. Use the native assembly version rather than a separate
hardcoded product version. The window must remain within the available client area.
