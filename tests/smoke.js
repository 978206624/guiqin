// 冒烟测试：node tests/smoke.js [html路径]  （需要 playwright）
const {chromium}=require('playwright'),path=require('path');
(async()=>{const f=path.resolve(process.argv[2]||path.join(__dirname,'../dist/guiqin.html'));
const b=await chromium.launch(),p=await b.newPage({viewport:{width:720,height:1280}}),errs=[];
p.on('pageerror',e=>errs.push(e.message));
await p.goto('file://'+f);await p.waitForTimeout(2500);
const r=await p.evaluate(()=>({data:!!window.DATA,maps:Object.keys(window.DATA.maps).length,assets:Object.keys(window.ASSETS||{}).length}));
console.log(r,errs.length?errs:'no page errors');
await b.close();process.exit(errs.length||!r.data?1:0)})();
