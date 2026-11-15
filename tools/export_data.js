// 导出语言无关的数据：data/data.json（全部数值/地图/NPC/关卡/任务…）、data/collision/*.json（碰撞位图）、
// data/assets_manifest.json（资源清单：key → 文件 / 像素尺寸）。Unity 里直接 JsonUtility / Newtonsoft 读取即可。
// 用法：node tools/export_data.js
const fs=require('fs'),path=require('path'),vm=require('vm');
const R=path.join(__dirname,'..');
const sb={window:{}};sb.window.window=sb.window;vm.createContext(sb);
for(const f of ['js/data.js','js/masks.js']) vm.runInContext(fs.readFileSync(path.join(R,f),'utf8'),sb);
fs.mkdirSync(R+'/data/collision',{recursive:true});
fs.writeFileSync(R+'/data/data.json',JSON.stringify(sb.window.DATA,null,1));
const M=sb.window.MASKS;
for(const k in M) fs.writeFileSync(`${R}/data/collision/${k}.json`,JSON.stringify(M[k]));
// 资源清单
const files=fs.readdirSync(R+'/assets').sort(),man={};
let sizeOf;try{sizeOf=require('image-size')}catch(e){}
for(const f of files){const e={file:'assets/'+f,bytes:fs.statSync(R+'/assets/'+f).size};
  if(sizeOf){try{const s=sizeOf(R+'/assets/'+f);e.w=s.width;e.h=s.height}catch(_){}}
  man[f.replace(/\.[^.]+$/,'')]=e}
fs.writeFileSync(R+'/data/assets_manifest.json',JSON.stringify(man,null,1));
console.log('data.json',Object.keys(sb.window.DATA).length,'keys; masks',Object.keys(M).join(','),'; assets',files.length);
