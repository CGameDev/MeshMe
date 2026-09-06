// SPDX-License-Identifier: AGPL-3.0-only
// Scene assembly, distinct from Boolean union. Inputs are already in world space.
export function combineGeometry(objects,{weld=false}={}){
 if(!Array.isArray(objects)||objects.length<2)throw new Error('Select at least two objects to combine.');
 const materials=[],groups=[],vertices=[],indices=[],coincident=new Map();
 let inputVertices=0;
 for(const object of objects){
  const p=object.positions,faces=object.indices??Uint32Array.from({length:p.length/3},(_,i)=>i);
  if(!(p instanceof Float32Array)||p.length%3||!p.every(Number.isFinite)||faces.length%3||!faces.every(i=>Number.isInteger(i)&&i>=0&&i<p.length/3))throw new Error('Invalid combine geometry.');
  inputVertices+=p.length/3;if(inputVertices>3000000||indices.length+faces.length>9000000)throw new Error('Combine exceeds the three-million vertex/triangle budget.');
  const map=new Uint32Array(p.length/3);
  for(let i=0;i<map.length;i++){
   const xyz=[p[i*3],p[i*3+1],p[i*3+2]],key=weld?xyz.join(','):null;
   let index=weld?coincident.get(key):undefined;
   if(index===undefined){index=vertices.length/3;vertices.push(...xyz);if(weld)coincident.set(key,index);}
   map[i]=index;
  }
  const start=indices.length,materialOffset=materials.length;
  for(const i of faces)indices.push(map[i]);
  materials.push(...structuredClone(object.materials??[object.material]));
  for(const g of object.groups?.length?object.groups:[{start:0,count:faces.length,materialIndex:0}])groups.push({start:start+g.start,count:g.count,materialIndex:materialOffset+g.materialIndex});
 }
 return{positions:new Float32Array(vertices),indices:new Uint32Array(indices),materials,groups,diagnostics:{vertices:vertices.length/3,triangles:indices.length/3,volume:null,status:'Combined geometry; intersections are not resolved',weldedVertices:inputVertices-vertices.length/3}};
}

export function remapMaterialGroups(groups,faceParents){
 if(!groups)return undefined;
 const oldCount=groups.at(-1).start+groups.at(-1).count,faceMaterials=new Uint32Array(oldCount/3);
 for(const g of groups)faceMaterials.fill(g.materialIndex,g.start/3,(g.start+g.count)/3);
 const output=[];
 for(let i=0;i<faceParents.length;i++){
  const parent=faceParents[i];if(!Number.isInteger(parent)||parent<0||parent>=faceMaterials.length)throw new Error('Relief material parent map is invalid.');
  const materialIndex=faceMaterials[parent],previous=output.at(-1);
  if(previous?.materialIndex===materialIndex)previous.count+=3;else output.push({start:i*3,count:3,materialIndex});
 }
 return output;
}
