// SPDX-License-Identifier: AGPL-3.0-only
import {SceneDocument,objectFromGeometry} from './scene-document.js';
export function attachLegacyProject({document,engine,openDialog,el,capture,applySnapshot,refreshScene,setBusy,prepareReplacement}){
 let mode='settings';
 function chooseLegacyMode(){return new Promise(resolve=>{
  let close;const button=(label,value)=>{const b=el('button','',label);b.type='button';b.onclick=async()=>{
   if(value==='all'){
    setBusy(false);let permitted=false;try{permitted=await prepareReplacement();}finally{setBusy(true);}
    if(!permitted){resolve(null);close();return;}
   }
   mode=value;resolve(value);close();
  };return b;};
  const settings=button('Settings only','settings');settings.disabled=document.selected.length!==1||document.selected[0]?.locked;settings.title='Apply to one unlocked selected object; retain its geometry and painted mask.';
  close=openDialog('Open legacy project',[el('p','','Load the saved model and settings, or apply only the settings and texture to the selected object.'),button('Load model and settings','all'),settings],()=>resolve(null));
 });}
 async function importLegacy(file,load){
  capture();mode='settings';setBusy(true);
  try{
   if(await load()!==true)return false;
   const snapshot=engine.desktopSceneCapture();try{
    if(mode==='all'){
     if(!snapshot.geometry)throw new Error('The legacy project did not contain a valid model.');
     const fresh=new SceneDocument(),object=objectFromGeometry(snapshot.geometry,file.name.replace(/\.bumpmesh$/i,''),'bumpmesh');
     const prior=document.state;document.state=fresh.state;
     try{applySnapshot(object,snapshot);document.state.objects.push(object);document.state.selection=[object.id];document.state.name=file.name.replace(/\.bumpmesh$/i,'.mm360');}
     catch(error){document.state=prior;throw error;}
     document.undoStack=[];document.redoStack=[];document.revision=document.nextRevision++;document.savedRevision=-1;
    }else{
     if(document.selected.length!==1||document.selected[0].locked)throw new Error('Select one unlocked object before importing legacy settings.');
     document.transact('Import legacy texture settings',()=>{const object=document.selected[0],baseId=object.textureId;applySnapshot(object,snapshot);object.sourceEffects={sourceId:object.textureId,baseId,strength:1,blend:'normal',invert:false};});
    }
   }finally{snapshot.geometry?.dispose();}
   return true;
  }finally{setBusy(false);refreshScene({activate:true});}
 }
 return{chooseLegacyMode,importLegacy};
}
