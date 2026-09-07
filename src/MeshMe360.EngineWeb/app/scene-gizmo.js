// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from 'three';
import {TransformControls} from 'three/addons/controls/TransformControls.js';

export function createSceneGizmo({document,viewer,meshes,refreshScene,showError,isBusy}){
 const pivot=new THREE.Object3D();viewer.getScene().add(pivot);
 const controls=new TransformControls(viewer.getCamera(),viewer.getRenderer().domElement);
 viewer.getScene().add(controls.getHelper());let transaction=null,mode=null,linked=true,cancelling=false;
 const selectedBounds=new THREE.Box3(),selectionHelper=new THREE.Box3Helper(selectedBounds,0x499ac8);viewer.getScene().add(selectionHelper);
 function highlight(){selectedBounds.makeEmpty();for(const mesh of meshes())if(mesh.visible&&document.state.selection.includes(mesh.userData.sceneObjectId)){mesh.updateMatrixWorld();selectedBounds.union(new THREE.Box3().setFromObject(mesh));}selectionHelper.visible=!selectedBounds.isEmpty();}
 function sync(){
  controls.camera=viewer.getCamera();
  highlight();
  if(transaction)return;
  if(!mode||!document.selected.length||document.selected.some(o=>o.locked)||isBusy()){controls.detach();return;}
  pivot.position.set(0,0,0);for(const o of document.selected)pivot.position.add(new THREE.Vector3(...o.position));pivot.position.divideScalar(document.selected.length);
  pivot.quaternion.identity();pivot.scale.set(1,1,1);
  if(mode==='scale'&&document.selected.length===1)pivot.quaternion.fromArray(document.selected[0].rotation);
  pivot.updateMatrixWorld();controls.setMode(mode);controls.setSpace(mode==='scale'?'local':'world');controls.attach(pivot);viewer.requestRender();
 }
 function restore(){for(const mesh of meshes()){const object=document.state.objects.find(o=>o.id===mesh.userData.sceneObjectId);if(object){mesh.position.fromArray(object.position);mesh.quaternion.fromArray(object.rotation);mesh.scale.fromArray(object.scale);}}viewer.requestRender();}
 function cancel(){if(!transaction)return;cancelling=true;controls.reset();transaction=null;controls.dragging=false;viewer.getControls().enabled=true;restore();cancelling=false;sync();}
 controls.addEventListener('change',()=>viewer.requestRender());
 controls.addEventListener('mouseDown',()=>{
  if(isBusy()){controls.detach();return;}
  pivot.updateMatrixWorld();transaction={revision:document.revision,inverse:pivot.matrixWorld.clone().invert(),objects:document.selected.map(o=>structuredClone(o)),result:[]};viewer.getControls().enabled=false;
 });
 controls.addEventListener('objectChange',()=>{
  if(!transaction||cancelling)return;
  if(mode==='scale'&&(linked||transaction.objects.length>1)){
   const values=pivot.scale.toArray(),factor=values.reduce((best,v)=>Math.abs(v-1)>Math.abs(best-1)?v:best,1);pivot.scale.setScalar(Math.max(.0001,factor));
  }
  pivot.updateMatrixWorld();const delta=pivot.matrixWorld.clone().multiply(transaction.inverse);
  transaction.result=transaction.objects.map(o=>{
   const matrix=new THREE.Matrix4().compose(new THREE.Vector3(...o.position),new THREE.Quaternion(...o.rotation),new THREE.Vector3(...o.scale));matrix.premultiply(delta);
   const position=new THREE.Vector3(),rotation=new THREE.Quaternion(),scale=new THREE.Vector3();matrix.decompose(position,rotation,scale);
   return{id:o.id,position:position.toArray(),rotation:rotation.toArray(),scale:scale.toArray()};
  });
  for(const mesh of meshes()){const value=transaction.result.find(o=>o.id===mesh.userData.sceneObjectId);if(value){mesh.position.fromArray(value.position);mesh.quaternion.fromArray(value.rotation);mesh.scale.fromArray(value.scale);}}
  highlight();viewer.requestRender();
 });
 controls.addEventListener('mouseUp',()=>{
  if(!transaction)return;const completed=transaction;transaction=null;viewer.getControls().enabled=true;
  try{
   if(document.revision!==completed.revision)throw new Error('Document changed during transform; drag discarded.');
   if(completed.result.length)document.transact('Transform',state=>{for(const value of completed.result){const object=state.objects.find(o=>o.id===value.id);Object.assign(object,value);}});
  }catch(error){showError(error);}
  restore();refreshScene();sync();
 });
 window.addEventListener('keydown',event=>{if(event.key==='Escape'){cancel();mode=null;controls.detach();}});
 window.addEventListener('meshme:before-model-load',()=>{cancel();controls.detach();});
 return{activate(name){cancel();mode={Move:'translate',Rotate:'rotate',Scale:'scale'}[name]??null;sync();},sync,cancel,get interacting(){return !!transaction||controls.axis!==null;},get linked(){return linked;},set linked(value){linked=!!value;},get active(){return mode!==null;}};
}
