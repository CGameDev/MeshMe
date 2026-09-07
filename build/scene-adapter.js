// Desktop state/asset adapter appended to the staged, preserved engine module.
// Texture construction uses the same CanvasTexture/ImageData representation as
// presetTextures.js. No mapping, displacement or sampling algorithm is changed.
const desktopTextureAssets = new WeakMap();
let desktopOwnedTexture = null;
let desktopTextureLoadSequence = 0;
function desktopCaptureTexture() {
  if (!activeMapEntry) return null;
  let asset = desktopTextureAssets.get(activeMapEntry);
  if (!asset) {
    asset = {id:crypto.randomUUID(), name:activeMapEntry.name,
      mime:'application/x-meshme-rgba8', width:activeMapEntry.width,
      height:activeMapEntry.height, bytes:new Uint8Array(activeMapEntry.imageData.data)};
    desktopTextureAssets.set(activeMapEntry, asset);
  }
  return asset;
}
function desktopRestoreTexture(asset) {
  desktopTextureLoadSequence++;
  _effectiveMapCache=null;_effectiveMapCacheKey=null;
  if (desktopOwnedTexture) { desktopOwnedTexture.texture.dispose(); desktopOwnedTexture=null; }
  if (!asset) {activeMapEntry=null;_lastCustomMap=null;return;}
  if (asset.mime!=='application/x-meshme-rgba8'||!Number.isInteger(asset.width)||!Number.isInteger(asset.height)||asset.width<1||asset.height<1||asset.width>4096||asset.height>4096||!(asset.bytes instanceof Uint8Array)||asset.bytes.length!==asset.width*asset.height*4) throw new Error('Invalid embedded texture');
  const canvas=document.createElement('canvas');canvas.width=asset.width;canvas.height=asset.height;
  const imageData=new ImageData(new Uint8ClampedArray(asset.bytes),asset.width,asset.height);
  canvas.getContext('2d').putImageData(imageData,0,0);
  const texture=new THREE.CanvasTexture(canvas);texture.wrapS=texture.wrapT=THREE.RepeatWrapping;texture.name=asset.name;
  activeMapEntry={name:asset.name,width:asset.width,height:asset.height,fullCanvas:canvas,imageData,texture,isCustom:true};
  desktopOwnedTexture=activeMapEntry;_lastCustomMap=activeMapEntry;
  desktopTextureAssets.set(activeMapEntry,asset);activeMapName.textContent=asset.name;
  _showCustomMapThumb(activeMapEntry);
}
export function desktopSceneCapture() {
  return {geometry:currentGeometry?.clone(),settings:getSettingsSnapshot(),mask:_collectCurrentMask(),texture:desktopCaptureTexture()};
}
export function desktopSceneActivate(geometry,snapshot,mask,texture) {
  const name=currentStlName,work=geometry.index?geometry.toNonIndexed():geometry;
  if(work!==geometry)geometry.dispose();
  desktopRestoreTexture(texture);
  adoptBakedGeometry(work,computeBounds(work));currentStlName=name;
  applySettingsSnapshot(snapshot);if(mask)_restoreMask(mask);
}
export async function desktopLoadTexture(file) {
  const sequence=++desktopTextureLoadSequence;
  const entry=await loadCustomTexture(file);
  if(sequence!==desktopTextureLoadSequence){entry.texture.dispose();return false;}
  if(desktopOwnedTexture)desktopOwnedTexture.texture.dispose();
  entry.isCustom=true;activeMapEntry=entry;desktopOwnedTexture=entry;_lastCustomMap=entry;
  activeMapName.textContent=file.name;
  document.querySelectorAll('.preset-swatch').forEach(s=>s.classList.remove('active'));
  _showCustomMapThumb(entry);customMapSwatch.classList.add('active');
  resetTextureSmoothing();updatePreview();
  window.dispatchEvent(new CustomEvent('meshme:texture-loaded'));
  return true;
}
export function desktopConfigureTexture(snapshot){applySettingsSnapshot(snapshot);updatePreview();}
export function desktopTextureSettings(){return getSettingsSnapshot();}
export function desktopDisplacementInput(){
  if(!currentGeometry||!activeMapEntry)throw new Error('Select a model and texture source');
  const hasAngleMask=settings.bottomAngleLimit>0||settings.topAngleLimit>0;
  const faceWeights=(excludedFaces.size>0||selectionMode||hasAngleMask)?buildCombinedFaceWeights(currentGeometry,excludedFaces,selectionMode,settings):null;
  const entry=getEffectiveMapEntry();
  // Exact input fields from the preserved bakeTextures() pipeline call.
  return{positions:currentGeometry.attributes.position.array,faceWeights,imageData:entry.imageData,imgWidth:entry.width,imgHeight:entry.height,settings:structuredClone(settings),bounds:currentBounds,regularizeOpts:_regularizeOpts(),mode:'bake',desktopMaskBaked:bakeMaskChk.checked};
}
export function desktopAdoptDisplacement(result,input){
  if(!(result.positions instanceof Float32Array)||result.positions.length%9||!result.positions.every(Number.isFinite))throw new Error('Invalid displacement result');
  const geometry=new THREE.BufferGeometry();geometry.setAttribute('position',new THREE.BufferAttribute(result.positions,3));if(result.normals)geometry.setAttribute('normal',new THREE.BufferAttribute(result.normals,3));
  // Same parent-face exclusion rule as bakeTextures(), retained at the scene
  // commit boundary so the worker preview cannot mutate the engine's base mesh.
  let preExcluded=null;
  if(input.desktopMaskBaked){preExcluded=[];const wasParentExcluded=input.faceWeights?i=>input.faceWeights[i*3]>.99:()=>false;for(let i=0;i<result.faceParentId.length;i++)if(!wasParentExcluded(result.faceParentId[i]))preExcluded.push(i);}
  adoptBakedGeometry(geometry,computeBounds(geometry),{preExcludedFaces:preExcluded});
}
