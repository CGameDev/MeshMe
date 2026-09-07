// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import assert from 'node:assert/strict';
import * as THREE from '../../artifacts/web/vendor/three-0.170.0/build/three.module.js';
import {SceneDocument} from '../../artifacts/web/app/scene-document.js';
import {buildSceneExport} from '../../artifacts/web/app/export-core.js';
import {unzipSync,strFromU8} from '../../artifacts/web/vendor/fflate-0.8.2/esm/browser.js';
import {placementProposal} from '../../artifacts/web/app/print-analysis.js';

test('Print placement proposes world-space translation without changing scale or geometry',()=>{
 const doc=new SceneDocument();doc.add(new THREE.BoxGeometry(20,10,6),'Print');doc.transform({position:[30,20,40],scale:[2,1,1],rotation:new THREE.Quaternion().setFromAxisAngle(new THREE.Vector3(0,0,1),Math.PI/2).toArray()});
 const original=structuredClone(doc.selected[0]),proposal=placementProposal(doc.selected[0]);assert.deepEqual(proposal.dimensionsMm,[10,40,6]);assert.deepEqual(proposal.translation,[-30,-20,-37]);assert.deepEqual(doc.selected[0],original);
});

test('Scene exports preserve world triangles and expose real format capabilities',async()=>{
 const doc=new SceneDocument();doc.add(new THREE.BoxGeometry(20,20,20),'First');doc.add(new THREE.BoxGeometry(20,20,20),'Second\nusemtl injected');doc.transform({position:[30,0,0]});doc.setMaterial({color:'#ff4400',metallic:.5,roughness:.4,opacity:.7});
 const before=structuredClone(doc.state);
 for(const format of ['stl','3mf','obj']){
  const result=await buildSceneExport({objects:doc.state.objects,format,title:'Test <project>',profile:'Generic STL'});
  assert.equal(result.reports.reduce((n,r)=>n+r.tris,0),24);assert.ok(result.reports.every(r=>r.open===0&&r.nonManifold===0&&r.slivers===0));assert.deepEqual(result.reports[1].bounds,{min:[20,-10,-10],max:[40,10,10]});
  if(format==='stl'){assert.equal(new DataView(result.bytes.buffer).getUint32(80,true),24);assert.equal(result.bytes.length,84+24*50);assert.match(result.warning,/geometry only/);}
  else {const files=unzipSync(result.bytes);if(format==='3mf'){const xml=strFromU8(files['3D/3dmodel.model']);assert.equal((xml.match(/<triangle /g)||[]).length,24);assert.match(xml,/unit="millimeter"/);assert.match(xml,/Test &lt;project&gt;/);}else{const obj=strFromU8(files['model.obj']),mtl=strFromU8(files['materials.mtl']);assert.equal((obj.match(/^f /gm)||[]).length,24);assert.equal((obj.match(/^usemtl /gm)||[]).length,2);assert.match(mtl,/d 0.7/);assert.ok(files['meshme-metadata.json']);assert.equal(result.extension,'.obj.zip');}}
 }
 assert.deepEqual(doc.state,before);
});

test('Export rejects malformed geometry and preserves material-group face coverage',async()=>{
 const doc=new SceneDocument();doc.add(new THREE.BoxGeometry(20,20,20),'Grouped');const o=doc.selected[0];o.groups=[{start:0,count:18,materialIndex:0},{start:18,count:18,materialIndex:1}];o.materials=[{...o.material},{...o.material,color:'#00ff00'}];
 const result=await buildSceneExport({objects:[o],format:'obj'}),files=unzipSync(result.bytes),obj=strFromU8(files['model.obj']);assert.equal((obj.match(/^f /gm)||[]).length,12);assert.equal((obj.match(/^usemtl /gm)||[]).length,2);assert.match(strFromU8(files['materials.mtl']),/Kd 0 1 0/);
 const invalid=structuredClone(o);invalid.positions[0]=NaN;await assert.rejects(()=>buildSceneExport({objects:[invalid],format:'stl'}),/positions/);
});
