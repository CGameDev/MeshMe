// SPDX-License-Identifier: AGPL-3.0-only
import test from 'node:test';
import assert from 'node:assert/strict';
import {suggestedAction,dropKind} from '../../src/MeshMe360.EngineWeb/app/ux-assistance.js';
import {qualityValues} from '../../src/MeshMe360.EngineWeb/app/quality-presets.js';
test('suggestions use current verified state and never invent a healthy mesh',()=>{
 assert.deepEqual(suggestedAction({selected:[{}],reports:[],busy:false}),['Mesh Diagnostics']);
 assert.deepEqual(suggestedAction({selected:[{}],reports:[{open:4}],busy:false}),['Repair Mesh']);
 assert.deepEqual(suggestedAction({selected:[{}],reports:[{solid:true}],busy:false}),['Texture','Prepare for Print']);
 assert.deepEqual(suggestedAction({selected:[{textureId:'texture'}],reports:[],busy:false}),['Apply Texture','Emboss']);
 assert.deepEqual(suggestedAction({selected:[{},{}],reports:[{solid:true},{solid:true}],busy:false}),['Boolean','Combine']);
 assert.equal(suggestedAction({selected:[{}],reports:[],busy:true}),null);
});
test('drop routing distinguishes model/project/legacy/image and rejects unknown types',()=>{
 for(const extension of ['STL','obj','3mf','step','stp'])assert.equal(dropKind('model.'+extension),'model');
 assert.equal(dropKind('project.mm360'),'project');assert.equal(dropKind('old.bumpmesh'),'legacy');assert.equal(dropKind('texture.PNG'),'image');assert.equal(dropKind('app.exe'),'unsupported');
});
test('quality presets map to documented deterministic parameters',()=>{
 assert.deepEqual(qualityValues('solidify',2),{Draft:4,Standard:2,High:1});
 assert.deepEqual(qualityValues('refine',100),{Draft:10,Standard:5,High:2.5});
 assert.deepEqual(qualityValues('simplify',1000),{Draft:250,Standard:500,High:750});
});
