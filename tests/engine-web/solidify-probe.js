import createManifold from './manifold.js';
import createSolidify from './solidify.js';
import {runSolidOperation} from '../app/solid-operations.js';
const checks=[];
function assert(ok,message){if(!ok)throw new Error(message);checks.push(message);}
try{
 const module=await createManifold({locateFile:name=>new URL(name,import.meta.url).href});module.setup();
 const backend=await createSolidify({locateFile:name=>new URL(name,import.meta.url).href});
 const cube=module.Manifold.cube([10,10,10]);const mesh=cube.getMesh();cube.delete();
 const positions=new Float32Array(mesh.vertProperties),indices=new Uint32Array(mesh.triVerts);
 const before=positions.slice();
 const reports={};
 for(const [name,faces]of [['closed',indices],['missing-triangle',indices.slice(3)]]){
  const output=backend.solidify(positions,faces,1);
  assert(!output.error,name+' reconstructs: '+(output.error??'valid'));
  const checked=runSolidOperation(module,{operation:'validate',objects:[output]}).results[0];
  assert(checked.diagnostics.status==='NoError',name+' watertight Manifold round-trip');
  assert(checked.diagnostics.volume>800&&checked.diagnostics.volume<=1001,name+' volume within documented coarse reconstruction envelope');
  assert(output.positions.every(Number.isFinite)&&output.indices.every(i=>i<output.positions.length/3),name+' finite geometry and valid indices');
  assert(checked.diagnostics.bounds.min.every(v=>v>=-1)&&checked.diagnostics.bounds.max.every(v=>v<=11),name+' bounds within one cell');
  const repeat=backend.solidify(positions,faces,1);
  assert(repeat.positions.length===output.positions.length&&repeat.indices.length===output.indices.length&&repeat.positions.every((v,i)=>Math.abs(v-output.positions[i])<=1e-6)&&repeat.indices.every((v,i)=>v===output.indices[i]),name+' repeatable topology and samples within 1e-6 mm');
  reports[name]=checked.diagnostics;
 }
 const coarse=backend.solidify(positions,indices,2),fine=backend.solidify(positions,indices,.5);
 assert(!coarse.error&&!fine.error&&fine.triangles>coarse.triangles,'adjustable reconstruction resolution');
 assert(backend.solidify(positions,indices,.00001).error?.includes('budget'),'excessive resolution rejected before reconstruction');
 const invalid=positions.slice();invalid[0]=NaN;
 assert(backend.solidify(invalid,indices,1).error?.includes('non-finite'),'non-finite input rejected');
 assert(backend.solidify(positions,new Uint32Array([0,0,0]),1).error?.includes('zero-area'),'degenerate face rejected');
 assert(backend.solidify(new Float32Array([0,0,0,10,0,0,0,10,0]),new Uint32Array([0,1,2]),1).error?.includes('No enclosed solid'),'single open sheet is inapplicable');
 assert(positions.every((v,i)=>v===before[i]),'source geometry unchanged');
 document.body.textContent='PASS: '+JSON.stringify({checks,reports},null,2);
}catch(error){document.body.textContent='BLOCKED: '+error.stack;}
