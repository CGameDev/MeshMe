// SPDX-License-Identifier: AGPL-3.0-only
import createManifold from '../vendor/manifold-3.5.1-csp/manifold.js';
import {runSolidOperation} from './solid-operations.js';
const ready=createManifold({locateFile:name=>new URL('../vendor/manifold-3.5.1-csp/'+name,import.meta.url).href}).then(module=>{module.setup();return module;});
self.onmessage=async({data})=>{
 try{
  const result=runSolidOperation(await ready,data.request);
  self.postMessage({id:data.id,result},result.results.flatMap(r=>[r.positions.buffer,r.indices.buffer]));
 }catch(error){self.postMessage({id:data.id,error:String(error.message??error)});}
};
