// SPDX-License-Identifier: AGPL-3.0-only
/** Coordinates native save acknowledgement and safe document replacement. */
import {serializeProject,deserializeProject,SceneDocument} from './scene-document.js';

export function attachDocumentLifecycle({document,host,registry,capture,refreshScene,setBusy,showError}) {
 const pending=new Map();
 async function save(){
  if(registry.busy)return false;
  capture();setBusy(true);
  try{
   const revision=document.revision,name=document.state.name.replace(/\.(bumpmesh|mm360)$/i,'')+'.mm360';
   const blob=new Blob([serializeProject(document.state)],{type:'application/octet-stream'});
   let resolve;const completed=new Promise(r=>resolve=r);
   const grant=await host.download(blob,name,registered=>pending.set(registered.requestId,{revision,name,resolve}));
   if(!grant)return false;
   return await completed;
  }finally{setBusy(false);}
 }
 async function prepareReplacement(){
  if(registry.busy)return false;
  capture();
  if(!document.dirty)return true;
  const choice=await host.rpc.request('document.confirmReplacement',{name:document.state.name});
  if(choice==='discard')return true;
  if(choice!=='save')return false;
  return await save()&&!document.dirty;
 }
 function replace(state,name){document.state=state;document.state.name=name;document.undoStack=[];document.redoStack=[];document.revision=document.nextRevision++;document.markSaved();refreshScene({activate:true});}
 async function openProject(file){
  // Parse before offering to discard the current valid document.
  const state=deserializeProject(new Uint8Array(await file.arrayBuffer()));
  if(!await prepareReplacement())throw new Error('Project open cancelled; the current document was kept.');
  replace(state,file.name);
 }
 registry.register('Save',save,()=>!!host&&!registry.busy);
 registry.register('Save As',save,()=>!!host&&!registry.busy);
 registry.register('New Project',async()=>{if(await prepareReplacement()){const empty=new SceneDocument();replace(empty.state,empty.state.name);}});
 registry.register('Exit',()=>host.rpc.request('window.close',{}),()=>!!host);
 host?.rpc.onMessage(message=>{
  if(message.type==='document.stateChanged'&&pending.has(message.requestId)){
   const entry=pending.get(message.requestId);pending.delete(message.requestId);
   if(message.payload.downloadCompleted){document.savedRevision=entry.revision;document.state.name=message.payload.savedFilename??entry.name;refreshScene();}
   entry.resolve(message.payload.downloadCompleted===true);
  }else if(message.type==='host.closeRequested'){
   void (async()=>{let ready=false;try{ready=await prepareReplacement();}catch(error){showError(error);}finally{await host.rpc.request('window.closeResponse',{token:message.requestId,ready});}})().catch(showError);
  }
 });
 return {openProject,prepareReplacement};
}
