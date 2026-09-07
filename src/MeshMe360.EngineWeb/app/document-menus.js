// SPDX-License-Identifier: AGPL-3.0-only
/** Connects document commands, preferences and recent-file navigation. */
import {attachHelpTools} from './help-tools.js';
export function attachDocumentMenus({document,host,registry,openDialog,el,refreshScene}){
 attachHelpTools({host,registry,openDialog,el});
 function button(label,action){const b=el('button','',label);b.type='button';b.onclick=async()=>{try{await action();}catch(error){openDialog('Operation failed',[el('p','',String(error.message??error))]);}};return b;}
 function choice(label,entries,value){const row=el('label','locked-field'),input=el('select');input.setAttribute('aria-label',label);for(const [id,name]of entries){const option=el('option','',name);option.value=id;input.append(option);}input.value=value;row.append(el('span','',label),input);return{row,input};}
 registry.register('Project Settings',()=>{
  const units=choice('Project display units',[['mm','Metric (mm)'],['in','Imperial (in)']],document.state.units),profile=choice('Project export profile',document.state.exportProfiles.map(p=>[p.id,p.name]),document.state.activeProfile);
  let close;const apply=button('Apply project settings',()=>{if(document.state.units!==units.input.value||document.state.activeProfile!==profile.input.value){document.transact('Project settings',s=>{s.units=units.input.value;s.activeProfile=profile.input.value;});refreshScene();}close();});
  close=openDialog('Project Settings',[units.row,profile.row,el('p','','Display units do not rescale geometry. Export profiles are preparation presets; they do not generate G-code.'),apply]);
 });
 registry.register('Preferences',async()=>{
  const settings=await host.rpc.request('settings.get',{}),theme=choice('Application theme',[['System','Use Windows setting'],['Light','Light'],['Dark','Dark']],settings.theme);
  let close;const apply=button('Apply preferences',async()=>{await host.updateSettings({theme:theme.input.value});close();});
  close=openDialog('Preferences',[theme.row,el('p','','Appearance is saved for this Windows user and applies to all projects.'),apply]);
 },()=>!!host,'Open this app in the Windows desktop host');
 registry.register('Open Recent',async()=>{
  const entries=await host.rpc.request('recent.list',{}),list=el('div');let close;
  if(!entries.length)list.append(el('p','','No recent files.'));
  for(const entry of entries){const row=el('div'),name=entry.path.split(/[\\/]/).pop(),open=button(name+(entry.missing?' — missing':''),async()=>{await host.rpc.request('recent.open',{path:entry.path});close();});open.title=entry.path;open.disabled=entry.missing;open.dataset.recentPath=entry.path;
   const remove=button('Remove from recent files',async()=>{await host.rpc.request('recent.remove',{path:entry.path});await registry.execute('Open Recent');});remove.setAttribute('aria-label','Remove '+name+' from recent files');row.append(open,remove);list.append(row);
  }
  if(entries.length)list.append(button('Clear recent files',async()=>{await host.rpc.request('recent.clear',{});await registry.execute('Open Recent');}));
  close=openDialog('Open Recent',[list]);
 },()=>!!host,'Open this app in the Windows desktop host');
 function menu(name,entries){registry.register('menu:'+name,()=>{const list=el('div');for(const [label,command]of entries){const state=registry.state(command),b=button(label,()=>registry.execute(command));b.disabled=!state.enabled;b.title=state.enabled?'':state.reason;list.append(b);}openDialog(name,[list]);});}
 menu('File',[['New Project','New Project'],['Open Project…','Open Project'],['Open Model…','Import'],['Open Recent','Open Recent'],['Import Model…','Import'],['Save','Save'],['Save As…','Save As'],['Export…','Export'],['Project Settings','Project Settings'],['Exit','Exit']]);
 menu('Edit',[['Undo','Undo'],['Redo','Redo'],['Duplicate Selected','Duplicate Selected'],['Delete Selected','Delete Selected'],['Select All','Select All'],['Preferences','Preferences']]);
 menu('Mesh',['Repair','Make Solid','Hollow','Subdivide/Refine','Simplify/Decimate','Recalculate Normals','Boolean','Split','Combine','Diagnostics'].map(n=>[n,n]));
 menu('Texture',[['Texture panel','Texture'],['Procedural source','source:Procedural'],['Image source','source:Image'],['Texture Library','Texture Library'],...['Apply Texture','Emboss','Deboss','Pattern','Generate Texture'].map(n=>[n,n]),['Remove/Clear active texture','Clear Texture'],['Engine controls','legacy']]);
 menu('Modify',['Move','Rotate','Scale','Center Model','Place on Bed','Reset Transform'].map(n=>[n,n]));
 menu('Tools',['Measure','Prepare for Print','Mesh Diagnostics','Application Diagnostics','Preferences'].map(n=>[n,n]));
 // Capture before the preserved engine's independent Undo handler. Text-entry
 // Undo remains native; document shortcuts must address the scene history.
 window.addEventListener('keydown',event=>{
  if(!(event.ctrlKey||event.metaKey)||event.altKey)return;const key=event.key.toLowerCase(),target=event.target;
  const text=target.isContentEditable||target.tagName==='TEXTAREA'||(target.tagName==='INPUT'&&['text','number','search','email','url','tel','password'].includes(target.type));
  if(text&&['z','y'].includes(key))return;
  const command=key==='z'?(event.shiftKey?'Redo':'Undo'):key==='y'?'Redo':key==='s'?(event.shiftKey?'Save As':'Save'):key==='n'?'New Project':key==='o'?'Open Project':key===','?'Preferences':null;
  if(!command)return;event.preventDefault();event.stopImmediatePropagation();if(key==='s')target.blur?.();
  if(registry.state(command).enabled)Promise.resolve().then(()=>registry.execute(command)).catch(error=>openDialog('Operation failed',[el('p','',String(error.message??error))]));
 },true);
}
