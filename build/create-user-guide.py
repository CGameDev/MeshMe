# SPDX-License-Identifier: AGPL-3.0-only
"""Build the versioned, offline user guide with ReportLab 4.4.9."""
from pathlib import Path
from xml.sax.saxutils import escape
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.platypus import Paragraph
from reportlab.lib.styles import ParagraphStyle
ROOT = Path(__file__).resolve().parents[1]
PAGES = [
('Start here', 'Create. Texture. Print.', [
('Welcome to Mesh Me360', 'This guide covers the Windows x64 0.1.0 preview. Import a model, adjust its shape or surface, inspect the result, and export it for your next tool. Done by Computer Universal Technology Systems.'),
('Install and open', 'Save your work and close Mesh Me360 before installation. Open the MeshMe360 MSI, read the license, accept its terms to continue, and finish the wizard. Launch Mesh Me360 from the Start menu. The package includes .NET and the fixed WebView2 runtime; editing does not require a network connection.'),
('Your first project', '1. Choose File > Open Model or Import and select an STL, OBJ, 3MF, STEP or STP file.<br/>2. Select the model in the Project tree.<br/>3. Use Fit All in View to frame it.<br/>4. Choose File > Save As and save a .mm360 project.<br/>5. Try the texture workflow on page 4, then review and export on page 7.'),
('Keep your work', 'Save explicitly with Ctrl+S. This preview uses a save destination dialog. Automatic recovery scheduling and automatic updates are not yet available. Keep a separate original model and save a project before substantial edits.'),
('In this guide', '2 Workspace and navigation | 3 Projects and transforms | 4 Textures and relief | 5 Geometry tools | 6 Measurements and diagnostics | 7 Print and export | 8 Help, shortcuts and licensing')]),
('Workspace and navigation', 'Find the model. Keep the controls close.', [
('Around the viewport', 'The central viewport shows the model. The Project tree on the left lists models and project content. The right inspector contains Texture, Material and Properties controls. The lower shelf provides Library, Presets, Recent and Favorites. The top toolbar provides frequently used modeling commands.'),
('Select and move the view', 'Click an object in the viewport or Project tree to select it. Ctrl-click toggles additional objects. Left-drag to orbit, right-drag to pan, and turn the mouse wheel to zoom. Use View > Fit Selected or Fit All when an object is out of sight. The view cube and View menu provide standard directions.'),
('Change the display', 'The View menu contains Shaded, Wireframe, Textures and X-Ray, plus Grid and Axes. Switch between Perspective and Orthographic for the view you need. Display changes help inspection; they do not change exported geometry.'),
('Compare the result', 'Enable Before / after from View. Drag the comparison divider to inspect both sides; double-click it to return to the center. Use the arrow keys when the divider has focus.'),
('Organize the workspace', 'Use the Project search field to find entries. Rename a selected object with F2. Visibility and lock controls help isolate work. Preferences offers light and dark themes. View can show or hide the browser, inspector and lower shelf.')]),
('Projects and transforms', 'Save editable work in .mm360.', [
('Open, import and save', 'Open a project to resume its scene. Import adds supported model content to the current scene. A .mm360 project retains objects, transforms, source assets, measurements, materials and settings. Save As creates a separate project file. Legacy .bumpmesh files are compatibility inputs; save the converted work as .mm360.'),
('Unsaved changes', 'New, Open and Exit can ask whether to save the current document. Save writes through a native destination dialog; Don\'t Save discards unsaved changes; Cancel returns to the document. Verify the chosen filename and location before accepting a replacement.'),
('Move, Rotate and Scale', 'Select an unlocked model, then choose Move, Rotate or Scale. Drag the corresponding viewport handle or enter numeric values in Properties. Linked scaling preserves proportions; unlink it only when you intend independent axis scaling. Center Model, Place on Bed and Reset Transform are available in Modify.'),
('Units and multiple objects', 'Display units change how measurements are shown, not the physical mesh size. Geometry uses millimeters internally. Ctrl-click selects multiple objects for a shared transform. Review the result from more than one direction before saving.'),
('Undo and redo', 'Ctrl+Z undoes scene edits; Ctrl+Y or Ctrl+Shift+Z redoes them. While typing in a field, Undo applies to that field. Undo history has a memory budget and is not a substitute for saved project versions.')]),
('Textures and relief', 'Choose a source, preview the surface, commit the change.', [
('Choose a source', 'Select a model and open Texture. Choose a procedural, image or library source. The library includes surfaces such as Stone, Rock, Bricks, Scales, Wood and Metal. Choose Image to use your own height image. A source is not automatically a printable surface until you apply the relevant geometry operation.'),
('Set size and depth', 'Set Scale U and V for source size; keep the lock enabled for equal scaling. Depth controls relief depth in millimeters. Strength adjusts source intensity; Invert swaps high and low regions. Select an appropriate projection mapping, such as planar, cylindrical or spherical, and inspect seams and orientation.'),
('Emboss or deboss', 'Choose Emboss for raised detail or Deboss for recessed detail. Adjust the displayed settings and select Preview. Rotate and compare the model. Commit accepts the preview; Cancel leaves the original geometry. Enable Allow relief refinement only when additional triangles are needed. Dense results require more memory.'),
('Generate and arrange', 'Generate Texture offers patterns and controls such as seed, frequency, contrast, blur and invert. Preview before saving to the library. Pattern provides linear, grid or radial arrangements with spacing and count controls. Review repetition at the actual model scale.'),
('Advanced controls and library', 'Engine controls exposes the preserved mapping, masking and refinement tools. A mask limits the affected region; inspect it before applying relief. Library management can rename, favorite or delete custom entries; built-in entries are protected. Save the project to keep its source assets with the model.')]),
('Geometry tools', 'Preview first. Commit after inspection.', [
('Repair and normals', 'Use Diagnostics to understand the problem before Repair. Repair offers bounded cleanup operations; inspect what it proposes. Recalculate Normals updates shading normals and does not itself fix face winding or make a model watertight.'),
('Refine and simplify', 'Subdivide/Refine adds detail needed for later surface work. Simplify/Decimate reduces triangles. Compare silhouette, small features and boundaries before committing. Large jobs use workers and resource limits; cancel a job if it is unsuitable and retry with a smaller model or gentler settings.'),
('Boolean', 'Select two appropriate objects and choose Boolean. Union joins their volumes, Subtract removes the second operand from the first, and Intersect retains their overlap. Selection order matters for subtraction. Review the operation and preview; originals are retained until Commit.'),
('Split and combine', 'Split cuts using its plane controls. Combine brings selected objects into one result and offers exact welding where applicable. Combining objects does not guarantee that overlapping volumes become one watertight solid; inspect diagnostics afterward.'),
('Hollow and Make Solid', 'Hollow provides wall and drain settings. Make Solid reconstructs a volume using its resolution controls. These operations can change detail and dimensions. Check thin areas, openings and the preview before Commit. Use Undo to restore the prior scene if needed.')]),
('Measure and inspect', 'Use diagnostics as evidence, then inspect the model.', [
('Measurements', 'Choose Measure, select the measurement type, and pick the requested points on the model. Distance and angle tools use actual surface picks. Bounds reports overall dimensions. Measurements can be named or removed and retained in the saved project.'),
('Mesh diagnostics', 'Run Mesh Diagnostics after import and after major edits. Review edge, winding and shell results and use overlays to locate reported areas. Multiple shells can be intentional, but unexpected disconnected pieces should be investigated.'),
('Wall sampling', 'Sampled wall measurements are local checks. They do not certify the global minimum wall thickness, every possible self-intersection, or the physical strength of a print. Check critical regions and confirm suitability in your downstream preparation workflow.'),
('When something fails', 'Cancel the current operation if available and preserve the original project. Retry with a smaller mesh or reduced settings. If the model disappears, check its visibility, selection, dimensions and Fit All before reimporting. If relief appears reversed, inspect source Invert and Emboss/Deboss settings.'),
('Application diagnostics', 'Help > Application Diagnostics provides application and runtime information, the latest engine error and log access. Copy the information when reporting a reproducible problem. Include what you opened, the command and settings, and what happened; avoid sharing confidential model content unintentionally.')]),
('Prepare for print and export', 'A saved project and an exported model serve different purposes.', [
('Review for print', 'Choose Prepare for Print. Review dimensions, bed placement, mesh findings, sampled walls and the selected export profile. Apply placement changes only after checking the proposal. Mesh Me360 prepares models; it does not create G-code or control a printer.'),
('Choose an output', 'STL: geometry only, suitable for many slicers.<br/>3MF: review the available units and metadata options and any capability warnings.<br/>OBJ: packaged ZIP containing the OBJ, material file and source textures. Source images in the ZIP are not a promise of UV-mapped texture export.'),
('Export steps', '1. Save an editable .mm360 project.<br/>2. Select the intended model or objects.<br/>3. Choose Export or the STL, 3MF or OBJ command.<br/>4. Review the displayed inputs and preparation options.<br/>5. Wait for preparation and validation.<br/>6. Choose the output destination in the Windows dialog.<br/>7. Open the exported result in your slicer or receiving application and confirm size, orientation and detail.'),
('Keep editable and manufacturing files', 'An exported mesh may not preserve the editable project settings. Retain the .mm360 file alongside the output. Confirm that a new relief preview has been committed or that the export explicitly includes the intended live relief input.'),
('Practical checks', 'Check small details at physical scale, not only while zoomed in. Consider the receiving application\'s material support. Validate the final sliced result before printing; an apparently smooth viewport is not proof that a model is printable.')]),
('Help and reference', 'Keep this guide available offline.', [
('Open the guide again', 'Choose Help > User Guide. Windows opens the installed PDF in your default PDF application. If Windows asks, choose a PDF reader. If the guide is missing, repair or reinstall Mesh Me360. Help also contains in-app Documentation, Keyboard Shortcuts, licensing, third-party notices and About.'),
('Keyboard reference', 'Ctrl+N: New project | Ctrl+O: Open<br/>Ctrl+S: Save | Ctrl+Shift+S: Save As<br/>Ctrl+Z: Undo | Ctrl+Y / Ctrl+Shift+Z: Redo<br/>Ctrl+,: Preferences | F1: Documentation<br/>F2: Rename selected object | Delete: Remove selected unlocked objects<br/>Escape: Close a transient panel or cancel an active tool'),
('Installation and removal', 'The MSI installs for the current Windows user and adds a Start menu shortcut. It registers .mm360 projects and Open with entries for supported mesh formats without taking their existing defaults. Use Windows Settings > Apps to uninstall. User projects and application data are retained. This preview is unsigned; clean-machine installation, upgrade and removal still require release acceptance testing.'),
('License and acknowledgments', 'Mesh Me360 is distributed under GNU AGPL version 3. The installer displays the full application license. The original BumpMesh engine is by CNC Kitchen / Stefan Hermann and contributors. Microsoft runtime components and other dependencies retain their own licenses and notices, included with the installation.'),
('Source and version', 'Source code and project documentation: <link href="https://github.com/CGameDev/MeshMe" color="#1979b8">github.com/CGameDev/MeshMe</link><br/>Guide edition: 0.1.0 preview, September 2026. This guide describes current features; it does not assert completion of all v1.0 milestones.')])]

def build():
    """Lay out eight fixed sections with selectable text and consistent page furniture."""
    out = ROOT / 'output/pdf/MeshMe360-User-Guide.pdf'
    out.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(out), pagesize=(612,792), invariant=1)
    c.setTitle('Mesh Me360 User Guide'); c.setAuthor('Computer Universal Technology Systems')
    style = ParagraphStyle('body', fontName='Helvetica', fontSize=10.5, leading=15, textColor=HexColor('#33434e'))
    for number,(title,subtitle,sections) in enumerate(PAGES,1):
        c.setFillColor(HexColor('#142b3b'));c.rect(0,672,612,120,fill=1,stroke=0)
        c.setFillColor(HexColor('#3cabed'));c.setFont('Helvetica-Bold',12);c.drawString(44,754,'MESH ME360 / USER GUIDE')
        c.setFillColor(HexColor('#ffffff'));c.setFont('Helvetica-Bold',23);c.drawString(44,718,title)
        c.setFont('Helvetica',10.5);c.drawString(44,693,subtitle)
        y=647
        for heading,body in sections:
            c.setFillColor(HexColor('#167cb5'));c.setFont('Helvetica-Bold',12);c.drawString(44,y,heading);y-=9
            p=Paragraph(body,style);_,h=p.wrap(524,650);y-=h;p.drawOn(c,44,y);y-=25
        if y<49: raise RuntimeError(f'Page {number} exceeds safe area: {y}')
        c.setStrokeColor(HexColor('#d5dfe5'));c.line(44,42,568,42)
        c.setFillColor(HexColor('#667884'));c.setFont('Helvetica',9);c.drawString(44,27,'Mesh Me360 0.1.0 preview');c.drawRightString(568,27,f'{number} / {len(PAGES)}')
        c.showPage()
    c.save();print(out)
if __name__=='__main__': build()
