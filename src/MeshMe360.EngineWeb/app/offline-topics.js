// SPDX-License-Identifier: AGPL-3.0-only
export const offlineTopics={
 Texture:'Choose an Image, Procedural or Library source. Scale controls physical repeat size, Depth controls relief depth and Strength changes the source. Expand Advanced for projection and blend choices. Apply Texture opens the same reviewable relief workflow as the toolbar; commit only after checking the preview.',
 Emboss:'Adds positive relief using the selected source and preserved displacement engine. Preview first, then Commit or Cancel. Undo restores the original scene.',
 Deboss:'Cuts negative relief into the selected surface. Check the remaining wall using Measure or Prepare for Print. Local sampled checks do not certify global minimum thickness.',
 Repair:'Run Model Health to inspect current geometry. Repair Preview shows proposed changes before Commit. Advanced Maximum hole diameter limits safe hole filling; zero leaves openings intact. Large intentional openings are not silently filled.',
 Hollow:'Select one unlocked closed mesh, set Wall thickness, Preview and review the result before Commit. Minimum wall thickness and drainage suitability still require review.',
 'Make Solid':'Reconstructs an approximate solid from an enclosed interior. Draft uses twice the Standard cell size; High Quality uses half. Smaller cells cost memory and can hit existing resource limits. Editing Cell size selects Custom.',
 Boolean:'Select at least two suitable closed meshes. The first selection is the base for Subtract. Preview validates the inputs and keeps originals until Commit. Combine joins geometry without removing intersections.',
 Split:'Select one closed mesh, choose a plane normal and offset, then Preview. Keep both sides or one selected side. Commit creates separate undoable scene objects.',
 Measure:'Pick points for distance or angle, inspect bounds, or sample a local opposing wall. Sampled thickness is not a global safety certificate. Saved measurements are snapshots.',
 'Prepare for Print':'Review units, dimensions, build-plane placement, open edges, normals and sampled wall estimates. Export STL, 3MF or OBJ through the normal export review. Mesh Me360 does not generate G-code.',
 Preferences:'Appearance applies per Windows user. Under Performance → Graphics, Automatic keeps hardware acceleration enabled. Software Compatibility Mode requires restart and can be slower. Windows Graphics Settings manages power-saving and high-performance adapter preferences.',
 'Model Health':'Checks committed geometry. Results become stale after an edit. Run diagnostics to inspect open/non-manifold edges, degenerates, normals and local wall samples. Use the existing Repair command for a reviewable fix.',
 Export:'STL contains geometry only. OBJ packages supported materials and texture references. 3MF preserves the supported units and material metadata described in the export review. Saving an MM360 project retains editable scene state.',
 'Texture Library':'Apply or favorite a built-in preset. Custom images can be renamed or deleted from the managed library; deletion never removes your original image. Right-click an item for its applicable actions.',
 License:'Start 7-Day Trial is explicit and requires a successful online response. Paid activation permits one active PC and supports a signed offline lease of up to 30 days. Help → License provides status, online retry, support Device ID and deactivation.'
};
