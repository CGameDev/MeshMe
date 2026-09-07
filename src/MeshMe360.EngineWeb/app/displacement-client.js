// SPDX-License-Identifier: AGPL-3.0-only
/** Owns relief worker startup, completion and cancellation. */
// The original pipeline runs behind a resource guard in an isolated worker.
/** Return a job promise and cancellation handle without transferring the authored input buffer. */
export function startDisplacement(input,onProgress=()=>{}){
 const worker=new Worker(input.pattern?new URL('./pattern-worker.js',import.meta.url):new URL('./relief-worker.js',import.meta.url),{type:'module'});let settled=false,rejectPending,timer;
 const finish=()=>{settled=true;clearTimeout(timer);worker.terminate();};
 const promise=new Promise((resolve,reject)=>{rejectPending=reject;timer=setTimeout(()=>{finish();reject(new Error('Displacement worker initialization timed out'));},20000);worker.onmessage=({data})=>{if(settled)return;if(data.type==='ready'){clearTimeout(timer);try{worker.postMessage({cmd:'run',input});}catch(error){finish();reject(error);}}if(data.type==='progress')onProgress(data);if(data.type==='done'){finish();resolve(data.result);}if(data.type==='error'){finish();reject(new Error(data.message));}};worker.onerror=e=>{if(settled)return;finish();reject(new Error(e.message||'Displacement worker failed'));};worker.onmessageerror=()=>{if(settled)return;finish();reject(new Error('Unreadable displacement worker result'));};});
 return{promise,cancel(){if(settled)return;finish();rejectPending(new DOMException('Displacement cancelled','AbortError'));}};
}
