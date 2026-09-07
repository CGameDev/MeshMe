// SPDX-License-Identifier: AGPL-3.0-only
/** Runs print-analysis requests outside the UI event loop. */
import createManifold from '../vendor/manifold-3.5.1-csp/manifold.js';
import {analyzePrintObject} from './print-analysis.js';
const ready=createManifold({locateFile:name=>new URL('../vendor/manifold-3.5.1-csp/'+name,import.meta.url).href}).then(m=>{m.setup();return m;});
self.onmessage=async({data})=>{
 try{
  const objects=data.objects;if(!Array.isArray(objects)||!objects.length||objects.reduce((n,o)=>n+(o.indices?.length??o.positions.length/3)/3,0)>3000000)throw new Error('Choose models within the three-million-triangle inspection budget.');
  const module=await ready,reports=[];for(const object of objects){self.postMessage({type:'progress',name:object.name});reports.push(analyzePrintObject(object,module));}
  self.postMessage({type:'done',reports});
 }catch(error){self.postMessage({type:'error',message:String(error.message??error)});}
};
