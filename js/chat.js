// ============================================================
// 聊天：综合 / 世界 / 系统 三个页签 + 滚动的系统公告条。
// 全是前端模拟：路人发言、公告都是定时器生成的；自己发的世界消息只显示在本机。
// 接真服务器时：发送处（submit）改成发请求；收到别人的消息时调用 CHAT.add('world', {...})；
// 系统公告调用 CHAT.notice(文本)。simulate* 两个函数删掉即可。
// 游戏本体通过 CHAT.bind() 提供当前角色、天气、是否在战斗中。
// ============================================================
(function () {
'use strict';
const D = window.DATA.chat, CLS = window.DATA.classes;
const $ = id => document.getElementById(id);
const rnd = (a, b) => a + Math.random() * (b - a), pick = a => a[Math.floor(Math.random() * a.length)];
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const CH = { world: { tag: '世界', color: '#7ad7ff' }, system: { tag: '系统', color: '#ff7a5a' }, notice: { tag: '公告', color: '#ffb020' } };
const TABS = [['all', '综合'], ['world', '世界'], ['system', '系统']];
const inTab = (m, tab) => tab === 'all' || (tab === 'world' ? m.ch === 'world' : m.ch !== 'world');
const hhmm = t => { const d = new Date(t); return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };

let ctx = () => ({ name: '', cls: 'm_sword', lv: 1, weather: '晴', battle: false });
const hist = []; let tab = 'all', isOpen = false, lastSend = 0, lastText = '', timers = [];

function lineHTML(m, mini) {
  const c = CH[m.ch];
  let h = '<span style="color:' + c.color + '">[' + c.tag + ']</span> ';
  if (m.ch === 'world') {
    h += '<span class="cn' + (m.self ? ' me' : '') + '">' + esc(m.from) + '</span>';
    if (!mini) h += '<i class="cl">' + (CLS[m.cls] ? CLS[m.cls].name : '') + ' ' + m.lv + '级</i>';
    h += '：' + esc(m.text);
  } else h += esc(m.text);
  return h;
}
function drawMini() {
  const log = $('chatLog'); log.innerHTML = '';
  hist.slice(-3).forEach(m => { const d = document.createElement('div'); d.innerHTML = lineHTML(m, true); log.appendChild(d); });
}
function drawList(stick) {
  const el = $('chatList'), near = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  el.innerHTML = hist.filter(m => inTab(m, tab)).map(m =>
    '<div class="cm ' + (m.ch === 'notice' ? 'n' : '') + '">' + lineHTML(m) + '<small>' + hhmm(m.t) + '</small></div>').join('') || '<p class="cempty">还没有消息。</p>';
  if (stick || near) el.scrollTop = el.scrollHeight;
}
function add(ch, o) {
  hist.push({ ch, from: o.from || '', cls: o.cls, lv: o.lv, text: o.text, self: !!o.self, t: Date.now() });
  while (hist.length > D.history) hist.shift();
  drawMini(); if (isOpen) drawList(o.self);
}

// ---------- 面板 ----------
function drawTabs() {
  $('chatTabs').innerHTML = TABS.map(t => '<button type="button" class="ctab' + (t[0] === tab ? ' on' : '') + '" data-tab="' + t[0] + '">' + t[1] + '</button>').join('');
  const ro = tab === 'system'; $('chatInput').disabled = ro; $('chatSend').disabled = ro;
  $('chatInput').placeholder = ro ? '系统频道只能看，不能发言' : '说点什么…（最多 ' + D.maxLen + ' 字）';
  $('chatCh').textContent = ro ? '系统' : '世界';
}
function open() { isOpen = true; $('chatP').hidden = false; $('chatHint').textContent = ''; drawTabs(); drawList(true); }
function close() { isOpen = false; $('chatP').hidden = true; $('chatInput').blur(); }
['pointerdown', 'pointermove', 'pointerup', 'click'].forEach(ev => $('chatP').addEventListener(ev, e => e.stopPropagation()));
$('chat').addEventListener('click', e => { e.stopPropagation(); open(); });
$('chatDim').addEventListener('click', close); $('chatX').addEventListener('click', close);
$('chatTabs').addEventListener('click', e => { const b = e.target.closest('[data-tab]'); if (b) { tab = b.dataset.tab; drawTabs(); drawList(true); } });
$('chatForm').addEventListener('submit', e => {
  e.preventDefault();
  const me = ctx(), text = $('chatInput').value.trim().slice(0, D.maxLen), hint = $('chatHint');
  if (tab === 'system' || !text) return;
  const wait = Math.ceil(D.cooldown - (Date.now() - lastSend) / 1000);
  if (wait > 0) { hint.textContent = '发言太快了，请 ' + wait + ' 秒后再试'; return; }
  if (text === lastText) { hint.textContent = '请不要重复发言'; return; }
  hint.textContent = ''; lastSend = Date.now(); lastText = text; $('chatInput').value = '';
  add('world', { from: me.name, cls: me.cls, lv: me.lv, text, self: true });
  simulateReply(text);
});

// ---------- 公告条 ----------
const queue = []; let showing = false;
function nextNotice() {
  if (showing || !queue.length) return;
  showing = true; const text = queue.shift(), box = $('notice'), sp = $('noticeTxt');
  sp.textContent = text; box.hidden = false;
  const W = box.querySelector('.nr').clientWidth, w = sp.offsetWidth;
  const done = () => { box.hidden = true; showing = false; setTimeout(nextNotice, 600); };
  if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) { sp.style.transform = 'none'; setTimeout(done, 6000); return; }
  const a = sp.animate([{ transform: 'translateX(' + W + 'px)' }, { transform: 'translateX(' + (-w) + 'px)' }], { duration: (W + w) / 70 * 1000, easing: 'linear', fill: 'forwards' });
  a.onfinish = done;
}
function notice(text) { add('notice', { text }); if (queue.length < 4) queue.push(text); nextNotice(); }
setInterval(() => { $('notice').style.visibility = ctx().battle ? 'hidden' : ''; }, 400);   // 战斗中不挡画面

// ---------- 模拟：路人发言 / 回话 / 公告 ----------
const fill = t => t.replace('{weather}', ctx().weather).replace('{name}', ctx().name).replace('{bot}', pick(D.bots).name);
function simulateTalk() {
  const b = pick(D.bots); add('world', { from: b.name, cls: b.cls, lv: b.lv, text: fill(pick(D.lines)) });
  timers.push(setTimeout(simulateTalk, rnd(6000, 15000)));
}
function simulateReply(text) {
  if (Math.random() > .6) return;
  let pool = D.fallback; for (const k in D.replies) if (new RegExp(k).test(text)) { pool = D.replies[k]; break; }
  timers.push(setTimeout(() => { const b = pick(D.bots); add('world', { from: b.name, cls: b.cls, lv: b.lv, text: pick(pool) }); }, rnd(1500, 4500)));
}
function simulateNotice() { notice(fill(pick(D.notices))); timers.push(setTimeout(simulateNotice, rnd(70000, 130000))); }

function reset(serverName, roleName) {
  timers.forEach(clearTimeout); timers = []; hist.length = 0; queue.length = 0; lastSend = 0; lastText = '';
  add('system', { text: '欢迎来到 ' + serverName + '，' + roleName });
  add('system', { text: '点击地面行走，按住可以拖着走；点击人物对话' });
  add('world', { from: '青锋', cls: 'm_spear', lv: 44, text: '新来的少侠，有问题就在世界频道问～' });
  timers.push(setTimeout(() => notice('欢迎来到归秦！新服已开，愿各位少侠仗剑江湖。'), 5000));
  timers.push(setTimeout(simulateTalk, 7000));
  timers.push(setTimeout(simulateNotice, rnd(50000, 80000)));
}

window.CHAT = {
  add, notice, reset, open, close, bind(fn) { ctx = fn; },
  sys(text) { add('system', { text }); }
};
drawMini();
})();
