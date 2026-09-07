// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from 'three';
import {geometryFromObject} from './scene-document.js';
export function attachAppDiagnostics({host,scene,engine,viewer,registry,openDialog,el}){
 registry.register('Application Diagnostics',async()=>{
  const native=await host.rpc.request('diagnostics.get',{}),gl=viewer.getRenderer().getContext(),extension=gl.getExtension('WEBGL_debug_renderer_info'),object=scene.document.selected[0];let bounds=null;
  if(object){const g=geometryFromObject(object,{world:true});g.computeBoundingBox();bounds=g.boundingBox.getSize(new THREE.Vector3()).toArray();g.dispose();}
  const settings=engine.desktopTextureSettings(),report={...native,webGLVendor:gl.getParameter(extension?.UNMASKED_VENDOR_WEBGL??gl.VENDOR),webGLRenderer:gl.getParameter(extension?.UNMASKED_RENDERER_WEBGL??gl.RENDERER),activeModelFormat:object?.sourceFormat??null,triangles:object?(object.indices?.length??object.positions.length/3)/3:0,dimensionsMm:bounds,cameraProjection:viewer.getCamera().isPerspectiveCamera?'Perspective':'Orthographic',textureProjection:({0:'Planar XY',1:'Planar XZ',2:'Planar YZ',3:'Cylindrical',4:'Spherical',5:'Triplanar',6:'Cubic'})[settings.mappingMode]??String(settings.mappingMode),displacementPreview:document.getElementById('displacement-preview')?.checked??null};
  const text=JSON.stringify(report,null,2),output=el('pre','',text);output.style.whiteSpace='pre-wrap';output.style.overflowWrap='anywhere';output.dataset.appDiagnostics='';
  const status=el('p'),copy=el('button','','Copy Diagnostics'),logs=el('button','','Open Log Folder');
  function action(button,type,payload,message){button.onclick=async()=>{try{await host.rpc.request(type,payload);status.textContent=message;}catch(error){status.textContent=String(error.message??error);}};}
  action(copy,'diagnostics.copy',{text},'Diagnostics copied.');action(logs,'diagnostics.openLogFolder',{},'Log folder opened.');
  openDialog('Application Diagnostics',[el('p','','Runtime information and active model statistics only. No mesh or texture contents are included.'),output,copy,logs,status]);
 },()=>!!host,'Open this app in the Windows desktop host');
}
