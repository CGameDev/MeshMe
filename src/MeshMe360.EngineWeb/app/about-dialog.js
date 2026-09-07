// SPDX-License-Identifier: AGPL-3.0-only
/** Product identity and attribution in the compact About window. */
export function attachAboutDialog({host,registry,openDialog,el,source}){
 /** Build fresh nodes so closing/reopening cannot retain stale runtime details. */
 async function showAbout(){
  const content=el('div','about-content'),banner=el('div','about-banner');
  const mark=el('div','about-mark');
  // Original product glyph; branding text remains selectable and accessible.
  mark.innerHTML='<svg viewBox="0 0 46 36" aria-hidden="true"><path fill="currentColor" d="M1 33 12 4Q14 0 19 3L25 15 33 3Q35 0 42 2L43 33H33L34 17 24 31 17 18 11 33Z"/></svg>';
  const identity=el('div');identity.append(el('h3','','Mesh Me360'),el('p','','Create. Texture. Print.'));banner.append(mark,identity);
  const info=el('div','about-info');info.append(el('strong','','Done by Computer Universal Technology Systems'));
  const version=el('span','','Windows desktop application');info.append(version);
  const credits=el('div','about-credits');credits.tabIndex=0;credits.setAttribute('aria-label','License and credits');
  credits.append(el('p','','Original BumpMesh engine: CNC Kitchen / Stefan Hermann and contributors.'),el('p','','GNU AGPLv3. Mesh Me360 includes third-party open-source components. Full license texts and dependency credits are available from Help → Source Code / License and Third-Party Notices.'),source());
  const footer=el('div','about-footer'),button=el('button','','Close');footer.append(button);content.append(banner,info,credits,footer);
  const close=openDialog('About Mesh Me360',[content],undefined,'about');button.onclick=close;button.focus();
  // A failed diagnostics request must never hide product identity or attribution.
  try{const details=await host?.rpc.request('diagnostics.get',{});if(details?.version)version.textContent='Version '+details.version+' · Windows x64';}catch{/* About remains usable when runtime diagnostics are unavailable. */}
 }
 registry.register('About Mesh Me360',showAbout);
}
