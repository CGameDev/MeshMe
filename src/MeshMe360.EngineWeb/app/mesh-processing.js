// SPDX-License-Identifier: AGPL-3.0-only
/** Adapts preserved processing algorithms to scene transforms and material groups. */
// Orchestration only. Subdivision and QEM are the pinned, unchanged engine.
import * as THREE from '../vendor/three-0.170.0/build/three.module.js';
import {subdivide} from '../js/subdivision.js';
import {decimate} from '../js/decimation.js';
import {geometryFromObject} from './scene-document.js';
import {remapMaterialGroups} from './combine-geometry.js';
import {countEdgeDefects,countAreaSlivers} from '../js/meshRepair.js';
const limit=3000000;
const faceKey=(p,i)=>[0,1,2].map(v=>Array.from(p.subarray(i+v*3,i+v*3+3)).join(',')).sort().join(';');
function boundaryLocks(p){
 const points=Array.from({length:p.length/3},(_,i)=>Array.from(p.subarray(i*3,i*3+3)).join(',')),edges=new Map();
 for(let i=0;i<points.length;i+=3)for(let e=0;e<3;e++){const a=points[i+e],b=points[i+(e+1)%3],key=[a,b].sort().join(';'),edge=edges.get(key);if(edge)edge.count++;else edges.set(key,{a,b,count:1});}
 const boundary=new Set();for(const edge of edges.values())if(edge.count===1){boundary.add(edge.a);boundary.add(edge.b);}
 return Uint8Array.from({length:p.length/9},(_,i)=>[0,1,2].some(v=>boundary.has(points[i*3+v]))?1:0);
}
function stats(g){let soup=g.index?g.toNonIndexed():g;const value={...countEdgeDefects(soup),degenerate:countAreaSlivers(soup)};if(soup!==g)soup.dispose();g.computeBoundingBox();return{...value,bounds:{min:g.boundingBox.min.toArray(),max:g.boundingBox.max.toArray()}};}
/** Process a geometry copy and preserve material and mask correspondence where supported. */
export async function processMesh(object,options,onProgress=()=>{}){
 let g=geometryFromObject(object,{world:options.kind!=='normals'}),output=null;const count=(g.index?.count??g.attributes.position.count)/3;
 try{
  if(count>limit)throw new Error('Select geometry within the three-million-triangle processing budget.');
  const before=stats(g),result={groups:object.groups,engineMask:object.engineMask??null};
  if(options.kind==='normals'){
   g.computeVertexNormals();return{positions:object.positions.slice(),indices:object.indices?.slice()??null,normals:g.attributes.normal.array.slice(),...result,before,after:stats(g),warning:'Normals follow existing triangle winding. This does not repair inward or inconsistent winding; use Repair for that.'};
  }
  if(g.index){const soup=g.toNonIndexed();g.dispose();g=soup;}
  if(options.kind==='refine'){
   if(!Number.isFinite(options.edgeLength)||options.edgeLength<=0)throw new Error('Enter a positive maximum edge length in millimetres.');
   const refined=await subdivide(g,options.edgeLength,(p,tris)=>{if(tris>limit)throw new Error('Refinement exceeded the three-million-triangle budget. Use a larger edge length.');onProgress(p);});output=refined.geometry;
   if(refined.safetyCapHit)throw new Error('The upstream refinement safety cap was reached. Use a larger edge length.');
   result.groups=remapMaterialGroups(object.groups,refined.faceParentId);
   if(object.engineMask){const original=new Set(object.engineMask.excluded),excluded=[];refined.faceParentId.forEach((parent,i)=>{if(original.has(parent))excluded.push(i);});result.engineMask={...object.engineMask,excluded};}
  }else if(options.kind==='simplify'){
   if(!Number.isInteger(options.target)||options.target<4||options.target>limit)throw new Error('Choose a target from 4 to 3,000,000 triangles.');
   const parts=object.groups??[{start:0,count:g.attributes.position.count,materialIndex:0}],positions=[],groups=[],excluded=[],painted=new Set(object.engineMask?.excluded??[]);let offset=0;
   for(const [partIndex,part]of parts.entries()){
    const p=g.attributes.position.array.slice(part.start*3,(part.start+part.count)*3),sub=new THREE.BufferGeometry();sub.setAttribute('position',new THREE.BufferAttribute(p,3));sub.computeVertexNormals();
    const locked=parts.length>1?boundaryLocks(p):new Uint8Array(part.count/3),keys=new Map();
    for(let i=0;i<locked.length;i++){const paintedFace=painted.has(part.start/3+i)?1:0;locked[i]||=paintedFace;if(painted.size){const key=faceKey(p,i*9);if(keys.has(key)&&keys.get(key)!==paintedFace)throw new Error('Coincident duplicate faces have ambiguous mask membership. Repair duplicates before simplifying.');keys.set(key,paintedFace);}}
    let reduced;try{reduced=await decimate(sub,Math.max(4,Math.floor(options.target*part.count/g.attributes.position.count)),p=>onProgress((partIndex+p)/parts.length),true,.005,painted.size||parts.length>1?locked:null);
     const bytes=reduced.attributes.position.array;positions.push(bytes.slice());groups.push({start:offset,count:bytes.length/3,materialIndex:part.materialIndex});if(painted.size)for(let i=0;i<bytes.length/9;i++)if(keys.get(faceKey(bytes,i*9))===1)excluded.push(offset/3+i);offset+=bytes.length/3;
    }finally{reduced?.dispose();sub.dispose();}
   }
   const p=new Float32Array(offset*3);let start=0;for(const part of positions){p.set(part,start);start+=part.length;}output=new THREE.BufferGeometry();output.setAttribute('position',new THREE.BufferAttribute(p,3));output.computeVertexNormals();if(object.groups)result.groups=groups;if(object.engineMask)result.engineMask={...object.engineMask,excluded};
   result.warning='Protected boundaries, creases and painted faces can keep the result above the requested target. Material groups are simplified separately with fixed boundary vertices. The upstream 0.005 mm flat-face harvest may reduce below the target.';
  }else throw new Error('Unknown mesh processing operation');
  if(output.attributes.position.count/3>limit)throw new Error('The result exceeds the processing budget.');
  const after=stats(output);output.applyMatrix4(new THREE.Matrix4().compose(new THREE.Vector3(...object.position),new THREE.Quaternion(...object.rotation),new THREE.Vector3(...object.scale)).invert());
  return{...result,positions:output.attributes.position.array.slice(),indices:null,normals:output.attributes.normal.array.slice(),before,after};
 }finally{if(output&&output!==g)output.dispose();g.dispose();}
}
