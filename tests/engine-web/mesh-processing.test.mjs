// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from '../../artifacts/web/vendor/three-0.170.0/build/three.module.js';
import {SceneDocument,geometryFromObject,serializeProject,deserializeProject} from '../../artifacts/web/app/scene-document.js';
import {processMesh} from '../../artifacts/web/app/mesh-processing.js';
import {buildSceneExport} from '../../artifacts/web/app/export-core.js';
test('Pinned refinement/decimation preserve world bounds, groups, painted faces and source inputs',async()=>{
 const d=new SceneDocument(),g=new THREE.BoxGeometry(20,20,20,8,8,8),o=d.add(g,'Grouped cube');o.groups=structuredClone(g.groups);o.materials=g.groups.map(()=>structuredClone(o.material));o.engineMask={selectionMode:false,excluded:[0,1]};const before=structuredClone(o);
 const refined=await processMesh(o,{kind:'refine',edgeLength:3});assert.ok(refined.after.tris>refined.before.tris);assert.deepEqual(refined.after.bounds,refined.before.bounds);assert.equal(refined.after.open,0);assert.equal(refined.after.nonManifold,0);assert.equal(refined.groups.length,6);assert.ok(refined.engineMask.excluded.length>2);assert.deepEqual(o,before);
 const reduced=await processMesh(o,{kind:'simplify',target:24});assert.ok(reduced.after.tris<reduced.before.tris);assert.deepEqual(reduced.after.bounds,reduced.before.bounds);assert.equal(reduced.after.open,0);assert.equal(reduced.after.nonManifold,0);assert.equal(reduced.engineMask.excluded.length,2);assert.equal(reduced.groups.reduce((n,g)=>n+g.count,0),reduced.positions.length/3);assert.deepEqual(o,before);
 const next={...o,...refined};d.state.objects=[next];const reopened=deserializeProject(serializeProject(d.state));assert.deepEqual(reopened.objects[0].normals,refined.normals);assert.deepEqual(reopened.objects[0].engineMask,refined.engineMask);assert.ok(geometryFromObject(reopened.objects[0]).attributes.normal.array.every(Number.isFinite));
 const repeated=await processMesh(o,{kind:'refine',edgeLength:3});assert.deepEqual(repeated.positions,refined.positions);const stl=await buildSceneExport({objects:[{...o,...reduced}],format:'stl'});assert.equal(stl.bytes.length,84+50*reduced.after.tris);
});
test('Normal recalculation changes only normals and round-trips; unsafe requests reject',async()=>{
 const d=new SceneDocument(),o=d.add(new THREE.BoxGeometry(10,10,10),'Normals');o.normals.fill(0);const result=await processMesh(o,{kind:'normals'});assert.deepEqual(result.positions,o.positions);assert.deepEqual(result.indices,o.indices);assert.equal(result.normals[0]**2+result.normals[1]**2+result.normals[2]**2,1);assert.ok(o.normals.every(n=>n===0));
 await assert.rejects(processMesh(o,{kind:'refine',edgeLength:0}),/positive/);await assert.rejects(processMesh(o,{kind:'simplify',target:1}),/target/);const bad={...o,normals:new Float32Array(3)};assert.throws(()=>geometryFromObject(bad),/normals/);
});
