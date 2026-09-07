// SPDX-License-Identifier: AGPL-3.0-only
/** Projects an interactive orientation cube from the actual camera pose. */
import * as THREE from 'three';
// Original vector navigation cube, projected from the actual Z-up camera.
export function attachOrientationCube({container,viewer,registry}){
 const ns='http://www.w3.org/2000/svg',make=(tag,attrs={})=>{const n=document.createElementNS(ns,tag);for(const [k,v]of Object.entries(attrs))n.setAttribute(k,v);return n;};
 const svg=make('svg',{viewBox:'0 0 80 100','aria-label':'Orientation cube'}),axis=make('text',{x:40,y:12,class:'cube-axis'});axis.textContent='Z';
 container.replaceChildren(svg);svg.append(axis);
 const definitions=[['Top',[0,0,1],[[-1,-1,1],[1,-1,1],[1,1,1],[-1,1,1]]],['Bottom',[0,0,-1],[[-1,1,-1],[1,1,-1],[1,-1,-1],[-1,-1,-1]]],['Front',[0,-1,0],[[-1,-1,-1],[1,-1,-1],[1,-1,1],[-1,-1,1]]],['Back',[0,1,0],[[1,1,-1],[-1,1,-1],[-1,1,1],[1,1,1]]],['Left',[-1,0,0],[[-1,1,-1],[-1,-1,-1],[-1,-1,1],[-1,1,1]]],['Right',[1,0,0],[[1,-1,-1],[1,1,-1],[1,1,1],[1,-1,1]]]];
 const faces=definitions.map(([name,normal,vertices])=>{
  const group=make('g',{role:'button',tabindex:0,'aria-label':name+' view','data-cube-face':name}),polygon=make('polygon'),label=make('text');label.textContent=name;
  const title=make('title');title.textContent=name+' view';group.append(title,polygon,label);svg.append(group);
  const activate=()=>registry.execute('view:'+name);group.addEventListener('click',activate);group.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();activate();}});
  return {group,polygon,label,normal:new THREE.Vector3(...normal),vertices:vertices.map(v=>new THREE.Vector3(...v))};
 });
 function sync(){
  const rotation=viewer.getCamera().quaternion.clone().invert();
  for(const face of faces){
   const n=face.normal.clone().applyQuaternion(rotation),visible=n.z>.001;face.group.style.display=visible?'':'none';face.group.setAttribute('aria-hidden',String(!visible));if(!visible)continue;
   const points=face.vertices.map(v=>v.clone().applyQuaternion(rotation));face.polygon.setAttribute('points',points.map(v=>`${40+v.x*23},${58-v.y*23}`).join(' '));
   const shade=Math.round(105+55*n.z);face.polygon.setAttribute('fill',`rgb(${shade},${shade+7},${shade+13})`);
   face.label.setAttribute('x',40+n.x*23);face.label.setAttribute('y',58-n.y*23+3);face.group.dataset.facing=n.z.toFixed(4);
  }
 }
 viewer.getControls().addEventListener('change',sync);window.addEventListener('meshme:scene-changed',sync);sync();
 return{sync};
}
