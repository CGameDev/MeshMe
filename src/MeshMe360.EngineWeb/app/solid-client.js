// SPDX-License-Identifier: AGPL-3.0-only
/** Owns isolated solid-operation workers and cancellation. */
// One isolated worker per operation: cancel/error releases the WASM heap and
// cannot mutate document arrays. The caller commits only the returned preview.
/** Start an isolated solid-backend job that can be terminated safely. */
export function startSolidOperation(request){
 const worker=new Worker(new URL('./solid-worker.js',import.meta.url),{type:'module'});
 const id=crypto.randomUUID();let rejectPending,settled=false;
 function finish(){settled=true;worker.terminate();}
 const promise=new Promise((resolve,reject)=>{
  rejectPending=reject;
  worker.onmessage=({data})=>{if(data.id!==id||settled)return;finish();data.error?reject(new Error(data.error)):resolve(data.result);};
  worker.onerror=event=>{if(settled)return;event.preventDefault();finish();reject(new Error(event.message||'Geometry worker failed'));};
  worker.onmessageerror=()=>{if(settled)return;finish();reject(new Error('Geometry worker returned unreadable data'));};
  try{worker.postMessage({id,request});}catch(error){finish();reject(error);}
 });
 return{promise,cancel(){if(settled)return;finish();rejectPending(new DOMException('Geometry operation cancelled','AbortError'));}};
}
