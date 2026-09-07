// SPDX-License-Identifier: AGPL-3.0-only
import {composePattern} from './pattern.js';
import {runGuardedPipeline} from './guarded-pipeline.js';
self.onmessage=async({data})=>{
 if(data?.cmd!=='run')return;
 try{
  const input=data.input;self.postMessage({type:'progress',stage:'Composing pattern',p:0});
  const {imageData,layout}=composePattern(input.imageData,input.pattern);
  const prepared={...input,imageData,imgWidth:imageData.width,imgHeight:imageData.height,settings:{...input.settings,scaleU:layout.width,scaleV:layout.height,textureAspectU:1,textureAspectV:1,offsetU:0,offsetV:0,rotation:0,symmetricDisplacement:false}};
  const result=await runGuardedPipeline(prepared,(stage,p,info)=>self.postMessage({type:'progress',stage,p,info}));
  result.pattern={...input.pattern,tileWidthMm:layout.width,tileHeightMm:layout.height,resolution:1024};
  const transfers=[result.positions.buffer];if(result.normals)transfers.push(result.normals.buffer);if(result.faceParentId)transfers.push(result.faceParentId.buffer);
  self.postMessage({type:'done',result},transfers);
 }catch(error){self.postMessage({type:'error',message:String(error.message??error)});}
};
self.postMessage({type:'ready'});
