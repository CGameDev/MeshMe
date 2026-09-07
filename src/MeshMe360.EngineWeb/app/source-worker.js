// SPDX-License-Identifier: AGPL-3.0-only
import {composeSource} from './source-composition.js';
self.onmessage=({data})=>{try{const result=composeSource(data.source,data.base,data.options);self.postMessage({result},[result.bytes.buffer]);}catch(error){self.postMessage({error:String(error.message??error)});}};
