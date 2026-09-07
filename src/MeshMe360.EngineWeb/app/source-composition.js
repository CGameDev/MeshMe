// SPDX-License-Identifier: AGPL-3.0-only
/** Combines height sources using bounded strength, inversion and blend modes. */
export const sourceModes=['normal','multiply','add','subtract','min'];
/** Return new source-height bytes after strength, inversion and the selected blend operation. */
export function composeSource(source,base,{strength=1,blend='normal',invert=false}={}){
 const valid=a=>a?.mime==='application/x-meshme-rgba8'&&Number.isInteger(a.width)&&Number.isInteger(a.height)&&a.width>0&&a.height>0&&a.width<=4096&&a.height<=4096&&a.bytes instanceof Uint8Array&&a.bytes.length===a.width*a.height*4;
 if(!valid(source)||base&&!valid(base)||!Number.isFinite(strength)||strength<0||strength>1||!sourceModes.includes(blend)||typeof invert!=='boolean')throw new Error('Invalid source composition');
 if(blend!=='normal'&&!base)throw new Error('Choose another texture source first to establish a blend base.');
 const bytes=new Uint8Array(source.bytes.length);
 for(let y=0;y<source.height;y++)for(let x=0;x<source.width;x++){
  const i=(y*source.width+x)*4,background=base?base.bytes[(Math.floor(y/source.height*base.height)*base.width+Math.floor(x/source.width*base.width))*4]/255:0;
  const value=invert?1-source.bytes[i]/255:source.bytes[i]/255;
  const mixed=blend==='multiply'?background*value:blend==='add'?Math.min(1,background+value):blend==='subtract'?Math.max(0,background-value):blend==='min'?Math.min(background,value):value;
  const result=Math.round((background*(1-strength)+mixed*strength)*255);bytes[i]=bytes[i+1]=bytes[i+2]=result;bytes[i+3]=255;
 }
 return{width:source.width,height:source.height,mime:source.mime,bytes};
}
