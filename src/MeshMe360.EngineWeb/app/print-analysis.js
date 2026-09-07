// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from '../vendor/three-0.170.0/build/three.module.js';
import {geometryFromObject} from './scene-document.js';
import {countEdgeDefects,countAreaSlivers} from '../js/meshRepair.js';
import {runSolidOperation} from './solid-operations.js';
import {estimateWall} from './measurement.js';

export function placementProposal(object){
 const g=geometryFromObject(object,{world:true});g.computeBoundingBox();const b=g.boundingBox,c=b.getCenter(new THREE.Vector3()),size=b.getSize(new THREE.Vector3());
 const result={id:object.id,name:object.name,dimensionsMm:size.toArray(),bounds:{min:b.min.toArray(),max:b.max.toArray()},translation:[-c.x,-c.y,-b.min.z]};g.dispose();return result;
}
export function analyzePrintObject(object,module){
 const proposal=placementProposal(object);let g=geometryFromObject(object,{world:true});if(g.index){const soup=g.toNonIndexed();g.dispose();g=soup;}
 const report={...proposal,...countEdgeDefects(g),degenerate:countAreaSlivers(g),solid:false,normals:'Unverified',wall:{sampled:0,failed:0,minimumSampleMm:null}};
 try{
  try{const result=runSolidOperation(module,{operation:'validate',objects:[{positions:g.attributes.position.array,indices:null}]});report.solid=true;report.volumeMm3=result.results[0].diagnostics.volume;report.normals='Consistent closed winding with positive volume';}
  catch(error){report.validationError=String(error.message??error);report.normals='Closed outward winding not validated: '+report.validationError;}
  if(report.solid&&report.degenerate===0){
   const mesh=new THREE.Mesh(g,new THREE.MeshBasicMaterial({side:THREE.DoubleSide}));mesh.updateMatrixWorld(true);const p=g.attributes.position,a=new THREE.Vector3(),b=new THREE.Vector3(),c=new THREE.Vector3();
   try{const samples=Math.min(64,report.tris);for(let i=0;i<samples;i++){
    const face=Math.floor(i*report.tris/samples),offset=face*3;a.fromBufferAttribute(p,offset);b.fromBufferAttribute(p,offset+1);c.fromBufferAttribute(p,offset+2);
    const normal=new THREE.Triangle(a,b,c).getNormal(new THREE.Vector3()),point=a.clone().add(b).add(c).multiplyScalar(1/3);
    try{const value=estimateWall(mesh,{point,face:{normal}}).valueMm;report.wall.sampled++;report.wall.minimumSampleMm=report.wall.minimumSampleMm===null?value:Math.min(report.wall.minimumSampleMm,value);}catch{report.wall.failed++;}
   }}finally{mesh.material.dispose();}
  }
  report.wall.note='At most 64 deterministic face-centre normal rays. The smallest successful sample is not the global minimum; thin areas between samples and self-intersections require review.';
  return report;
 }finally{g.dispose();}
}
