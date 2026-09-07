// SPDX-License-Identifier: AGPL-3.0-only
/** Runs scene export preparation in a cancellable worker. */
/** Start worker-side export preparation and forward progress to the caller. */
export function startSceneExport(request,onProgress=()=>{}){
 const worker=new Worker(new URL('./export-worker.js',import.meta.url),{type:'module'});let settled=false,rejectPending,timer;
 const finish=()=>{settled=true;clearTimeout(timer);worker.terminate();};
 const promise=new Promise((resolve,reject)=>{rejectPending=reject;timer=setTimeout(()=>{finish();reject(new Error('Export worker initialization timed out'));},20000);
  worker.onmessage=({data})=>{if(settled)return;if(data.type==='ready'){clearTimeout(timer);try{worker.postMessage({request});}catch(error){finish();reject(error);return;}}if(data.type==='progress')onProgress(data);if(data.type==='done'){finish();resolve(data.result);}if(data.type==='error'){finish();reject(new Error(data.message));}};
  worker.onerror=e=>{if(settled)return;finish();reject(new Error(e.message||'Export worker failed'));};worker.onmessageerror=()=>{if(settled)return;finish();reject(new Error('Unreadable export worker result'));};
 });return{promise,cancel(){if(settled)return;finish();rejectPending(new DOMException('Export cancelled','AbortError'));}};
}
