// SPDX-License-Identifier: AGPL-3.0-only
/** Presents existing commands as anchored keyboard-accessible dropdown menus. */
// Compact command presentation; the existing command handlers stay authoritative.
export function attachCommandMenu({root,body,close,registry,icon}){
 const names=['File','Edit','Mesh','Texture','Modify','View','Tools','Help','Export'];
 const breaks=new Set(['Save','Export…','Project Settings','Exit','Duplicate Selected','Select All','Preferences','Boolean','Diagnostics','Apply Texture','Generate Texture','Engine controls','Center Model','Grid','Home view','Perspective','Browser panel','Before / after','Top','Application Diagnostics','About Mesh Me360','Export Selected']);
 const shortcuts={'New Project':'Ctrl+N','Open Project…':'Ctrl+O','Save':'Ctrl+S','Save As…':'Ctrl+Shift+S','Undo':'Ctrl+Z','Redo':'Ctrl+Y','Delete Selected':'Del','Preferences':'Ctrl+,','Documentation':'F1'};
 let anchor=null;
 const triggers=()=>[...document.querySelectorAll('.locked-menu [data-command^="menu:"]')];
 function reset(){for(const b of triggers())b.setAttribute('aria-expanded','false');}
 /** Close only a live menu; optionally return keyboard focus to its trigger. */
 function dismiss(focus=false){if(!root.classList.contains('command-menu')||root.hidden)return;close();reset();if(focus)anchor?.focus();}
 /** Clamp the popup to the client area while retaining its trigger anchor. */
 function place(){if(!anchor)return;const r=anchor.getBoundingClientRect();root.style.left=Math.max(6,Math.min(r.left,innerWidth-root.offsetWidth-6))+'px';root.style.top=(r.bottom+4)+'px';root.style.maxHeight=Math.max(120,innerHeight-r.bottom-12)+'px';}
 /** Decorate the existing command nodes without replacing their action handlers. */
 function present(title){
  reset();const active=names.includes(title);root.classList.toggle('command-menu',active);root.style.left=root.style.top=root.style.maxHeight='';root.setAttribute('role',active?'menu':'dialog');root.setAttribute('aria-modal',String(!active));root.setAttribute('aria-label',title);
  if(!active)return;
  anchor=triggers().find(b=>b.dataset.command==='menu:'+title);anchor?.setAttribute('aria-expanded','true');
  const buttons=[...body.querySelectorAll('button')];
  for(const [i,b]of buttons.entries()){
   const label=b.textContent;b.setAttribute('role','menuitem');b.tabIndex=-1;b.dataset.shortcut=shortcuts[label]??'';b.setAttribute('aria-label',label);if(i&&breaks.has(label))b.classList.add('menu-separator');
   const symbol=document.createElement('span');symbol.className='menu-icon';symbol.setAttribute('aria-hidden','true');symbol.innerHTML=icon(({'Shaded':'Texture','Wireframe':'Texture','Textures':'Texture','X-Ray':'Hollow','Grid':'Pattern','Axes':'Move','Home view':'Texture','Perspective':'Texture','Orthographic':'Texture','Top':'Texture','Bottom':'Texture','Front':'Texture','Back':'Texture','Left':'Texture','Right':'Texture','Project Settings':'Preferences','Application Diagnostics':'Measure','Diagnostics':'Measure','Mesh Diagnostics':'Measure','Exit':'Import','Keyboard Shortcuts':'Pattern','Before / after':'Split','Texture panel':'Texture','Procedural source':'Pattern','Image source':'Texture','Engine controls':'Preferences'})[label]??label.split(' ')[0]);b.prepend(symbol);
   // Dismiss before executing, so a command may immediately open its own tool.
   b.addEventListener('click',()=>{dismiss(true);},true);
   b.addEventListener('pointermove',()=>{if(!b.disabled)b.focus({preventScroll:true});});
  }
  place();buttons.find(b=>!b.disabled)?.focus({preventScroll:true});
 }
 for(const b of triggers()){
  b.addEventListener('click',e=>{if(!root.hidden&&root.classList.contains('command-menu')&&anchor===b){e.stopImmediatePropagation();dismiss(true);}},true);
  b.setAttribute('aria-haspopup','menu');b.setAttribute('aria-expanded','false');
  b.addEventListener('pointerenter',()=>{if(!root.hidden&&root.classList.contains('command-menu')&&anchor!==b)registry.execute(b.dataset.command);});
  b.addEventListener('keydown',e=>{if(['ArrowDown','ArrowUp'].includes(e.key)){e.preventDefault();registry.execute(b.dataset.command);}});
 }
 document.addEventListener('pointerdown',e=>{if(!root.contains(e.target)&&!triggers().some(b=>b.contains(e.target)))dismiss();},true);
 document.addEventListener('keydown',e=>{
  if(root.hidden||!root.classList.contains('command-menu'))return;
  const items=[...body.querySelectorAll('button:not(:disabled)')],i=items.indexOf(document.activeElement);
  if(['ArrowDown','ArrowUp','Home','End'].includes(e.key)){e.preventDefault();e.stopImmediatePropagation();const n=e.key==='Home'?0:e.key==='End'?items.length-1:(i+(e.key==='ArrowDown'?1:-1)+items.length)%items.length;items[n]?.focus();}
  else if(['ArrowLeft','ArrowRight'].includes(e.key)){e.preventDefault();e.stopImmediatePropagation();const all=triggers(),n=(all.indexOf(anchor)+(e.key==='ArrowRight'?1:-1)+all.length)%all.length;registry.execute(all[n].dataset.command);}
  else if(e.key==='Escape'){e.preventDefault();e.stopImmediatePropagation();dismiss(true);}
  else if(e.key==='Tab')dismiss();
 },true);
 window.addEventListener('resize',()=>{if(!root.hidden&&root.classList.contains('command-menu'))place();});
 return{present,reset};
}
