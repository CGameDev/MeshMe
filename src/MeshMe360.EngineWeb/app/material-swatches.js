// SPDX-License-Identifier: AGPL-3.0-only
/** Renders material previews from the same height sources used by the tools. */
import * as THREE from 'three';
const appearances={Stone:['#99968f',.95,0,.055],Rock:['#797d7b',.95,0,.06],Scales:['#8b8677',.8,.05,.045],Bricks:['#ba8064',.95,0,.025],Wood:['#ac7b46',.7,0,.018],Metal:['#b4b8bc',.25,.8,.004],Fabric:['#a79a83',1,0,.015],Carbon:['#353b3d',.6,.15,.025],Leather:['#965f3e',.85,0,.025],Concrete:['#95928b',1,0,.035],Sand:['#c1ae8e',1,0,.018]};
// Render real source-height bump/displacement on a sphere. These preview
// materials do not replace the model's independently editable PBR material.
export function createMaterialSwatches(renderer,sources){
 const prior={target:renderer.getRenderTarget(),color:renderer.getClearColor(new THREE.Color()),alpha:renderer.getClearAlpha(),viewport:renderer.getViewport(new THREE.Vector4()),scissor:renderer.getScissor(new THREE.Vector4()),scissorTest:renderer.getScissorTest(),autoClear:renderer.autoClear};
 const size=160,target=new THREE.WebGLRenderTarget(size,size,{depthBuffer:true});target.texture.colorSpace=THREE.SRGBColorSpace;
 const scene=new THREE.Scene(),camera=new THREE.PerspectiveCamera(30,1,.1,20);camera.position.set(0,0,3.9);camera.lookAt(0,0,0);
 scene.add(new THREE.HemisphereLight(0xf1f3f5,0x353738,2));const key=new THREE.DirectionalLight(0xfff6e8,3);key.position.set(-3,4,5);scene.add(key);const fill=new THREE.DirectionalLight(0xe1ebf4,1);fill.position.set(3,0,3);scene.add(fill);
 const geometry=new THREE.SphereGeometry(.98,80,56),result={};
 try{
  renderer.setRenderTarget(target);renderer.setViewport(0,0,size,size);renderer.setScissorTest(false);renderer.setClearColor(0x000000,0);renderer.autoClear=true;
  for(const [name,height]of Object.entries(sources)){
   const [color,roughness,metalness,depth]=appearances[name]??appearances.Stone,base=new THREE.Color(color).convertLinearToSRGB().toArray(),mortarColor=[.63,.62,.59],flat=document.createElement('canvas');flat.width=height.width;flat.height=height.height;
   const context=flat.getContext('2d'),image=height.getContext('2d').getImageData(0,0,height.width,height.height);
   for(let i=0;i<image.data.length;i+=4){const h=image.data[i]/255,shade=.68+.4*h,mortar=name==='Bricks'&&h<.25;for(let channel=0;channel<3;channel++)image.data[i+channel]=Math.round(255*(mortar?mortarColor[channel]:Math.min(1,base[channel]*shade)));}
   context.putImageData(image,0,0);result[name]={flat:flat.toDataURL('image/png')};
   const map=new THREE.CanvasTexture(flat),bump=new THREE.CanvasTexture(height);map.colorSpace=THREE.SRGBColorSpace;map.wrapS=map.wrapT=bump.wrapS=bump.wrapT=THREE.RepeatWrapping;
   const material=new THREE.MeshStandardMaterial({map,bumpMap:bump,bumpScale:depth,displacementMap:bump,displacementScale:depth,displacementBias:-depth/2,roughness,metalness}),mesh=new THREE.Mesh(geometry,material);mesh.rotation.y=.4;scene.add(mesh);
   try{renderer.render(scene,camera);const pixels=new Uint8Array(size*size*4);renderer.readRenderTargetPixels(target,0,0,size,size,pixels);const canvas=document.createElement('canvas');canvas.width=canvas.height=size;const ctx=canvas.getContext('2d'),image=ctx.createImageData(size,size);for(let y=0;y<size;y++)image.data.set(pixels.subarray((size-y-1)*size*4,(size-y)*size*4),y*size*4);ctx.putImageData(image,0,0);result[name].sphere=canvas.toDataURL('image/png');}
   finally{scene.remove(mesh);material.dispose();map.dispose();bump.dispose();}
  }
  return result;
 }finally{geometry.dispose();target.dispose();renderer.setRenderTarget(prior.target);renderer.setViewport(prior.viewport);renderer.setScissor(prior.scissor);renderer.setScissorTest(prior.scissorTest);renderer.setClearColor(prior.color,prior.alpha);renderer.autoClear=prior.autoClear;}
}
