// 解 Godot3 RSCC(zstd) 资源，输出 RSRC 原始字节
const fs=require('fs'),zlib=require('zlib');
module.exports=function(file){
  const d=fs.readFileSync(file); if(d.slice(0,4).toString()!=='RSCC') return d;
  const mode=d.readUInt32LE(4),bs=d.readUInt32LE(8),total=d.readUInt32LE(12),n=Math.ceil(total/bs);
  let p=16+4*n; const out=[];
  for(let i=0;i<n;i++){const s=d.readUInt32LE(16+4*i);const blk=d.slice(p,p+s);p+=s;const exp=i<n-1?bs:total-bs*(n-1);
    out.push(s===exp?blk:(mode===2?zlib.zstdDecompressSync(blk):zlib.inflateSync(blk)));}
  return Buffer.concat(out);
};
if(require.main===module){const o=module.exports(process.argv[2]);fs.writeFileSync(process.argv[3],o);console.log(o.length,o.slice(0,12));}
