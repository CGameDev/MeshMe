// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from '../vendor/three-0.170.0/build/three.module.js';
import {zipSync,unzipSync,strToU8,strFromU8} from '../vendor/fflate-0.8.2/esm/browser.js';
import {validateMeasurements} from './measurement.js';
export const publisher='Done by Computer Universal Technology Systems';
const finite=(values,count)=>Array.isArray(values)&&values.length===count&&values.every(Number.isFinite);
export function validateObject(object){
 if(typeof object.id!=='string'||typeof object.name!=='string'||!(object.positions instanceof Float32Array)||object.positions.length%3||!object.positions.length||!object.positions.every(Number.isFinite))throw new Error('Invalid scene object positions');
 if(object.indices && (!(object.indices instanceof Uint32Array)||object.indices.length%3||!object.indices.every(i=>i<object.positions.length/3)))throw new Error('Invalid triangle indices');
 if(!object.indices&&object.positions.length%9)throw new Error('Incomplete triangles');
 if(object.normals&&(!(object.normals instanceof Float32Array)||object.normals.length!==object.positions.length||!object.normals.every(Number.isFinite)))throw new Error('Invalid object normals');
 if(!finite(object.position,3)||!finite(object.rotation,4)||!finite(object.scale,3)||object.scale.some(v=>v<=0))throw new Error('Invalid object transform');
 const materials=object.materials??[object.material];if(!Array.isArray(materials)||!materials.length)throw new Error('Invalid object materials');
 for(const m of [object.material,...materials])if(!m||!/^#[0-9a-f]{6}$/i.test(m.color)||!['metallic','roughness','opacity'].every(k=>Number.isFinite(m[k])&&m[k]>=0&&m[k]<=1))throw new Error('Invalid object material');
 if(object.groups){let end=0;if(!Array.isArray(object.groups)||!object.groups.length)throw new Error('Invalid material groups');for(const g of object.groups){if(!Number.isInteger(g.start)||g.start!==end||!Number.isInteger(g.count)||g.count<=0||g.count%3||!Number.isInteger(g.materialIndex)||g.materialIndex<0||g.materialIndex>=materials.length)throw new Error('Invalid material group');end+=g.count;}if(end!==(object.indices?.length??object.positions.length/3))throw new Error('Incomplete material groups');}
 return object;
}
export function objectFromGeometry(geometry,name,sourceFormat='stl'){
 return validateObject({id:crypto.randomUUID(),name,sourceFormat,visible:true,locked:false,positions:new Float32Array(geometry.getAttribute('position').array),indices:geometry.index?new Uint32Array(geometry.index.array):null,...(geometry.attributes.normal?{normals:new Float32Array(geometry.attributes.normal.array)}:{}),position:[0,0,0],rotation:[0,0,0,1],scale:[1,1,1],material:{color:'#a0a7ae',metallic:.15,roughness:.65,opacity:1},modifiers:[]});
}
export function geometryFromObject(object,{world=false}={}){
 validateObject(object);const g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(object.positions.slice(),3));if(object.indices)g.setIndex(new THREE.BufferAttribute(object.indices.slice(),1));if(object.normals)g.setAttribute('normal',new THREE.BufferAttribute(object.normals.slice(),3));else g.computeVertexNormals();
 for(const group of object.groups??[])g.addGroup(group.start,group.count,group.materialIndex);
 if(world)g.applyMatrix4(new THREE.Matrix4().compose(new THREE.Vector3(...object.position),new THREE.Quaternion(...object.rotation),new THREE.Vector3(...object.scale)));return g;
}
export class SceneDocument {
 constructor({historyBytes=256*1024*1024}={}){this.state={format:'MeshMe360',version:1,publisher,name:'Untitled.mm360',objects:[],textures:[],measurements:[],exportProfiles:[{id:'generic-stl',name:'Generic STL',format:'stl'},{id:'bambu-020',name:'Bambu Lab (0.20mm)',format:'3mf',layerHeightMm:.2}],activeProfile:'generic-stl',units:'mm',selection:[]};this.undoStack=[];this.redoStack=[];this.historyBytes=historyBytes;this.revision=0;this.savedRevision=0;this.nextRevision=1;}
 get dirty(){return this.revision!==this.savedRevision;}
 get selected(){const objects=new Map(this.state.objects.map(o=>[o.id,o]));return this.state.selection.map(id=>objects.get(id)).filter(Boolean);}
 get historySize(){return [...this.undoStack,...this.redoStack].reduce((n,s)=>n+s.bytes,0);}
 snapshot(){const state=structuredClone(this.state);return{state,revision:this.revision,bytes:state.objects.reduce((n,o)=>n+o.positions.byteLength+(o.indices?.byteLength??0)+(o.normals?.byteLength??0),0)+state.textures.reduce((n,t)=>n+t.bytes.byteLength,0)};}
 transact(label,change){const prior=this.snapshot();if(prior.bytes>this.historyBytes)throw new Error('Operation requires more undo memory than the 256 MiB history budget; reduce model size before editing.');try{change(this.state);this.state.objects.forEach(validateObject);}catch(e){this.state=prior.state;throw e;}this.undoStack.push({...prior,label});this.redoStack=[];while(this.historySize>this.historyBytes&&this.undoStack.length>1)this.undoStack.shift();this.revision=this.nextRevision++;}
 undo(){const prior=this.undoStack.pop();if(!prior)return false;this.redoStack.push({...this.snapshot(),label:prior.label});this.state=prior.state;this.revision=prior.revision;return true;}
 redo(){const next=this.redoStack.pop();if(!next)return false;this.undoStack.push({...this.snapshot(),label:next.label});this.state=next.state;this.revision=next.revision;return true;}
 markSaved(){this.savedRevision=this.revision;}
 add(geometry,name,sourceFormat){const object=objectFromGeometry(geometry,name,sourceFormat);this.transact('Import',s=>{s.objects.push(object);s.selection=[object.id];});return object;}
 select(ids){if(ids.some(id=>!this.state.objects.some(o=>o.id===id)))throw new Error('Unknown selection');this.state.selection=[...new Set(ids)];}
 selectionForClick(id,extend=false){return extend?(this.state.selection.includes(id)?this.state.selection.filter(selected=>selected!==id):[...this.state.selection,id]):[id];}
 transform({position,rotation,scale}){this.transact('Transform',()=>{for(const o of this.selected){if(o.locked)throw new Error('Unlock the selected object');if(position)o.position=[...position];if(rotation)o.rotation=[...rotation];if(scale)o.scale=[...scale];}});}
 setMaterial(material){this.transact('Material',()=>{for(const o of this.selected){if(o.locked)throw new Error('Unlock the selected object');o.material={...material};if(o.materials)o.materials=o.materials.map(()=>({...material}));}});}
 center(){this.transact('Center Model',()=>{for(const o of this.selected){if(o.locked)throw new Error('Unlock the selected object');const g=geometryFromObject(o,{world:true});g.computeBoundingBox();const b=g.boundingBox,c=b.getCenter(new THREE.Vector3());o.position[0]-=c.x;o.position[1]-=c.y;o.position[2]-=b.min.z;g.dispose();}});}
 placeOnBed(){this.transact('Place on Bed',()=>{for(const o of this.selected){if(o.locked)throw new Error('Unlock the selected object');const g=geometryFromObject(o,{world:true});g.computeBoundingBox();o.position[2]-=g.boundingBox.min.z;g.dispose();}});}
 resetTransform(){this.transact('Reset Transform',()=>{for(const o of this.selected){if(o.locked)throw new Error('Unlock the selected object');o.position=[0,0,0];o.rotation=[0,0,0,1];o.scale=[1,1,1];}});}
 removeSelected(){this.transact('Delete',s=>{if(this.selected.some(o=>o.locked))throw new Error('Unlock the selected object');s.objects=s.objects.filter(o=>!s.selection.includes(o.id));s.selection=[];});}
 duplicate(){this.transact('Duplicate',s=>{const copies=this.selected.map(o=>({...structuredClone(o),id:crypto.randomUUID(),name:o.name+' copy',locked:false}));s.objects.push(...copies);s.selection=copies.map(o=>o.id);});}
}
export function serializeProject(state){
 validateMeasurements(state.measurements);
 const manifest=structuredClone(state),files={};
 manifest.objects=state.objects.map((o,i)=>{validateObject(o);const path=`meshes/${i}`;files[path+'.positions.bin']=new Uint8Array(o.positions.buffer,o.positions.byteOffset,o.positions.byteLength);if(o.indices)files[path+'.indices.bin']=new Uint8Array(o.indices.buffer,o.indices.byteOffset,o.indices.byteLength);if(o.normals)files[path+'.normals.bin']=new Uint8Array(o.normals.buffer,o.normals.byteOffset,o.normals.byteLength);return{...o,positions:path+'.positions.bin',indices:o.indices?path+'.indices.bin':null,...(o.normals?{normals:path+'.normals.bin'}:{})};});
 manifest.textures=state.textures.map((t,i)=>{const path=`textures/${i}.bin`;files[path]=t.bytes;return{...t,bytes:path};});
 files['project.json']=strToU8(JSON.stringify(manifest));return zipSync(files);
}
export function deserializeProject(bytes){
 if(!(bytes instanceof Uint8Array)||bytes.byteLength>500*1024*1024)throw new Error('Project exceeds the 500 MiB compressed input limit');
 let total=0,count=0;const files=unzipSync(bytes,{filter:entry=>{total+=entry.originalSize;if(++count>10000||total>512*1024*1024||entry.originalSize>256*1024*1024)throw new Error('Project exceeds the asset memory budget');return true;}}),raw=files['project.json'];if(!raw)throw new Error('Not a Mesh Me360 project');if(raw.length>8*1024*1024)throw new Error('Project manifest is too large');const state=JSON.parse(strFromU8(raw));
 if(state.format!=='MeshMe360'||state.version!==1||!Array.isArray(state.objects)||!Array.isArray(state.textures)||!Array.isArray(state.measurements)||!Array.isArray(state.selection))throw new Error('Unsupported project schema');
 validateMeasurements(state.measurements);
 const typed=(path,Type)=>{const bytes=files[path];if(!bytes||bytes.byteLength%4)throw new Error('Missing or truncated geometry asset');return new Type(bytes.slice().buffer);};
 state.objects=state.objects.map(o=>validateObject({...o,positions:typed(o.positions,Float32Array),indices:o.indices?typed(o.indices,Uint32Array):null,...(o.normals?{normals:typed(o.normals,Float32Array)}:{})}));
 if(new Set(state.objects.map(o=>o.id)).size!==state.objects.length||state.selection.some(id=>!state.objects.some(o=>o.id===id)))throw new Error('Invalid project object identity');
 state.textures=state.textures.map(t=>{if(!files[t.bytes])throw new Error('Missing texture asset');const asset={...t,bytes:files[t.bytes].slice()};if(asset.mime==='application/x-meshme-rgba8'&&(!Number.isInteger(asset.width)||!Number.isInteger(asset.height)||asset.width<1||asset.height<1||asset.width>4096||asset.height>4096||asset.bytes.length!==asset.width*asset.height*4))throw new Error('Invalid embedded texture dimensions');return asset;});
 if(state.objects.some(o=>o.textureId&&!state.textures.some(t=>t.id===o.textureId)))throw new Error('Missing object texture reference');
 for(const o of state.objects)if(o.sourceEffects){const e=o.sourceEffects;if(!state.textures.some(t=>t.id===e.sourceId)||e.baseId&&!state.textures.some(t=>t.id===e.baseId)||!Number.isFinite(e.strength)||e.strength<0||e.strength>1||typeof e.invert!=='boolean'||!['normal','multiply','add','subtract','min'].includes(e.blend))throw new Error('Invalid source composition reference or settings');}
 return state;
}
