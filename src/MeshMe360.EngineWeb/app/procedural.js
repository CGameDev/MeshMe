// SPDX-License-Identifier: AGPL-3.0-only
const clamp=x=>Math.max(0,Math.min(1,x));
function hash(x,y,seed){let n=Math.imul(x,374761393)^Math.imul(y,668265263)^Math.imul(seed,1274126177);n=Math.imul(n^(n>>>13),1274126177);return ((n^(n>>>16))>>>0)/4294967295;}
const smooth=x=>x*x*(3-2*x);
function noise(x,y,seed){const a=Math.floor(x),b=Math.floor(y),u=smooth(x-a),v=smooth(y-b);return (1-v)*((1-u)*hash(a,b,seed)+u*hash(a+1,b,seed))+v*((1-u)*hash(a,b+1,seed)+u*hash(a+1,b+1,seed));}
export function generateHeight({kind='noise',seed=1,frequency=6,contrast=1,invert=false,strength=1,size=256,blur=0}={}){
 if(!Number.isInteger(size)||size<8||size>2048||!Number.isFinite(frequency)||frequency<=0||frequency>128||!Number.isFinite(contrast)||contrast<0||contrast>8||!Number.isFinite(strength)||strength<0||strength>1)throw new Error('Invalid texture parameters');
 if(!Number.isInteger(seed)||seed<0||seed>4294967295||!Number.isInteger(blur)||blur<0||blur>32)throw new Error('Invalid seed or blur radius');
 const bytes=new Uint8Array(size*size);
 for(let j=0;j<size;j++)for(let i=0;i<size;i++){const x=i/size*frequency,y=j/size*frequency;let h;
  switch(kind){
   case 'waves':h=.5+.5*Math.sin(x*Math.PI*2+2*noise(x*.3,y*.3,seed));break;
   case 'ridges':h=1-Math.abs(2*noise(x,y,seed)-1);break;
   case 'cells':{let d=2;for(let b=-1;b<=1;b++)for(let a=-1;a<=1;a++){const u=Math.floor(x)+a,v=Math.floor(y)+b;d=Math.min(d,Math.hypot(x-u-hash(u,v,seed),y-v-hash(u,v,seed+7)));}h=clamp(d);break;}
   case 'checker':h=(Math.floor(x)+Math.floor(y))%2?.8:.2;break;
   case 'brick':{const u=(x+(Math.floor(y)%2)*.5)%1,v=y%1;h=u<.055||v<.065?.12:.65+.22*noise(x*8,y*8,seed);break;}
   case 'scales':{const u=(x+(Math.floor(y)%2)*.5)%1-.5,v=y%1;h=clamp(1-Math.hypot(u*1.7,v*.9));break;}
   case 'stone':{let first=Infinity,second=Infinity;for(let b=-1;b<=1;b++)for(let a=-1;a<=1;a++){const u=Math.floor(x)+a,v=Math.floor(y)+b,d=Math.hypot(x-u-hash(u,v,seed),y-v-hash(u,v,seed+7));if(d<first){second=first;first=d;}else second=Math.min(second,d);}const seam=smooth(clamp((second-first)*12));h=.08+seam*(.55+.22*noise(x*6,y*6,seed+13))+.12*noise(x*35,y*35,seed+17);break;}
   case 'woodgrain':{const grain=x+1.7*noise(x*.23,y*.035,seed)+.13*noise(x*.8,y*.12,seed+4);h=.48+.24*Math.sin(grain*Math.PI*2)+.14*Math.sin(grain*Math.PI*8)+.12*noise(x*18,y*3,seed+5);break;}
   case 'plates':{const u=(x+(Math.floor(y)%2)*.5)%1-.5,v=y%1,width=.48*(1-.6*v),edge=smooth(clamp((width-Math.abs(u))*28))*smooth(clamp(v*25));h=.08+edge*(.48+.19*(1-v)+.17*noise(x*18,y*18,seed))+noise(x*55,y*55,seed+4)*.08;break;}
   case 'noise':h=(noise(x,y,seed)*.6+noise(x*4,y*4,seed+1)*.3+noise(x*12,y*12,seed+2)*.1);break;
   default:throw new Error('Unsupported procedural texture');
  }
  h=clamp((h-.5)*contrast+.5);bytes[j*size+i]=Math.round((invert?1-h:h)*strength*255);
 }
 if(!blur)return bytes;
 // Deterministic separable box smoothing with periodic texture boundaries.
 const horizontal=new Float32Array(bytes.length),result=new Uint8Array(bytes.length),width=blur*2+1;
 const wrap=n=>(n%size+size)%size;
 for(let y=0;y<size;y++){let sum=0;for(let k=-blur;k<=blur;k++)sum+=bytes[y*size+wrap(k)];for(let x=0;x<size;x++){horizontal[y*size+x]=sum/width;sum+=bytes[y*size+wrap(x+blur+1)]-bytes[y*size+wrap(x-blur)];}}
 for(let x=0;x<size;x++){let sum=0;for(let k=-blur;k<=blur;k++)sum+=horizontal[wrap(k)*size+x];for(let y=0;y<size;y++){result[y*size+x]=Math.round(sum/width);sum+=horizontal[wrap(y+blur+1)*size+x]-horizontal[wrap(y-blur)*size+x];}}
 return result;
}
export const presetParameters={Stone:{kind:'stone',frequency:5,contrast:1.3},Rock:{kind:'noise',frequency:9,contrast:2},Scales:{kind:'plates',frequency:7,contrast:1.3},Bricks:{kind:'brick',frequency:6},Wood:{kind:'woodgrain',frequency:13},Metal:{kind:'ridges',frequency:20,contrast:.6},Fabric:{kind:'checker',frequency:48,contrast:.5},Carbon:{kind:'checker',frequency:18,contrast:1.4},Leather:{kind:'cells',frequency:28,contrast:.7},Concrete:{kind:'noise',frequency:30,contrast:1.2},Sand:{kind:'noise',frequency:64,contrast:.8}};
export function heightCanvas(parameters){const size=parameters.size??256,bytes=generateHeight({...parameters,size}),canvas=document.createElement('canvas');canvas.width=canvas.height=size;const context=canvas.getContext('2d'),data=context.createImageData(size,size);for(let i=0;i<bytes.length;i++){data.data[i*4]=data.data[i*4+1]=data.data[i*4+2]=bytes[i];data.data[i*4+3]=255;}context.putImageData(data,0,0);return canvas;}
