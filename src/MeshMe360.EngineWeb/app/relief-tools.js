// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from 'three';
import {startDisplacement} from './displacement-client.js';

export function attachReliefTools({engine,document,registry,openDialog,el,setBusy,setPreview,refreshScene,commitResult}){
 const available=()=>!registry.busy&&document.selected.length===1&&!document.selected[0].locked&&!!document.selected[0].textureId;
 function launch(name){
  const original=engine.desktopSceneCapture(),revision=document.revision;original.geometry?.dispose();
  let job=null,previewGeometry=null,result=null,input=null,closed=false,committed=false,closeDialog;
  const form=el('div'),status=el('p','','Preview uses the preserved subdivision/displacement engine.');status.dataset.reliefStatus='idle';
  const fields={};for(const [label,key,value]of [['Depth (mm)','textureHeight',Math.abs(original.settings.textureHeight??.5)],['Scale U (mm)','scaleU',original.settings.scaleU],['Scale V (mm)','scaleV',original.settings.scaleV]]){const row=el('label','locked-field'),control=el('input');control.type='number';control.min='.001';control.step='.01';control.value=value;control.setAttribute('aria-label',label);row.append(el('span','',label),control);form.append(row);fields[key]=control;control.oninput=invalidate;}
  const preview=el('button','','Preview'),commit=el('button','primary','Commit'),cancel=el('button','','Cancel');commit.disabled=true;
  function clear(){setPreview(null);previewGeometry?.dispose();previewGeometry=null;result=null;commit.disabled=true;}
  function invalidate(){job?.cancel();job=null;clear();status.dataset.reliefStatus='idle';status.textContent='Parameters changed. Preview again.';}
  function cleanup(){if(closed)return;closed=true;job?.cancel();clear();if(!committed)engine.desktopConfigureTexture(original.settings);setBusy(false);refreshScene();window.removeEventListener('meshme:before-model-load',onImport);}
  function onImport(){closeDialog();}
  preview.onclick=async()=>{
   invalidate();setBusy(true);status.dataset.reliefStatus='running';status.textContent='Processing… Cancel preserves the current mesh.';
   try{
    if(document.revision!==revision)throw new Error('Document changed; reopen the texture tool');
    const options=Object.fromEntries(Object.entries(fields).map(([key,field])=>[key,field.valueAsNumber]));if(!Object.values(options).every(n=>Number.isFinite(n)&&n>=0)||options.scaleU<=0||options.scaleV<=0)throw new Error('Enter valid depth and positive scale');
    engine.desktopConfigureTexture({...options,invertDisplacement:name==='Deboss'?true:name==='Emboss'?false:original.settings.invertDisplacement,symmetricDisplacement:false});
    input=engine.desktopDisplacementInput();const current=startDisplacement(input,data=>{status.textContent=`${data.stage}: ${Math.round(data.p*100)}%`;});job=current;
    const output=await current.promise;if(closed||job!==current)return;job=null;result=output;
    previewGeometry=new THREE.BufferGeometry();previewGeometry.setAttribute('position',new THREE.BufferAttribute(output.positions,3));if(output.normals)previewGeometry.setAttribute('normal',new THREE.BufferAttribute(output.normals,3));else previewGeometry.computeVertexNormals();setPreview(previewGeometry);
    status.dataset.reliefStatus='ready';status.textContent=`Preview ready: ${(output.positions.length/9).toLocaleString()} triangles. Commit creates one undoable geometry edit.`;commit.disabled=false;
   }catch(error){if(closed||error.name==='AbortError')return;status.dataset.reliefStatus='error';status.textContent=error.message;}
  };
  commit.onclick=()=>{try{if(!result||document.revision!==revision)throw new Error('Preview is stale');commitResult(name,result,input);committed=true;closeDialog();}catch(error){status.textContent=error.message;}};
  cancel.onclick=()=>closeDialog();closeDialog=openDialog(name,[form,preview,status,commit,cancel],cleanup,'tool');setBusy(true);window.addEventListener('meshme:before-model-load',onImport);
 }
 for(const name of ['Emboss','Deboss','Apply Texture'])registry.register(name,()=>launch(name),available,'Select one unlocked object with a texture source');
}
