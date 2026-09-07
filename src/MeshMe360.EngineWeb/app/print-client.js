// SPDX-License-Identifier: AGPL-3.0-only
export function startPrintAnalysis(objects,onProgress=()=>{}){
 const worker=new Worker(new URL('./print-worker.js',import.meta.url),{type:'module'});let settled=false,rejectPending;
 const finish=()=>{settled=true;worker.terminate();};
 const promise=new Promise((resolve,reject)=>{rejectPending=reject;worker.onmessage=({data})=>{if(settled)return;if(data.type==='progress')onProgress(data.name);else if(data.type==='done'){finish();resolve(data.reports);}else if(data.type==='error'){finish();reject(new Error(data.message));}};worker.onerror=e=>{if(!settled){finish();reject(new Error(e.message||'Print checks failed'));}};worker.onmessageerror=()=>{if(!settled){finish();reject(new Error('Unreadable print report'));}};try{worker.postMessage({objects});}catch(error){finish();reject(error);}});
 return{promise,cancel(){if(settled)return;finish();rejectPending(new DOMException('Print checks cancelled','AbortError'));}};
}
