// SPDX-License-Identifier: AGPL-3.0-only
/** Runs height-source composition through a cancellable worker. */
/** Compose height assets in a worker without changing either source. */
export function startSourceComposition(source,base,options){
 const worker=new Worker(new URL('./source-worker.js',import.meta.url),{type:'module'});let settled=false,rejectPending;
 const finish=()=>{settled=true;worker.terminate();};
 const promise=new Promise((resolve,reject)=>{rejectPending=reject;worker.onmessage=({data})=>{if(settled)return;finish();data.error?reject(new Error(data.error)):resolve(data.result);};worker.onerror=e=>{if(!settled){finish();reject(new Error(e.message||'Source composition failed'));}};worker.onmessageerror=()=>{if(!settled){finish();reject(new Error('Unreadable texture result'));}};try{worker.postMessage({source,base,options});}catch(error){finish();reject(error);}});
 return{promise,cancel(){if(settled)return;finish();rejectPending(new DOMException('Source composition cancelled','AbortError'));}};
}
