// ============================================================
// 聊天：综合 / 世界 / 帮派 / 队伍 / 私聊 / 系统 六个页签，加上表情、物品链接、点名字弹菜单、滚动的系统公告条。
// 全是前端模拟：路人发言、帮派成员、队友、私聊对象的回话、公告都是定时器生成的，没有联网。
// 接真服务器时：发送处（send）改成发请求；收到别人的消息时调用 CHAT.add(频道, {...})；
// 公告调用 CHAT.notice(文本)；组队 / 入帮 / 加好友这些状态由服务器给。simulate* 全部删掉即可。
// 游戏本体通过 CHAT.bind() 提供：当前角色、天气、是否在战斗、背包里的物品、物品详情窗口。
// ============================================================
(function () {
'use strict';
const D = window.DATA.chat, CLS = window.DATA.classes, ITEMS = window.DATA.items;
const $ = id => document.getElementById(id);
const rnd = (a, b) => a + Math.random() * (b - a), pick = a => a[Math.floor(Math.random() * a.length)];
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
const botOf = name => D.bots.find(b => b.name === name);
const hhmm = t => { const d = new Date(t); return String(d.getHours()).padStart(2, '0') + ':' + String(d.getMinutes()).padStart(2, '0'); };

// 频道：tag 是消息前面的标签，tab 是它归在哪个页签
const CH = {
  world:  { tag: '世界', color: '#7ad7ff', tab: 'world' },
  guild:  { tag: '帮派', color: '#7fe07f', tab: 'guild' },
  party:  { tag: '队伍', color: '#ffb36b', tab: 'party' },
  pm:     { tag: '私聊', color: '#ff8ad4', tab: 'pm' },
  system: { tag: '系统', color: '#ff7a5a', tab: 'system' },
  notice: { tag: '公告', color: '#ffb020', tab: 'system' }
};
const TABS = [['all', '综合'], ['world', '世界'], ['guild', '帮派'], ['party', '队伍'], ['pm', '私聊'], ['system', '系统']];
const COOL = { world: D.cooldown, guild: 1.5, party: 1.5, pm: 1 };

// ---------- 表情：全是 SVG 小脸，不依赖系统字体 ----------
const FACE = (extra, fill) => '<svg class="em" viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="10.5" fill="' + (fill || '#ffd24a') + '" stroke="#b9750d" stroke-width="1.2"/>' + extra + '</svg>';
const S_ = 'fill="none" stroke="#5a2d0a" stroke-width="1.6" stroke-linecap="round"';
const EMOTE = {
  '笑': FACE('<path d="M7 10q1.5-2.2 3 0M14 10q1.5-2.2 3 0" ' + S_ + '/><path d="M8 14.5q4 4 8 0" ' + S_ + '/>'),
  '大笑': FACE('<path d="M6.5 10q2-2.6 4 0M13.5 10q2-2.6 4 0" ' + S_ + '/><path d="M7 13.5h10a5 5 0 0 1-10 0z" fill="#7a2a0a"/><path d="M9 17q3 1.6 6 0" fill="none" stroke="#ff8a7a" stroke-width="1.6" stroke-linecap="round"/>'),
  '哭': FACE('<circle cx="8.5" cy="10" r="1.3" fill="#5a2d0a"/><circle cx="15.5" cy="10" r="1.3" fill="#5a2d0a"/><path d="M8.5 12.5v4M15.5 12.5v4" stroke="#4aa8ff" stroke-width="1.8" stroke-linecap="round"/><path d="M8.5 18q3.5-3.2 7 0" ' + S_ + '/>'),
  '怒': FACE('<path d="M6.5 8.5l4 1.6M17.5 8.5l-4 1.6" ' + S_ + '/><circle cx="8.8" cy="11.5" r="1.2" fill="#5a2d0a"/><circle cx="15.2" cy="11.5" r="1.2" fill="#5a2d0a"/><path d="M8.5 17.5q3.5-3.2 7 0" ' + S_ + '/>', '#ff8a5a'),
  '惊': FACE('<circle cx="8.5" cy="10" r="2.4" fill="#fff" stroke="#5a2d0a"/><circle cx="15.5" cy="10" r="2.4" fill="#fff" stroke="#5a2d0a"/><circle cx="8.5" cy="10" r="1" fill="#5a2d0a"/><circle cx="15.5" cy="10" r="1" fill="#5a2d0a"/><ellipse cx="12" cy="16.5" rx="2" ry="2.6" fill="#7a2a0a"/>'),
  '酷': FACE('<path d="M4.5 9h6.5v3.5a3 3 0 0 1-3 2.5h-.5a3 3 0 0 1-3-2.5zM13 9h6.5v3.5a3 3 0 0 1-3 2.5h-.5a3 3 0 0 1-3-2.5z" fill="#2a2a3a"/><path d="M10.5 10h3" stroke="#2a2a3a" stroke-width="1.5"/><path d="M8.5 17q3.5 2.6 7 0" ' + S_ + '/>'),
  '晕': FACE('<path d="M6.5 8.5l4 4M10.5 8.5l-4 4M13.5 8.5l4 4M17.5 8.5l-4 4" ' + S_ + '/><path d="M8 17q1.3-1.8 2.7 0t2.6 0t2.7 0" ' + S_ + '/>'),
  '睡': FACE('<path d="M6.5 11q1.7 1.6 3.4 0M14 11q1.7 1.6 3.4 0" ' + S_ + '/><ellipse cx="12" cy="16.3" rx="1.6" ry="1.2" fill="#7a2a0a"/><path d="M16 4.5h3.6l-3.6 4h3.6" fill="none" stroke="#3a78c8" stroke-width="1.5" stroke-linejoin="round"/>'),
  '害羞': FACE('<path d="M7 10q1.5-2.2 3 0M14 10q1.5-2.2 3 0" ' + S_ + '/><circle cx="7" cy="14" r="2" fill="#ff7f9a" opacity=".7"/><circle cx="17" cy="14" r="2" fill="#ff7f9a" opacity=".7"/><path d="M9.5 16q2.5 2 5 0" ' + S_ + '/>'),
  '心': '<svg class="em" viewBox="0 0 24 24" aria-hidden="true"><path d="M12 21C5 15.5 2.5 12 2.5 8.4A5 5 0 0 1 12 6.2a5 5 0 0 1 9.5 2.2C21.5 12 19 15.5 12 21z" fill="#ff4d6d" stroke="#b81d3d" stroke-width="1.2" stroke-linejoin="round"/></svg>',
  '赞': '<svg class="em" viewBox="0 0 24 24" aria-hidden="true"><path d="M3 11h4v10H3zM8 11l4-8c1.8 0 2.7 1.2 2.4 3L14 9h5.2a1.9 1.9 0 0 1 1.9 2.3l-1.4 7.4A2.5 2.5 0 0 1 17.3 20H8z" fill="#ffd24a" stroke="#b9750d" stroke-width="1.2" stroke-linejoin="round"/></svg>'
};
const EMOTES = Object.keys(EMOTE);
const itemByName = {}; for (const id in ITEMS) itemByName[ITEMS[id].name] = id;
const qualColor = id => { const it = ITEMS[id]; return it.type === 'equip' ? '#ffcf5a' : it.type === 'potion' ? '#8fe08f' : '#e8e8ff'; };
function rich(text) {                                   // 文本 → 安全 HTML：先转义，再把 [笑] / [精铁剑] 换成表情和物品链接
  return esc(text).replace(/\[([^\[\]]{1,8})\]/g, (m, n) => EMOTE[n] ? EMOTE[n] : itemByName[n] ? '<a class="il" data-item="' + itemByName[n] + '" style="color:' + qualColor(itemByName[n]) + '">[' + n + ']</a>' : m);
}

let ctx = () => ({ name: '', cls: 'm_sword', lv: 1, weather: '晴', battle: false, bagItems: () => [], showItem() {} });
const hist = []; let tab = 'all', isOpen = false, sendCh = 'world', timers = [], uid = 0;
const lastAt = {}, lastTxt = {};
const unread = {}; let guild = null, party = [], pmPeer = null; const friends = new Set(), peers = [];
const inTab = (m, t) => t === 'all' || (t === 'pm' ? m.ch === 'pm' && m.peer === pmPeer : CH[m.ch].tab === t);

// ---------- 渲染 ----------
function lineHTML(m, mini) {
  const c = CH[m.ch]; let h = '<span style="color:' + c.color + '">[' + c.tag + ']</span> ';
  const nameA = (n, me) => mini ? '<span class="cn' + (me ? ' me' : '') + '">' + esc(n) + '</span>' : '<a class="cn' + (me ? ' me' : '') + '" data-from="' + esc(n) + '">' + esc(n) + '</a>';
  if (m.ch === 'notice' || m.ch === 'system') return h + esc(m.text);
  if (m.ch === 'pm') h += m.self ? '你对 ' + nameA(m.peer) + ' 说：' : nameA(m.from) + ' 对你说：';
  else {
    h += nameA(m.from, m.self);
    if (!mini) h += '<i class="cl">' + (CLS[m.cls] ? CLS[m.cls].name : '') + ' ' + m.lv + '级</i>';
    h += '：';
  }
  return h + rich(m.text);
}
function drawMini() {
  const log = $('chatLog'); log.innerHTML = '';
  hist.slice(-3).forEach(m => { const d = document.createElement('div'); d.innerHTML = lineHTML(m, true); log.appendChild(d); });
  const n = Object.values(unread).reduce((a, b) => a + b, 0); $('chatDot').hidden = !n;
}
function drawList(stick) {
  const el = $('chatList'), near = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  const ms = hist.filter(m => inTab(m, tab));
  el.innerHTML = ms.map(m => '<div class="cm ' + (m.ch === 'notice' ? 'n' : '') + '">' + lineHTML(m) + '<small>' + hhmm(m.t) + '</small></div>').join('') || '<p class="cempty">' + emptyText() + '</p>';
  if (stick || near) el.scrollTop = el.scrollHeight;
}
function emptyText() {
  if (tab === 'pm') return pmPeer ? '和 ' + pmPeer + ' 还没有聊过，说点什么吧。' : '点聊天里的名字，选“私聊”，就能悄悄说话。';
  if (tab === 'guild') return guild ? '帮派频道里还没有人说话。' : '';
  if (tab === 'party') return party.length ? '队伍里还没有人说话。' : '';
  return '还没有消息。';
}
function drawTabs() {
  $('chatTabs').innerHTML = TABS.map(t => '<button type="button" class="ctab' + (t[0] === tab ? ' on' : '') + '" data-tab="' + t[0] + '">' + t[1] + (unread[t[0]] ? '<u></u>' : '') + '</button>').join('');
}
function myChannels() { const a = ['world']; if (guild) a.push('guild'); if (party.length) a.push('party'); return a; }
function curSend() {                                   // 当前输入框发到哪个频道
  if (tab === 'pm') return pmPeer ? 'pm' : null;
  if (tab === 'system') return null;
  if (tab === 'world' || tab === 'guild' || tab === 'party') return (tab === 'guild' && !guild) || (tab === 'party' && !party.length) ? null : tab;
  return myChannels().includes(sendCh) ? sendCh : 'world';
}
function drawStrip() {                                  // 列表上面的一条：帮派信息 / 队伍成员 / 私聊对象
  const s = $('chatStrip'); let h = '';
  if (tab === 'guild') {
    h = guild ? '<span>「' + esc(guild.name) + '」　成员 ' + (guild.members.length + 1) + ' 人</span><button type="button" data-do="guildLeave">退出帮派</button>'
      : '<div class="card"><h4>你还没有加入帮派</h4><p>帮派频道只有帮派成员能看到。（演示：直接加入一个现成的帮派）</p><button type="button" class="tbtn" data-do="guildJoin">加入「' + esc(D.guildName) + '」</button></div>';
  } else if (tab === 'party') {
    h = party.length ? '<span>队友：' + party.map(n => '<b>' + esc(n) + '</b>').join('、') + '</span><button type="button" data-do="partyLeave">离开队伍</button>'
      : '<div class="card"><h4>你还没有队伍</h4><p>点世界频道里的名字，选“邀请组队”；或者先在世界频道喊一声。</p><button type="button" class="tbtn" data-do="partyShout">在世界频道招募</button></div>';
  } else if (tab === 'pm') {
    const all = peers.slice(); friends.forEach(f => { if (!all.includes(f)) all.push(f); });
    h = all.length ? '<div class="peers">' + all.map(n => '<button type="button" class="peer' + (n === pmPeer ? ' on' : '') + '" data-peer="' + esc(n) + '">' + (friends.has(n) ? '★ ' : '') + esc(n) + (unread['pm:' + n] ? '<u></u>' : '') + '</button>').join('') + '</div>' : '';
  }
  s.innerHTML = h; s.hidden = !h;
}
function drawInput() {
  const ch = curSend(), ro = !ch;
  $('chatInput').disabled = ro; $('chatSend').disabled = ro; $('chatEmo').disabled = ro; $('chatItem').disabled = ro;
  $('chatInput').placeholder = tab === 'system' ? '系统频道只能看，不能发言' : ro ? (tab === 'pm' ? '先选一个私聊对象' : tab === 'guild' ? '加入帮派后才能说话' : '组队后才能说话') : '说点什么…（最多 ' + D.maxLen + ' 字）';
  const pill = $('chatCh'); pill.textContent = ch === 'pm' ? '私聊' : ch ? CH[ch].tag : '—'; pill.style.color = ch ? CH[ch].color : ''; pill.style.borderColor = ch ? CH[ch].color : '';
  pill.disabled = !(tab === 'all' && myChannels().length > 1);
}
function redraw(stick) { drawTabs(); drawStrip(); drawInput(); drawList(stick); drawMini(); }

function add(ch, o) {
  const m = { id: ++uid, ch, from: o.from || '', cls: o.cls, lv: o.lv, text: o.text, self: !!o.self, t: Date.now(), peer: o.peer };
  hist.push(m); while (hist.length > D.history) hist.shift();
  const home = CH[ch].tab, shownNow = isOpen && (tab === 'all' || inTab(m, tab));
  if (!shownNow && !m.self) { unread[home] = (unread[home] || 0) + 1; if (ch === 'pm') unread['pm:' + m.peer] = (unread['pm:' + m.peer] || 0) + 1; }
  if (isOpen) { drawTabs(); if (tab === 'pm') drawStrip(); drawList(m.self); }
  drawMini();
}
const sysMsg = text => add('system', { text });

// ---------- 面板 ----------
function open(t) {
  isOpen = true; $('chatP').hidden = false; $('chatHint').textContent = ''; $('chatPop').hidden = true; $('chatMenu').hidden = true;
  if (t) tab = t; if (tab === 'pm' && !pmPeer) pmPeer = peers[0] || [...friends][0] || null;
  clearUnread(); redraw(true);
}
function close() { isOpen = false; $('chatP').hidden = true; $('chatInput').blur(); $('chatPop').hidden = true; $('chatMenu').hidden = true; }
function clearUnread() { unread[tab] = 0; if (tab === 'pm' && pmPeer) unread['pm:' + pmPeer] = 0; if (tab === 'all') for (const k in unread) unread[k] = 0; }
['pointerdown', 'pointermove', 'pointerup', 'click'].forEach(ev => $('chatP').addEventListener(ev, e => e.stopPropagation()));
$('chat').addEventListener('click', e => { e.stopPropagation(); open(); });
$('chatDim').addEventListener('click', close); $('chatX').addEventListener('click', close);
$('chatTabs').addEventListener('click', e => {
  const b = e.target.closest('[data-tab]'); if (!b) return;
  tab = b.dataset.tab; if (tab === 'pm' && !pmPeer) pmPeer = peers[0] || [...friends][0] || null;
  clearUnread(); $('chatPop').hidden = true; $('chatMenu').hidden = true; redraw(true);
});
$('chatCh').addEventListener('click', () => { const a = myChannels(), i = a.indexOf(curSend()); sendCh = a[(i + 1) % a.length]; drawInput(); });

// 帮派 / 队伍 / 私聊条上的按钮
$('chatStrip').addEventListener('click', e => {
  const p = e.target.closest('[data-peer]'); if (p) { pmPeer = p.dataset.peer; unread['pm:' + pmPeer] = 0; redraw(true); return; }
  const a = e.target.closest('[data-do]'); if (!a) return;
  ({ guildJoin, guildLeave, partyLeave, partyShout })[a.dataset.do]();
});
const partyChanged = () => window.dispatchEvent(new Event('chat-party'));
function guildJoin() {
  const mem = D.guildMembers.slice(); guild = { name: D.guildName, members: mem };
  sysMsg('你加入了帮派「' + guild.name + '」'); add('guild', { from: mem[0], cls: botOf(mem[0]).cls, lv: botOf(mem[0]).lv, text: '欢迎新人入帮～[心] 帮派公告：今晚八点帮战，别迟到哦！' });
  sendCh = 'guild'; timers.push(setTimeout(simulateGuild, rnd(8000, 14000))); redraw(true);
}
function guildLeave() { if (!guild) return; sysMsg('你退出了帮派「' + guild.name + '」'); guild = null; if (sendCh === 'guild') sendCh = 'world'; redraw(true); }
function partyLeave() { if (!party.length) return; sysMsg('你离开了队伍'); party = []; partyChanged(); if (sendCh === 'party') sendCh = 'world'; redraw(true); }
function partyShout() { sendTo('world', '组队！' + ctx().lv + ' 级 ' + CLS[ctx().cls].name + '，来个队友[笑]'); tab = 'world'; redraw(true); }

// ---------- 发送 ----------
function sendTo(ch, text) {
  const me = ctx(), hint = $('chatHint'); text = text.trim().slice(0, D.maxLen); if (!text) return false;
  const wait = Math.ceil(COOL[ch] - (Date.now() - (lastAt[ch] || 0)) / 1000);
  if (wait > 0) { hint.textContent = '发言太快了，请 ' + wait + ' 秒后再试'; return false; }
  if (text === lastTxt[ch]) { hint.textContent = '请不要重复发言'; return false; }
  hint.textContent = ''; lastAt[ch] = Date.now(); lastTxt[ch] = text;
  if (ch === 'pm') { add('pm', { from: me.name, peer: pmPeer, text, self: true }); simulatePmReply(pmPeer, text); }
  else { add(ch, { from: me.name, cls: me.cls, lv: me.lv, text, self: true }); ({ world: simulateReply, guild: simulateGuildReply, party: simulatePartyReply })[ch](text); }
  return true;
}
$('chatForm').addEventListener('submit', e => {
  e.preventDefault(); const ch = curSend(); if (!ch) return;
  if (sendTo(ch, $('chatInput').value)) $('chatInput').value = '';
});

// ---------- 表情 / 物品选择 ----------
function popup(kind) {
  const p = $('chatPop'); if (!p.hidden && p.dataset.kind === kind) { p.hidden = true; return; }
  p.dataset.kind = kind; p.hidden = false;
  if (kind === 'emo') p.innerHTML = '<div class="emg">' + EMOTES.map(n => '<button type="button" data-emo="' + n + '" aria-label="' + n + '">' + EMOTE[n] + '</button>').join('') + '</div>';
  else {
    const ids = ctx().bagItems();
    p.innerHTML = ids.length ? '<div class="itg">' + ids.map(id => '<button type="button" data-itemid="' + id + '" style="color:' + qualColor(id) + '">[' + ITEMS[id].name + ']</button>').join('') + '</div>' : '<p class="cempty" style="margin:14px">背包和身上都是空的。</p>';
  }
}
$('chatEmo').addEventListener('click', () => popup('emo')); $('chatItem').addEventListener('click', () => popup('item'));
$('chatPop').addEventListener('click', e => {
  const t = e.target.closest('[data-emo],[data-itemid]'); if (!t) return;
  const ins = t.dataset.emo ? '[' + t.dataset.emo + ']' : '[' + ITEMS[t.dataset.itemid].name + ']', inp = $('chatInput');
  if (inp.value.length + ins.length <= D.maxLen) inp.value += ins; inp.focus(); $('chatPop').hidden = t.dataset.itemid ? true : false;
});

// ---------- 点名字弹菜单 / 点物品看详情 ----------
let menuFor = null;
$('chatList').addEventListener('click', e => {
  const it = e.target.closest('[data-item]'); if (it) { ctx().showItem(it.dataset.item); return; }
  const n = e.target.closest('[data-from]'); if (!n || n.dataset.from === ctx().name) return;
  menuFor = n.dataset.from; const r = n.getBoundingClientRect(), st = $('stage').getBoundingClientRect(), k = st.width / 720, m = $('chatMenu');
  const isF = friends.has(menuFor), inP = party.includes(menuFor);
  m.innerHTML = '<b>' + esc(menuFor) + '</b><button data-m="pm">私聊</button><button data-m="friend">' + (isF ? '已是好友' : '加好友') + '</button><button data-m="party">' + (inP ? '已在队伍' : '邀请组队') + '</button><button data-m="info">查看</button>';
  m.hidden = false; m.style.left = Math.min(520, Math.max(10, (r.left - st.left) / k)) + 'px'; m.style.top = Math.max(120, (r.bottom - st.top) / k + 4) + 'px';
  e.stopPropagation();
});
$('chatP').addEventListener('click', e => { if (!e.target.closest('#chatMenu') && !e.target.closest('[data-from]')) $('chatMenu').hidden = true; });
$('chatMenu').addEventListener('click', e => {
  const b = e.target.closest('[data-m]'); if (!b) return; const who = menuFor; $('chatMenu').hidden = true;
  ({ pm: () => { if (!peers.includes(who)) peers.push(who); pmPeer = who; tab = 'pm'; unread['pm:' + who] = 0; redraw(true); $('chatInput').focus(); },
     friend: () => { if (!friends.has(who)) { friends.add(who); sysMsg('已添加 ' + who + ' 为好友'); drawStrip(); } },
     party: () => invite(who),
     info: () => { const b2 = botOf(who) || {}; ctx().showInfo(who, b2.cls, b2.lv); } })[b.dataset.m]();
});

// ---------- 组队邀请 / 好友 ----------
function invite(who) {
  if (party.includes(who)) return; if (party.length >= 4) { sysMsg('队伍已满（最多 5 人）'); return; }
  sysMsg('已向 ' + who + ' 发出组队邀请');
  timers.push(setTimeout(() => {
    if (Math.random() < .75) {
      party.push(who); sysMsg(who + ' 加入了队伍'); add('party', { from: who, cls: (botOf(who) || {}).cls, lv: (botOf(who) || {}).lv, text: '来啦～[笑] 往哪儿走？' });
      partyChanged(); sendCh = 'party'; if (party.length === 1) timers.push(setTimeout(simulateParty, rnd(7000, 12000)));
    } else sysMsg(pick(D.partyDecline).replace('{bot}', who));
    if (isOpen) redraw();
  }, rnd(1500, 4000)));
}

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

// ---------- 模拟：路人 / 帮派 / 队友 / 私聊对象 ----------
const fill = t => t.replace('{weather}', ctx().weather).replace('{name}', ctx().name).replace('{bot}', pick(D.bots).name);
const say = (ch, name, text) => { const b = botOf(name) || {}; add(ch, { from: name, cls: b.cls, lv: b.lv, text, peer: ch === 'pm' ? name : undefined }); };
function pool(text, table) { for (const k in table) if (new RegExp(k).test(text)) return table[k]; return D.fallback; }
function simulateTalk() { const b = pick(D.bots); say('world', b.name, fill(pick(D.lines))); timers.push(setTimeout(simulateTalk, rnd(6000, 15000))); }
function simulateReply(text) { if (Math.random() < .6) timers.push(setTimeout(() => say('world', pick(D.bots).name, pick(pool(text, D.replies))), rnd(1500, 4500))); }
function simulateGuild() { if (!guild) return; say('guild', pick(guild.members), fill(pick(D.guildLines))); timers.push(setTimeout(simulateGuild, rnd(10000, 24000))); }
function simulateGuildReply(text) { if (guild && Math.random() < .75) timers.push(setTimeout(() => guild && say('guild', pick(guild.members), pick(pool(text, D.replies))), rnd(1500, 4000))); }
function simulateParty() { if (!party.length) return; say('party', pick(party), fill(pick(D.partyLines))); timers.push(setTimeout(simulateParty, rnd(9000, 20000))); }
function simulatePartyReply(text) { if (party.length && Math.random() < .8) timers.push(setTimeout(() => party.length && say('party', pick(party), pick(pool(text, D.replies))), rnd(1200, 3500))); }
function simulatePmReply(peer, text) {
  if (Math.random() < .9) timers.push(setTimeout(() => { say('pm', peer, pick(pool(text, D.replies).concat(D.pmReplies))); if (isOpen && tab === 'pm' && pmPeer === peer) unread['pm:' + peer] = 0; }, rnd(1200, 3200)));
}
function simulateWhisper() {                      // 偶尔有陌生人主动私聊你一句
  const b = pick(D.bots); if (!peers.includes(b.name)) peers.push(b.name);
  say('pm', b.name, fill(pick(D.pmGreets)).replace('{bot}', b.name)); if (isOpen) drawStrip();
}
function simulateNotice() { notice(fill(pick(D.notices))); timers.push(setTimeout(simulateNotice, rnd(70000, 130000))); }

function reset(serverName, roleName) {
  timers.forEach(clearTimeout); timers = []; hist.length = 0; queue.length = 0; peers.length = 0; friends.clear(); party = []; guild = null; pmPeer = null;
  for (const k in unread) delete unread[k]; for (const k in lastAt) delete lastAt[k]; for (const k in lastTxt) delete lastTxt[k]; sendCh = 'world'; tab = 'all';
  add('system', { text: '欢迎来到 ' + serverName + '，' + roleName });
  add('system', { text: '点击地面行走，按住可以拖着走；点击人物对话' });
  timers.push(setTimeout(() => notice('欢迎来到归秦！新服已开，愿各位少侠仗剑江湖。'), 5000));
  timers.push(setTimeout(simulateTalk, 7000));
  timers.push(setTimeout(simulateWhisper, rnd(25000, 40000)));
  timers.push(setTimeout(simulateNotice, rnd(50000, 80000)));
  drawMini(); if (isOpen) redraw(true);
}

window.CHAT = {
  add, notice, reset, open, close, bind(fn) { ctx = fn; },
  sys: sysMsg,
  party: () => party.slice(), inGuild: () => !!guild,
  friends: () => [...friends], peers: () => peers.slice()
};
drawMini();
})();
