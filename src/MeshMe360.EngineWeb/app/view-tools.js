// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from 'three';
import {geometryFromObject} from './scene-document.js';
export function attachViewTools({scene,viewer,registry,openDialog,el,work}){
 let grid=true,axes=true;
 function syncGuides(render=true){if(viewer.getGrid())viewer.getGrid().visible=grid;if(viewer.getDesktopAxes())viewer.getDesktopAxes().visible=axes;if(render)viewer.requestRender();}
 window.addEventListener('meshme:scene-changed',()=>syncGuides());
 const objects=all=>(all?scene.document.state.objects:scene.document.selected).filter(o=>o.visible);
 function fit(all,home=false){const box=new THREE.Box3();for(const object of objects(all)){const g=geometryFromObject(object,{world:true});g.computeBoundingBox();box.union(g.boundingBox);g.dispose();}if(box.isEmpty())return;
  const camera=viewer.getCamera(),controls=viewer.getControls(),center=box.getCenter(new THREE.Vector3()),radius=Math.max(box.getSize(new THREE.Vector3()).length()/2,.001),direction=home?new THREE.Vector3(.6,-1.2,.8).normalize():camera.position.clone().sub(controls.target).normalize();
  const aspect=viewer.getRenderer().domElement.clientWidth/viewer.getRenderer().domElement.clientHeight;
  if(camera.isOrthographicCamera){const half=radius*1.2;camera.left=-half*aspect;camera.right=half*aspect;camera.top=half;camera.bottom=-half;camera.zoom=Math.min(1,aspect);camera.near=-radius*200;camera.far=radius*200;camera.position.copy(center).addScaledVector(direction,radius*4);}
  else {const halfAngle=Math.min(THREE.MathUtils.degToRad(camera.fov/2),Math.atan(Math.tan(THREE.MathUtils.degToRad(camera.fov/2))*aspect));camera.position.copy(center).addScaledVector(direction,radius/Math.sin(halfAngle)*1.2);camera.near=radius*.001;camera.far=radius*400;}
  controls.target.copy(center);camera.up.set(0,0,1);camera.lookAt(center);camera.updateProjectionMatrix();controls.update();viewer.requestRender();
 }
 registry.register('Grid',()=>{grid=!grid;syncGuides();});registry.register('Axes',()=>{axes=!axes;syncGuides();});
 registry.register('Fit Selected',()=>fit(false),()=>objects(false).length>0,'Select visible objects');registry.register('Fit All',()=>fit(true),()=>objects(true).length>0,'Import visible objects');registry.register('Home view',()=>fit(true,true),()=>objects(true).length>0,'Import visible objects');
 registry.register('Fit',()=>fit(!objects(false).length),()=>objects(true).length>0,'Import visible objects');
 for(const name of ['Perspective','Orthographic'])registry.register('view:'+name,()=>{viewer.setProjection(name==='Perspective');scene.refreshScene();});
 for(const [name,css]of [['Browser panel','no-project'],['Right inspector panel','no-inspector'],['Lower library shelf','no-shelf']])registry.register('view:'+name,()=>{work.classList.toggle(css);window.dispatchEvent(new Event('resize'));viewer.requestRender();});
 registry.register('menu:View',()=>{const list=el('div');for(const [label,command]of [...['Shaded','Wireframe','Textures','X-Ray'].map(n=>[n,'mode:'+n]),['Grid','Grid'],['Axes','Axes'],...['Home view','Fit Selected','Fit All'].map(n=>[n,n]),...['Perspective','Orthographic','Browser panel','Right inspector panel','Lower library shelf'].map(n=>[n,'view:'+n]),['Before / after','Comparison'],...['Top','Bottom','Front','Back','Left','Right'].map(n=>[n,'view:'+n])]){const b=el('button','',label),state=registry.state(command);b.disabled=!state.enabled;b.title=state.enabled?'':state.reason;b.onclick=()=>{try{registry.execute(command);}catch(error){openDialog('View unavailable',[el('p','',String(error.message??error))]);}};list.append(b);}openDialog('View',[list]);});
 syncGuides();
 return{syncGuides};
}
