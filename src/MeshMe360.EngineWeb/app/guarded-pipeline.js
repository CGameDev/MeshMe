// SPDX-License-Identifier: AGPL-3.0-only
/** Enforces resource budgets around the preserved relief pipeline. */
import {runExportPipeline} from '../js/exportPipeline.js';
export const reliefTriangleLimit=3_000_000;
// A resource guard around the original pipeline, not a replacement algorithm.
/** Run the original pipeline unchanged and reject input or observed output above the resource budget. */
export async function runGuardedPipeline(input,onProgress=()=>{},limit=reliefTriangleLimit){
 if(!Number.isInteger(limit)||limit<1||limit>reliefTriangleLimit)throw new Error('Invalid relief triangle budget');
 if(!(input.positions instanceof Float32Array)||input.positions.length%9||input.positions.length/9>limit)throw new Error('Relief input exceeds the triangle budget or contains incomplete triangles.');
 const check=count=>{if(count>limit)throw new Error(`Refinement exceeds the ${limit.toLocaleString()}-triangle budget. Increase Resolution (mm) in Engine controls and preview again. The current model was kept.`);};
 const result=await runExportPipeline(input,(stage,p,info)=>{if(info?.triCount)check(info.triCount);onProgress(stage,p,info);});
 if(!result)throw new Error('Relief pipeline did not produce a result.');
 check(result.positions.length/9);
 if(result.safetyCapHit)throw new Error('The upstream refinement safety cap was reached. Increase Resolution (mm) and preview again.');
 return result;
}
