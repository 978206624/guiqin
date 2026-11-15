// 第一章通关测试：node tests/story_playthrough.js [html路径]。自动按任务栏寻路、点对白、打首领，直到主线最后一拍；有页面报错会打印出来。
const {chromium}=require('playwright');
const S=require('os').tmpdir()+'/';
(async()=>{
 const b=await chromium.launch(); const errs=[]; const pg=await b.newPage({viewport:{width:720,height:1280}});
 pg.on('pageerror',e=>errs.push(e.message)); pg.on('console',m=>{if(m.type()==='error'&&!/ERR_|fonts/.test(m.text()))errs.push(m.text())});
 await pg.goto('file://'+require('path').resolve(process.argv[2]||require('path').join(__dirname,'../dist/guiqin.html'))+''); await pg.waitForTimeout(800);
 await pg.fill('#fAccount','qin'); await pg.fill('#fPassword','x'); await pg.locator('#fLoginForm button[type=submit]').click(); await pg.waitForTimeout(300);
 await pg.locator('[data-tab="a"]').click(); await pg.locator('[data-srv="s1"]').click(); await pg.waitForTimeout(300);
 await pg.fill('#fNewName','云长风'); await pg.locator('#fCreateForm button[type=submit]').click(); await pg.waitForTimeout(300);
 await pg.locator('#fEnter').click(); await pg.waitForTimeout(2800);
 const st=()=>pg.evaluate(()=>({step:GAME.state.step,cnt:GAME.state.cnt,lv:GAME.state.lv,map:GAME.map.id,n:GAME.data.quests.length,q:(GAME.data.quests[GAME.state.step]||{}).goal}));
 let shots=0,last=-1,idle=0;
 for(let i=0;i<400;i++){
  const s=await st(); if(s.step>=s.n){console.log('DONE',s);break}
  if(s.step!==last){console.log(JSON.stringify(s)); last=s.step; idle=0}
  if(await pg.locator('#dialog').isVisible()){ if(shots<3&&i%5==0){await pg.screenshot({path:S+'s32_dlg'+(shots++)+'.png'})} await pg.locator('#dialog').click(); await pg.waitForTimeout(120); continue}
  const sc=pg.locator('[data-act="sect:dao_ty"]'); if(await sc.isVisible().catch(()=>false)){ console.log('拜入天音'); await sc.click(); await pg.waitForTimeout(400); continue}
  const fb=pg.locator('[data-act^="fight:"]'); if(await fb.first().isVisible().catch(()=>false)){ await fb.first().click(); await pg.waitForTimeout(600); continue}
  if(await pg.evaluate(()=>GAME.battle.active)){ 
    const a=pg.locator('[data-cmd="attack"]'); if(await a.isVisible().catch(()=>false)) {await a.click().catch(()=>{});} 
    const l=pg.locator('[data-act="leave"]'); if(await l.first().isVisible().catch(()=>false)){ await l.first().click(); await pg.waitForTimeout(400);} 
    await pg.waitForTimeout(250); continue }
  const l2=pg.locator('[data-act="leave"],[data-act="close"]'); if(await l2.first().isVisible().catch(()=>false)){ await l2.first().click(); await pg.waitForTimeout(300); continue }
  await pg.locator('#quest').click().catch(()=>{}); await pg.waitForTimeout(900); idle++; if(idle>60){console.log('STUCK',await st());break}
 }
 await pg.screenshot({path:S+'s32_end.png'}); console.log('final',await st(),errs); await b.close()})();
