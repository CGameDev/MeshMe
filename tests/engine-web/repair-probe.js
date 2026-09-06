import createManifold from './manifold.js';
import createRepair from './repair.js';
import {runSolidOperation} from '../app/solid-operations.js';
const checks=[];
function assert(ok,message){if(!ok)throw new Error(message);checks.push(message);}
try{
 const module=await createManifold({locateFile:name=>new URL(name,import.meta.url).href});module.setup();
 const backend=await createRepair({locateFile:name=>new URL(name,import.meta.url).href});
 const cube=module.Manifold.cube([10,10,10]),mesh=cube.getMesh();cube.delete();
 const positions=new Float32Array(mesh.vertProperties),indices=new Uint32Array(mesh.triVerts);
 const soup=[];for(const i of indices)soup.push(...positions.slice(i*3,i*3+3));
 [soup[0],soup[3]]=[soup[3],soup[0]];[soup[1],soup[4]]=[soup[4],soup[1]];[soup[2],soup[5]]=[soup[5],soup[2]];
 soup.push(0,0,0,0,0,0,0,0,0,NaN,0,0,0,0,0,0,1,0);
 const damaged=new Float32Array(soup),faces=Uint32Array.from({length:damaged.length/3},(_,i)=>i);
 const result=backend.repair(damaged,faces,0);assert(!result.error,'damaged mesh repair: '+(result.error??'valid'));
 assert(result.report.invalidVerticesRemoved===1&&result.report.invalidTrianglesRemoved===1,'invalid vertex and incident triangle removed');
 assert(result.report.degenerateTrianglesRemoved===1&&result.report.duplicateVerticesMerged>0,'zero-area removal and exact duplicate weld');
 assert(result.report.after.inconsistentEdges===0&&result.report.trianglesReoriented>0,'inconsistent winding corrected');
 const checked=runSolidOperation(module,{operation:'validate',objects:[result]}).results[0];
 assert(checked.diagnostics.triangles===12&&Math.abs(checked.diagnostics.volume-1000)<1e-5,'repaired cube has original triangles, positive volume and manifold state');
 assert(JSON.stringify(checked.diagnostics.bounds)==='{"min":[0,0,0],"max":[10,10,10]}','repair preserves bounds');
 const opening=indices.slice(3),left=backend.repair(positions,opening,1);
 assert(!left.error&&left.report.holesFound===1&&left.report.holesFilled===0&&left.report.after.boundaryEdges===3,'opening larger than explicit threshold remains unchanged');
 const filled=backend.repair(positions,opening,15);assert(!filled.error&&filled.report.holesFilled===1&&filled.report.after.boundaryEdges===0,'bounded hole filled by PMP: '+JSON.stringify(filled.report));
 const filledCheck=runSolidOperation(module,{operation:'validate',objects:[filled]}).results[0];
 assert(Math.abs(filledCheck.diagnostics.volume-1000)<1e-4,'filled cube passes manifold and volume checks');
 const repeated=backend.repair(positions,opening,15);
 assert(repeated.positions.length===filled.positions.length&&repeated.indices.length===filled.indices.length&&repeated.positions.every((v,i)=>Math.abs(v-filled.positions[i])<1e-6)&&repeated.indices.every((v,i)=>v===filled.indices[i]),'hole repair repeatable topology and samples');
 assert(damaged.some(Number.isNaN)&&positions.length===24&&indices.length===36,'source arrays unchanged');
 const sheet=backend.repair(new Float32Array([0,0,0,10,0,0,0,10,0]),new Uint32Array([0,1,2]),20);
 assert(!sheet.error&&sheet.report.holesFilled===0&&sheet.report.after.boundaryEdges===3,'zero-volume closure rejected without destroying open sheet');
 const hollow=runSolidOperation(module,{operation:'hollow',objects:[{positions,indices}],thickness:1}).results[0];
 const cavity=backend.repair(hollow.positions,hollow.indices,0);
 assert(!cavity.error&&!cavity.report.closedShellReversed&&Math.abs(runSolidOperation(module,{operation:'validate',objects:[cavity]}).results[0].diagnostics.volume-488)<1e-4,'nested cavity orientation preserved');
 const duplicateFace=new Uint32Array([...indices,...indices.slice(0,3)]),ambiguous=backend.repair(positions,duplicateFace,15);
 assert(!ambiguous.error&&ambiguous.report.after.nonManifoldEdges===3&&ambiguous.report.holesFilled===0&&ambiguous.report.unresolved.some(s=>s.includes('Non-manifold')),'ambiguous non-manifold geometry retained and reported');
 document.body.textContent='PASS: '+JSON.stringify({checks,cleanup:result.report,filled:filled.report},null,2);
}catch(error){document.body.textContent='BLOCKED: '+error.stack;}
