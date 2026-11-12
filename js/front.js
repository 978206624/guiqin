// ============================================================
// 归秦 —— 进游戏之前的界面：登录 → 选区 → 角色列表 / 创建角色
// 纯前端演示：账号密码不校验、不保存、不上传。只把“角色列表”存在浏览器本地。
// ============================================================
(function () {
'use strict';
const D = window.DATA, AS = window.ASSETS, G = window.GAME;
const IMG = k => AS.img[k];
const $ = id => document.getElementById(id);
const ROLE_KEY = 'guiqin-roles-v1', MAX_ROLES = 3;

// ---------- 区服（演示数据）----------
const SERVERS = [
  { id: 's12', no: 12, name: '函谷关', status: 'new' },
  { id: 's11', no: 11, name: '骊山',   status: 'ok' },
  { id: 's10', no: 10, name: '阿房',   status: 'busy' },
  { id: 's9',  no: 9,  name: '云梦泽', status: 'ok' },
  { id: 's8',  no: 8,  name: '巴蜀',   status: 'busy' },
  { id: 's7',  no: 7,  name: '陇西',   status: 'ok' },
  { id: 's6',  no: 6,  name: '邯郸',   status: 'down' },
  { id: 's5',  no: 5,  name: '琅琊',   status: 'ok' },
  { id: 's4',  no: 4,  name: '大梁',   status: 'busy' },
  { id: 's3',  no: 3,  name: '郢都',   status: 'hot' },
  { id: 's2',  no: 2,  name: '临淄',   status: 'hot' },
  { id: 's1',  no: 1,  name: '咸阳',   status: 'hot' }
];
const STATUS = { new: '新服', ok: '流畅', busy: '繁忙', hot: '爆满', down: '维护' };
const GROUPS = [['rec', '推荐'], ['mine', '我的'], ['a', '1-6 区'], ['b', '7-12 区']];
const NAMES_A = ['云', '沈', '陆', '苏', '顾', '白', '叶', '楚', '燕', '秦', '江', '温', '谢', '萧'];
const NAMES_B = { m: ['中客', '长风', '惊鸿', '无咎', '临渊', '照夜', '停云', '归雁', '怀瑾', '行舟'], f: ['清歌', '晚照', '听雪', '疏影', '知微', '若水', '采薇', '映月', '流萤', '锦瑟'] };

// ---------- 本地数据：每个区的角色列表 ----------
let book = {};                              // { 区服 id: [ { id, name, cls } ] }，另有 last 记上次进的区
try { const t = localStorage.getItem(ROLE_KEY); if (t) book = JSON.parse(t) || {}; } catch (e) {}
const persist = () => { try { localStorage.setItem(ROLE_KEY, JSON.stringify(book)); } catch (e) {} };
const rolesOf = sid => book[sid] || [];
let account = '', server = null, picked = 0, group = 'rec';
let draft = { gender: 'm', cls: 'm_sword' };

// ---------- 切换界面 ----------
const SCREENS = ['fLogin', 'fServer', 'fRoles', 'fCreate'];
function show(id) {
  $('front').hidden = !id;
  SCREENS.forEach(s => { $(s).hidden = s !== id; });
  if (id === 'fServer') drawServers();
  if (id === 'fRoles') drawRoles();
  if (id === 'fCreate') drawCreate();
}
$('frontBg').src = IMG('login');
$('front').addEventListener('click', e => e.stopPropagation());
['pointerdown', 'pointermove', 'pointerup'].forEach(ev => $('front').addEventListener(ev, e => e.stopPropagation()));

// ---------- 登录 ----------
function login(guest) {
  const name = $('fAccount').value.trim();
  const err = $('fLoginErr');
  if (!guest && !name) { err.textContent = '请输入账号，或者点“游客试玩”'; $('fAccount').focus(); return; }
  if (!$('fAgree').checked) { err.textContent = '请先勾选同意用户协议'; return; }
  err.textContent = '';
  account = guest ? '游客' : name;
  $('fPassword').value = '';                 // 密码用完即弃，不保存
  show('fServer');
}
$('fLoginForm').addEventListener('submit', e => { e.preventDefault(); login(false); });
$('fGuest').addEventListener('click', () => login(true));

// ---------- 选区 ----------
function serverBtn(s) {
  const n = rolesOf(s.id).length;
  return '<button type="button" class="srv ' + s.status + '" data-srv="' + s.id + '"' + (s.status === 'down' ? ' disabled' : '') + '>' +
    '<i></i><span>秦 ' + s.no + ' 区　' + s.name + '</span><em>' + STATUS[s.status] + '</em>' + (n ? '<b>' + n + ' 个角色</b>' : '') + '</button>';
}
function drawServers() {
  $('fWho').textContent = account;
  $('fTabs').innerHTML = GROUPS.map(g => '<button type="button" class="tab' + (g[0] === group ? ' on' : '') + '" data-tab="' + g[0] + '">' + g[1] + '</button>').join('');
  let list;
  if (group === 'rec') list = SERVERS.filter(s => s.status === 'new' || s.status === 'ok').slice(0, 4);
  else if (group === 'mine') list = SERVERS.filter(s => rolesOf(s.id).length);
  else if (group === 'a') list = SERVERS.filter(s => s.no <= 6);
  else list = SERVERS.filter(s => s.no > 6);
  $('fList').innerHTML = list.length ? list.map(serverBtn).join('') : '<p class="fempty">这个账号还没有在任何区创建角色。</p>';
  const lastS = SERVERS.find(s => s.id === book.last);
  $('fLast').innerHTML = lastS ? '<small>上次登录</small>' + serverBtn(lastS) : '<small>上次登录</small><p class="fempty">还没有进过任何区。</p>';
}
$('fServer').addEventListener('click', e => {
  const t = e.target.closest('[data-tab]'); if (t) { group = t.dataset.tab; drawServers(); return; }
  const b = e.target.closest('[data-srv]'); if (!b || b.disabled) return;
  server = SERVERS.find(s => s.id === b.dataset.srv); book.last = server.id; persist();
  picked = 0; show(rolesOf(server.id).length ? 'fRoles' : 'fCreate');
});
$('fBackLogin').addEventListener('click', () => show('fLogin'));

// ---------- 角色列表 ----------
function drawRoles() {
  const roles = rolesOf(server.id);
  if (picked >= roles.length) picked = Math.max(0, roles.length - 1);
  $('fSrvName').textContent = '秦 ' + server.no + ' 区 · ' + server.name;
  let h = '';
  for (let i = 0; i < MAX_ROLES; i++) {
    const r = roles[i];
    h += r ? '<button type="button" class="slot' + (i === picked ? ' on' : '') + '" data-slot="' + i + '"><img alt="" src="' + IMG('head_' + r.cls) + '"><span>' + r.name + '</span><small>' + G.peekLevel(r.id) + ' 级 ' + D.classes[r.cls].name + '</small></button>'
           : '<button type="button" class="slot empty" data-new="1"><i>＋</i><span>创建角色</span><small>空栏位</small></button>';
  }
  $('fSlots').innerHTML = h;
  const r = roles[picked];
  $('fConfirmDel').hidden = true;
  if (r) {
    const c = D.classes[r.cls];
    $('fRoleArt').src = IMG('cls_' + r.cls); $('fRoleArt').hidden = false;
    $('fRoleName').textContent = r.name; $('fRoleInfo').textContent = G.peekLevel(r.id) + ' 级 · ' + c.name + ' · ' + (c.gender === 'm' ? '男' : '女');
    $('fEnter').disabled = false; $('fDel').hidden = false;
  } else {
    $('fRoleArt').hidden = true; $('fRoleName').textContent = '还没有角色'; $('fRoleInfo').textContent = '点下面的空栏位创建一个';
    $('fEnter').disabled = true; $('fDel').hidden = true;
  }
}
$('fSlots').addEventListener('click', e => {
  const s = e.target.closest('[data-slot]'); if (s) { picked = +s.dataset.slot; drawRoles(); return; }
  if (e.target.closest('[data-new]')) show('fCreate');
});
$('fBackServer').addEventListener('click', () => show('fServer'));
$('fDel').addEventListener('click', () => { $('fConfirmDel').hidden = false; $('fDelName').textContent = rolesOf(server.id)[picked].name; });
$('fDelNo').addEventListener('click', () => { $('fConfirmDel').hidden = true; });
$('fDelYes').addEventListener('click', () => {
  const roles = rolesOf(server.id), r = roles[picked];
  G.wipe(r.id); roles.splice(picked, 1); book[server.id] = roles; persist(); picked = 0; drawRoles();
});
$('fEnter').addEventListener('click', () => {
  const r = rolesOf(server.id)[picked]; if (!r) return;
  show(null); G.enter(r, '秦 ' + server.no + ' 区');
});

// ---------- 创建角色 ----------
function randomName() { const g = D.classes[draft.cls].gender; return NAMES_A[Math.floor(Math.random() * NAMES_A.length)] + NAMES_B[g][Math.floor(Math.random() * NAMES_B[g].length)]; }
function drawCreate() {
  const ids = Object.keys(D.classes).filter(k => D.classes[k].gender === draft.gender);
  if (!ids.includes(draft.cls)) draft.cls = ids[0];
  const c = D.classes[draft.cls];
  $('fGender').innerHTML = [['m', '男'], ['f', '女']].map(g => '<button type="button" class="tab' + (g[0] === draft.gender ? ' on' : '') + '" data-gender="' + g[0] + '">' + g[1] + '</button>').join('');
  $('fClasses').innerHTML = ids.map(id => '<button type="button" class="cls' + (id === draft.cls ? ' on' : '') + '" data-cls="' + id + '"><img alt="" src="' + IMG('head_' + id) + '"><span>' + D.classes[id].name + '</span></button>').join('');
  $('fNewArt').src = IMG('cls_' + draft.cls);
  $('fClsName').textContent = c.name; $('fClsRole').textContent = c.role; $('fClsDesc').textContent = c.desc;
  $('fStats').innerHTML = Object.keys(c.stats).map(k => '<div><span>' + k + '</span><i><b style="width:' + c.stats[k] * 20 + '%"></b></i></div>').join('');
  $('fNewErr').textContent = '';
  $('fBackRoles').textContent = rolesOf(server.id).length ? '返回' : '换区';
}
$('fCreate').addEventListener('click', e => {
  const g = e.target.closest('[data-gender]'); if (g) { draft.gender = g.dataset.gender; drawCreate(); return; }
  const c = e.target.closest('[data-cls]'); if (c) { draft.cls = c.dataset.cls; drawCreate(); }
});
$('fDice').addEventListener('click', () => { $('fNewName').value = randomName(); $('fNewErr').textContent = ''; });
$('fBackRoles').addEventListener('click', () => show(rolesOf(server.id).length ? 'fRoles' : 'fServer'));
$('fCreateForm').addEventListener('submit', e => {
  e.preventDefault();
  const name = $('fNewName').value.trim(), roles = rolesOf(server.id), err = $('fNewErr');
  if (roles.length >= MAX_ROLES) { err.textContent = '每个区最多创建 ' + MAX_ROLES + ' 个角色'; return; }
  if (name.length < 2 || name.length > 6) { err.textContent = '名字要 2 到 6 个字'; return; }
  if (roles.some(r => r.name === name)) { err.textContent = '这个名字已经用过了'; return; }
  roles.push({ id: server.id + '-' + Date.now().toString(36), name, cls: draft.cls });
  book[server.id] = roles; persist(); picked = roles.length - 1; $('fNewName').value = '';
  show('fRoles');
});

// 游戏里“切换角色”会回到角色列表
window.FRONT = { showRoles() { if (server) show('fRoles'); else show('fLogin'); }, showLogin() { show('fLogin'); } };
show('fLogin');
})();
