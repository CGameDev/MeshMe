// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from '../../src/MeshMe360.EngineWeb/vendor/three-0.170.0/build/three.module.js';
import {SceneDocument,serializeProject,deserializeProject,geometryFromObject} from '../../src/MeshMe360.EngineWeb/app/scene-document.js';
import {generateHeight} from '../../src/MeshMe360.EngineWeb/app/procedural.js';
import {CommandRegistry,toolbar} from '../../src/MeshMe360.EngineWeb/app/locked-controls.js';
import {combineGeometry,remapMaterialGroups} from '../../src/MeshMe360.EngineWeb/app/combine-geometry.js';
import {pointMeasurement,formatMeasurement,estimateWall} from '../../src/MeshMe360.EngineWeb/app/measurement.js';
import {patternLayout,composePattern} from '../../src/MeshMe360.EngineWeb/app/pattern.js';
import {composeSource} from '../../src/MeshMe360.EngineWeb/app/source-composition.js';

test('Ctrl selection toggles membership and preserves first-picked Boolean operand order',()=>{
 const d=new SceneDocument(),a=d.add(new THREE.BoxGeometry(10,10,10),'First imported'),b=d.add(new THREE.BoxGeometry(10,10,10),'Second imported');
 const revision=d.revision;d.select(d.selectionForClick(b.id));d.select(d.selectionForClick(a.id,true));assert.deepEqual(d.selected.map(o=>o.id),[b.id,a.id]);
 d.select(d.selectionForClick(b.id,true));assert.deepEqual(d.selected.map(o=>o.id),[a.id]);d.select(d.selectionForClick(b.id,true));assert.deepEqual(d.selected.map(o=>o.id),[a.id,b.id]);assert.equal(d.revision,revision);
 const reopened=deserializeProject(serializeProject(d.state));d.state=reopened;assert.deepEqual(d.selected.map(o=>o.id),[a.id,b.id]);
});

test('Source influence, inversion and blend modes compose real height bytes without mutating sources',()=>{
 const asset=v=>({mime:'application/x-meshme-rgba8',width:1,height:1,bytes:new Uint8Array([v,v,v,255])}),source=asset(64),base=asset(128),before=structuredClone([source,base]);
 for(const [blend,expected]of [['normal',64],['multiply',32],['add',192],['subtract',64],['min',64]])assert.equal(composeSource(source,base,{blend}).bytes[0],expected);
 assert.equal(composeSource(source,base,{strength:.5}).bytes[0],96);assert.equal(composeSource(source,base,{strength:0}).bytes[0],128);assert.equal(composeSource(source,null,{strength:0}).bytes[0],0);assert.equal(composeSource(source,base,{invert:true}).bytes[0],191);
 assert.deepEqual([source,base],before);assert.throws(()=>composeSource(source,null,{blend:'multiply'}),/blend base/);assert.throws(()=>composeSource(source,base,{strength:2}),/Invalid/);
});

test('Pattern count, physical spacing, rotation and repeated source heights are deterministic',()=>{
 const options={kind:'linear',count:3,rows:2,spacing:6,width:4,height:2,orientation:0};
 const linear=patternLayout(options);assert.deepEqual(linear.stamps.map(p=>p.x),[-6,0,6]);assert.equal(linear.stamps.length,3);
 const grid=patternLayout({...options,kind:'grid'});assert.equal(grid.stamps.length,6);assert.equal(grid.stamps[3].y-grid.stamps[0].y,6);
 const radial=patternLayout({...options,kind:'radial',count:4});assert.equal(radial.stamps.length,4);assert.ok(Math.abs(radial.stamps[0].x-24/(2*Math.PI))<1e-12);assert.equal(radial.stamps[1].angle,Math.PI/2);
 const turned=patternLayout({...options,orientation:90});assert.ok(Math.abs(turned.stamps[0].x)<1e-12);assert.equal(turned.stamps[0].y,-6);
 const source={width:2,height:2,data:new Uint8Array([255,0,0,255,80,255,255,255,0,0,0,255,180,0,0,255])};
 for(const kind of ['linear','grid','radial']){const a=composePattern(source,{...options,kind},128),b=composePattern(source,{...options,kind},128);assert.deepEqual(a.imageData.data,b.imageData.data);const heights=new Set(a.imageData.data.filter((_,i)=>i%4===0));assert.deepEqual(heights,new Set([0,80,180,255]));}
 assert.throws(()=>patternLayout({...options,count:65}),/counts/);assert.throws(()=>patternLayout({...options,kind:'grid',count:9,rows:9}),/64 details/);assert.throws(()=>patternLayout({...options,spacing:0}),/positive/);
});

test('Measurements use canonical distances, angles and opposite-surface rays',()=>{
 assert.equal(pointMeasurement('distance',[[0,0,0],[3,4,0]]).valueMm,5);
 assert.equal(pointMeasurement('angle',[[1,0,0],[0,0,0],[0,1,0]]).valueDegrees,90);
 assert.throws(()=>pointMeasurement('angle',[[0,0,0],[0,0,0],[0,1,0]]),/distinct/);
 assert.equal(formatMeasurement({kind:'distance',valueMm:25.4},'in'),'1.000 in');
 const mesh=new THREE.Mesh(new THREE.BoxGeometry(20,20,20),new THREE.MeshBasicMaterial({side:THREE.DoubleSide}));mesh.updateMatrixWorld(true);
 const hit=new THREE.Raycaster(new THREE.Vector3(0,0,30),new THREE.Vector3(0,0,-1)).intersectObject(mesh)[0];assert.ok(Math.abs(estimateWall(mesh,hit).valueMm-20)<1e-6);
 const plane=new THREE.Mesh(new THREE.PlaneGeometry(20,20),new THREE.MeshBasicMaterial({side:THREE.DoubleSide}));plane.updateMatrixWorld(true);const flatHit=new THREE.Raycaster(new THREE.Vector3(0,0,30),new THREE.Vector3(0,0,-1)).intersectObject(plane)[0];assert.throws(()=>estimateWall(plane,flatHit),/opposite surface/);
});

test('Combine preserves world geometry, material groups and exact optional welding',()=>{
 const doc=new SceneDocument();doc.add(new THREE.BoxGeometry(10,10,10),'First');doc.add(new THREE.BoxGeometry(10,10,10),'Second');doc.transform({position:[20,0,0]});doc.setMaterial({color:'#ff4400',metallic:.5,roughness:.4,opacity:1});
 const inputs=doc.state.objects.map(o=>{const g=geometryFromObject(o,{world:true});return{positions:g.attributes.position.array,indices:g.index.array,material:o.material};});
 const combined=combineGeometry(inputs);assert.equal(combined.indices.length,72);assert.equal(combined.groups.length,2);assert.equal(combined.materials[1].color,'#ff4400');assert.equal(Math.max(...combined.positions.filter((_,i)=>i%3===0)),25);
 const welded=combineGeometry(inputs,{weld:true});assert.equal(welded.positions.length,16*3);assert.equal(welded.indices.length,72);assert.deepEqual(welded.groups,combined.groups);
 const object={...structuredClone(doc.state.objects[0]),positions:combined.positions,indices:combined.indices,groups:combined.groups,materials:combined.materials};doc.transact('Combine',s=>{s.objects=[object];s.selection=[object.id];});
 const reopened=deserializeProject(serializeProject(doc.state));assert.deepEqual(reopened.objects[0].groups,combined.groups);assert.deepEqual(reopened.objects[0].materials,combined.materials);assert.equal(geometryFromObject(reopened.objects[0]).groups.length,2);doc.undo();assert.equal(doc.state.objects.length,2);
 const invalid=structuredClone(reopened);invalid.objects[0].groups[1].start=1;assert.throws(()=>serializeProject(invalid),/group/);
});

test('Subdivision parent metadata preserves combined material membership',()=>{
 const groups=[{start:0,count:6,materialIndex:0},{start:6,count:3,materialIndex:1}];
 assert.deepEqual(remapMaterialGroups(groups,new Uint32Array([0,0,2,2,1])),[{start:0,count:6,materialIndex:0},{start:6,count:6,materialIndex:1},{start:12,count:3,materialIndex:0}]);
 assert.throws(()=>remapMaterialGroups(groups,new Uint32Array([3])),/parent/);
});
test('mm360 multi-object/texture/material/measurement round trip and dirty undo',()=>{
 const doc=new SceneDocument();const box=new THREE.BoxGeometry(20,20,20);doc.add(box,'Cube');doc.add(box,'Second');doc.state.textures.push({name:'custom',mime:'image/png',bytes:new Uint8Array([1,2,3])});doc.state.measurements.push({id:'distance',name:'Distance',points:[[0,0,0],[3,4,0]],valueMm:5});doc.transform({position:[30,10,5],scale:[2,1,1]});
 assert.equal(doc.dirty,true);const reopened=deserializeProject(serializeProject(doc.state));assert.equal(reopened.objects.length,2);assert.deepEqual(reopened.objects[1].position,[30,10,5]);assert.deepEqual(reopened.textures[0].bytes,new Uint8Array([1,2,3]));assert.equal(reopened.measurements[0].valueMm,5);assert.deepEqual(reopened.objects[1].material,doc.state.objects[1].material);doc.markSaved();assert.equal(doc.dirty,false);doc.undo();assert.equal(doc.dirty,true);doc.redo();assert.equal(doc.dirty,false);
});
test('invalid saved measurements are rejected before project display',()=>{
 const doc=new SceneDocument();const valid={id:'distance',name:'Distance',...pointMeasurement('distance',[[0,0,0],[3,4,0]])};
 for(const invalid of [{...valid,valueMm:NaN},{...valid,points:[[0,0,0]]},{...valid,kind:'unknown'},{...valid,name:''}]){doc.state.measurements=[invalid];assert.throws(()=>serializeProject(doc.state),/measurement/);}
 doc.state.measurements=[valid,valid];assert.throws(()=>serializeProject(doc.state),/measurement/);
});
test('transforms and centering preserve geometry and have undo',()=>{const d=new SceneDocument();d.add(new THREE.BoxGeometry(20,10,6),'box');const original=d.selected[0].positions.slice();d.transform({position:[12,30,42],rotation:new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0,0,1),Math.PI/2).toArray(),scale:[2,1,1]});d.center();const g=geometryFromObject(d.selected[0],{world:true});g.computeBoundingBox();const b=g.boundingBox;assert.ok(Math.abs(b.min.z)<1e-6);assert.ok(Math.abs(b.min.x+b.max.x)<1e-6);assert.ok(Math.abs(b.min.y+b.max.y)<1e-6);assert.deepEqual(d.selected[0].positions,original);assert.ok(d.undo());assert.deepEqual(d.selected[0].position,[12,30,42]);});
test('failed transform rolls back and malformed project is rejected',()=>{const d=new SceneDocument();d.add(new THREE.BoxGeometry(),'box');assert.throws(()=>d.transform({scale:[0,1,1]}),/transform/);assert.deepEqual(d.selected[0].scale,[1,1,1]);assert.throws(()=>deserializeProject(new Uint8Array([1,2,3])));});
test('procedural sources deterministic and inversion byte-exact',()=>{for(const kind of ['noise','ridges','cells','waves','checker','brick','scales','stone','woodgrain','plates']){const a=generateHeight({kind,seed:9,size:32}),b=generateHeight({kind,seed:9,size:32}),inv=generateHeight({kind,seed:9,size:32,invert:true});assert.deepEqual(a,b);assert.ok(a.every((x,i)=>Math.abs(255-x-inv[i])<=1));assert.ok(new Set(a).size>1);}assert.throws(()=>generateHeight({frequency:Infinity}));});
test('locked order is exact and missing command cannot pass its registry gate',()=>{assert.equal(toolbar.join('|'),'Select|Move|Rotate|Scale|Texture|Emboss|Deboss|Pattern|Repair|Hollow|Measure|Boolean|Split|Combine|STL|3MF|OBJ|Import');const r=new CommandRegistry();assert.equal(r.state('Boolean').implemented,false);assert.throws(()=>r.execute('Boolean'));r.register('Boolean',()=>true,()=>false,'Select two closed meshes');assert.deepEqual(r.state('Boolean'),{implemented:true,enabled:false,reason:'Select two closed meshes'});});
test('material transactions validate, undo and survive project serialization',()=>{const d=new SceneDocument();d.add(new THREE.BoxGeometry(),'box');const old=structuredClone(d.selected[0].material),m={color:'#123456',metallic:.6,roughness:.3,opacity:.5};d.setMaterial(m);assert.deepEqual(deserializeProject(serializeProject(d.state)).objects[0].material,m);d.undo();assert.deepEqual(d.selected[0].material,old);assert.throws(()=>d.setMaterial({...m,opacity:2}),/material/);assert.deepEqual(d.selected[0].material,old);});
test('busy registry prevents conflicting commands but permits navigation',()=>{const r=new CommandRegistry();r.register('Import',()=>true);r.register('Orbit',()=>true);r.busy=true;assert.throws(()=>r.execute('Import'),/Finish or cancel/);assert.equal(r.execute('Orbit'),true);r.busy=false;assert.equal(r.execute('Import'),true);});
test('procedural blur is deterministic, preserves mean and accepts periodic small images',()=>{const p={kind:'noise',seed:33,size:32};const raw=generateHeight(p),smooth=generateHeight({...p,blur:4});assert.deepEqual(smooth,generateHeight({...p,blur:4}));const mean=a=>a.reduce((s,v)=>s+v,0)/a.length;assert.ok(Math.abs(mean(raw)-mean(smooth))<.51);const variance=a=>a.reduce((s,v)=>s+(v-mean(a))**2,0)/a.length;assert.ok(variance(smooth)<variance(raw));assert.ok(generateHeight({...p,size:8,blur:32}).every(Number.isFinite));assert.throws(()=>generateHeight({...p,seed:NaN}),/seed/);});
