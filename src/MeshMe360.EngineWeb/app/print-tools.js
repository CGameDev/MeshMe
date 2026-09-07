// SPDX-License-Identifier: AGPL-3.0-only
import {placementProposal} from './print-analysis.js';
import {startPrintAnalysis} from './print-client.js';

export function attachPrintTools({document,registry,openDialog,el,setBusy,refreshScene,capture}){
 function launch(){
  capture();let revision=document.revision,job=null,reports=null,closed=false,closeDialog;
  const ids=document.selected.filter(o=>o.visible).map(o=>o.id),selected=()=>document.state.objects.filter(o=>ids.includes(o.id));
  const form=el('div'),summary=el('div'),diagnostics=el('div'),status=el('p','','Review dimensions and proposed placement, then run the checks.');status.dataset.printStatus='idle';
  function choice(label,entries,value){const row=el('label','locked-field'),select=el('select');select.setAttribute('aria-label',label);for(const [key,text]of entries){const option=el('option','',text);option.value=key;select.append(option);}select.value=value;row.append(el('span','',label),select);form.append(row);return select;}
  const units=choice('Preparation display units',[['mm','Millimetres'],['in','Inches']],document.state.units),profile=choice('Preparation profile',document.state.exportProfiles.map(p=>[p.id,p.name]),document.state.activeProfile);
  const format=choice('Final export format',[['stl','STL'],['3mf','3MF'],['obj','OBJ package']],document.state.exportProfiles.find(p=>p.id===profile.value)?.format??'stl');
  const thresholdRow=el('label','locked-field'),threshold=el('input');threshold.type='number';threshold.min='0';threshold.step='.1';threshold.value='0';threshold.setAttribute('aria-label','Wall warning threshold (mm)');thresholdRow.append(el('span','','Wall warning threshold (mm)'),threshold);form.append(thresholdRow,el('p','','0 reports samples without a process-specific threshold. Choose a suitable threshold for your printer and material. Bambu Lab (0.20mm) is an export/preparation preset, not a slicer or printer guarantee.'));
  const run=el('button','','Run checks'),place=el('button','','Apply proposed placement'),apply=el('button','','Apply units and profile'),exportButton=el('button','primary','Continue to export'),cancel=el('button','','Cancel');exportButton.disabled=true;
  const length=v=>(v/(units.value==='in'?25.4:1)).toFixed(3),vector=v=>v.map(length).join(' × ')+' '+units.value;
  function showProposal(){summary.replaceChildren(el('p','','Coordinates remain millimetres internally. Display units do not rescale imported geometry. Proposed placement centres each selected object on X/Y and puts its lowest point on Z=0; multiple objects may overlap.'));
   for(const object of selected()){const p=placementProposal(object);summary.append(el('p','',`${p.name}: ${vector(p.dimensionsMm)}. Proposed translation: ${vector(p.translation)}.`));}
   place.disabled=selected().some(o=>o.locked);place.title=place.disabled?'Unlock selected objects to change placement.':'Review the translations above before applying.';
  }
  function showReports(){diagnostics.replaceChildren();if(!reports)return;for(const r of reports){const row=el('section');row.dataset.printObject=r.id;const wall=r.wall.minimumSampleMm;
   row.append(el('h3','',r.name),el('p','',`${r.tris.toLocaleString()} triangles; ${r.open} open edges; ${r.nonManifold} non-manifold edges; ${r.degenerate} degenerate triangles.`),el('p','',`Normals: ${r.normals}. Solid state: ${r.solid?'closed with positive volume':'not validated'}.`),el('p','',wall===null?'Wall estimate unavailable for this mesh.':`Smallest sampled wall: ${length(wall)} ${units.value}, from ${r.wall.sampled} rays (${r.wall.failed} unsuccessful).`),el('p','',r.wall.note));
   if(wall!==null&&threshold.valueAsNumber>0&&wall<threshold.valueAsNumber)row.append(el('p','',`Warning: sampled wall is below your ${threshold.valueAsNumber} mm threshold.`));diagnostics.append(row);}
  }
  function checkRevision(){if(document.revision!==revision)throw new Error('The document changed. Reopen Prepare for Print.');}
  function invalid(){job?.cancel();job=null;reports=null;exportButton.disabled=true;status.dataset.printStatus='idle';diagnostics.replaceChildren();}
  function fail(error){status.dataset.printStatus='error';status.textContent=String(error.message??error);}
  run.onclick=async()=>{invalid();run.disabled=true;status.dataset.printStatus='running';status.textContent='Checking committed geometry in the background…';try{checkRevision();if(!Number.isFinite(threshold.valueAsNumber)||threshold.valueAsNumber<0)throw new Error('Enter a nonnegative wall threshold.');const current=startPrintAnalysis(selected().map(o=>structuredClone(o)),name=>{status.textContent='Checking '+name+'…';});job=current;const output=await current.promise;if(closed||job!==current)return;job=null;reports=output;showReports();status.dataset.printStatus='ready';status.textContent='Checks finished. Review all warnings. No geometry was changed; self-intersections and the true minimum wall remain unverified.';exportButton.disabled=false;}catch(error){if(!closed&&error.name!=='AbortError')fail(error);}finally{if(!closed)run.disabled=false;}};
  place.onclick=()=>{try{checkRevision();if(selected().some(o=>o.locked))throw new Error('Unlock selected objects.');invalid();document.transact('Prepare: centre and place',()=>{for(const o of selected()){const p=placementProposal(o);o.position=o.position.map((v,i)=>v+p.translation[i]);}});revision=document.revision;refreshScene();showProposal();status.textContent='Placement applied as one undoable edit. Run checks again.';}catch(error){fail(error);}};
  function applyPreferences(){checkRevision();if(document.state.units!==units.value||document.state.activeProfile!==profile.value){document.transact('Preparation units and profile',s=>{s.units=units.value;s.activeProfile=profile.value;});revision=document.revision;refreshScene();}}
  apply.onclick=()=>{try{applyPreferences();status.textContent='Display units and export profile saved to the project.';}catch(error){fail(error);}};
  exportButton.onclick=()=>{try{if(!reports)throw new Error('Run checks first.');applyPreferences();const command=format.value.toUpperCase();closeDialog();registry.execute(command);}catch(error){fail(error);}};
  profile.onchange=()=>{format.value=document.state.exportProfiles.find(p=>p.id===profile.value).format;};units.onchange=()=>{showProposal();showReports();};threshold.oninput=showReports;
  function cleanup(){if(closed)return;closed=true;job?.cancel();setBusy(false);window.removeEventListener('meshme:before-model-load',onImport);}
  function onImport(){closeDialog();}cancel.onclick=()=>closeDialog();const actions=el('div');actions.append(run,place,apply,exportButton,cancel);showProposal();
  closeDialog=openDialog('Prepare for Print',[form,summary,status,diagnostics,actions],cleanup,'tool');setBusy(true);window.addEventListener('meshme:before-model-load',onImport);
 }
 registry.register('Prepare for Print',launch,()=>document.selected.some(o=>o.visible),'Select visible objects to prepare');
}
