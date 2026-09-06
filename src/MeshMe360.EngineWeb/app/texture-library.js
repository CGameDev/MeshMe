// SPDX-License-Identifier: AGPL-3.0-only
import {TextureLibraryStore} from './texture-library-store.js';

export function attachTextureLibrary({registry,openDialog,el,applyFile,onChange,showError}){
 const store=new TextureLibraryStore(),urls=new Map();let items=[];
 async function refresh(){items=await store.list();for(const [id,url]of urls)if(!items.some(item=>item.id===id)){URL.revokeObjectURL(url);urls.delete(id);}onChange();}
 async function add(file,parameters){if(file.size>32*1024*1024)throw new Error('Texture exceeds the 32 MiB library limit');const bitmap=await createImageBitmap(file);bitmap.close();const item={id:'custom-'+crypto.randomUUID(),name:file.name.replace(/\.[^.]+$/,''),extension:file.name.split('.').pop(),blob:file,parameters:parameters??null,favorite:false,createdAt:new Date().toISOString()};await store.put(item);await refresh();return item;}
 async function apply(id){const item=items.find(i=>i.id===id);if(!item)throw new Error('Texture is no longer in the library');await applyFile(new File([item.blob],item.name+'.'+(item.extension??'png'),{type:item.blob.type}));return item;}
 const button=(label,run)=>{const b=el('button','',label);b.type='button';b.onclick=()=>{try{Promise.resolve(run()).catch(showError);}catch(error){showError(error);}};return b;};
 function open(){
  const list=el('div');
  function render(){list.replaceChildren();for(const item of items){const row=el('div'),name=el('input');name.value=item.name;name.setAttribute('aria-label','Texture name');row.append(name,button('Rename',async()=>{await store.put({...item,name:name.value.trim()});await refresh();render();}),button('Apply',()=>apply(item.id)),button(item.favorite?'Unfavorite':'Favorite',async()=>{await store.put({...item,favorite:!item.favorite});await refresh();render();}),button('Delete',async()=>{if(confirm('Delete custom texture “'+item.name+'” from the library?')){await store.remove(item.id);await refresh();render();}}));list.append(row);}if(!items.length)list.append(el('p','','Import an image or save a generated texture to your library. Built-in presets remain available in the shelf.'));}
  const input=el('input');input.type='file';input.accept='image/*';input.onchange=async()=>{try{if(input.files[0]){await add(input.files[0]);render();}input.value='';}catch(error){showError(error);}};
  render();openDialog('Texture Library',[input,list]);
 }
 function generate(){
  let worker=null,blob=null,closed=false;const form=el('div'),status=el('p','','Choose parameters and generate a preview.'),canvas=el('canvas');canvas.width=canvas.height=256;canvas.style.width='100%';canvas.style.maxWidth='256px';
  const inputs={};
  function field(name,type,value){const row=el('label','locked-field'),input=el('input');input.type=type;input.value=value;input.setAttribute('aria-label',name);row.append(el('span','',name),input);form.append(row);inputs[name]=input;input.oninput=invalidate;return input;}
  const kind=el('select');kind.setAttribute('aria-label','Pattern');for(const value of ['noise','ridges','cells','waves','checker','brick']){const option=el('option','',value);option.value=value;kind.append(option);}kind.onchange=invalidate;form.append(kind);
  field('Name','text','Generated texture');field('Seed','number',1);field('Frequency','number',6);field('Contrast','number',1).step='.1';field('Blur','number',0);field('Invert','checkbox','');
  const save=button('Save to Library',async()=>{if(!blob)throw new Error('Generate the current settings first');const item=await add(new File([blob],inputs.Name.value.trim()+'.png',{type:'image/png'}),parameters());status.textContent='Saved to Library: '+item.name;});save.disabled=true;
  function parameters(){return{kind:kind.value,seed:inputs.Seed.valueAsNumber,frequency:inputs.Frequency.valueAsNumber,contrast:inputs.Contrast.valueAsNumber,blur:inputs.Blur.valueAsNumber,invert:inputs.Invert.checked,size:256};}
  function invalidate(){worker?.terminate();worker=null;blob=null;save.disabled=true;status.textContent='Parameters changed. Generate a new preview.';}
  const generateButton=button('Generate Preview',()=>{
   invalidate();const current=new Worker(new URL('./procedural-worker.js',import.meta.url),{type:'module'});worker=current;status.textContent='Generating…';
   current.onmessage=async({data})=>{current.terminate();if(closed||worker!==current)return;if(data.error){status.textContent=data.error;return;}const context=canvas.getContext('2d'),pixels=context.createImageData(256,256);for(let i=0;i<data.bytes.length;i++){pixels.data[i*4]=pixels.data[i*4+1]=pixels.data[i*4+2]=data.bytes[i];pixels.data[i*4+3]=255;}context.putImageData(pixels,0,0);const png=await new Promise(resolve=>canvas.toBlob(resolve,'image/png'));if(closed||worker!==current)return;blob=png;worker=null;save.disabled=false;status.textContent='Preview ready. Save to Library to retain it.';};
   current.onerror=e=>{current.terminate();status.textContent=e.message||'Texture generation failed';};current.postMessage(parameters());
  });
  openDialog('Generate Texture',[form,generateButton,canvas,status,save],()=>{closed=true;worker?.terminate();},'tool');
 }
 registry.register('Generate Texture',generate);registry.register('Texture Library',open);
 void refresh().catch(showError);
 return{get items(){return items;},add,apply,open,refresh,store,url(id){if(!urls.has(id)){const item=items.find(i=>i.id===id);if(item)urls.set(id,URL.createObjectURL(item.blob));}return urls.get(id);},async favorite(id){const item=items.find(i=>i.id===id);if(!item)throw new Error('Unknown custom texture');await store.put({...item,favorite:!item.favorite});await refresh();}};
}
