// SPDX-License-Identifier: AGPL-3.0-only
import createManifold from '../vendor/manifold-3.5.1-csp/manifold.js';
import {runSolidOperation} from './solid-operations.js';
const ready=createManifold({locateFile:name=>new URL('../vendor/manifold-3.5.1-csp/'+name,import.meta.url).href}).then(module=>{module.setup();return module;});
self.onmessage=async({data})=>{
 try{
  const module=await ready;
  let result;
  if(data.request.operation==='solidify'){
   if(data.request.objects.length!==1)throw new Error('Select one mesh for Make Solid.');
   const {default:createSolidify}=await import('../vendor/meshme-solidify-1/solidify.js');
   const backend=await createSolidify({locateFile:name=>new URL('../vendor/meshme-solidify-1/'+name,import.meta.url).href});
   const input=data.request.objects[0];
   if(!(input.positions instanceof Float32Array)||input.positions.length%3)throw new Error('Invalid mesh positions.');
   const indices=input.indices??Uint32Array.from({length:input.positions.length/3},(_,i)=>i);
   const output=backend.solidify(input.positions,indices,data.request.cellSize);
   if(output.error)throw new Error(output.error);
   result=runSolidOperation(module,{operation:'validate',objects:[output]});
   result.warning='Approximate reconstruction: small detail and openings below the cell size may change. Review before committing.';
  }else result=runSolidOperation(module,data.request);
  self.postMessage({id:data.id,result},result.results.flatMap(r=>[r.positions.buffer,r.indices.buffer]));
 }catch(error){self.postMessage({id:data.id,error:String(error.message??error)});}
};
