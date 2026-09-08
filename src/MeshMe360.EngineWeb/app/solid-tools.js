// SPDX-License-Identifier: AGPL-3.0-only
/** Presents solid-operation parameters and transactional previews. */
import * as THREE from 'three';
import {geometryFromObject,objectFromGeometry} from './scene-document.js';
import {startSolidOperation} from './solid-client.js';
import {attachQuality} from './quality-presets.js';

export function attachSolidTools({document,viewer,registry,editable,setBusy,refreshScene,openDialog,el}){
 function launch(kind){
  const inputs=document.selected.map(o=>structuredClone(o)),revision=document.revision;
  let job=null,result=null,closed=false,closeDialog;const hiddenOriginals=[];
  const previewGroup=new THREE.Group();viewer.getScene().add(previewGroup);
  const status=el('p','','Choose settings, then Preview. Originals remain unchanged until Commit.');status.dataset.solidStatus='idle';
  const actions=el('div'),form=el('div');
  function field(label,type,value){const row=el('label','locked-field'),input=el('input');input.type=type;input.value=value;input.setAttribute('aria-label',label);row.append(el('span','',label),input);form.append(row);input.addEventListener('input',invalidate);return input;}
  function choices(label,values){const row=el('label','locked-field'),input=el('select');input.setAttribute('aria-label',label);for(const [value,text]of values){const option=el('option','',text);option.value=value;input.append(option);}row.append(el('span','',label),input);form.append(row);input.onchange=invalidate;return input;}
  let operation,thickness,normal,offset,keep,cellSize,maxHoleDiameter,weld;
  if(kind==='Boolean')operation=choices('Operation',[['union','Union'],['subtract','Subtract (first selected minus the others)'],['intersect','Intersect']]);
  if(kind==='Hollow')thickness=field('Wall thickness (mm)','number',1);
  if(kind==='Combine'){
   weld=field('Weld exactly coincident vertices','checkbox','');
   form.append(el('p','','Combine merges geometry and retains material groups. Intersections remain; this does not create a Boolean union. Uncommitted texture/displacement previews are not baked. Apply relief first if you want it in the merged geometry.'));
  }
  if(kind==='Repair'){
   maxHoleDiameter=field('Maximum hole diameter (mm)','number',1);maxHoleDiameter.min='0';maxHoleDiameter.step='.1';
   form.append(el('p','','Preview removes invalid/zero-area triangles, merges exact duplicate vertices and corrects unambiguous winding. Only boundary loops within this diameter and 64 edges are eligible for filling; use 0 to leave all openings. Review the report before Commit.'));
  }
  if(kind==='Make Solid'){
   const g=geometryFromObject(inputs[0],{world:true});g.computeBoundingBox();const size=g.boundingBox.getSize(new THREE.Vector3());g.dispose();
   cellSize=field('Cell size (mm)','number',Math.max(size.x,size.y,size.z)/32);cellSize.min='.001';cellSize.step='.1';
   attachQuality(form,cellSize,'solidify',Number(cellSize.value),el);
   form.append(el('p','','Approximate reconstruction for meshes with a coherent enclosed interior. Smaller cells retain more detail and create more triangles. Open sheets may have no valid interior.'));
  }
  if(kind==='Split'){
   normal=[field('Plane normal X','number',0),field('Plane normal Y','number',0),field('Plane normal Z','number',1)];
   const g=geometryFromObject(inputs[0],{world:true});g.computeBoundingBox();const b=g.boundingBox;g.dispose();
   offset=field('Plane offset (mm)','number',(b.min.z+b.max.z)/2);offset.step='.1';
   const slider=field('Slide plane (mm)','range',offset.value);slider.min=b.min.z;slider.max=b.max.z;slider.step='.01';slider.oninput=()=>{offset.value=slider.value;invalidate();drawPlane();};
   keep=choices('Keep',[['both','Both sides'],['positive','Positive side'],['negative','Negative side']]);
   for(const input of [...normal,offset])input.addEventListener('input',drawPlane);
   function drawPlane(){clearPreview();const n=new THREE.Vector3(...normal.map(i=>i.valueAsNumber));if(!Number.isFinite(n.length())||!n.length()||!Number.isFinite(offset.valueAsNumber))return;n.normalize();const size=b.getSize(new THREE.Vector3()).length()*1.2;const plane=new THREE.Mesh(new THREE.PlaneGeometry(size,size),new THREE.MeshBasicMaterial({color:0x47b6ff,side:THREE.DoubleSide,transparent:true,opacity:.2,depthWrite:false}));plane.quaternion.setFromUnitVectors(new THREE.Vector3(0,0,1),n);plane.position.copy(b.getCenter(new THREE.Vector3()));plane.position.addScaledVector(n,offset.valueAsNumber-plane.position.dot(n));previewGroup.add(plane);viewer.requestRender();}
   drawPlane();
  }
  const preview=el('button','','Preview'),commit=el('button','primary','Commit'),cancel=el('button','','Cancel');commit.disabled=true;actions.append(preview,commit,cancel);
  if(kind==='Repair'){const advanced=el('details','locked-advanced');advanced.append(el('summary','','Advanced'));advanced.append(maxHoleDiameter.closest('label'));form.prepend(advanced);}
  function clearPreview(){for(const mesh of hiddenOriginals.splice(0))mesh.visible=true;for(const mesh of [...previewGroup.children]){previewGroup.remove(mesh);mesh.geometry.dispose();mesh.material.dispose();}viewer.requestRender();}
  function invalidate(){if(job){job.cancel();job=null;}result=null;commit.disabled=true;clearPreview();setBusy(false);status.dataset.solidStatus='idle';status.textContent='Settings changed. Preview again before committing.';preview.disabled=false;}
  function cleanup(){if(closed)return;closed=true;job?.cancel();clearPreview();viewer.getScene().remove(previewGroup);setBusy(false);refreshScene();window.removeEventListener('meshme:before-model-load',cancelOnImport);}
  function cancelOnImport(){closeDialog();}
  preview.onclick=async()=>{
   invalidate();setBusy(true);preview.disabled=true;status.dataset.solidStatus='running';status.textContent='Processing in background… Cancel preserves the original objects.';
   try{
    if(document.revision!==revision)throw new Error('The document changed. Reopen the tool.');
    const objects=inputs.map(o=>{const g=geometryFromObject(o,{world:true});const data={positions:new Float32Array(g.attributes.position.array),indices:g.index?new Uint32Array(g.index.array):null,material:o.material,materials:o.materials,groups:o.groups};g.dispose();return data;});
    const request={operation:kind==='Boolean'?operation.value:kind==='Make Solid'?'solidify':kind.toLowerCase(),objects,thickness:thickness?.valueAsNumber,normal:normal?.map(i=>i.valueAsNumber),offset:offset?.valueAsNumber,cellSize:cellSize?.valueAsNumber,maxHoleDiameter:maxHoleDiameter?.valueAsNumber,weld:weld?.checked};
    const currentJob=startSolidOperation(request);job=currentJob;const response=await currentJob.promise;if(closed||job!==currentJob)return;
    job=null;result=response;if(kind==='Split'&&keep.value!=='both')result.results=[response.results[keep.value==='positive'?0:1]];
    result.request=request;
    clearPreview();viewer.getScene().traverse(mesh=>{if(mesh.isMesh&&mesh.visible&&inputs.some(o=>o.id===mesh.userData.sceneObjectId)){hiddenOriginals.push(mesh);mesh.visible=false;}});for(const item of result.results){const geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.BufferAttribute(item.positions,3));geometry.setIndex(new THREE.BufferAttribute(item.indices,1));geometry.computeVertexNormals();previewGroup.add(new THREE.Mesh(geometry,new THREE.MeshStandardMaterial({color:0x54b7ef,transparent:true,opacity:.72,roughness:.6,side:THREE.DoubleSide})));}
    status.dataset.solidStatus='ready';status.textContent=result.results.map((r,i)=>`Result ${i+1}: ${r.diagnostics.triangles.toLocaleString()} triangles; ${Number.isFinite(r.diagnostics.volume)?r.diagnostics.volume.toFixed(3)+' mm³':'volume unavailable'}; ${r.diagnostics.status}.`).join(' ');
    for(const r of result.results)if(r.repairReport){const q=r.repairReport;status.textContent+=` Before → After: triangles ${q.before.triangles} → ${q.after.triangles}; boundary edges ${q.before.boundaryEdges} → ${q.after.boundaryEdges}; non-manifold edges ${q.before.nonManifoldEdges} → ${q.after.nonManifoldEdges}; inconsistent edges ${q.before.inconsistentEdges} → ${q.after.inconsistentEdges}. Removed ${q.invalidVerticesRemoved} invalid vertices, ${q.invalidTrianglesRemoved} invalid triangles, ${q.degenerateTrianglesRemoved} degenerate triangles; merged ${q.duplicateVerticesMerged} duplicate vertices; reoriented ${q.trianglesReoriented} faces${q.closedShellReversed?' and reversed a closed shell':''}; filled ${q.holesFilled}/${q.holesFound} holes.`;}
    status.textContent+=(response.warning?' '+response.warning:'')+(kind==='Hollow'?' Minimum wall thickness still requires verification.':'')+(result.results.some(r=>r.diagnostics.triangles>250000)?' High triangle count: saving and later edits may require substantial memory.':'');commit.disabled=false;viewer.requestRender();
   }catch(error){if(closed||error.name==='AbortError')return;status.dataset.solidStatus='error';status.textContent=String(error.message??error);setBusy(false);}
   finally{if(!closed)preview.disabled=false;}
  };
  commit.onclick=()=>{
   try{
    if(!result||document.revision!==revision)throw new Error('Preview is stale. Cancel and reopen the tool.');
    const output=result.results.map((r,i)=>{
     const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(r.positions.slice(),3));g.setIndex(new THREE.BufferAttribute(r.indices.slice(),1));
     // Split and hollow retain the original object's coordinate frame.
     if(kind!=='Boolean')g.applyMatrix4(new THREE.Matrix4().compose(new THREE.Vector3(...inputs[0].position),new THREE.Quaternion(...inputs[0].rotation),new THREE.Vector3(...inputs[0].scale)).invert());
     const object=objectFromGeometry(g,`${inputs[0].name} ${kind.toLowerCase()}${result.results.length>1?' '+(i+1):''}`,'meshme');g.dispose();
     object.material=structuredClone(inputs[0].material);
     if(r.materials){object.materials=structuredClone(r.materials);object.groups=structuredClone(r.groups);}
     if(kind!=='Boolean')for(const key of ['position','rotation','scale'])object[key]=[...inputs[0][key]];
     object.modifiers=[{name:kind+' (baked)',operation:result.request.operation,thickness:result.request.thickness,normal:result.request.normal,offset:result.request.offset,cellSize:result.request.cellSize,maxHoleDiameter:result.request.maxHoleDiameter,diagnostics:r.diagnostics,repairReport:r.repairReport}];return object;
    });
    document.transact(kind,s=>{s.objects=s.objects.filter(o=>!inputs.some(input=>input.id===o.id));s.objects.push(...output);s.selection=output.map(o=>o.id);});
    closeDialog();refreshScene({activate:true});
   }catch(error){status.textContent=String(error.message??error);}
  };
  cancel.onclick=()=>closeDialog();closeDialog=openDialog(kind,[form,status,actions],cleanup,'tool');window.addEventListener('meshme:before-model-load',cancelOnImport);
 }
 registry.register('Boolean',()=>launch('Boolean'),()=>editable()&&document.selected.length>=2,'Select at least two unlocked closed meshes');
 for(const kind of ['Split','Hollow'])registry.register(kind,()=>launch(kind),()=>editable()&&document.selected.length===1,'Select one unlocked closed mesh');
 registry.register('Hollow Model',()=>registry.execute('Hollow'),()=>registry.state('Hollow').enabled,'Select one unlocked closed mesh');
 registry.register('Make Solid',()=>launch('Make Solid'),()=>editable()&&document.selected.length===1,'Select one unlocked mesh with an enclosed interior');
 registry.register('Repair',()=>launch('Repair'),()=>editable()&&document.selected.length===1,'Select one unlocked mesh');
 registry.register('Repair Mesh',()=>registry.execute('Repair'),()=>registry.state('Repair').enabled,'Select one unlocked mesh');
 registry.register('Combine',()=>launch('Combine'),()=>editable()&&document.selected.length>=2,'Select at least two unlocked objects');
}
