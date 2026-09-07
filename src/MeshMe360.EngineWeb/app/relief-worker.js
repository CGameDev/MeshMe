// SPDX-License-Identifier: AGPL-3.0-only
/** Runs the original relief pipeline behind the application resource guard. */
import {runGuardedPipeline} from './guarded-pipeline.js';
self.onmessage=async({data})=>{
 if(data?.cmd!=='run')return;
 try{
  const result=await runGuardedPipeline(data.input,(stage,p,info)=>self.postMessage({type:'progress',stage,p,info}));
  const transfers=[result.positions.buffer];if(result.normals)transfers.push(result.normals.buffer);if(result.faceParentId)transfers.push(result.faceParentId.buffer);
  self.postMessage({type:'done',result},transfers);
 }catch(error){self.postMessage({type:'error',message:String(error.message??error)});}
};
self.postMessage({type:'ready'});
