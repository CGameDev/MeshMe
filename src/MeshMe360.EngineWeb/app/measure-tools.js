// SPDX-License-Identifier: AGPL-3.0-only
/** Coordinates surface picking and persistent measurement commands. */
import * as THREE from 'three';
import {geometryFromObject} from './scene-document.js';
import {startSolidOperation} from './solid-client.js';
import {pointMeasurement,formatMeasurement,estimateWall} from './measurement.js';

export function attachMeasureTools({document,viewer,registry,openDialog,el,setBusy,refreshScene}){
 function launch(){
  const inputs=document.selected.filter(o=>o.visible).map(o=>structuredClone(o)),revision=document.revision;
  const meshes=inputs.map(o=>{const mesh=new THREE.Mesh(geometryFromObject(o,{world:true}),new THREE.MeshBasicMaterial({side:THREE.DoubleSide}));mesh.userData.objectId=o.id;mesh.updateMatrixWorld(true);return mesh;});
  const bounds=new THREE.Box3();for(const mesh of meshes){mesh.geometry.computeBoundingBox();bounds.union(mesh.geometry.boundingBox);}
  const overlay=new THREE.Group();viewer.getScene().add(overlay);const radius=Math.max(.02,bounds.getSize(new THREE.Vector3()).length()*.003);
  let points=[],result=null,closed=false,job=null,token=0,qualified=false,closeDialog,down;
  const mode=el('select');mode.setAttribute('aria-label','Measurement type');for(const [value,label]of [['distance','Point-to-point distance'],['angle','Three-point angle (vertex second)'],['bounds','Bounding dimensions'],['thickness','Local wall estimate']]){const option=el('option','',label);option.value=value;mode.append(option);}
  const name=el('input');name.value='Measurement '+(document.state.measurements.length+1);name.setAttribute('aria-label','Measurement name');
  const status=el('p');status.dataset.measureStatus='picking';const save=el('button','primary','Save measurement'),clear=el('button','','Clear points'),cancel=el('button','','Cancel');save.disabled=true;
  function clearOverlay(){for(const child of [...overlay.children]){overlay.remove(child);child.geometry?.dispose();if(Array.isArray(child.material))child.material.forEach(m=>m.dispose());else child.material?.dispose();}}
  function draw(){clearOverlay();if(mode.value==='bounds'){overlay.add(new THREE.Box3Helper(bounds.clone(),0x54b7ef));}else{for(const p of points){const marker=new THREE.Mesh(new THREE.SphereGeometry(radius,12,8),new THREE.MeshBasicMaterial({color:0x54b7ef,depthTest:false}));marker.position.fromArray(p);overlay.add(marker);}if(points.length>1)overlay.add(new THREE.Line(new THREE.BufferGeometry().setFromPoints(points.map(p=>new THREE.Vector3(...p))),new THREE.LineBasicMaterial({color:0x54b7ef,depthTest:false})));}viewer.requestRender();}
  function showResult(value){result={...value,objectIds:inputs.map(o=>o.id),snapshot:true};points=value.points??[];status.dataset.measureStatus='ready';status.textContent=formatMeasurement(result,document.state.units)+(result.kind==='thickness'?' — local normal-ray estimate; not a global minimum.':'');save.disabled=false;draw();}
  async function reset(){
   const current=++token;job?.cancel();job=null;points=[];result=null;qualified=false;save.disabled=true;draw();status.dataset.measureStatus='picking';
   if(mode.value==='bounds'){showResult({kind:'bounds',dimensionsMm:bounds.getSize(new THREE.Vector3()).toArray(),bounds:{min:bounds.min.toArray(),max:bounds.max.toArray()}});return;}
   if(mode.value==='thickness'){
    status.dataset.measureStatus='validating';status.textContent='Checking closed mesh winding before estimating wall thickness…';
    try{for(const mesh of meshes){job=startSolidOperation({operation:'validate',objects:[{positions:mesh.geometry.attributes.position.array,indices:mesh.geometry.index?.array??null}]});await job.promise;if(closed||token!==current)return;}job=null;qualified=true;status.dataset.measureStatus='picking';status.textContent='Pick a surface point for an inward normal-ray wall estimate.';}
    catch(error){if(closed||error.name==='AbortError')return;status.dataset.measureStatus='error';status.textContent='Wall estimate unavailable: '+String(error.message??error);}
   }else status.textContent='Pick '+(mode.value==='angle'?'three points; the second point is the angle vertex.':'two points on the selected objects.');
  }
  const canvas=viewer.getRenderer().domElement,ray=new THREE.Raycaster();
  function pointerDown(e){down=[e.clientX,e.clientY];}
  function pointerUp(e){
   if(closed||!down||e.button!==0||Math.hypot(e.clientX-down[0],e.clientY-down[1])>3||mode.value==='bounds'||mode.value==='thickness'&&!qualified)return;
   const r=canvas.getBoundingClientRect();ray.setFromCamera(new THREE.Vector2((e.clientX-r.left)/r.width*2-1,1-(e.clientY-r.top)/r.height*2),viewer.getCamera());const hit=ray.intersectObjects(meshes)[0];if(!hit)return;
   try{if(mode.value==='thickness'){showResult(estimateWall(hit.object,hit));return;}if(result){points=[];result=null;save.disabled=true;}points.push(hit.point.toArray());const needed=mode.value==='angle'?3:2;if(points.length===needed)showResult(pointMeasurement(mode.value,points));else{status.textContent=`Picked ${points.length}/${needed} points.`;draw();}}
   catch(error){status.dataset.measureStatus='error';status.textContent=String(error.message??error);}
  }
  function cleanup(){if(closed)return;closed=true;job?.cancel();canvas.removeEventListener('pointerdown',pointerDown);canvas.removeEventListener('pointerup',pointerUp);window.removeEventListener('meshme:before-model-load',cancelOnImport);clearOverlay();viewer.getScene().remove(overlay);for(const mesh of meshes){mesh.geometry.dispose();mesh.material.dispose();}setBusy(false);refreshScene();}
  function cancelOnImport(){closeDialog();}
  save.onclick=()=>{try{if(!result||document.revision!==revision)throw new Error('The measurement is stale. Reopen Measure.');if(!name.value.trim())throw new Error('Enter a measurement name.');document.transact('Add measurement',s=>s.measurements.push({id:crypto.randomUUID(),name:name.value.trim(),...structuredClone(result)}));closeDialog();}catch(error){status.textContent=String(error.message??error);}};
  mode.onchange=()=>void reset();clear.onclick=()=>void reset();cancel.onclick=()=>closeDialog();const actions=el('div');actions.append(save,clear,cancel);
  registry.execute('mode:Shaded');closeDialog=openDialog('Measure',[mode,name,el('p','','Measurements use committed geometry and are saved as world-space snapshots.'),status,actions],cleanup,'tool');setBusy(true);canvas.addEventListener('pointerdown',pointerDown);canvas.addEventListener('pointerup',pointerUp);window.addEventListener('meshme:before-model-load',cancelOnImport);void reset();
 }
 registry.register('Measure',launch,()=>document.selected.some(o=>o.visible),'Select visible objects to measure');
}
