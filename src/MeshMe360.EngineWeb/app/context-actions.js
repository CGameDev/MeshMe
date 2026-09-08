// SPDX-License-Identifier: AGPL-3.0-only
let dismissCurrent=null;
export function showContextActions(event,entries){
 dismissCurrent?.();event.preventDefault();event.stopPropagation();
 const previous=document.activeElement,menu=document.createElement('div');menu.className='locked-dialog command-menu';menu.setAttribute('role','menu');menu.setAttribute('aria-label','Context actions');document.body.append(menu);
 function dismiss(){menu.remove();document.removeEventListener('pointerdown',outside,true);document.removeEventListener('keydown',keys,true);dismissCurrent=null;previous?.focus();}
 function outside(e){if(!menu.contains(e.target))dismiss();}
 const buttons=entries.map(({label,enabled=true,run})=>{const b=document.createElement('button');b.type='button';b.textContent=label;b.disabled=!enabled;b.setAttribute('role','menuitem');b.style.display='block';b.style.width='100%';b.onclick=()=>{dismiss();run();};menu.append(b);return b;}).filter(b=>!b.disabled);
 function keys(e){if(['Escape','Tab'].includes(e.key)){if(e.key==='Escape'){e.preventDefault();e.stopImmediatePropagation();}dismiss();}else if(['ArrowUp','ArrowDown','Home','End'].includes(e.key)){e.preventDefault();e.stopImmediatePropagation();const index=buttons.indexOf(document.activeElement);buttons[e.key==='Home'?0:e.key==='End'?buttons.length-1:(index+(e.key==='ArrowDown'?1:-1)+buttons.length)%buttons.length]?.focus();}}
 menu.style.left=Math.max(6,Math.min(event.clientX,innerWidth-menu.offsetWidth-6))+'px';menu.style.top=Math.max(6,Math.min(event.clientY,innerHeight-menu.offsetHeight-6))+'px';
 dismissCurrent=dismiss;document.addEventListener('pointerdown',outside,true);document.addEventListener('keydown',keys,true);buttons[0]?.focus();return dismiss;
}
