// SPDX-License-Identifier: AGPL-3.0-only
/** Registers offline help, licensing and product-information surfaces. */
import {attachAboutDialog} from './about-dialog.js';
/** Register local help, license and product-information commands. */
export function attachHelpTools({host,registry,openDialog,el}){
 const source=()=>{const a=el('a','','Mesh Me360 source code');a.href='https://github.com/CGameDev/MeshMe';a.target='_blank';a.rel='noopener';return a;};
 async function legal(title,path){const response=await fetch(path);if(!response.ok)throw new Error('The packaged '+title+' could not be read.');const text=el('pre','',await response.text());text.style.whiteSpace='pre-wrap';text.style.overflowWrap='anywhere';openDialog(title,[text]);}
 registry.register('Documentation',()=>openDialog('Documentation',[
  el('h3','','Create. Texture. Print.'),el('p','','Import supported STL, OBJ, 3MF or STEP models. Select objects in the Project tree or viewport; Ctrl-click toggles multiple selection. Move, Rotate and Scale offer viewport handles and numeric Properties.'),
  el('p','','Choose a Procedural, Image or Library source in Texture. Scale changes its physical size; Depth sets relief depth; Strength and Invert change source heights. Emboss, Deboss and Pattern offer Preview, Commit and Cancel. The comparison divider can be dragged; double-click resets it.'),
  el('p','','Geometry tools show their applicable inputs and operation settings. Review diagnostics before committing. Undo restores the previous scene. Prepare for Print reviews dimensions, placement, mesh issues, sampled wall thickness and export profile; it does not generate G-code.'),
  el('p','','Save a self-contained .mm360 project to retain objects, source assets, measurements, materials and settings. STL stores geometry; 3MF and OBJ offer the capabilities described in their export review. Display units do not rescale geometry.'),source()
 ]));
 registry.register('Keyboard Shortcuts',()=>openDialog('Keyboard Shortcuts',[
  ...['Ctrl+N — New Project','Ctrl+O — Open model or project','Ctrl+S — Save Project','Ctrl+Shift+S — Save Project As','Ctrl+Z — Undo scene edit (native field Undo while typing)','Ctrl+Y / Ctrl+Shift+Z — Redo scene edit','Ctrl+, — Preferences','F1 — Documentation','F2 — Rename selected object','Delete — Remove selected unlocked objects','Ctrl-click — Toggle object selection','Left drag — Orbit; right drag or Pan tool — Pan; mouse wheel — Zoom','Escape — Close transient panel or cancel active tool'].map(text=>el('p','',text))
 ]));
 registry.register('Source Code / License',()=>legal('GNU AGPLv3 License','LICENSE'));
 registry.register('Third-Party Notices',()=>legal('Third-Party Notices','THIRD_PARTY_NOTICES.md'));
 attachAboutDialog({host,registry,openDialog,el,source});
 registry.register('menu:Help',()=>{const list=el('div');for(const name of ['Documentation','Keyboard Shortcuts','Source Code / License','Third-Party Notices','Application Diagnostics','About Mesh Me360']){const b=el('button','',name);b.onclick=()=>{Promise.resolve().then(()=>registry.execute(name)).catch(error=>openDialog('Help unavailable',[el('p','',String(error.message??error))]));};list.append(b);}openDialog('Help',[list]);});
 window.addEventListener('keydown',event=>{if(event.key==='F1'&&registry.state('Documentation').enabled){event.preventDefault();registry.execute('Documentation');}},true);
}
