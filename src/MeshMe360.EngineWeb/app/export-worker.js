// SPDX-License-Identifier: AGPL-3.0-only
import {buildSceneExport} from './export-core.js';
self.onmessage=async({data})=>{
 try{const result=await buildSceneExport(data.request,p=>self.postMessage({type:'progress',...p}));self.postMessage({type:'done',result},[result.bytes.buffer]);}
 catch(error){self.postMessage({type:'error',message:String(error.message??error)});}
};
self.postMessage({type:'ready'});
