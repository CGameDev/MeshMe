// SPDX-License-Identifier: AGPL-3.0-only
/** Transient assistance routes through existing authoritative command handlers. */
import {showContextActions} from './context-actions.js';
export function suggestedAction({selected,reports,busy}){
 if(busy)return null;
 if(selected.length===2)return reports.length===2&&reports.every(r=>r?.solid)?['Boolean','Combine']:['Combine'];
 if(selected.length!==1||selected[0].locked)return null;
 const report=reports[0];if(report&&(report.open||report.nonManifold||report.degenerate))return ['Repair Mesh'];
 if(selected[0].textureId)return ['Apply Texture','Emboss'];
 if(report?.solid)return ['Texture','Prepare for Print'];
 return ['Mesh Diagnostics'];
}
export function dropKind(name){const extension=name.split('.').pop().toLowerCase();return extension==='mm360'?'project':extension==='bumpmesh'?'legacy':['stl','obj','3mf','step','stp'].includes(extension)?'model':['png','jpg','jpeg','webp','bmp'].includes(extension)?'image':'unsupported';}
export async function recentCommands(host,registry){
 if(!host)return [];
 const entries=await host.rpc.request('recent.list',{});
 for(const entry of entries)registry.register('recent:'+entry.path,()=>host.rpc.request('recent.open',{path:entry.path}),()=>!entry.missing,'This recent file is missing');
 return entries;
}
export function attachUxAssistance({host,scene,engine,registry,openDialog,el,operation,issue,viewport,tree}){
 const run=name=>Promise.resolve().then(()=>registry.execute(name)).catch(error=>openDialog('Operation failed',[el('p','',String(error.message??error))]));
 const button=(label,name)=>{const b=el('button','',label);b.type='button';b.onclick=()=>run(name);const state=registry.state(name);b.disabled=!state.enabled;b.title=state.enabled?'Open '+label+' using the current selection.':state.reason;return b;};
 function suggest(){operation.replaceChildren();if(registry.busy){operation.textContent='Processing…';return;}
  const selected=scene.document.selected,reports=selected.map(o=>scene.diagnostics.reportFor(o.id)),actions=suggestedAction({selected,reports,busy:registry.busy});
  if(!actions){operation.textContent='Ready';return;}operation.append(button('Next: '+actions[0],actions[0]));
  if(actions[1])operation.append(button(actions[1],actions[1]));
 }
 for(const name of ['meshme:scene-changed','meshme:diagnostics','meshme:processing'])window.addEventListener(name,suggest);suggest();
 issue.tabIndex=0;issue.setAttribute('role','button');issue.setAttribute('aria-label','Open Model Health');issue.onclick=()=>run('Model Health');issue.onkeydown=e=>{if(['Enter',' '].includes(e.key)){e.preventDefault();run('Model Health');}};
 registry.register('Model Health',()=>registry.execute('Mesh Diagnostics'),()=>registry.state('Mesh Diagnostics').enabled,'Import a model to check its health');
 registry.register('Command Search',async()=>{
  const recent=await recentCommands(host,registry),search=el('input'),list=el('div'),status=el('p');search.type='search';search.setAttribute('aria-label','Search commands, projects, presets and help');list.setAttribute('role','listbox');search.setAttribute('role','combobox');search.setAttribute('aria-controls','meshme-command-results');search.setAttribute('aria-expanded','true');list.id='meshme-command-results';
  let close,rows=[],index=0;
  const label=name=>name.startsWith('recent:')?'Recent: '+name.slice(7).split(/[\\/]/).pop():name.startsWith('preset:')?'Preset: '+(window.meshMe360Locked?.library.items.find(i=>'preset:'+i.id===name)?.name??name.slice(7)):name;
  function focus(){rows.forEach((b,i)=>{b.setAttribute('aria-selected',String(i===index));});if(rows[index]){search.setAttribute('aria-activedescendant',rows[index].id);rows[index].scrollIntoView({block:'nearest'});}else search.removeAttribute('aria-activedescendant');}
  function render(){list.replaceChildren();rows=[];index=0;const query=search.value.trim().toLowerCase();for(const name of registry.commands.keys()){
   if(name==='Command Search'||name.startsWith('menu:')||!label(name).toLowerCase().includes(query))continue;
   const state=registry.state(name),b=el('button','',label(name)+(state.enabled?'':' — '+state.reason));b.disabled=!state.enabled;b.setAttribute('role','option');b.tabIndex=-1;b.id='command-result-'+list.children.length;b.onclick=()=>{close();run(name);};list.append(b);if(state.enabled)rows.push(b);
  }status.textContent=list.children.length+' results';focus();}
  search.oninput=render;search.onkeydown=e=>{if(['ArrowDown','ArrowUp','Enter','Escape'].includes(e.key)){e.preventDefault();e.stopImmediatePropagation();if(e.key==='Escape'){close();return;}if(e.key==='Enter'){rows[index]?.click();return;}if(rows.length)index=(index+(e.key==='ArrowDown'?1:-1)+rows.length)%rows.length;focus();}};
  close=openDialog('Command Search',[search,status,list]);render();search.focus();
 });
 window.addEventListener('keydown',e=>{if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==='k'){e.preventDefault();e.stopImmediatePropagation();run('Command Search');}},true);
 let start=null;viewport.addEventListener('pointerdown',e=>{if(e.button===2)start=[e.clientX,e.clientY];},true);
 function context(e){if(registry.busy)return;if(e.currentTarget===viewport&&start&&Math.hypot(e.clientX-start[0],e.clientY-start[1])>4)return;
  const row=e.target.closest('[data-object-id]');if(row&&!scene.document.state.selection.includes(row.dataset.objectId)){scene.capture();scene.document.select([row.dataset.objectId]);scene.refreshScene({activate:true});}
  if(!scene.document.selected.length)return;e.preventDefault();e.stopPropagation();
  const names=scene.document.selected.length===1?['Rename Selected','Duplicate Selected','Hide/Show Selected','Lock/Unlock Selected','Center Model','Place on Bed','Repair Mesh','Make Solid','Export Selected','Delete Selected']:['Boolean','Combine','Move','Rotate','Scale','Hide/Show Selected','Delete Selected'];
  showContextActions(e,names.map(n=>({label:n,enabled:registry.state(n).enabled,run:()=>run(n)})));
 }
 tree.addEventListener('contextmenu',context);viewport.addEventListener('contextmenu',context);
 viewport.addEventListener('dragover',e=>{e.preventDefault();},true);
 viewport.addEventListener('drop',e=>{
  e.preventDefault();e.stopImmediatePropagation();const files=[...e.dataTransfer.files];
  void(async()=>{
   if(registry.busy)throw new Error('Finish or cancel the current operation before dropping files.');
   if(files.some(f=>['project','legacy','image'].includes(dropKind(f.name)))&&files.length!==1)throw new Error('Drop one project or image at a time. Multiple model imports are supported.');
   for(const file of files){const kind=dropKind(file.name);
    if(kind==='project')await scene.openProject(file);else if(kind==='legacy')await engine.desktopLoadProject(file);else if(kind==='model')await engine.desktopLoadModel(file);
    else if(kind==='image'){
     if(scene.document.selected.length!==1||scene.document.selected[0].locked)throw new Error('Select one unlocked model before dropping an image.');
     let close;const nodes=['Use as Texture','Use as Displacement Source'].map((label,i)=>{const b=el('button','',label);b.onclick=async()=>{try{close();if(await engine.desktopLoadTexture(file)){await registry.execute(i?'Emboss':'Texture');}}catch(error){openDialog('Image could not be applied',[el('p','',String(error.message??error))]);}};return b;});close=openDialog('Use dropped image',nodes);
    }else throw new Error('Unsupported file. Drop STL, OBJ, 3MF, STEP, STP, MM360, BUMPMESH or a supported image.');
   }
  })().catch(error=>openDialog('Drop could not complete',[el('p','',String(error.message??error))]));
 },true);
 const empty=el('div','owner-empty');empty.append(el('p','','Open a model or drop a supported model/project here.'),button('Open Model','Import'),button('Recent Projects','Open Recent'));
 let pendingGoal=null;
 for(const [label,command]of [['Texture a Model','Texture'],['Repair a Model','Repair Mesh'],['Prepare for 3D Printing','Prepare for Print']]){const b=el('button','',label);b.onclick=()=>{if(pendingGoal)window.removeEventListener('meshme:model-result',pendingGoal);pendingGoal=event=>{pendingGoal=null;if(event.detail.success)run(command);};window.addEventListener('meshme:model-result',pendingGoal,{once:true});run('Import');};empty.append(b);}viewport.append(empty);
 const updateEmpty=()=>{empty.hidden=scene.document.state.objects.length>0;};window.addEventListener('meshme:scene-changed',updateEmpty);updateEmpty();
}
