import createManifold from './manifold.js';
import {runSolidOperation} from '../app/solid-operations.js';
const checks=[];
function assert(ok,message){if(!ok)throw new Error(message);checks.push(message);}
try{
 const module=await createManifold({locateFile:name=>new URL(name,import.meta.url).href});module.setup();
 function cube(x=0){const a=module.Manifold.cube([10,10,10]),b=a.translate([x,0,0]);try{const mesh=b.getMesh();return{positions:new Float32Array(mesh.vertProperties),indices:new Uint32Array(mesh.triVerts)};}finally{a.delete();b.delete();}}
 const a=cube(),b=cube(5),before=a.positions.slice();
 const results={};
 for(const [operation,volume] of [['union',1500],['subtract',500],['intersect',500]]){
  const result=runSolidOperation(module,{operation,objects:[a,b]}).results[0];
  assert(Math.abs(result.diagnostics.volume-volume)<1e-5,operation+' volume');
  assert(result.diagnostics.status==='NoError'&&result.indices.length%3===0,operation+' valid triangles/manifold');
  assert(result.diagnostics.triangles===(operation==='union'?28:12),operation+' triangle count');
  const bounds=result.diagnostics.bounds;
  assert(JSON.stringify(bounds.min)===JSON.stringify(operation==='intersect'?[5,0,0]:[0,0,0])&&JSON.stringify(bounds.max)===JSON.stringify(operation==='union'?[15,10,10]:operation==='subtract'?[5,10,10]:[10,10,10]),operation+' expected bounds');
  const repeat=runSolidOperation(module,{operation,objects:[a,b]}).results[0];
  assert(result.positions.every((v,i)=>v===repeat.positions[i])&&result.indices.every((v,i)=>v===repeat.indices[i]),operation+' deterministic geometry samples');
  const again=runSolidOperation(module,{operation:'validate',objects:[result]}).results[0];
  assert(Math.abs(again.diagnostics.volume-volume)<1e-5,operation+' mesh round trip');
  results[operation]=result.diagnostics;
 }
 assert(before.every((v,i)=>v===a.positions[i]),'inputs unchanged');
 const split=runSolidOperation(module,{operation:'split',objects:[a],normal:[1,0,0],offset:5});
 assert(split.results.length===2&&split.results.every(r=>Math.abs(r.diagnostics.volume-500)<1e-5),'split keeps two closed halves');
 const hollow=runSolidOperation(module,{operation:'hollow',objects:[a],thickness:1});
 assert(Math.abs(hollow.results[0].diagnostics.volume-488)<1e-3,'inward hollow cube volume');
 let rejected=false;try{runSolidOperation(module,{operation:'union',objects:[{...a,indices:a.indices.slice(3)},b]});}catch{rejected=true;}
 assert(rejected,'open input rejected');
 rejected=false;try{runSolidOperation(module,{operation:'hollow',objects:[a],thickness:20});}catch{rejected=true;}
 assert(rejected,'wall exceeding interior rejected');
 document.body.textContent='PASS: '+JSON.stringify({checks,results,hollow:hollow.results[0].diagnostics},null,2);
}catch(error){document.body.textContent='BLOCKED: '+error.stack;}
