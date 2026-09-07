// SPDX-License-Identifier: AGPL-3.0-only
// Compose repeated source-height stamps. Geometry stays in the upstream pipeline.
export function patternLayout(options){
 const {kind,count,rows,spacing,width,height,orientation}=options;
 if(!['linear','grid','radial'].includes(kind)||!Number.isInteger(count)||count<1||count>64||!Number.isInteger(rows)||rows<1||rows>64||![spacing,width,height,orientation].every(Number.isFinite)||spacing<=0||width<=0||height<=0||Math.max(spacing,width,height)>100000)throw new Error('Use positive dimensions and counts from 1 to 64.');
 const total=count*(kind==='grid'?rows:1);if(total>64)throw new Error('A pattern supports at most 64 details per tile.');
 const angle=orientation*Math.PI/180,c=Math.cos(angle),s=Math.sin(angle),stamps=[];
 for(let row=0;row<(kind==='grid'?rows:1);row++)for(let i=0;i<count;i++){
  const radial=kind==='radial',theta=radial?i*2*Math.PI/count:0,radius=radial&&count>1?spacing*count/(2*Math.PI):0;
  const x=radial?radius*Math.cos(theta):(i-(count-1)/2)*spacing,y=radial?radius*Math.sin(theta):(row-((kind==='grid'?rows:1)-1)/2)*spacing;
  stamps.push({x:x*c-y*s,y:x*s+y*c,angle:angle+(radial?theta:0)});
 }
 let minX=Infinity,minY=Infinity,maxX=-Infinity,maxY=-Infinity;
 for(const p of stamps){const ex=(Math.abs(Math.cos(p.angle))*width+Math.abs(Math.sin(p.angle))*height)/2,ey=(Math.abs(Math.sin(p.angle))*width+Math.abs(Math.cos(p.angle))*height)/2;minX=Math.min(minX,p.x-ex);maxX=Math.max(maxX,p.x+ex);minY=Math.min(minY,p.y-ey);maxY=Math.max(maxY,p.y+ey);}
 // A small border makes the tile boundary explicit and avoids touching repeats.
 const border=Math.min(width,height)*.05;
 return{stamps,minX:minX-border,minY:minY-border,width:maxX-minX+2*border,height:maxY-minY+2*border,detailWidth:width,detailHeight:height};
}
export function composePattern(source,options,size=1024){
 if(!source||!Number.isInteger(source.width)||!Number.isInteger(source.height)||source.width<1||source.height<1||source.width>4096||source.height>4096||!source.data||source.data.length!==source.width*source.height*4||!Number.isInteger(size)||size<32||size>2048)throw new Error('Invalid pattern source or resolution');
 const layout=patternLayout(options),data=new Uint8ClampedArray(size*size*4);for(let i=3;i<data.length;i+=4)data[i]=255;
 for(const stamp of layout.stamps){
  const c=Math.cos(stamp.angle),s=Math.sin(stamp.angle),ex=(Math.abs(c)*layout.detailWidth+Math.abs(s)*layout.detailHeight)/2,ey=(Math.abs(s)*layout.detailWidth+Math.abs(c)*layout.detailHeight)/2;
  const x0=Math.max(0,Math.floor((stamp.x-ex-layout.minX)/layout.width*size)),x1=Math.min(size,Math.ceil((stamp.x+ex-layout.minX)/layout.width*size));
  const y0=Math.max(0,Math.floor((stamp.y-ey-layout.minY)/layout.height*size)),y1=Math.min(size,Math.ceil((stamp.y+ey-layout.minY)/layout.height*size));
  for(let y=y0;y<y1;y++)for(let x=x0;x<x1;x++){
   const dx=layout.minX+(x+.5)/size*layout.width-stamp.x,dy=layout.minY+(y+.5)/size*layout.height-stamp.y;
   const u=(dx*c+dy*s)/layout.detailWidth+.5,v=(-dx*s+dy*c)/layout.detailHeight+.5;if(u<0||v<0||u>=1||v>=1)continue;
   const sourceIndex=(Math.floor(v*source.height)*source.width+Math.floor(u*source.width))*4,target=(y*size+x)*4;
   // The preserved displacement sampler reads the red channel as height.
   const height=source.data[sourceIndex];
   const value=Math.max(data[target],Math.round(height));data[target]=data[target+1]=data[target+2]=value;
  }
 }
 return{imageData:{data,width:size,height:size},layout};
}
