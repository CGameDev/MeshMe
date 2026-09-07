// SPDX-License-Identifier: AGPL-3.0-only
export function startMeshProcessing(object,options,onProgress=()=>{}){
 const worker=new Worker(new URL('./mesh-processing-worker.js',import.meta.url),{type:'module'});let settled=false,rejectPending;
 const finish=()=>{settled=true;worker.terminate();};
 const promise=new Promise((resolve,reject)=>{rejectPending=reject;worker.onmessage=({data})=>{if(settled)return;if(data.type==='progress')onProgress(data.p);else if(data.type==='done'){finish();resolve(data.result);}else if(data.type==='error'){finish();reject(new Error(data.message));}};worker.onerror=e=>{if(!settled){finish();reject(new Error(e.message||'Mesh processing failed'));}};worker.onmessageerror=()=>{if(!settled){finish();reject(new Error('Unreadable mesh result'));}};try{worker.postMessage({object,options});}catch(error){finish();reject(error);}});
 return{promise,cancel(){if(!settled){finish();rejectPending(new DOMException('Mesh processing cancelled','AbortError'));}}};
}
