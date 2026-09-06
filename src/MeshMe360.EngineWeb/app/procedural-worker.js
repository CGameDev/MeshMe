// SPDX-License-Identifier: AGPL-3.0-only
import {generateHeight} from './procedural.js';
self.onmessage=({data})=>{try{const bytes=generateHeight(data);self.postMessage({bytes},[bytes.buffer]);}catch(error){self.postMessage({error:String(error.message??error)});}};
