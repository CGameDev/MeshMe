# Repository Integration Checklist for Codex

When this package is supplied to the `CGameDev/MeshMe` working tree:

1. Copy:
   - `docs/ui/MeshMe360_UI_LOCKED_REFERENCE.png`
   - `docs/milestones/MILESTONE_03_LOCKED_UI_AND_FUNCTIONAL_TOOLSET.md`
   - `docs/UI_REFERENCE_OVERRIDE.md`
   - `docs/UI_CONTROL_CONTRACT.md`

2. Update `AGENTS.md`:
   - UI rules must name the locked PNG as primary authority.
   - Replace the old statement that the SVG mockups are controlling.
   - Add `Computer Universal Technology Systems` as product publisher/credit.
   - Keep repository owner `CGameDev/MeshMe`.
   - Preserve upstream licensing rules.
   - Change native project association from `.bumpmesh` to `.mm360`.
   - Keep `.bumpmesh` as legacy compatibility.

3. Update `docs/CODEX_MASTER_PLAN.md`:
   - Replace/supersede the old “Fusion 360-inspired UI shell” Milestone 3 with the locked milestone.
   - Link directly to the PNG and the milestone/control contract.
   - Do not retain a checkpoint that only says “mockup layout is recognizable”.
   - New checkpoint must require visual fidelity + functional controls.

4. Update `docs/UI_SPEC.md`:
   - Header must state the locked PNG wins when prose conflicts.
   - Remove/replace old command lists that conflict with the reference.
   - Do not keep the old DESIGN/TEXTURE/MESH/INSPECT layout as a competing implementation contract.

5. Update `docs/FEATURE_MATRIX.md` and `docs/TEST_PLAN.md`:
   - add every new functional tool;
   - add `.mm360`;
   - add multi-object scene;
   - add visual reference gate.

6. Update README/product metadata:
   - product credit: `Done by Computer Universal Technology Systems`
   - native file type `.mm360`
   - upstream attribution remains in legal/credits sections.

7. Do not delete old SVG files solely to hide history. They may remain as archived historical artifacts, but mark them superseded so Codex cannot accidentally use them as the primary target.
