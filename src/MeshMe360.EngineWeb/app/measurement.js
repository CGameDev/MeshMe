// SPDX-License-Identifier: AGPL-3.0-only
/** Calculates canonical millimetre measurements and validates saved records. */
import * as THREE from '../vendor/three-0.170.0/build/three.module.js';
/** Reject unsupported measurement kinds, invalid points and non-finite saved values. */
export function validateMeasurements(items){
 const vector=p=>Array.isArray(p)&&p.length===3&&p.every(Number.isFinite);
 if(!Array.isArray(items)||new Set(items.map(m=>m?.id)).size!==items.length)throw new Error('Invalid measurement identities');
 for(const m of items){
  const kind=m?.kind??'distance';
  if(!m||typeof m.id!=='string'||!m.id||typeof m.name!=='string'||!m.name.trim())throw new Error('Invalid measurement identity');
  if(kind==='bounds'){
   if(!vector(m.dimensionsMm)||m.dimensionsMm.some(v=>v<0)||!vector(m.bounds?.min)||!vector(m.bounds?.max)||m.bounds.min.some((v,i)=>v>m.bounds.max[i]))throw new Error('Invalid measurement bounds');
  }else{
   if(!['distance','angle','thickness'].includes(kind)||!Array.isArray(m.points)||m.points.length!==(kind==='angle'?3:2)||!m.points.every(vector))throw new Error('Invalid measurement points');
   const value=kind==='angle'?m.valueDegrees:m.valueMm;if(!Number.isFinite(value)||value<0||kind==='angle'&&value>180)throw new Error('Invalid measurement value');
  }
  if(m.objectIds!==undefined&&(!Array.isArray(m.objectIds)||!m.objectIds.every(id=>typeof id==='string')))throw new Error('Invalid measurement references');
 }
 return items;
}
/** Calculate distance or angle from the supplied canonical points. */
export function pointMeasurement(kind,points){
 if(!Array.isArray(points)||!points.every(p=>Array.isArray(p)&&p.length===3&&p.every(Number.isFinite)))throw new Error('Pick finite points on the model.');
 const v=points.map(p=>new THREE.Vector3(...p));
 if(kind==='distance'&&v.length===2)return{kind,points:structuredClone(points),valueMm:v[0].distanceTo(v[1])};
 if(kind==='angle'&&v.length===3){const a=v[0].clone().sub(v[1]),b=v[2].clone().sub(v[1]);if(a.lengthSq()<1e-16||b.lengthSq()<1e-16)throw new Error('The angle needs three distinct points, with the vertex picked second.');return{kind,points:structuredClone(points),valueDegrees:THREE.MathUtils.radToDeg(a.angleTo(b))};}
 throw new Error('Pick '+(kind==='angle'?3:2)+' points.');
}
/** Format a saved measurement in display units without rescaling its stored value. */
export function formatMeasurement(item,units='mm'){
 if(item.kind==='angle')return item.valueDegrees.toFixed(2)+'°';
 const factor=units==='in'?25.4:1,suffix=units==='in'?'in':'mm';
 if(item.kind==='bounds')return item.dimensionsMm.map(v=>(v/factor).toFixed(3)).join(' × ')+' '+suffix;
 return (item.valueMm/factor).toFixed(3)+' '+suffix;
}
// Caller must qualify a closed, consistently oriented mesh before this estimate.
/** Sample the opposite surface along a ray; this is not a global minimum-wall proof. */
export function estimateWall(mesh,hit){
 const normal=hit.face.normal.clone().applyMatrix3(new THREE.Matrix3().getNormalMatrix(mesh.matrixWorld)).normalize();
 mesh.geometry.computeBoundingBox();const diagonal=mesh.geometry.boundingBox.getSize(new THREE.Vector3()).length(),epsilon=Math.max(1e-6,diagonal*1e-7);
 const origin=hit.point.clone().addScaledVector(normal,-epsilon),ray=new THREE.Raycaster(origin,normal.clone().negate(),epsilon,Infinity);
 const next=ray.intersectObject(mesh).find(h=>h.point.distanceTo(hit.point)>epsilon*2);
 if(!next)throw new Error('No opposite surface was found along the inward normal.');
 const opposite=next.face.normal.clone().applyMatrix3(new THREE.Matrix3().getNormalMatrix(mesh.matrixWorld)).normalize();
 if(opposite.dot(normal)>-.1)throw new Error('The next surface is not sufficiently opposite for a valid wall estimate.');
 return{kind:'thickness',points:[hit.point.toArray(),next.point.toArray()],valueMm:hit.point.distanceTo(next.point),method:'Inward normal ray; local estimate, not minimum-wall certification'};
}
