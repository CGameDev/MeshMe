// SPDX-License-Identifier: AGPL-3.0-only
/** Coordinates revision-aware diagnostics and read-only viewport feedback. */
import {startPrintAnalysis} from './print-client.js';
export function attachDiagnosticsTools({document,registry,openDialog,el,setBusy,refreshScene}){
 let checkedRevision=-1,reports=[];
 const reportFor=id=>checkedRevision===document.revision?reports.find(r=>r.id===id):undefined;
 function summary(){const current=document.state.objects.filter(o=>o.visible).map(o=>reportFor(o.id)),checked=current.filter(Boolean),issues=checked.reduce((n,r)=>n+r.open+r.nonManifold+r.degenerate+(!r.solid?1:0),0);const text=issues?`${issues} detected mesh issues`:checked.length&&checked.length===current.length?'No detected mesh issues':checked.length?`${checked.length}/${current.length} visible models checked`:'Diagnostics not run';window.dispatchEvent(new CustomEvent('meshme:diagnostics',{detail:{text,complete:checked.length>0&&checked.length===current.length,issues}}));}
 window.addEventListener('meshme:scene-changed',summary);
 function launch(){
  const objects=(document.selected.length?document.selected:document.state.objects).filter(o=>o.visible),revision=document.revision,output=el('div'),status=el('p','','Ready to inspect committed geometry.');status.dataset.diagnosticsStatus='idle';
  const run=el('button','','Run diagnostics'),cancel=el('button','','Cancel');let job=null,closed=false,close;
  run.onclick=async()=>{run.disabled=true;status.dataset.diagnosticsStatus='running';status.textContent='Checking mesh geometry…';output.replaceChildren();try{job=startPrintAnalysis(objects.map(o=>structuredClone(o)),name=>{status.textContent='Checking '+name+'…';});const result=await job.promise;job=null;if(closed)return;if(document.revision!==revision)throw new Error('The scene changed during diagnostics. Run checks again.');
    checkedRevision=revision;reports=result;for(const r of result){const row=el('section');row.dataset.diagnosticsObject=r.id;row.append(el('h3','',r.name),el('p','',`${r.tris} triangles · ${r.open} open edges · ${r.nonManifold} non-manifold edges · ${r.degenerate} degenerate triangles`),el('p','',`Solid: ${r.solid?'closed with positive volume':'not validated'}`),el('p','',`Normals: ${r.normals}`),el('p','',r.wall.minimumSampleMm===null?'Wall estimate unavailable.':`Smallest sampled wall: ${r.wall.minimumSampleMm.toFixed(3)} mm.`),el('p','',r.wall.note));output.append(row);}status.dataset.diagnosticsStatus='ready';status.textContent='Checks complete. Self-intersections and the true minimum wall still require review. No geometry was changed.';refreshScene();summary();
   }catch(error){if(!closed){status.dataset.diagnosticsStatus='error';status.textContent=String(error.message??error);}}finally{if(!closed)run.disabled=false;}
  };
  function cleanup(){closed=true;job?.cancel();setBusy(false);window.removeEventListener('meshme:before-model-load',onLoad);}
  function onLoad(){close();}cancel.onclick=()=>close();close=openDialog('Mesh Diagnostics',[el('p','','Inspect selected visible models, or all visible models when nothing is selected. Results expire after the next project edit.'),run,cancel,status,output],cleanup,'tool');setBusy(true);window.addEventListener('meshme:before-model-load',onLoad);
 }
 for(const name of ['Diagnostics','Mesh Diagnostics'])registry.register(name,launch,()=>(document.selected.length?document.selected:document.state.objects).some(o=>o.visible),'Select a visible model, or clear selection to inspect all visible models');
 return{reportFor,summary};
}
