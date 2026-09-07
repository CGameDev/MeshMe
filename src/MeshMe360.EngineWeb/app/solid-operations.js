// SPDX-License-Identifier: AGPL-3.0-only
/** Adapts licensed solid backends to scene operation requests. */
// Data/ownership adapter only. All solid geometry operations are Manifold's.
/** Dispatch the requested operation to the supplied licensed backend module. */
export function runSolidOperation(module, request) {
 const owned=[];
 const own=value=>{owned.push(value);return value;};
 function check(solid,allowEmpty=false){
  const status=solid.status();
  if(status!=='NoError')throw new Error('Manifold rejected geometry: '+status);
  if(!allowEmpty&&solid.isEmpty())throw new Error('The operation produced no solid. Originals are unchanged.');
  return solid;
 }
 function input(object){
  const positions=object.positions,indices=object.indices;
  if(!(positions instanceof Float32Array)||!positions.length||positions.length%3||!positions.every(Number.isFinite))throw new Error('Invalid input vertices');
  if(indices&&(!(indices instanceof Uint32Array)||indices.length%3||!indices.every(i=>i<positions.length/3)))throw new Error('Invalid input triangles');
  if(!indices&&positions.length%9)throw new Error('Incomplete input triangles');
  const mesh=new module.Mesh({numProp:3,vertProperties:positions,triVerts:indices??Uint32Array.from({length:positions.length/3},(_,i)=>i)});
  // The documented upstream seam merger is necessary for STL triangle soup.
  // No application tolerance inflation, hole filling or topology substitute.
  mesh.merge();
  const solid=check(own(new module.Manifold(mesh)));
  if(solid.volume()<=0)throw new Error('Input must enclose a positive volume with consistent outward winding');
  return solid;
 }
 function output(solid){
  check(solid);const mesh=solid.getMesh(),positions=new Float32Array(mesh.numVert*3);
  for(let i=0;i<mesh.numVert;i++)positions.set(mesh.position(i),i*3);
  const indices=new Uint32Array(mesh.triVerts);
  if(!positions.every(Number.isFinite)||!indices.every(i=>i<positions.length/3))throw new Error('Invalid backend result');
  return {positions,indices,diagnostics:{status:solid.status(),volume:solid.volume(),bounds:solid.boundingBox(),vertices:positions.length/3,triangles:indices.length/3}};
 }
 try {
  if(!Array.isArray(request.objects)||!request.objects.length)throw new Error('Select input objects');
  const solids=request.objects.map(input);
  if(request.operation==='validate')return{results:solids.map(output)};
  if(['union','subtract','intersect'].includes(request.operation)){
   if(solids.length<2)throw new Error('Select at least two closed meshes');
   let result=solids[0];
   for(const solid of solids.slice(1))result=own(request.operation==='union'?result.add(solid):request.operation==='subtract'?result.subtract(solid):result.intersect(solid));
   return {results:[output(result)]};
  }
  if(solids.length!==1)throw new Error('Select exactly one closed mesh');
  if(request.operation==='split'){
   const normal=request.normal,offset=request.offset;
   if(!Array.isArray(normal)||normal.length!==3||!normal.every(Number.isFinite)||Math.hypot(...normal)===0||!Number.isFinite(offset))throw new Error('Enter a finite, nonzero split plane');
   const halves=solids[0].splitByPlane(normal,offset).map(own);
   return{results:halves.map(output)};
  }
  if(request.operation==='hollow'){
   const thickness=request.thickness;
   if(!Number.isFinite(thickness)||thickness<=0)throw new Error('Wall thickness must be greater than zero');
   // Upstream morphological erosion with a discretized spherical kernel.
   // This is an approximate offset; minimum-wall qualification is separate.
   const segments=32;
   const kernel=own(module.Manifold.sphere(thickness,segments));
   const inner=check(own(solids[0].minkowskiDifference(kernel)));
   const result=own(solids[0].subtract(inner));
   return{results:[output(result)],warning:'Closed internal cavity. Add a drain opening when required by your printing process. Offset uses a 32-segment sphere approximation.',thickness};
  }
  throw new Error('Unsupported solid operation');
 }finally{for(const solid of owned.reverse())solid.delete();}
}
