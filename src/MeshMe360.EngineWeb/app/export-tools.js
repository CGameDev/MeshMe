// SPDX-License-Identifier: AGPL-3.0-only
import {startSceneExport} from './export-client.js';
import {geometryFromObject} from './scene-document.js';

export function attachExportTools({document,engine,host,registry,openDialog,el,capture,refreshScene,setBusy}){
 let lastFormat=null;
 const available=()=>!!host&&document.state.objects.length>0;
 function launch(preselected=null,selectedOnly=false){
  const revision=document.revision;let job=null,result=null,closed=false,closeDialog;
  const body=el('div'),status=el('p','','Validate and prepare the export, then choose its destination.');status.dataset.exportStatus='idle';
  function choice(label,options,value){const row=el('label','locked-field'),select=el('select');select.setAttribute('aria-label',label);for(const [key,text]of options){const option=el('option','',text);option.value=key;select.append(option);}select.value=value;row.append(el('span','',label),select);body.append(row);return select;}
  const profile=document.state.exportProfiles.find(p=>p.id===document.state.activeProfile);
  const format=choice('Export format',[['stl','STL'],['3mf','3MF'],['obj','OBJ package']],preselected??lastFormat??profile?.format??'stl');
  const scope=choice('Export scope',[['selected','Selected objects'],['project','Visible project objects']],selectedOnly||document.selected.length?'selected':'project');
  const live=el('input');live.type='checkbox';live.setAttribute('aria-label','Include current relief preview');const liveRow=el('label','','Include current relief preview ');liveRow.append(live);body.append(liveRow);
  const note=el('p','','Preparing with current relief enabled uses a 3,000,000-triangle refinement result budget. Temporary memory can be higher. Increase Resolution (mm) if the budget is exceeded. Exported scene coordinates remain millimetres. OBJ is packaged with MTL and source images; STL omits materials; 3MF retains supported units and metadata.');body.append(note);
  const prepare=el('button','','Validate and prepare'),save=el('button','primary','Choose destination…'),cancel=el('button','','Cancel');save.disabled=true;
  const selected=()=>scope.value==='selected'?document.selected:document.state.objects.filter(o=>o.visible);
  function options(){const objects=selected();live.disabled=objects.some(o=>o.groups)||!objects.some(o=>o.textureId);if(live.disabled)live.checked=false;live.title=objects.some(o=>o.groups)?'Apply relief before export to preserve material groups.':'Use the preserved displacement/export pipeline on copies, with a 3,000,000-triangle refinement budget.';}
  options();live.checked=!live.disabled;
  function invalidate(){job?.cancel();job=null;result=null;save.disabled=true;prepare.disabled=false;setBusy(false);status.dataset.exportStatus='idle';status.textContent='Export settings changed. Validate again.';options();}
  for(const input of [format,scope,live])input.onchange=invalidate;
  function cleanup(){if(closed)return;closed=true;job?.cancel();result=null;setBusy(false);window.removeEventListener('meshme:before-model-load',cancelOnImport);}
  function cancelOnImport(){closeDialog();}
  prepare.onclick=async()=>{
   invalidate();setBusy(true);prepare.disabled=true;status.dataset.exportStatus='running';status.textContent='Preparing geometry in background… Cancel keeps the project unchanged.';
   try{
    if(document.revision!==revision)throw new Error('The document changed. Reopen Export.');
    capture();const objects=selected().map(o=>structuredClone(o));if(!objects.length)throw new Error('Choose at least one object.');
    if(live.checked){try{for(const object of objects)if(object.textureId){engine.desktopSceneActivate(geometryFromObject(object),object.engineSettings,object.engineMask,document.state.textures.find(t=>t.id===object.textureId));object.pipelineInput=structuredClone(engine.desktopDisplacementInput());object.pipelineInput.mode='export';}}finally{refreshScene({activate:true});}}
    const current=startSceneExport({objects,format:format.value,textures:document.state.textures,title:document.state.name,profile:profile?.name??'Generic STL'},p=>{if(!closed)status.textContent=p.stage+'…';});job=current;
    const output=await current.promise;if(closed||job!==current)return;job=null;result=output;
    status.dataset.exportStatus='ready';status.textContent=output.reports.map(r=>`${r.name}: ${r.tris.toLocaleString()} triangles; ${r.open} open edges; ${r.nonManifold} non-manifold edges; ${r.slivers} degenerate triangles. ${r.pipelineWarnings.join(' ')}`).join(' ')+' '+output.warning+' Diagnostics use the preserved engine’s edge grid; self-intersections and minimum wall thickness are not certified.';save.disabled=false;
   }catch(error){if(closed||error.name==='AbortError')return;status.dataset.exportStatus='error';status.textContent=String(error.message??error);setBusy(false);}
   finally{if(!closed)prepare.disabled=false;}
  };
  save.onclick=async()=>{
   if(!result)return;save.disabled=true;
   try{const name=document.state.name.replace(/\.(mm360|bumpmesh)$/i,'').replace(/[\\/:*?"<>|]/g,'_')+result.extension;const registered=await host.download(new Blob([result.bytes],{type:'application/octet-stream'}),name);if(!registered?.requestId)throw new Error('The native save request was not accepted.');lastFormat=format.value;closeDialog();}
   catch(error){status.textContent=String(error.message??error);save.disabled=false;}
  };
  cancel.onclick=()=>closeDialog();const actions=el('div');actions.append(prepare,save,cancel);closeDialog=openDialog('Export',[body,status,actions],cleanup,'tool');window.addEventListener('meshme:before-model-load',cancelOnImport);
 }
 for(const [command,format]of [['STL','stl'],['3MF','3mf'],['OBJ','obj']])registry.register(command,()=>launch(format),available,'Import a model first');
 registry.register('Export',()=>launch(),available,'Import a model first');
 registry.register('Export Selected',()=>launch(null,true),()=>available()&&document.selected.length>0,'Select objects to export');
 registry.register('menu:Export',()=>{const list=el('div');for(const [label,command]of [['Export STL','STL'],['Export 3MF','3MF'],['Export OBJ package','OBJ'],['Export Selected','Export Selected'],['Export using active profile','Export']]){const button=el('button','',label);button.disabled=!registry.state(command).enabled;button.onclick=()=>registry.execute(command);list.append(button);}openDialog('Export',[list]);},available,'Import a model first');
}
