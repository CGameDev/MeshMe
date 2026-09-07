# START HERE — Mesh Me360 Locked UI Milestone

Repository: `CGameDev/MeshMe`
Product: **Mesh Me360**
Product credit / publisher: **Computer Universal Technology Systems**
Date: 2026-09-05

## Mandatory instruction

Read, in this order:

1. `AGENTS.md`
2. `docs/milestones/MILESTONE_03_LOCKED_UI_AND_FUNCTIONAL_TOOLSET.md`
3. `docs/UI_REFERENCE_OVERRIDE.md`
4. `docs/UI_CONTROL_CONTRACT.md`
5. `docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png`
6. Existing architecture, licensing, feature matrix and test documents.

The PNG is **NOT a concept, mockup, example, mood board, or inspiration**.

It is the authoritative visual target for the Mesh Me360 client-area UI.

Implement the application so that, at the reference client size of 1536×1024, the layout, proportions, control order, spacing, panel placement, dark theme, accent treatment, typography hierarchy, toolbar organization and overall appearance match the PNG as closely as technically practical.

Do not reinterpret the design.
Do not replace it with a generic WPF layout.
Do not rearrange controls because another layout seems cleaner.
Do not remove visible tools because the upstream project did not originally contain them.
Do not leave placeholder controls or permanently disabled decorative buttons.
Do not substitute the old SVG mockups for the locked PNG.

Where this package conflicts with the old UI prose/SVGs, **this package wins for UI and functional scope**.

The 3D dragon shown in the PNG is illustrative viewport content only. The application is not required to recreate or ship that dragon model. Visual comparison should mask dynamic model pixels while still comparing viewport chrome, grid, controls, divider, statistics, view cube, panel geometry and layout.

## Product identity

User-visible credit must be:

`Done by Computer Universal Technology Systems`

Do not display `by CGameDev` as the product author/publisher in application UI, About, installer metadata, splash/welcome content, documentation credits, or generated project metadata.

The GitHub repository may remain hosted under the `CGameDev` account. Repository hosting identity is not the product publisher identity.

Preserve all upstream CNCKitchen / Stefan Hermann attribution and AGPL notices in the legally appropriate third-party/source/license surfaces.

## Execution rule

Implement the visual shell and functional wiring in stages, but **do not mark the milestone complete until every visible control described in the locked reference is either:**

- functional in the state where it is applicable, or
- intentionally disabled because the current selection/state makes the action invalid, with a tooltip explaining the requirement.

A button may not be declared complete if it only logs a message, opens an empty dialog, returns “not implemented”, or performs a fake/demo action.

Start implementation now and continue through the milestone checkpoint unless a genuine blocking technical or licensing issue is discovered.
