// SPDX-License-Identifier: AGPL-3.0-only
/** Isolates mesh-processing work from the application event loop. */
import {processMesh} from './mesh-processing.js';
self.onmessage=async({data})=>{try{self.postMessage({type:'done',result:await processMesh(data.object,data.options,p=>self.postMessage({type:'progress',p}))});}catch(error){self.postMessage({type:'error',message:String(error.message??error)});}};
