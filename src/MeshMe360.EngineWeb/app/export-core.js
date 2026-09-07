// SPDX-License-Identifier: AGPL-3.0-only
import * as THREE from '../vendor/three-0.170.0/build/three.module.js';
import {OBJExporter} from '../vendor/three-0.170.0/examples/jsm/exporters/OBJExporter.js';
import {zipSync,unzipSync,strToU8,strFromU8} from '../vendor/fflate-0.8.2/esm/browser.js';
import {geometryFromObject} from './scene-document.js';
import {exportSTL,export3MF} from '../js/exporter.js';
import {countEdgeDefects,countAreaSlivers} from '../js/meshRepair.js';
import {runExportPipeline} from '../js/exportPipeline.js';

const xml=s=>String(s).replace(/[<>&"']/g,c=>({'<':'&lt;','>':'&gt;','&':'&amp;','"':'&quot;',"'":'&apos;'}[c]));
const safeName=s=>String(s).replace(/[^a-zA-Z0-9_.-]/g,'_').slice(0,80)||'model';
export const exportWarnings={
 stl:'STL contains geometry only, with coordinates in millimetres. Materials, texture sources, object names and explicit unit metadata are not preserved.',
 '3mf':'3MF preserves millimetre units and supported project/profile metadata. This exporter writes one prepared mesh; material groups and texture sources are not preserved.',
 obj:'OBJ is saved as an .obj.zip package containing model.obj and materials.mtl. Diffuse color and opacity are retained; metallic/roughness remain in a metadata file. Source height images are included separately; procedural projection does not supply OBJ UV texture mapping.'
};

export async function buildSceneExport(request,progress=()=>{}){
 const {objects,format,textures=[],title='Untitled',profile='Generic STL'}=request;
 if(!exportWarnings[format]||!objects?.length)throw new Error('Choose an export format and at least one object.');
 const prepared=[],reports=[];let totalFloats=0;
 try{
  for(let i=0;i<objects.length;i++){
   const object=objects[i];progress({stage:'Preparing '+object.name,index:i,total:objects.length});
   let g;const pipelineWarnings=[];
   if(object.pipelineInput){
    if(object.groups)throw new Error('Apply relief before exporting a material-group mesh with live displacement.');
    const result=await runExportPipeline({...object.pipelineInput,mode:'export'},(stage,p)=>progress({stage,index:i,total:objects.length,fraction:p}));
    if(!result)throw new Error('Export pipeline did not produce a result.');
    if(result.safetyCapHit)pipelineWarnings.push('The upstream subdivision safety cap was reached.');
    if(result.lockedOverBudget)pipelineWarnings.push('Preserved geometry exceeds the requested triangle budget.');
    g=new THREE.BufferGeometry();g.setAttribute('position',new THREE.BufferAttribute(result.positions,3));if(result.normals)g.setAttribute('normal',new THREE.BufferAttribute(result.normals,3));
    // Scene transforms describe authored placement, unlike the legacy engine's
    // temporary texturing pose. Apply the scene transform exactly once.
    g.applyMatrix4(new THREE.Matrix4().compose(new THREE.Vector3(...object.position),new THREE.Quaternion(...object.rotation),new THREE.Vector3(...object.scale)));
   }else {g=geometryFromObject(object,{world:true});if(g.index){const soup=g.toNonIndexed();g.dispose();g=soup;}}
   prepared.push({g,object});const p=g.attributes.position.array;
   if(p.length%9||!p.every(Number.isFinite))throw new Error('Export contains invalid or non-finite triangles.');
   totalFloats+=p.length;if(totalFloats>27000000)throw new Error('Export exceeds the three-million-triangle memory budget.');
   const defects=countEdgeDefects(g),slivers=countAreaSlivers(g);g.computeBoundingBox();
   reports.push({name:object.name,...defects,slivers,pipelineWarnings,bounds:{min:g.boundingBox.min.toArray(),max:g.boundingBox.max.toArray()}});
  }
  progress({stage:'Writing '+format.toUpperCase(),index:objects.length,total:objects.length});
  let bytes;
  if(format==='obj'){
   const scene=new THREE.Group(),materials=[],files={};let mtl='';
   try{
    for(const {g,object} of prepared){
     const groups=g.groups.length?g.groups:[{start:0,count:g.attributes.position.count,materialIndex:0}],palette=object.materials??[object.material];
     for(let i=0;i<groups.length;i++){
      const group=groups[i],m=palette[group.materialIndex],name='material_'+materials.length,part=new THREE.BufferGeometry();
      for(const attribute of ['position','normal'])if(g.attributes[attribute])part.setAttribute(attribute,new THREE.BufferAttribute(g.attributes[attribute].array.slice(group.start*3,(group.start+group.count)*3),3));
      const material=new THREE.MeshStandardMaterial();material.name=name;const mesh=new THREE.Mesh(part,material);mesh.name=safeName(object.name)+'_'+i;scene.add(mesh);
      const rgb=[1,3,5].map(n=>parseInt(m.color.slice(n,n+2),16)/255);mtl+=`newmtl ${name}\nKd ${rgb.join(' ')}\nd ${m.opacity}\nillum 2\n\n`;materials.push({name,...m,object:object.name,textureId:object.textureId??null});
     }
    }
    scene.updateMatrixWorld(true);files['model.obj']=strToU8('mtllib materials.mtl\n'+new OBJExporter().parse(scene));files['materials.mtl']=strToU8(mtl);
   }finally{for(const mesh of scene.children){mesh.geometry.dispose();mesh.material.dispose();}}
   const sources=[];
   for(const asset of textures.filter(t=>objects.some(o=>o.textureId===t.id))){
    if(asset.mime!=='application/x-meshme-rgba8')continue;
    const canvas=new OffscreenCanvas(asset.width,asset.height);canvas.getContext('2d').putImageData(new ImageData(new Uint8ClampedArray(asset.bytes),asset.width,asset.height),0,0);
    const path='source-textures/'+sources.length+'_'+safeName(asset.name)+'.png';files[path]=new Uint8Array(await(await canvas.convertToBlob({type:'image/png'})).arrayBuffer());sources.push({id:asset.id,path,role:'Source height image; no OBJ UV mapping'});
   }
   files['meshme-metadata.json']=strToU8(JSON.stringify({application:'Mesh Me360',publisher:'Done by Computer Universal Technology Systems',title,profile,units:'mm',materials,sources,reports},null,2));
   files['README.txt']=strToU8(exportWarnings.obj+'\nCoordinates are millimetres. Import model.obj with materials.mtl beside it.\n');bytes=zipSync(files);
  }else{
   const p=new Float32Array(totalFloats),n=new Float32Array(totalFloats);let offset=0;
   for(const {g} of prepared){if(!g.attributes.normal)g.computeVertexNormals();p.set(g.attributes.position.array,offset);n.set(g.attributes.normal.array,offset);offset+=g.attributes.position.array.length;}
   const merged=new THREE.BufferGeometry();merged.setAttribute('position',new THREE.BufferAttribute(p,3));merged.setAttribute('normal',new THREE.BufferAttribute(n,3));
   try{bytes=format==='stl'?new Uint8Array(exportSTL(merged,null)):export3MF(merged,null);}finally{merged.dispose();}
   if(format==='3mf'){
    const files=unzipSync(bytes),path='3D/3dmodel.model';let model=strFromU8(files[path]);
    if(!model.includes('<resources>'))throw new Error('Unexpected upstream 3MF container structure.');
    model=model.replace('<resources>',`<metadata name="Application">Mesh Me360</metadata>\n<metadata name="Title">${xml(title)}</metadata>\n<metadata name="Description">${xml(profile)}</metadata>\n<resources>`);files[path]=strToU8(model);bytes=zipSync(files);
   }
  }
  return{bytes,reports,warning:exportWarnings[format],extension:format==='obj'?'.obj.zip':'.'+format};
 }finally{for(const {g}of prepared)g.dispose();}
}
