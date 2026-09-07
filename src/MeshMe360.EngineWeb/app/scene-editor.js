// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from 'three';
import {SceneDocument,serializeProject,deserializeProject,geometryFromObject} from './scene-document.js';
import {attachSolidTools} from './solid-tools.js';
import {createSceneGizmo} from './scene-gizmo.js';
import {attachReliefTools} from './relief-tools.js';
import {remapMaterialGroups} from './combine-geometry.js';
import {attachExportTools} from './export-tools.js';
import {attachMeasureTools} from './measure-tools.js';
import {formatMeasurement} from './measurement.js';
import {attachPrintTools} from './print-tools.js';
import {startSourceComposition} from './source-client.js';
import {attachDocumentMenus} from './document-menus.js';
import {attachDiagnosticsTools} from './diagnostics-tools.js';
import {attachMeshProcessing} from './mesh-processing-tools.js';
import {attachDocumentLifecycle} from './document-lifecycle.js';
export function attachSceneEditor(engine,{host,viewer,registry,tree,documentName,propertiesPanel,search,openDialog,el,refresh,updateStats,viewport,setPreview}){
 const document=new SceneDocument();let activeId=null,busy=false,gizmo,diagnostics;const otherMeshes=new THREE.Group();viewer.getScene().add(otherMeshes);let ownedMaterials=[];
 function renderMaterials(object){const list=(object.materials??[object.material]).map(m=>new THREE.MeshStandardMaterial({color:m.color,metalness:m.metallic,roughness:m.roughness,opacity:m.opacity,transparent:m.opacity<1}));ownedMaterials.push(...list);return object.groups?list:list[0];}
 const editable=()=>!busy&&document.selected.length>0&&document.selected.every(o=>!o.locked);
 function applySnapshot(object,snapshot){object.engineSettings=snapshot.settings;object.engineMask=snapshot.mask;object.textureId=snapshot.texture?.id??null;if(snapshot.texture&&!document.state.textures.some(t=>t.id===snapshot.texture.id))document.state.textures.push(snapshot.texture);}
 function capture(){const object=document.state.objects.find(o=>o.id===activeId);if(!object)return;const snapshot=engine.desktopSceneCapture();applySnapshot(object,snapshot);snapshot.geometry?.dispose();}
 function commitTextureSettings(){
  if(!editable()||document.selected.length!==1)return;
  const object=document.selected[0],settings=engine.desktopTextureSettings();
  if(object.id!==activeId||JSON.stringify(settings)===JSON.stringify(object.engineSettings))return;
  try{document.transact('Texture settings',()=>{object.engineSettings=settings;});refreshScene();}
  catch(error){engine.desktopConfigureTexture(document.selected[0]?.engineSettings);refreshScene();throw error;}
 }
 async function composeTexture(options){
  if(!editable()||document.selected.length!==1)throw new Error('Select one unlocked textured object');
  capture();const object=document.selected[0],revision=document.revision,previous=object.sourceEffects??{sourceId:object.textureId,baseId:null,strength:1,blend:'normal',invert:false};
  const source=document.state.textures.find(t=>t.id===previous.sourceId),base=document.state.textures.find(t=>t.id===previous.baseId),next={...previous,...options};
  if(!source)throw new Error('Choose a texture source first');const job=startSourceComposition(source,base,next),cancel=()=>job.cancel();window.addEventListener('meshme:before-model-load',cancel);setBusy(true);
  try{const result=await job.promise;if(document.revision!==revision)throw new Error('The document changed during texture composition');
   const asset={...result,id:crypto.randomUUID(),name:source.name+' — composed'};document.transact('Texture source influence',s=>{s.textures.push(asset);object.textureId=asset.id;object.sourceEffects=next;});refreshScene({activate:true});
  }finally{window.removeEventListener('meshme:before-model-load',cancel);setBusy(false);}
 }
 function applyTransform(mesh,object){mesh.position.fromArray(object.position);mesh.quaternion.fromArray(object.rotation);mesh.scale.fromArray(object.scale);mesh.visible=object.visible;mesh.userData.sceneObjectId=object.id;}
 function refreshScene({activate=false}={}){
  if(activate){const selected=document.selected[0]??document.state.objects[0];if(selected){activeId=selected.id;engine.desktopSceneActivate(geometryFromObject(selected),selected.engineSettings,selected.engineMask,document.state.textures.find(t=>t.id===selected.textureId));}else{activeId=null;const mesh=viewer.getCurrentMesh();if(mesh)mesh.visible=false;}}
  for(const mesh of [...otherMeshes.children]){otherMeshes.remove(mesh);mesh.geometry.dispose();}
  for(const material of ownedMaterials)material.dispose();ownedMaterials=[];
  for(const object of document.state.objects){if(object.id===activeId){const mesh=viewer.getCurrentMesh();if(mesh){applyTransform(mesh,object);mesh.userData.groupMaterials=object.groups?renderMaterials(object):null;mesh.userData.hasTexture=!!object.textureId;}continue;}const g=geometryFromObject(object);const mesh=new THREE.Mesh(g,renderMaterials(object));applyTransform(mesh,object);otherMeshes.add(mesh);}
  documentName.textContent=document.state.name+(document.dirty?' *':'');window.dispatchEvent(new CustomEvent('meshme:scene-changed',{detail:{units:document.state.units,material:document.selected[0]?.material,editable:editable()}}));updateStats();renderTree();renderProperties();refresh();gizmo?.sync();viewer.requestRender();void host?.rpc.request('document.dirtyChanged',{dirty:document.dirty}).catch(()=>{});
 }
 function select(ids){capture();document.select(ids);refreshScene({activate:true});}
 function button(text,action){const b=el('button','',text);b.type='button';b.onclick=()=>{try{Promise.resolve(action()).catch(showError);}catch(e){showError(e);}};return b;}
 function showError(e){openDialog('Operation failed',[el('p','',String(e.message??e))]);}
 function renderTree(){tree.replaceChildren(el('div','tree-row tree-root','⌄  ▧  '+document.state.name),el('div','tree-row tree-child','⌄  ▣  Models'));for(const o of document.state.objects){if(search.value&&!o.name.toLowerCase().includes(search.value.toLowerCase()))continue;const row=el('div','tree-row tree-child');const name=button(o.name,()=>{});name.className='model-name';name.classList.toggle('active',document.state.selection.includes(o.id));name.onclick=e=>select(document.selectionForClick(o.id,e.ctrlKey));name.ondblclick=()=>rename(o);const eye=button(o.visible?'◉':'○',()=>{document.transact('Visibility',()=>{o.visible=!o.visible;});refreshScene();});eye.setAttribute('aria-label','Toggle '+o.name+' visibility');row.append(name,eye);tree.append(row);}for(const category of ['Textures','Modifiers','Measurements','Export Profiles']){tree.append(el('div','tree-row tree-child','⌄  ▧  '+category));const items=category==='Textures'?document.state.textures:category==='Measurements'?document.state.measurements:category==='Export Profiles'?document.state.exportProfiles:document.state.objects.flatMap(o=>o.modifiers);for(const item of items){if(category==='Measurements'){const row=el('div','tree-row tree-child'),entry=button(item.name+' · '+formatMeasurement(item,document.state.units),()=>openDialog('Measurement',[el('p','',formatMeasurement(item,document.state.units)),el('p','','Saved world-space snapshot; later object edits do not recalculate it.'),button('Rename',()=>rename(item,'measurement')),button('Delete',()=>{document.transact('Delete measurement',s=>{s.measurements=s.measurements.filter(m=>m.id!==item.id);});refreshScene();})]));entry.dataset.measurementId=item.id;entry.ondblclick=()=>rename(item,'measurement');row.append(entry);tree.append(row);}else if(category==='Export Profiles'){const profile=button(item.name,()=>{if(document.state.activeProfile!==item.id){document.transact('Export profile',s=>{s.activeProfile=item.id;});refreshScene();}});profile.dataset.exportProfile=item.id;profile.setAttribute('aria-pressed',String(document.state.activeProfile===item.id));profile.className='tree-row tree-child';tree.append(profile);}else tree.append(el('div','tree-row tree-child','    '+(item.name??item.label)));}}}
 function rename(o,kind='object'){const input=el('input');input.value=o.name;const save=button('Rename',()=>{if(!input.value.trim())throw new Error('Enter a name');document.transact('Rename',()=>{o.name=input.value.trim();});refreshScene();});openDialog('Rename '+kind,[input,save]);}
 function numeric(label,values,commit){const row=el('div');row.append(el('p','',label));const inputs=values.map((value,i)=>{const input=el('input');input.type='number';input.step='.01';input.value=value;input.style.width='28%';input.setAttribute('aria-label',label+' '+['X','Y','Z'][i]);row.append(input);return input;});row.append(button('Apply',()=>{const result=inputs.map(i=>i.valueAsNumber);if(!result.every(Number.isFinite))throw new Error('Enter finite numeric values');commit(result);refreshScene();}));return row;}
 function renderProperties(){
  propertiesPanel.replaceChildren();const o=document.selected[0];if(!o){propertiesPanel.append(el('p','','Select an object.'));return;}
  const unit=document.state.units==='in'?'in':'mm',factor=unit==='in'?25.4:1;
  propertiesPanel.append(el('h3','',o.name),button('Rename',()=>rename(o)),button(o.locked?'Unlock':'Lock',()=>{document.transact('Lock',()=>{o.locked=!o.locked;});refreshScene();}),el('p','',`Source: ${o.sourceFormat}`));
  const linked=el('label','','Link scale axes '),checkbox=el('input');checkbox.type='checkbox';checkbox.checked=gizmo?.linked??true;checkbox.setAttribute('aria-label','Link scale axes');checkbox.onchange=()=>{gizmo.linked=checkbox.checked;renderProperties();};linked.append(checkbox);propertiesPanel.append(linked);
  const scaleForm=numeric('Scale',o.scale,v=>{if(gizmo.linked){const axis=v.findIndex((n,i)=>n!==o.scale[i]);if(axis>=0){const ratio=v[axis]/o.scale[axis];v=o.scale.map(n=>n*ratio);}}document.transform({scale:v});});
  if(gizmo?.linked){const inputs=[...scaleForm.querySelectorAll('input')];inputs.forEach((input,axis)=>{input.oninput=()=>{const ratio=input.valueAsNumber/o.scale[axis];inputs.forEach((other,i)=>{if(i!==axis)other.value=o.scale[i]*ratio;});};});}
  propertiesPanel.append(numeric(`Position (${unit})`,o.position.map(v=>v/factor),v=>document.transform({position:v.map(n=>n*factor)})),numeric('Rotation (degrees)',new THREE.Euler().setFromQuaternion(new THREE.Quaternion(...o.rotation)).toArray().slice(0,3).map(THREE.MathUtils.radToDeg),v=>document.transform({rotation:new THREE.Quaternion().setFromEuler(new THREE.Euler(...v.map(THREE.MathUtils.degToRad))).toArray()})),scaleForm);
  const report=diagnostics?.reportFor(o.id);propertiesPanel.append(el('p','',report?'Solid: '+(report.solid?'Closed with positive volume':'Not validated'):'Solid: Not checked for the current project state'),el('p','',report?'Normals: '+report.normals:'Normals: Not checked'));
  const geometry=geometryFromObject(o,{world:true});geometry.computeBoundingBox();const size=geometry.boundingBox.getSize(new THREE.Vector3());geometry.dispose();propertiesPanel.append(el('p','',`Dimensions: ${size.toArray().map(n=>(n/factor).toFixed(3)).join(' × ')} ${unit}`),el('p','',`Vertices: ${o.positions.length/3} · Triangles: ${(o.indices?.length??o.positions.length/3)/3}`),el('p','',`Visibility: ${o.visible?'Visible':'Hidden'}`),el('p','',o.modifiers.map(m=>m.name).join(', ')||'No committed modifiers'));
 }
 function syncToolButton(name){for(const node of window.document.querySelectorAll('.locked-toolbar button'))if(['Select','Move','Rotate','Scale'].includes(node.dataset.command))node.classList.toggle('active',node.dataset.command===name);}
 function editTransform(name){registry.execute('tab:Properties');gizmo.activate(name);syncToolButton(name);renderProperties();}
 for(const name of ['Move','Rotate','Scale'])registry.register(name,()=>editTransform(name),editable,'Select unlocked objects');
 registry.register('Center Model',()=>{document.center();refreshScene();},editable,'Select unlocked objects');registry.register('Undo',()=>{document.undo();refreshScene({activate:true});},()=>!busy&&document.undoStack.length>0,'No undo transaction');registry.register('Redo',()=>{document.redo();refreshScene({activate:true});},()=>!busy&&document.redoStack.length>0,'No redo transaction');registry.register('Select',()=>{gizmo.activate(null);viewer.getControls().enabled=true;syncToolButton('Select');});
 registry.register('Clear Texture',()=>{
  capture();document.transact('Clear active texture',()=>{for(const object of document.selected){object.textureId=null;delete object.sourceEffects;}});setPreview(null);refreshScene({activate:true});
 },()=>editable()&&document.selected.some(o=>o.textureId),'Select unlocked objects with an active texture. Baked relief is geometry; use Undo to reverse its commit.');
 registry.register('Place on Bed',()=>{document.placeOnBed();refreshScene();},editable,'Select unlocked objects');registry.register('Reset Transform',()=>{document.resetTransform();refreshScene();},editable,'Select unlocked objects');
 registry.register('Duplicate Selected',()=>{document.duplicate();refreshScene({activate:true});},editable,'Select unlocked objects');registry.register('Delete Selected',()=>{document.removeSelected();refreshScene({activate:true});},editable,'Select unlocked objects');registry.register('Select All',()=>select(document.state.objects.map(o=>o.id)),()=>!busy);
 const raycaster=new THREE.Raycaster();let down;
 viewport.addEventListener('pointerdown',e=>{down=[e.clientX,e.clientY];});viewport.addEventListener('pointerup',e=>{if(e.target.tagName!=='CANVAS'||!down||Math.hypot(e.clientX-down[0],e.clientY-down[1])>3||busy||gizmo?.interacting)return;const r=viewer.getRenderer().domElement.getBoundingClientRect();raycaster.setFromCamera(new THREE.Vector2((e.clientX-r.left)/r.width*2-1,-(e.clientY-r.top)/r.height*2+1),viewer.getCamera());const hits=raycaster.intersectObjects([viewer.getCurrentMesh(),...otherMeshes.children].filter(Boolean)).filter(h=>h.object.visible&&h.object.userData.sceneObjectId);if(hits.length){const id=hits[0].object.userData.sceneObjectId;select(document.selectionForClick(id,e.ctrlKey));}else if(!e.ctrlKey)select([]);});
 window.addEventListener('meshme:before-model-load',capture);
 window.addEventListener('meshme:model-result',event=>{if(!event.detail.success)return;const snapshot=engine.desktopSceneCapture();if(!snapshot.geometry)return;const object=document.add(snapshot.geometry,event.detail.file.name,event.detail.file.name.split('.').pop());snapshot.geometry.dispose();applySnapshot(object,snapshot);activeId=object.id;refreshScene();});
 window.addEventListener('meshme:texture-loaded',()=>{const object=document.state.objects.find(o=>o.id===activeId);if(!object)return;const snapshot=engine.desktopSceneCapture();try{document.transact('Texture source',()=>{const baseId=object.textureId;applySnapshot(object,snapshot);object.sourceEffects={sourceId:object.textureId,baseId,strength:1,blend:'normal',invert:false};});refreshScene();}finally{snapshot.geometry?.dispose();}});
 const openProject=file=>lifecycle.openProject(file);
 const openInput=el('input');openInput.type='file';openInput.accept='.mm360,.bumpmesh,.stl,.obj,.3mf,.step,.stp';openInput.hidden=true;window.document.body.append(openInput);openInput.onchange=()=>{const file=openInput.files[0];if(!file)return;void (/\.mm360$/i.test(file.name)?openProject(file):/\.bumpmesh$/i.test(file.name)?engine.desktopLoadProject(file):engine.desktopLoadModel(file)).catch(showError);openInput.value='';};
 registry.register('Open Project',()=>openInput.click());
 registry.register('menu:File',()=>{const list=el('div');for(const [label,command]of [['New Project','New Project'],['Open Project…','Open Project'],['Open Model…','Import'],['Import Model…','Import'],['Save','Save'],['Save As…','Save As'],['Export STL…','STL'],['Export 3MF…','3MF']])list.append(button(label,()=>registry.execute(command)));openDialog('File',[list]);});
 registry.register('menu:Edit',()=>{const list=el('div');for(const command of ['Undo','Redo','Duplicate Selected','Delete Selected','Select All']){const b=button(command,()=>registry.execute(command));b.disabled=!registry.state(command).enabled;list.append(b);}openDialog('Edit',[list]);});
 window.document.addEventListener('keydown',e=>{if(['INPUT','TEXTAREA','SELECT'].includes(e.target.tagName))return;if(e.key==='F2'&&document.selected[0]){e.preventDefault();rename(document.selected[0]);}if(e.key==='Delete'&&editable()){e.preventDefault();registry.execute('Delete Selected');}if(e.key==='Escape'&&!busy){document.select([]);gizmo.activate(null);syncToolButton('Select');refreshScene();}});
 gizmo=createSceneGizmo({document,viewer,meshes:()=>[viewer.getCurrentMesh(),...otherMeshes.children].filter(Boolean),refreshScene,showError,isBusy:()=>busy});
 const setBusy=value=>{busy=value;registry.busy=value;window.dispatchEvent(new CustomEvent('meshme:processing',{detail:{busy:value}}));if(value)gizmo.activate(null);for(const panel of window.document.querySelectorAll('.locked-project,.locked-inspector-body,.locked-shelf'))panel.inert=value;refresh();};
 const lifecycle=attachDocumentLifecycle({document,host,registry,capture,refreshScene,setBusy,showError});
 attachSolidTools({document,viewer,registry,editable,setBusy,refreshScene,openDialog,el});
 attachExportTools({document,engine,host,registry,openDialog,el,capture,refreshScene,setBusy});
 attachMeasureTools({document,viewer,registry,openDialog,el,setBusy,refreshScene});
 attachPrintTools({document,registry,openDialog,el,setBusy,refreshScene,capture});
 attachReliefTools({engine,document,registry,openDialog,el,setBusy,setPreview,refreshScene,commitResult:(name,result,input)=>{try{document.transact(name,()=>{const object=document.selected[0],groups=remapMaterialGroups(object.groups,result.faceParentId);engine.desktopAdoptDisplacement(result,input);const snapshot=engine.desktopSceneCapture();try{object.positions=new Float32Array(snapshot.geometry.attributes.position.array);object.indices=null;object.normals=new Float32Array(snapshot.geometry.attributes.normal.array);if(groups)object.groups=groups;applySnapshot(object,snapshot);object.modifiers.push({name:name+' (baked)',source:object.textureId,settings:structuredClone(input.settings),...(result.pattern?{pattern:result.pattern}:{})});}finally{snapshot.geometry.dispose();}});refreshScene({activate:true});}catch(error){refreshScene({activate:true});throw error;}}});
 diagnostics=attachDiagnosticsTools({document,registry,openDialog,el,setBusy,refreshScene});
 attachMeshProcessing({document,viewer,registry,openDialog,el,setBusy,refreshScene,capture});
 attachDocumentMenus({document,host,registry,openDialog,el,refreshScene});
 search.oninput=renderTree;refreshScene({activate:true});syncToolButton('Select');return{document,openProject,prepareReplacement:lifecycle.prepareReplacement,renderTree,renderProperties,refreshScene,capture,composeTexture,commitTextureSettings,diagnostics};
}
