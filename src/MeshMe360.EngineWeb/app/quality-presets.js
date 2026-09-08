// SPDX-License-Identifier: AGPL-3.0-only
/** Explicit deterministic parameter assignments; edits always switch to Custom. */
export function qualityValues(kind,base){
 if(kind==='solidify')return {Draft:base*2,Standard:base,High:base/2};
 if(kind==='refine')return {Draft:10,Standard:5,High:2.5};
 if(kind==='simplify')return {Draft:Math.max(4,Math.floor(base*.25)),Standard:Math.max(4,Math.floor(base*.5)),High:Math.max(4,Math.floor(base*.75))};
 throw new Error('Unsupported quality preset');
}
export function attachQuality(form,input,kind,base,el){
 const values=qualityValues(kind,base),row=el('label','locked-field'),select=el('select');select.setAttribute('aria-label','Quality');row.append(el('span','','Quality'),select);
 for(const [value,label]of [['Draft','Draft'],['Standard','Standard — Recommended'],['High','High Quality'],['Custom','Advanced / Custom']])select.append(new Option(label,value));select.value='Standard';
 select.onchange=()=>{if(select.value==='Custom')return;input.value=values[select.value];input.dispatchEvent(new Event('input',{bubbles:true}));select.value=Object.keys(values).find(k=>values[k]===Number(input.value))??'Custom';};
 input.addEventListener('input',()=>{select.value='Custom';});form.prepend(row);return select;
}
