// ============================================================
// 临安夜市 —— 游戏逻辑
// 结构：工具 → 存档与属性 → 界面(HUD/弹窗) → 城镇 → 任务 → 战斗
// 坐标一律用 720x1280 的“设计稿像素”。素材通过 IMG(key) 取地址。
// ============================================================
(function () {
'use strict';
const D = window.DATA, AS = window.ASSETS;
const IMG = k => AS.img[k];
const $ = id => document.getElementById(id);
const stage = $('stage'), fit = $('fit'), world = $('world');
const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches;
const sleep = ms => new Promise(r => setTimeout(r, reduce ? Math.min(ms, 60) : ms));
const rnd = (a, b) => a + Math.random() * (b - a);
const fmt = n => Math.round(n).toLocaleString('en-US');

// ---------- 缩放：把 720x1280 的舞台塞进窗口 ----------
let scale = 1;
function layout() {
  const cs = getComputedStyle(document.body);
  const w = document.body.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
  const h = document.body.clientHeight;
  scale = Math.max(0.1, Math.min(w / 720, h / 1280));
  fit.style.width = 720 * scale + 'px'; fit.style.height = 1280 * scale + 'px';
  stage.style.transform = 'scale(' + scale + ')';
}
addEventListener('resize', layout); layout();
$('bgBattle').src = IMG('bg_battle');
$('hudFace').src = IMG('head_player');

// ============================================================ 存档与属性
let SAVE_KEY = 'guiqin-save:guest';      // 每个角色一份存档，进游戏时换成 guiqin-save:<角色 id>
let ROLE = { id: 'guest', name: '云中客', cls: 'm_sword' };
function newGame() {
  const s = JSON.parse(JSON.stringify(D.start));
  return { lv: s.lv, exp: 0, hp: null, mp: null, coin: s.coin, items: s.items, equip: s.equip, step: 0, cleared: [], gift: false, map: s.map, pos: null, visited: [] };
}
function load() {
  try { const t = localStorage.getItem(SAVE_KEY); if (t) { const s = JSON.parse(t); if (s && typeof s.lv === 'number') return s; } } catch (e) {}
  return null;
}
function save() { try { localStorage.setItem(SAVE_KEY, JSON.stringify(S)); } catch (e) {} }
let S = load() || newGame();

function stats() {
  const b = D.base, g = D.grow, L = S.lv;
  const st = { maxHp: b.hp + g.hp * L, maxMp: b.mp + g.mp * L, atk: b.atk + g.atk * L, def: b.def + g.def * L, spd: b.spd + g.spd * L };
  for (const slot in S.equip) {
    const it = D.items[S.equip[slot]]; if (!it) continue;
    st.atk += it.atk || 0; st.def += it.def || 0; st.maxHp += it.hp || 0; st.spd += it.spd || 0;
  }
  return st;
}
function petStats() {
  const b = D.base, g = D.grow, L = S.lv, p = D.pet;
  return { maxHp: Math.round((b.hp + g.hp * L) * p.hp), atk: Math.round((b.atk + g.atk * L) * p.atk), def: Math.round((b.def + g.def * L) * p.def), spd: Math.round((b.spd + g.spd * L) * p.spd) };
}
const expNeed = () => D.expNeed.base + D.expNeed.perLevel * S.lv;
function clampVitals() {
  const st = stats();
  S.hp = S.hp == null ? st.maxHp : Math.max(0, Math.min(st.maxHp, S.hp));
  S.mp = S.mp == null ? st.maxMp : Math.max(0, Math.min(st.maxMp, S.mp));
}
function addItem(id, n) { S.items[id] = (S.items[id] || 0) + n; if (S.items[id] <= 0) delete S.items[id]; }
// 发奖励；返回升了几级
function grant(r) {
  let ups = 0;
  if (r.coin) S.coin += r.coin;
  if (r.items) for (const id in r.items) addItem(id, r.items[id]);
  if (r.exp) {
    S.exp += r.exp;
    while (S.exp >= expNeed()) { S.exp -= expNeed(); S.lv++; ups++; }
    if (ups) { S.hp = null; S.mp = null; }
  }
  clampVitals(); save(); refresh();
  return ups;
}
function rewardText(r) {
  const parts = [];
  if (r.exp) parts.push('经验 +' + r.exp);
  if (r.coin) parts.push('银两 +' + r.coin);
  if (r.items) for (const id in r.items) parts.push(D.items[id].name + ' ×' + r.items[id]);
  return parts.join('，');
}

// ============================================================ 界面：HUD / 提示 / 聊天
function setBar(id, v, max) { const el = $(id); el.querySelector('i').style.width = Math.max(0, v / max * 100) + '%'; el.querySelector('b').textContent = Math.round(v) + '/' + max; }
function refresh() {
  clampVitals();
  const st = stats();
  $('hudLv').textContent = S.lv;
  if (B.active && B.player) { setBar('hp', B.player.hp, B.player.maxHp); setBar('mp', B.player.mp, B.player.maxMp); }
  else { setBar('hp', S.hp, st.maxHp); setBar('mp', S.mp, st.maxMp); }
  $('hudPow').textContent = fmt(powerOf(st));
  $('coin').textContent = '银两 ' + fmt(S.coin);
  $('expBar').style.width = (S.exp / expNeed() * 100) + '%';
  showQuest();
}
let toastT;
function toast(msg) { const t = $('toast'); t.textContent = msg; t.classList.add('show'); clearTimeout(toastT); toastT = setTimeout(() => t.classList.remove('show'), 2000); }
function say(html) { const log = $('chatLog'); const d = document.createElement('div'); d.innerHTML = html; log.appendChild(d); while (log.children.length > 3) log.removeChild(log.firstChild); }
const sys = t => say('<span style="color:#ff7a5a">[系统]</span> ' + t);

// ============================================================ 弹窗
let modalOnClose = null;
function openModal(title, html, onClose) { $('mTitle').textContent = title; $('mBody').innerHTML = html; $('modal').hidden = false; modalOnClose = onClose || null; }
function closeModal() { $('modal').hidden = true; const f = modalOnClose; modalOnClose = null; if (f) f(); }
$('mClose').addEventListener('click', closeModal);
$('modal').addEventListener('click', e => { e.stopPropagation(); if (e.target === $('modal')) closeModal(); });
// 弹窗里的所有按钮都用 data-act="动作:参数"
$('mBody').addEventListener('click', e => {
  const el = e.target.closest('[data-act]'); if (!el || el.disabled) return;
  const [act, arg] = el.dataset.act.split(':');
  ACTS[act] && ACTS[act](arg);
});
const itemImg = id => '<img alt="" src="' + IMG('item_' + D.items[id].icon) + '">';
function itemBonus(it) {
  const p = []; if (it.atk) p.push('攻击 +' + it.atk); if (it.def) p.push('防御 +' + it.def); if (it.hp && it.type === 'equip') p.push('气血 +' + it.hp); if (it.spd) p.push('速度 +' + it.spd);
  return p.join('　');
}

let bagSel = null;
function openBag() {
  const ids = Object.keys(S.items);
  if (!ids.includes(bagSel)) bagSel = ids[0] || null;
  let h = '<div class="grid">';
  ids.forEach(id => { h += '<button type="button" class="tile' + (id === bagSel ? ' sel' : '') + '" data-act="sel:' + id + '" aria-label="' + D.items[id].name + '">' + itemImg(id) + '<b>' + S.items[id] + '</b></button>'; });
  for (let i = ids.length; i < Math.max(10, Math.ceil(ids.length / 5) * 5); i++) h += '<div class="tile empty"></div>';
  h += '</div>';
  if (bagSel) {
    const it = D.items[bagSel];
    let btn = '';
    if (it.type === 'potion') btn = '<button type="button" class="tbtn" data-act="use:' + bagSel + '">使用</button>';
    if (it.type === 'equip') btn = '<button type="button" class="tbtn" data-act="equip:' + bagSel + '">装备</button>';
    h += '<div class="detail"><div><h5>' + it.name + '</h5><p>' + it.desc + '</p></div>' + btn + '</div>';
  } else h += '<div class="detail"><div><p>背包是空的。</p></div></div>';
  h += '<p class="center" style="margin:12px 0 0;color:var(--amber)">银两 ' + fmt(S.coin) + '</p>';
  openModal('背包', h);
}
function usePotion(id) {
  const it = D.items[id], st = stats();
  if (!S.items[id]) return false;
  if ((it.hp && S.hp >= st.maxHp && !it.mp) || (it.mp && S.mp >= st.maxMp && !it.hp)) { toast(it.hp ? '气血已满' : '法力已满'); return false; }
  addItem(id, -1); S.hp += it.hp || 0; S.mp += it.mp || 0; clampVitals(); save(); refresh();
  toast('使用' + it.name + (it.hp ? '，气血 +' + it.hp : '') + (it.mp ? '，法力 +' + it.mp : ''));
  return true;
}
let roleTab = 'attr';
const powerOf = st => Math.round(st.atk * 6 + st.def * 5 + st.maxHp / 2 + st.spd * 3);
function openRole() {
  const st = stats(), cls = D.classes[ROLE.cls], need = expNeed();
  const slotName = { weapon: '兵器', armor: '衣甲', charm: '饰品' };
  const bar = (cur, max, cls2) => '<div class="rbar ' + cls2 + '"><i style="width:' + Math.max(0, Math.min(100, cur / max * 100)) + '%"></i><span>' + Math.round(cur) + ' / ' + max + '</span></div>';
  let h = '<div class="who"><img alt="" src="' + IMG('cls_' + ROLE.cls) + '"><div><h5>' + ROLE.name + '</h5><p>' + cls.name + ' · ' + cls.role + ' · ' + (cls.gender === 'm' ? '男' : '女') + '</p>' +
    '<p>等级 <b class="gold">' + S.lv + '</b>　战力 <b class="gold">' + fmt(powerOf(st)) + '</b></p>' +
    '<p class="small">经验 ' + S.exp + ' / ' + need + '</p><div class="rbar exp"><i style="width:' + Math.min(100, S.exp / need * 100) + '%"></i></div></div></div>' +
    '<div class="rtabs"><button type="button" class="rtab' + (roleTab === 'attr' ? ' on' : '') + '" data-act="roleTab:attr">属性</button><button type="button" class="rtab' + (roleTab === 'equip' ? ' on' : '') + '" data-act="roleTab:equip">装备</button></div>';
  if (roleTab === 'attr') {
    // 每项属性拆成 基础 + 等级成长 + 装备，装备加成用绿色标出来
    const gear = { maxHp: 0, atk: 0, def: 0, spd: 0 };
    for (const slot in S.equip) { const it = D.items[S.equip[slot]]; if (!it) continue; gear.atk += it.atk || 0; gear.def += it.def || 0; gear.maxHp += it.hp || 0; gear.spd += it.spd || 0; }
    const base = D.base, grow = D.grow, L = S.lv;
    const line = (name, total, b, g2, e) => '<div class="arow"><span>' + name + '</span><b>' + total + '</b><small>基础 ' + b + ' + 成长 ' + g2 + (e ? ' <em>+ 装备 ' + e + '</em>' : '') + '</small></div>';
    h += '<div class="vit"><div><small>气血</small>' + bar(S.hp, st.maxHp, 'hp') + '</div><div><small>法力</small>' + bar(S.mp, st.maxMp, 'mp') + '</div></div>' +
      '<div class="alist">' +
      line('攻击', st.atk, base.atk, grow.atk * L, gear.atk) + line('防御', st.def, base.def, grow.def * L, gear.def) +
      line('速度', st.spd, base.spd, grow.spd * L, gear.spd) + line('气血上限', st.maxHp, base.hp, grow.hp * L, gear.maxHp) +
      line('法力上限', st.maxMp, base.mp, grow.mp * L, 0) +
      '<div class="arow"><span>暴击</span><b>10%</b><small>暴击时伤害 ×1.5</small></div>' +
      '<div class="arow"><span>银两</span><b>' + fmt(S.coin) + '</b><small></small></div></div>' +
      '<p class="hint">升级时五项属性按固定成长自动提升；装备和饰品提供额外加成。</p>';
  } else {
    h += '<div class="elist">';
    for (const slot in slotName) {
      const id = S.equip[slot], it = id && D.items[id];
      h += '<div class="erow"><div class="tile' + (id ? '' : ' empty') + '">' + (id ? itemImg(id) : '') + '</div><div><small>' + slotName[slot] + '</small><h6>' + (it ? it.name : '空') + '</h6><p>' + (it ? itemBonus(it) : '背包里有合适的就能装备') + '</p></div>' +
        (it ? '<button type="button" class="tbtn" data-act="unequip:' + slot + '">卸下</button>' : '') + '</div>';
    }
    h += '</div><p class="center" style="margin:12px 0 0"><button type="button" class="tbtn" data-act="openBag">去背包换装备</button></p>';
  }
  h += '<p class="center" style="margin:16px 0 0"><button type="button" class="tbtn" data-act="switchRole">切换角色</button>　<button type="button" class="tbtn red" data-act="reset">重新开始</button></p>';
  openModal('角色', h);
}
function openSkills() {
  let h = '';
  D.playerSkills.forEach(id => {
    const s = D.skills[id], ok = S.lv >= (s.lv || 1);
    h += '<div class="row" style="' + (ok ? '' : 'opacity:.5') + '"><div><h5>' + s.name + '</h5><p>' + s.desc + '</p></div><span>' + (ok ? '法力 ' + s.mp : s.lv + ' 级解锁') + '</span></div>';
  });
  openModal('技能', h + '<p style="color:var(--muted);margin:14px 0 0">技能在战斗中使用。</p>');
}
function openPet() {
  const p = petStats();
  openModal('宠物', '<div class="who"><img alt="" src="' + IMG('fox') + '" style="height:130px"><div><h5>灵狐</h5><p>等级 ' + S.lv + '（随主人成长）</p><p>战斗中自动出手，有几率喷出狐火。</p></div></div>' +
    '<div class="stats"><div>气血<b>' + p.maxHp + '</b></div><div>攻击<b>' + p.atk + '</b></div><div>防御<b>' + p.def + '</b></div><div>速度<b>' + p.spd + '</b></div></div>');
}
let shopId = null;
function openShop(id) {
  shopId = id; const shop = D.shops[id];
  let h = '<p class="center" style="margin:0;color:var(--amber)">银两 ' + fmt(S.coin) + '</p>';
  shop.sell.forEach(iid => {
    const it = D.items[iid];
    h += '<button type="button" class="row" data-act="buy:' + iid + '"' + (S.coin < it.price ? ' disabled' : '') + '>' + itemImg(iid) + '<div><h5>' + it.name + (S.items[iid] ? '　<small style="color:var(--muted)">已有 ' + S.items[iid] + '</small>' : '') + '</h5><p>' + it.desc + '</p></div><span>' + it.price + ' 两</span></button>';
  });
  const sellable = Object.keys(S.items).filter(i => D.items[i].type === 'mat' && D.items[i].price > 0);
  if (sellable.length) {
    h += '<p style="margin:16px 0 0;color:var(--muted)">出售材料</p>';
    sellable.forEach(iid => { const it = D.items[iid]; h += '<button type="button" class="row" data-act="sell:' + iid + '">' + itemImg(iid) + '<div><h5>' + it.name + ' ×' + S.items[iid] + '</h5><p>' + it.desc + '</p></div><span>卖 ' + it.price + ' 两</span></button>'; });
  }
  openModal(shop.name, h);
}
const ACTS = {
  sel(id) { bagSel = id; openBag(); },
  use(id) { usePotion(id); openBag(); },
  equip(id) {
    const it = D.items[id], old = S.equip[it.slot];
    addItem(id, -1); if (old) addItem(old, 1); S.equip[it.slot] = id; S.hp = Math.min(S.hp, stats().maxHp);
    save(); refresh(); toast('装备了' + it.name); openBag();
  },
  buy(id) { const it = D.items[id]; if (S.coin < it.price) return; S.coin -= it.price; addItem(id, 1); save(); refresh(); toast('买下了' + it.name); openShop(shopId); },
  sell(id) { const it = D.items[id]; if (!S.items[id]) return; addItem(id, -1); S.coin += it.price; save(); refresh(); openShop(shopId); },
  fight(id) { $('modal').hidden = true; modalOnClose = null; startBattle(D.stages.find(s => s.id === id)); },
  reset() { openModal('重新开始', '<p class="center">会清掉等级、背包和任务进度，从头再来。</p><p class="center"><button type="button" class="tbtn red" data-act="resetYes">确定清档</button>　<button type="button" class="tbtn" data-act="close">再想想</button></p>'); },
  resetYes() { try { localStorage.removeItem(SAVE_KEY); } catch (e) {} S = newGame(); bagSel = null; loadMap(S.map); closeModal(); refresh(); toast('已重新开始'); },
  arena() { $('modal').hidden = true; modalOnClose = null; if (B.active) { B.active = false; $('units').innerHTML = ''; } startArena(); },
  roleTab(t) { roleTab = t; openRole(); },
  unequip(slot) { if (!S.equip[slot]) return; const id = S.equip[slot]; S.equip[slot] = null; addItem(id, 1); clampVitals(); save(); refresh(); toast('卸下了' + D.items[id].name); openRole(); },
  openBag() { openBag(); },
  switchRole() { closeModal(); stop(); save(); window.FRONT.showRoles(); },
  close() { closeModal(); },
  leave() { closeModal(); }
};

// ============================================================ 地图与行走
// 一张地图 = 一张大底图 + 可行走多边形 + NPC / 传送点 / 地图上的敌人。镜头跟着主角走。
// 地图定义在 data.js 的 maps 里，坐标用“底图原图像素”，这里乘 scale 换成舞台像素。
let M = null;                                  // 当前地图的运行时数据
const cam = { x: 0, y: 0 };
let CELL = 16;                               // 寻路网格的格子边长
const bgMap = $('bgMap');
const toMap = (def, x, y) => ({ x: x * def.scale * (def.k || 1), y: y * def.scale * (def.k || 1) });
const I = (x, y) => toMap(M.def, x, y);
function inPoly(poly, p) {
  let c = false;
  for (let i = 0, j = poly.length - 1; i < poly.length; j = i++) {
    const a = poly[i], b = poly[j];
    if ((a.y > p.y) !== (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
  }
  return c;
}
function inside(p) {
  if (M.mask) {                                      // 有碰撞位图的地图：直接查位图（再减去 blocks）
    const m = M.mask, u = M.def.scale * (M.def.k || 1) * m.cell, gx = Math.floor(p.x / u), gy = Math.floor(p.y / u);
    if (gx < 0 || gy < 0 || gx >= m.w || gy >= m.h) return false;
    for (const poly of M.blocks) if (inPoly(poly, p)) return false;
    const i = gy * m.w + gx; return (m.bytes[i >> 3] >> (7 - (i & 7)) & 1) === 1;
  }
  for (const poly of M.blocks) if (inPoly(poly, p)) return false;      // 路中间的障碍（井、树）
  for (const poly of M.walk) if (inPoly(poly, p)) return true;
  return false;
}

// ---------- 角色 / 传送点 ----------
function makeActor(kind, id, sprite, name, pos, opt) {
  opt = opt || {};
  const sz = AS.size[sprite];
  const el = document.createElement('div'); el.className = 'actor';
  el.innerHTML = '<div class="sh"></div><img alt="" src="' + IMG(sprite) + '" style="width:' + sz[0] + 'px;height:' + sz[1] + 'px;left:' + (-sz[0] / 2) + 'px"><div class="tag" style="bottom:' + (sz[1] + 2) + 'px"></div>';
  world.appendChild(el);
  const a = { kind, id, name, el, img: el.querySelector('img'), tag: el.querySelector('.tag'), pos: { x: pos.x, y: pos.y }, face: opt.face || 1, moving: false,
    color: opt.color || '#ffe066', title: opt.title || '', titleColor: opt.titleColor || '#ffb05a', mark: false, h: sz[1], hop: opt.hop || 6 };
  drawTag(a); return a;
}
function makePortal(p) {
  const el = document.createElement('div'); el.className = 'portal'; el.innerHTML = '<i></i><div class="tag"></div>';
  world.appendChild(el);
  const a = { kind: 'portal', id: 'portal_' + p.to, name: p.name, el, tag: el.querySelector('.tag'), pos: I(p.at[0], p.at[1]), h: 50, mark: false, to: p.to, arrive: p.arrive, needStep: p.needStep, lockMsg: p.lockMsg };
  el.style.transform = 'translate(' + a.pos.x + 'px,' + a.pos.y + 'px)'; el.style.zIndex = 1;
  drawTag(a); return a;
}
function drawTag(a, auto) {
  if (a.kind === 'portal') { a.tag.innerHTML = (a.mark ? '<span class="mk">!</span>' : '') + '<span>' + a.name + ' ▶</span>'; return; }
  a.tag.innerHTML = (a.mark ? '<span class="mk">!</span>' : '') + (auto ? '<span class="auto">自动寻路中…</span>' : '') +
    (a.title ? '<small style="color:' + a.titleColor + '">' + a.title + '</small>' : '') + '<span style="color:' + a.color + '">' + a.name + '</span>';
}
const player = makeActor('player', 'player', 'player', '云中客', { x: 0, y: 0 }, { color: '#8affb0', title: '【游侠】', hop: 7 });
const fox = makeActor('pet', 'fox', 'fox', '灵狐', { x: 0, y: 0 }, { color: '#a8e6ff', hop: 5 });
// 主角用行走帧：正面一套（朝右下）、背面一套（朝左上），各 4 帧；另外两个方向靠水平翻转
// 精灵表一行 4 格，AS.size.walk_f = [每格宽, 每格高, 头部中心在格内的 x]
const WALK_SHEET = { f: 'walk_f', b: 'walk_b' }, STEP_LEN = 26;
player.walk = document.createElement('div'); player.walk.className = 'walk';
player.el.insertBefore(player.walk, player.tag); player.img.hidden = true;
player.dir = 'f'; player.shown = ''; player.dist = 0; player.mt = 0;
player.tag.style.bottom = (AS.size.walk_f[1] + 2) + 'px'; player.h = AS.size.walk_f[1];
Object.values(WALK_SHEET).forEach(k => { const im = new Image(); im.src = IMG(k); });
// 灵狐多一张背影，往上走时切过去
fox.imgB = document.createElement('img'); fox.imgB.alt = ''; fox.imgB.src = IMG('b_fox');
{ const sz = AS.size.b_fox, h = fox.h + 4, w = h * sz[0] / sz[1]; fox.imgB.style.cssText = 'width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px'; }
fox.imgB.hidden = true; fox.el.insertBefore(fox.imgB, fox.tag); fox.dir = 'f';

// ---------- 载入地图 ----------
function loadMap(id, at) {
  const def = D.maps[id];
  M = { id, def, W: def.iw * def.scale, H: def.ih * def.scale, npcs: {}, list: [] };
  const mk = window.MASKS && window.MASKS[id];
  if (mk) { const bin = atob(mk.bits), bytes = new Uint8Array(bin.length); for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i); M.mask = { cell: mk.cell, w: mk.w, h: mk.h, bytes }; CELL = 8; } else CELL = 16;
  M.walk = def.walk.map(poly => poly.map(p => I(p[0], p[1])));
  M.blocks = (def.blocks || []).map(b => { const c = I(b.at[0], b.at[1]), w = b.r * def.scale * (def.k || 1), o = []; for (let k = 0; k < 8; k++) o.push({ x: c.x + Math.cos(k * Math.PI / 4) * w, y: c.y + Math.sin(k * Math.PI / 4) * w * 0.6 }); return o; });
  (def.roads || []).forEach(r => {              // 道路：每段一个矩形，每个拐点一个八边形
    const pts = r.pts.map(p => I(p[0], p[1])), w = r.r * def.scale * (def.k || 1);
    pts.forEach((p, i) => {
      const oct = []; for (let k = 0; k < 8; k++) oct.push({ x: p.x + Math.cos(k * Math.PI / 4) * w, y: p.y + Math.sin(k * Math.PI / 4) * w });
      M.walk.push(oct);
      if (i) { const a = pts[i - 1], dx = p.x - a.x, dy = p.y - a.y, L = Math.hypot(dx, dy) || 1, nx = -dy / L * w, ny = dx / L * w;
        M.walk.push([{ x: a.x + nx, y: a.y + ny }, { x: p.x + nx, y: p.y + ny }, { x: p.x - nx, y: p.y - ny }, { x: a.x - nx, y: a.y - ny }]); }
    });
  });
  world.style.width = M.W + 'px'; world.style.height = M.H + 'px';
  bgMap.src = IMG(def.img); bgMap.style.width = M.W + 'px'; bgMap.style.height = M.H + 'px'; bgMap.alt = def.alt || def.name;
  world.querySelectorAll('.actor,.portal').forEach(e => e.remove());
  $('glows').innerHTML = '';
  (def.glows || []).forEach((g, i) => {
    const d = document.createElement('div'); d.className = 'glow'; const p = I(g[0], g[1]);
    d.style.left = p.x + 'px'; d.style.top = p.y + 'px'; d.style.animationDelay = (-i * 0.47) + 's'; d.style.animationDuration = (2.4 + i % 4 * 0.5) + 's';
    $('glows').appendChild(d);
  });
  world.appendChild(player.el); world.appendChild(fox.el);
  (def.npcs || []).forEach(n => {
    const info = D.npcs[n.id];
    const a = makeActor('npc', n.id, info.sprite || n.id, info.name, I(n.at[0], n.at[1]), { color: info.color, title: info.title, titleColor: info.titleColor });
    M.npcs[n.id] = a; M.list.push(a);
  });
  (def.portals || []).forEach(p => M.list.push(makePortal(p)));
  (def.foes || []).forEach(f => {
    const st = D.stages.find(s => s.id === f.stage);
    const lead = st.enemies.includes('boss') ? 'boss' : st.enemies[0];
    const a = makeActor('foe', 'foe_' + st.id, D.enemies[lead].sprite, st.name, I(f.at[0], f.at[1]), { color: '#ff9a8a', title: '建议 ' + st.rec + ' 级', titleColor: '#ffd56a', face: f.face || 1 });
    a.stage = st; M.list.push(a);
  });
  buildGrid(); drawMiniDots();
  const p = at && inside(at) ? at : I(def.start[0], def.start[1]);
  player.pos = { x: p.x, y: p.y }; player.face = 1;
  const fp = { x: p.x + 46, y: p.y + 18 }; fox.pos = inside(fp) ? fp : { x: p.x, y: p.y };
  stop();
  $('place').textContent = def.name; $('miniMap').style.backgroundImage = 'url(' + IMG(def.mini) + ')';
  $('miniMap').style.backgroundSize = (M.W * MINI) + 'px ' + (M.H * MINI) + 'px';
  $('petals').hidden = !def.petals;
  S.map = id; S.pos = { x: p.x, y: p.y };
  if (!S.visited) S.visited = []; if (!S.visited.includes(id)) S.visited.push(id);
  $('bgBattle').src = IMG(def.battleBg || 'bg_battle');
  camSnap(); showQuest();
}
const MINI = 0.09;
const dotKind = a => (a.kind === 'portal' ? 'p' : a.kind === 'foe' ? 'f' : '') + (a.mark ? ' q' : '');
function drawMiniDots() {
  $('miniDots').innerHTML = M.list.map(a => '<i class="' + dotKind(a) + '" style="left:' + (a.pos.x * MINI).toFixed(1) + 'px;top:' + (a.pos.y * MINI).toFixed(1) + 'px"></i>').join('');
}                              // 小地图缩放
function camTarget() {
  return { x: Math.max(0, Math.min(M.W - 720, player.pos.x - 360)), y: Math.max(0, Math.min(M.H - 1280, player.pos.y - 700)) };
}
function camSnap() { const t = camTarget(); cam.x = t.x; cam.y = t.y; }
function travel(portal) {
  const dest = D.maps[portal.to];
  loadMap(portal.to, toMap(dest, portal.arrive[0], portal.arrive[1]));
  save(); toast('来到 ' + dest.name);
}

// ---------- 寻路：网格 A* + 拉直 ----------
function buildGrid() {
  M.gw = Math.ceil(M.W / CELL); M.gh = Math.ceil(M.H / CELL); M.grid = new Uint8Array(M.gw * M.gh);
  for (let y = 0; y < M.gh; y++) for (let x = 0; x < M.gw; x++) if (inside({ x: (x + .5) * CELL, y: (y + .5) * CELL })) M.grid[y * M.gw + x] = 1;
}
const cellOf = p => ({ x: Math.floor(p.x / CELL), y: Math.floor(p.y / CELL) });
const cellOk = (x, y) => x >= 0 && y >= 0 && x < M.gw && y < M.gh && M.grid[y * M.gw + x] === 1;
const cellCenter = c => ({ x: (c.x + .5) * CELL, y: (c.y + .5) * CELL });
// 离某点最近的可走格子（点在障碍物上时用）
function nearestCell(p) {
  const c = cellOf(p); if (cellOk(c.x, c.y)) return c;
  let best = null, bd = 1e9;
  for (let r = 1; r <= 60 && !best; r++) {
    for (let dy = -r; dy <= r; dy++) for (let dx = -r; dx <= r; dx++) {
      if (Math.max(Math.abs(dx), Math.abs(dy)) !== r || !cellOk(c.x + dx, c.y + dy)) continue;
      const q = cellCenter({ x: c.x + dx, y: c.y + dy }), d = Math.hypot(q.x - p.x, q.y - p.y);
      if (d < bd) { bd = d; best = { x: c.x + dx, y: c.y + dy }; }
    }
  }
  return best;
}
function los(a, b) {
  const d = Math.hypot(b.x - a.x, b.y - a.y), n = Math.max(1, Math.ceil(d / 6));
  for (let i = 1; i < n; i++) if (!inside({ x: a.x + (b.x - a.x) * i / n, y: a.y + (b.y - a.y) * i / n })) return false;
  return true;
}
function findPath(from, to) {
  const s = nearestCell(from), g = nearestCell(to); if (!s || !g) return null;
  const W = M.gw, N = W * M.gh, si = s.y * W + s.x, gi = g.y * W + g.x;
  const cost = new Float32Array(N).fill(1e9), prev = new Int32Array(N).fill(-1), closed = new Uint8Array(N);
  const heap = [];                               // 小顶堆：[f, index]
  const push = (f, i) => { heap.push([f, i]); let k = heap.length - 1; while (k > 0) { const p = (k - 1) >> 1; if (heap[p][0] <= heap[k][0]) break; [heap[p], heap[k]] = [heap[k], heap[p]]; k = p; } };
  const pop = () => { const top = heap[0], lastN = heap.pop(); if (heap.length) { heap[0] = lastN; let k = 0; for (;;) { let m = k; const l = 2 * k + 1, r = l + 1; if (l < heap.length && heap[l][0] < heap[m][0]) m = l; if (r < heap.length && heap[r][0] < heap[m][0]) m = r; if (m === k) break; [heap[m], heap[k]] = [heap[k], heap[m]]; k = m; } } return top; };
  const hx = i => { const dx = Math.abs(i % W - g.x), dy = Math.abs(Math.floor(i / W) - g.y); return Math.max(dx, dy) + 0.414 * Math.min(dx, dy); };
  cost[si] = 0; push(hx(si), si);
  let found = false;
  while (heap.length) {
    const i = pop()[1]; if (closed[i]) continue; closed[i] = 1;
    if (i === gi) { found = true; break; }
    const x = i % W, y = Math.floor(i / W);
    for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) {
      if (!dx && !dy) continue;
      const nx = x + dx, ny = y + dy; if (!cellOk(nx, ny)) continue;
      if (dx && dy && (!cellOk(x + dx, y) || !cellOk(x, y + dy))) continue;   // 不许斜着擦墙角
      const j = ny * W + nx; if (closed[j]) continue;
      const c = cost[i] + (dx && dy ? 1.414 : 1);
      if (c < cost[j]) { cost[j] = c; prev[j] = i; push(c + hx(j), j); }
    }
  }
  if (!found) return null;
  let pts = [];
  for (let i = gi; i !== -1; i = prev[i]) pts.push(cellCenter({ x: i % W, y: Math.floor(i / W) }));
  pts.reverse();
  if (inside(from)) pts[0] = { x: from.x, y: from.y }; else pts.unshift({ x: from.x, y: from.y });
  if (inside(to)) pts[pts.length - 1] = { x: to.x, y: to.y };
  // 拉直：能直线走到的中间点全部跳过
  const out = [pts[0]]; let a = 0;
  while (a < pts.length - 1) { let b = pts.length - 1; while (b > a + 1 && !los(pts[a], pts[b])) b--; out.push(pts[b]); a = b; }
  return out.slice(1);
}

// ---------- 界面按钮 ----------
function addBtn(parent, icon, label, x, y, size, key, dot) {
  const b = document.createElement('button'); b.className = 'btn'; b.type = 'button';
  b.style.cssText = 'left:' + (x - size / 2) + 'px;top:' + (y - size / 2) + 'px;width:' + size + 'px';
  b.innerHTML = '<img alt="" src="' + IMG('icon_' + icon) + '">' + (label ? '<span>' + label + '</span>' : '') + (dot ? '<i class="dot"></i>' : '');
  b.setAttribute('aria-label', label || key);
  b.addEventListener('click', e => { e.stopPropagation(); onBtn(key, label, b); });
  parent.appendChild(b); return b;
}
[['act', '活动', 0], ['gift', '福利', 1], ['mall', '商城', 0], ['rank', '竞技', 0]].forEach((r, i) => addBtn($('btns'), r[0], r[1], 676, 246 + i * 76, 54, r[0], r[2]));
const autoBtn = addBtn($('btns'), 'auto', '', 652, 1090, 86, 'auto');
addBtn($('btns'), 'ride', '', 562, 1118, 56, 'ride'); addBtn($('btns'), 'potion', '', 594, 1048, 56, 'potion');
[['role', '角色'], ['bag', '背包'], ['skill', '技能'], ['pet', '宠物'], ['guild', '帮派'], ['friend', '好友']].forEach((r, i) => addBtn($('dockBtns'), r[0], r[1], 62 + i * 119, 1206, 66, r[0]));

let wander = false, wanderWait = 0;
function onBtn(key, label, b) {
  if (key === 'role') openRole();
  else if (key === 'bag') openBag();
  else if (key === 'skill') openSkills();
  else if (key === 'pet') openPet();
  else if (key === 'mall') openShop('vendor');
  else if (key === 'auto') { wander = !wander; wanderWait = 0; autoBtn.classList.toggle('on', wander); if (!wander) stop(); toast(wander ? '开始挂机闲逛，再点一次停止' : '停止挂机'); }
  else if (key === 'potion') { if (!S.items.hp_potion) toast('没有金创药了，找货郎买点吧'); else usePotion('hp_potion'); }
  else if (key === 'gift') {
    if (S.gift) toast('今日福利已领取');
    else { S.gift = true; grant({ coin: 200, items: { hp_potion: 2 } }); toast('领取福利：银两 +200，金创药 ×2'); const d = b.querySelector('.dot'); if (d) d.hidden = true; }
  }
  else if (key === 'rank') openModal('竞技场', '<p class="center">' + D.arena.allyName + ' 对 ' + D.arena.foeName + '<br>双方各 5 名侠客、5 只宠物，等级相同。<br>你操作自己的角色，队友和宠物自动出手；每回合 ' + TURN_SECONDS + ' 秒，超时自动行动。<br>演示对战，不影响等级和背包。</p><p class="center"><button type="button" class="tbtn red" data-act="arena">开始 5v5 对战</button></p>');
  else if (key === 'act') toast('中秋灯会将在戌时三刻开始');
  else toast('「' + (label || '坐骑') + '」暂未开放');
}
['hud', 'coin', 'chat', 'dock'].forEach(id => $(id).addEventListener('click', e => e.stopPropagation()));

// ---------- 世界地图：点小地图打开，去过的地方可以直接传送 ----------
function openWorld() {
  if (B.active) return;
  stop(); wander = false; autoBtn.classList.remove('on');
  const img = $('wmapImg'); img.src = IMG('world');
  const iw = AS.size.world[0], ih = AS.size.world[1], k = 1280 / ih, off = (iw * k - 720) / 2;   // 图片按高度铺满、左右裁掉
  $('wmapNodes').innerHTML = Object.keys(D.maps).map(id => {
    const d = D.maps[id], here = id === M.id, seen = (S.visited || []).includes(id);
    return '<button type="button" class="wnode' + (here ? ' here' : '') + '" data-go="' + id + '"' + (seen ? '' : ' disabled') + ' style="left:' + (d.world[0] * k - off) + 'px;top:' + (d.world[1] * k) + 'px">' + d.name +
      '<small>' + (here ? '你在这里' : seen ? d.note : '尚未去过') + '</small></button>';
  }).join('');
  $('wmap').hidden = false;
}
$('wmap').addEventListener('click', e => {
  e.stopPropagation();
  const b = e.target.closest('[data-go]'); if (!b || b.disabled) return;
  const id = b.dataset.go;
  if (id === M.id) { toast('你已经在' + D.maps[id].name); return; }
  $('wmap').hidden = true; loadMap(id); save(); toast('传送到 ' + D.maps[id].name);
});
$('wmapClose').addEventListener('click', e => { e.stopPropagation(); $('wmap').hidden = true; });
$('place').addEventListener('click', e => { e.stopPropagation(); openWorld(); });
// 场景地图（点小地图打开）：当前这张地图的全貌，标出自己、人物、传送点、敌人；点地面走过去，点标记去找人
function openScene() {
  if (B.active) return;
  stop(); wander = false; autoBtn.classList.remove('on');
  const def = M.def; $('smapImg').src = IMG(def.img); $('smapTitle').textContent = def.name; $('smapSub').textContent = '场景地图 · ' + (def.note || '') + '　点地面走过去，点标记去找人';
  const pct = (v, tot) => (v / tot * 100).toFixed(2) + '%';
  const dots = M.list.map((a, i) => '<button type="button" class="sd ' + dotKind(a) + '" data-i="' + i + '" style="left:' + pct(a.pos.x, M.W) + ';top:' + pct(a.pos.y, M.H) + '"><i></i><span>' + a.name + '</span></button>').join('') +
    '<div class="sd me" id="smapMe" style="left:' + pct(player.pos.x, M.W) + ';top:' + pct(player.pos.y, M.H) + '"><i></i></div>';
  $('smapDots').innerHTML = dots;
  const kindName = { npc: '人物', portal: '出口', foe: '敌人' };
  $('smapList').innerHTML = M.list.map((a, i) => '<button type="button" class="' + (a.mark ? 'q' : '') + '" data-i="' + i + '"><small>' + kindName[a.kind] + '</small>' + a.name + (a.mark ? ' ！' : '') + '</button>').join('');
  $('smap').hidden = false;
  const bx = $('smapBox'), lst = $('smapList'), room = 1280 - 150 - 140, used = bx.offsetHeight + 20 + lst.offsetHeight, top = 150 + Math.max(0, (room - used) / 2 - 20);
  bx.style.top = top + 'px'; lst.style.top = (top + bx.offsetHeight + 20) + 'px';
}
$('mini').addEventListener('click', e => { e.stopPropagation(); openScene(); });
$('smap').addEventListener('click', e => {
  e.stopPropagation();
  const d = e.target.closest('[data-i]');
  if (d) { const a = M.list[+d.dataset.i]; if (a) { $('smap').hidden = true; goTalk(a); } return; }
  const box = e.target.closest('#smapBox');
  if (box) {
    const r = box.getBoundingClientRect();
    const p = { x: (e.clientX - r.left) / r.width * M.W, y: (e.clientY - r.top) / r.height * M.H };
    $('smap').hidden = true; if (!moveTo(p, false)) toast('走不过去');
  }
});
$('smapClose').addEventListener('click', e => { e.stopPropagation(); $('smap').hidden = true; });
$('smapWorld').addEventListener('click', e => { e.stopPropagation(); $('smap').hidden = true; openWorld(); });

// ---------- 对话 ----------
let typing = null, dialogFull = '', dialogAfter = null;
function openDialog(n, text, after) {
  $('dFace').src = IMG((D.npcs[n.id] && D.npcs[n.id].head) || 'head_' + n.id); $('dName').textContent = n.name; $('dialog').hidden = false;
  const p = $('dText'); p.textContent = ''; let i = 0; clearInterval(typing);
  if (reduce) p.textContent = text; else typing = setInterval(() => { p.textContent = text.slice(0, ++i); if (i >= text.length) clearInterval(typing); }, 26);
  dialogFull = text; dialogAfter = after || null;
}
function closeDialog() {
  const p = $('dText');
  if (p.textContent.length < dialogFull.length) { clearInterval(typing); p.textContent = dialogFull; return; }
  $('dialog').hidden = true; const f = dialogAfter; dialogAfter = null; if (f) f();
}
$('dialog').addEventListener('click', e => { e.stopPropagation(); closeDialog(); });

function talk(n) {
  const q = D.quests[S.step];
  if (q && q.type === 'talk' && q.npc === n.id) {
    openDialog(n, q.say, () => {
      if (q.take) for (const id in q.take) addItem(id, -q.take[id]);
      S.step++; const ups = grant(q.reward || {});
      sys('完成「' + q.title + '」' + (q.reward ? '：' + rewardText(q.reward) : ''));
      toast(ups ? '升到 ' + S.lv + ' 级！气血法力已回满' : (D.quests[S.step] ? '任务更新：' + D.quests[S.step].goal : '主线任务全部完成！'));
    });
    return;
  }
  const info = D.npcs[n.id];
  openDialog(n, info.idle, () => {
    if (info.shop) openShop(info.shop);
    else if (info.act === 'arena') openModal('竞技场', '<p class="center">五对五模拟对战（不影响存档）。</p><p class="center"><button type="button" class="tbtn red" data-act="arena">上擂台</button>　<button type="button" class="tbtn" data-act="close">再看看</button></p>');
    else if (info.rest) { const st = stats(); if (S.hp < st.maxHp || S.mp < st.maxMp) { S.hp = st.maxHp; S.mp = st.maxMp; save(); refresh(); toast(info.restMsg || '歇了一会儿，气血法力回满'); } }
  });
}
// 地图上的敌人：走过去后弹出确认
function openFoe(a) {
  const st = a.stage, names = st.enemies.map(e => D.enemies[e].name).join('、');
  const needSt = st.need && !S.cleared.includes(st.need) ? D.stages.find(s => s.id === st.need) : null;
  let h;
  if (needSt) h = '<p class="center">他们人多势众，贸然上前太危险。<br>先打通「' + needSt.name + '」再来。</p><p class="center"><button type="button" class="tbtn" data-act="close">知道了</button></p>';
  else if (st.needStep != null && S.step < st.needStep) h = '<p class="center">山寨大门紧闭，头目不肯露面。<br>先回城里打听打听消息。</p><p class="center"><button type="button" class="tbtn" data-act="close">知道了</button></p>';
  else h = '<p class="center">' + st.desc + '<br>敌人：' + names + '　建议 ' + st.rec + ' 级（你现在 ' + S.lv + ' 级）</p>' +
    '<p class="center"><button type="button" class="tbtn red" data-act="fight:' + st.id + '">开战</button>　<button type="button" class="tbtn" data-act="close">再等等</button></p>';
  B.foe = a; openModal(st.name, h);
}

// ---------- 任务显示 ----------
const mapOfNpc = id => Object.keys(D.maps).find(m => (D.maps[m].npcs || []).some(n => n.id === id));
const mapOfStage = id => Object.keys(D.maps).find(m => (D.maps[m].foes || []).some(f => f.stage === id));
const portalTo = map => M.list.find(a => a.kind === 'portal' && a.to === map) || null;
// 要去 target 地图，在当前地图上该走哪个传送点（沿传送点连成的图做一次广度优先）
function hopTo(target) {
  if (!target || target === M.id) return null;
  const from = {}; from[M.id] = null; const queue = [M.id];
  while (queue.length) {
    const m = queue.shift();
    if (m === target) { let c = m; while (from[c] !== M.id) c = from[c]; return portalTo(c); }
    (D.maps[m].portals || []).forEach(p => { if (!(p.to in from)) { from[p.to] = m; queue.push(p.to); } });
  }
  return null;
}
// 当前任务在这张地图上该去找谁：目标不在本图时，指向通往那张图的传送点
function questTarget() {
  const q = D.quests[S.step]; if (!q || !M) return null;
  if (q.type === 'talk') return M.npcs[q.npc] || hopTo(mapOfNpc(q.npc));
  return M.list.find(a => a.kind === 'foe' && a.stage.id === q.stage) || hopTo(mapOfStage(q.stage));
}
function showQuest() {
  if (!M) return;
  const q = D.quests[S.step], tgt = questTarget();
  let ch = false; M.list.forEach(a => { const m = a === tgt; if (a.mark !== m) { a.mark = m; drawTag(a); ch = true; } }); if (ch) drawMiniDots();
  $('qTitle').textContent = q ? q.title : '江湖路远'; $('qGoal').textContent = q ? q.goal : '主线已完成，去竹林继续历练吧';
  $('qSide').textContent = '已打通 ' + S.cleared.length + ' / ' + D.stages.length + ' 处';
}
function questGo() {
  wander = false; autoBtn.classList.remove('on');
  const t = questTarget() || M.list.find(a => a.kind === 'portal');
  if (t) { goTalk(t); toast('自动寻路：' + t.name); }
}
$('quest').addEventListener('click', e => { e.stopPropagation(); questGo(); });
$('quest').addEventListener('keydown', e => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); questGo(); } });

// ---------- 行走 ----------
const SPEED = 230; let route = [], talkNpc = null, auto = false;
const mark = $('mark');
function moveTo(dest, isAuto, quiet) {
  let d = { x: dest.x, y: dest.y };
  if (!inside(d)) { const c = nearestCell(d); if (!c) return false; d = cellCenter(c); }   // 点到走不了的地方：去最近的可走点
  const path = findPath(player.pos, d); if (!path) return false;
  if (!path.length) return false;
  if (!player.moving) player.mt = 0;
  route = path; player.moving = true; auto = !!isAuto;
  drawTag(player, auto);
  if (!quiet) mark.style.setProperty('--at', 'translate(' + d.x + 'px,' + d.y + 'px)'); mark.classList.remove('go'); void mark.offsetWidth; mark.classList.add('go');
  return true;
}
function goTalk(n) {
  talkNpc = n;
  const side = player.pos.x >= n.pos.x ? 1 : -1;
  const cands = n.kind === 'portal' ? [{ x: n.pos.x, y: n.pos.y }] :
    [{ x: n.pos.x + 54 * side, y: n.pos.y + 16 }, { x: n.pos.x - 54 * side, y: n.pos.y + 16 }, { x: n.pos.x, y: n.pos.y + 40 }, { x: n.pos.x + 34 * side, y: n.pos.y + 44 }];
  const c = cands.find(inside) || n.pos;
  if (Math.hypot(c.x - player.pos.x, c.y - player.pos.y) < 8) return arrive();
  if (!moveTo(c, true)) arrive();
}
function stop() { player.moving = false; auto = false; route = []; talkNpc = null; drawTag(player, false); }
function arrive() {
  const n = talkNpc; const near = n && Math.hypot(n.pos.x - player.pos.x, n.pos.y - player.pos.y) < 150;
  stop(); S.pos = { x: player.pos.x, y: player.pos.y }; save();
  if (!near) return;
  if (n.kind !== 'portal') { player.face = n.pos.x >= player.pos.x ? 1 : -1; player.dir = n.pos.y < player.pos.y - 24 ? 'b' : 'f'; }
  if (n.kind === 'portal') { if (n.needStep != null && S.step < n.needStep) toast(n.lockMsg || '现在还不能过去'); else travel(n); }
  else if (n.kind === 'foe') openFoe(n); else talk(n);
}
function stepPlayer(dt) {
  if (!player.moving) return;
  player.mt += dt;
  let left = SPEED * Math.min(1, 0.4 + player.mt / 0.2) * dt;      // 起步有一小段加速
  const x0 = player.pos.x, y0 = player.pos.y;
  while (left > 0 && route.length) {
    const t = route[0], dx = t.x - player.pos.x, dy = t.y - player.pos.y, d = Math.hypot(dx, dy);
    if (d <= left) { player.pos.x = t.x; player.pos.y = t.y; left -= d; route.shift(); }
    else { player.pos.x += dx / d * left; player.pos.y += dy / d * left; left = 0; }
  }
  // 朝向：上下决定用正面还是背影，左右决定是否翻转；变化不大时保持原样，免得来回抖
  const mx = player.pos.x - x0, my = player.pos.y - y0, m = Math.hypot(mx, my);
  if (m > 0.01) {
    player.dist += m;
    if (Math.abs(my) > m * 0.3) player.dir = my < 0 ? 'b' : 'f';
    if (Math.abs(mx) > m * 0.25) player.face = mx > 0 ? 1 : -1;
  }
  if (!route.length) arrive();
}
function placePlayer() {
  const a = player, sz = AS.size[WALK_SHEET[a.dir]], w = a.walk;
  a.el.style.transform = 'translate(' + a.pos.x.toFixed(1) + 'px,' + a.pos.y.toFixed(1) + 'px)';
  a.el.style.zIndex = Math.round(a.pos.y);
  if (a.shown !== a.dir) {
    a.shown = a.dir;
    w.style.backgroundImage = 'url(' + IMG(WALK_SHEET[a.dir]) + ')'; w.style.backgroundSize = (sz[0] * 4) + 'px ' + sz[1] + 'px';
    w.style.width = sz[0] + 'px'; w.style.height = sz[1] + 'px'; w.style.left = (-sz[2]) + 'px'; w.style.setProperty('--ox', sz[2] + 'px');
  }
  const frame = a.moving && !reduce ? Math.floor(a.dist / STEP_LEN) % 4 : 1;     // 站着不动用“并脚”那一帧
  w.style.backgroundPosition = (-frame * sz[0]) + 'px 0';
  // 正面图朝右下：往左走要翻；背影图朝左上：往右走要翻
  w.style.transform = 'scaleX(' + (a.dir === 'f' ? a.face : -a.face) + ')';
}
function stepFox(dt) {
  const tx = player.pos.x - fox.pos.x, ty = player.pos.y + 10 - fox.pos.y, dist = Math.hypot(tx, ty);
  if (dist > 84) fox.moving = true; if (dist < 62) fox.moving = false;
  if (dist > 600) { fox.pos.x = player.pos.x; fox.pos.y = player.pos.y; return; }
  if (!fox.moving) return;
  const len = Math.min(dist - 58, (SPEED + 30) * dt), sx = tx / dist * len;
  fox.pos.x += sx; fox.pos.y += ty / dist * len; if (Math.abs(sx) > .05) fox.face = sx > 0 ? 1 : -1;
  if (Math.abs(ty) > dist * 0.35) fox.dir = ty < 0 ? 'b' : 'f';
}
function place(a, t) {
  a.el.style.transform = 'translate(' + a.pos.x.toFixed(1) + 'px,' + a.pos.y.toFixed(1) + 'px)';
  a.el.style.zIndex = Math.round(a.pos.y);
  const bob = a.moving && !reduce ? -Math.abs(Math.sin(t * 11)) * a.hop : 0;
  const br = a.moving || reduce ? 1 : 1 + Math.sin(t * 2.2 + a.pos.x) * .012;
  const im = a.imgB && a.dir === 'b' ? a.imgB : a.img, flip = im === a.imgB ? -a.face : a.face;
  if (a.imgB) { a.img.hidden = im !== a.img; a.imgB.hidden = im !== a.imgB; }
  im.style.transform = 'translateY(' + bob.toFixed(1) + 'px) scale(' + flip + ',' + br.toFixed(4) + ')'; im.style.transformOrigin = '50% 100%';
}
$('town').addEventListener('click', e => {
  if (!$('dialog').hidden) { closeDialog(); return; }
  const r = stage.getBoundingClientRect(); const p = { x: (e.clientX - r.left) / scale + cam.x, y: (e.clientY - r.top) / scale + cam.y };
  let hit = null;
  for (const n of M.list) if (Math.abs(p.x - n.pos.x) < 54 && p.y < n.pos.y + 20 && p.y > n.pos.y - (n.kind === 'portal' ? n.h : n.h * 1.35) - 40) hit = n;
  wander = false; autoBtn.classList.remove('on');
  if (hit) goTalk(hit); else { talkNpc = null; moveTo(p, false); }
});

// 按住拖动：手指不抬起时，主角一直朝手指的位置走
let holding = false, holdAt = 0;
$('town').addEventListener('pointerdown', () => { holding = true; holdAt = performance.now(); });
['pointerup', 'pointercancel', 'pointerleave'].forEach(ev => $('town').addEventListener(ev, () => { holding = false; }));
$('town').addEventListener('pointermove', e => {
  if (!holding || !$('dialog').hidden || !$('modal').hidden || !$('wmap').hidden) return;
  const now = performance.now(); if (now - holdAt < 160) return; holdAt = now;
  const r = stage.getBoundingClientRect(); talkNpc = null; wander = false; autoBtn.classList.remove('on');
  moveTo({ x: (e.clientX - r.left) / scale + cam.x, y: (e.clientY - r.top) / scale + cam.y }, false, true);
});

// ---------- 落花（屏幕空间，只在有 petals 标记的地图出现）----------
const petals = []; if (!reduce) for (let i = 0; i < 16; i++) { const d = document.createElement('div'); d.className = 'petal'; $('petals').appendChild(d);
  petals.push({ el: d, x: Math.random() * 820, y: Math.random() * 1000, vx: -25 - Math.random() * 30, vy: 40 + Math.random() * 45, ph: Math.random() * 10 }); }

// ---------- 主循环 ----------
const ring = $('ring'), me = $('me'); let last = performance.now();
function frame(now) {
  const dt = Math.min(.05, (now - last) / 1000), t = now / 1000; last = now;
  if (!B.active && M) {
    const busy = !$('dialog').hidden || !$('modal').hidden;
    if (wander && !player.moving && !busy) {
      wanderWait -= dt;
      if (wanderWait <= 0) { wanderWait = .6 + Math.random() * .8; const p = { x: player.pos.x + rnd(-320, 320), y: player.pos.y + rnd(-260, 260) }; if (inside(p)) moveTo(p, true); }
    }
    stepPlayer(dt); stepFox(dt);
    placePlayer(); place(fox, t); for (const a of M.list) if (a.kind !== 'portal') place(a, t);
    ring.style.transform = 'translate(' + player.pos.x.toFixed(1) + 'px,' + player.pos.y.toFixed(1) + 'px)';
    // 镜头：平滑跟随，不超出地图边缘
    const ct = camTarget(), k = reduce ? 1 : 1 - Math.exp(-7 * dt);
    cam.x += (ct.x - cam.x) * k; cam.y += (ct.y - cam.y) * k;
    world.style.transform = 'translate3d(' + (-cam.x).toFixed(1) + 'px,' + (-cam.y).toFixed(1) + 'px,0)';
    $('miniDots').style.transform = 'translate(' + (51.5 - player.pos.x * MINI).toFixed(1) + 'px,' + (51.5 - player.pos.y * MINI).toFixed(1) + 'px)';
    $('miniMap').style.backgroundPosition = (51.5 - player.pos.x * MINI).toFixed(1) + 'px ' + (51.5 - player.pos.y * MINI).toFixed(1) + 'px';
    if (M.def.petals) for (const p of petals) { p.x += (p.vx + Math.sin(t * 1.3 + p.ph) * 30) * dt; p.y += p.vy * dt; if (p.y > 1040 || p.x < -20) { p.x = 100 + Math.random() * 740; p.y = -20 + Math.random() * 200; }
      p.el.style.transform = 'translate(' + p.x.toFixed(1) + 'px,' + p.y.toFixed(1) + 'px) rotate(' + (Math.sin(t * 2 + p.ph) * 60).toFixed(0) + 'deg)'; }
  }
  requestAnimationFrame(frame);
}

// ============================================================ 战斗（回合制）
// 站位：敌方在左上、我方在右下。每边后排最多 5 个人物、前排最多 5 只宠物，沿同一条斜线排开。
// 每回合：主角选指令（其余单位自动）→ 全场按速度排序 → 依次出手 → 判断胜负
const B = { active: false, mode: 'pve', units: [], player: null, round: 0, auto: false, stage: null, resolve: null, over: null, foe: null, picking: null, clock: null };
const ROWS = { enemy: { person: { x: 235, y: 520 }, pet: { x: 312, y: 612 } }, ally: { pet: { x: 408, y: 768 }, person: { x: 485, y: 860 } } };
const ROW_STEP = { x: 84, y: -42 };
function slotPos(side, row, i, n) { const c = ROWS[side][row], k = i - (n - 1) / 2; return { x: c.x + ROW_STEP.x * k, y: c.y + ROW_STEP.y * k }; }
const TURN_SECONDS = 30;

// 某个等级的裸属性，再乘上角色自己的倍率
function levelStats(L, m) {
  const b = D.base, g = D.grow; m = m || {};
  const st = { maxHp: Math.round((b.hp + g.hp * L) * (m.hp || 1)), maxMp: Math.round((b.mp + g.mp * L) * (m.mp || 1)), atk: Math.round((b.atk + g.atk * L) * (m.atk || 1)), def: Math.round((b.def + g.def * L) * (m.def || 1)), spd: Math.round((b.spd + g.spd * L) * (m.spd || 1)) };
  st.hp = st.maxHp; st.mp = st.maxMp; return st;
}

function makeUnit(side, row, def, pos) {
  const sz = AS.size[def.sprite], h = def.h, w = h * sz[0] / sz[1];
  const el = document.createElement('div'); el.className = 'unit ' + side + ' ' + row;
  el.innerHTML = '<div class="sh"></div><img alt="" src="' + IMG(def.sprite) + '" style="width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px' + (def.flip ? ';scale:-1 1' : '') + '">' +
    '<div class="tap" style="left:' + (-w / 2) + 'px;bottom:0;width:' + w + 'px;height:' + h + 'px"></div>' +
    '<div class="hpb" style="bottom:' + (h + 4) + 'px"><i></i></div>' + (row === 'person' ? '<div class="nm" style="bottom:' + (h + 13) + 'px">' + def.name + '</div>' : '');
  el.style.left = pos.x + 'px'; el.style.top = pos.y + 'px'; el.style.zIndex = Math.round(pos.y);
  $('units').appendChild(el);
  const st = def.stats;
  const u = { side, row, name: def.name, el, pos, h, hp: st.hp, maxHp: st.maxHp, mp: st.mp || 0, maxMp: st.maxMp || 0, atk: st.atk, def: st.def, spd: st.spd,
    alive: true, defending: false, ai: def.ai || 'attack', aggro: def.aggro || 1, data: def.data || null };
  if (side === 'enemy') el.addEventListener('click', () => { if (B.picking && u.alive) { const f = B.picking; B.picking = null; f(u); } });
  B.units.push(u); drawUnit(u); return u;
}
function drawUnit(u) {
  u.el.querySelector('.hpb i').style.width = Math.max(0, u.hp / u.maxHp * 100) + '%';
  if (u === B.player) { setBar('hp', u.hp, u.maxHp); setBar('mp', u.mp, u.maxMp); }
}
function floatText(u, text, cls) {
  const f = document.createElement('div'); f.className = 'float ' + (cls || ''); f.textContent = text;
  f.style.left = u.pos.x + rnd(-14, 14) + 'px'; f.style.top = (u.pos.y - u.h - 20) + 'px';
  $('units').appendChild(f); setTimeout(() => f.remove(), 950);
}
function slashFx(u, fire) {
  const s = document.createElement('div'); s.className = 'slash' + (fire ? ' fire' : ''); s.style.setProperty('--r', rnd(-50, -20) + 'deg');
  s.style.left = u.pos.x + 'px'; s.style.top = (u.pos.y - u.h * 0.5) + 'px'; $('units').appendChild(s); setTimeout(() => s.remove(), 320);
}
const bmsg = t => { $('bmsg').textContent = t; };
const living = side => B.units.filter(u => u.alive && u.side === side);
const other = side => side === 'ally' ? 'enemy' : 'ally';
const weakestOf = side => living(side).sort((a, b) => a.hp - b.hp)[0];

// teams = { ally: { persons: [...], pets: [...] }, enemy: { persons: [...], pets: [...] } }，每个成员是 makeUnit 用的 def
function beginBattle(mode, title, teams, names) {
  stop(); wander = false; autoBtn.classList.remove('on');
  $('units').innerHTML = '';
  B.active = true; B.mode = mode; B.round = 0; B.auto = false; B.over = null; B.units = []; B.player = null; B.title = title;
  stage.classList.add('fight');
  $('bnameE').textContent = names[0]; $('bnameA').textContent = names[1];
  ['enemy', 'ally'].forEach(side => ['person', 'pet'].forEach(row => {
    const list = teams[side][row === 'person' ? 'persons' : 'pets'];
    list.forEach((def, i) => { const u = makeUnit(side, row, def, slotPos(side, row, i, list.length)); if (def.me) B.player = u; });
  }));
  if (mode === 'arena') { const p = B.player; $('hudLv').textContent = D.arena.lv; $('hudPow').textContent = fmt(p.atk * 6 + p.def * 5 + p.maxHp / 2 + p.spd * 3); }
  drawUnit(B.player); drawCmd(); loop();
}
// 野外 / 剧情战斗：我方是主角 + 灵狐，敌方按关卡配置
function startBattle(st) {
  clampVitals();
  const ps = stats(), fs = petStats();
  B.stage = st;
  const foes = { persons: [], pets: [] };
  st.enemies.forEach(id => {
    const e = D.enemies[id];
    (e.pet ? foes.pets : foes.persons).push({ name: e.name, sprite: e.sprite, h: e.h * 1.12, flip: true, data: e, ai: e.boss ? 'boss' : 'attack',
      stats: { hp: e.hp, maxHp: e.hp, atk: e.atk, def: e.def, spd: e.spd } });
  });
  beginBattle('pve', st.name, {
    ally: { persons: [{ name: ROLE.name, sprite: 'b_player', h: 126, me: true, stats: { hp: Math.max(1, S.hp), maxHp: ps.maxHp, mp: S.mp, maxMp: ps.maxMp, atk: ps.atk, def: ps.def, spd: ps.spd } }],
            pets: [{ name: '灵狐', sprite: 'b_fox', h: 62, ai: 'fox', stats: { hp: fs.maxHp, maxHp: fs.maxHp, atk: fs.atk, def: fs.def, spd: fs.spd } }] },
    enemy: foes
  }, [st.name, '我方']);
}
// 竞技场：两支满编队伍（5 人 + 5 宠）按同一等级对打，不影响存档
function startArena() {
  const A = D.arena, L = A.lv;
  const build = (list, isAlly) => {
    const persons = [], pets = [];
    list.forEach(m => {
      persons.push({ name: m.name, sprite: m.sprite, h: m.h || 126, ai: m.ai, aggro: m.aggro, me: !!m.me, stats: levelStats(L, m.mult) });
      const pm = Object.assign({}, D.pet, m.pet.mult || {});
      pets.push({ name: m.pet.name, sprite: m.pet.sprite, h: m.pet.h, ai: m.pet.ai || 'attack', stats: levelStats(L, pm) });
    });
    return { persons, pets };
  };
  B.stage = null;
  beginBattle('arena', '竞技场', { ally: build(A.ally, true), enemy: build(A.foe, false) }, [A.foeName, A.allyName]);
}

async function loop() {
  bmsg(B.title); await sleep(800);
  while (!B.over) {
    B.round++; $('bround').textContent = '第 ' + B.round + ' 回合';
    B.units.forEach(u => { u.defending = false; });
    let act = B.auto ? autoAct() : await chooseAction();
    $('cmdList').innerHTML = ''; tip(''); $('bclock').textContent = '';
    if (act.type === 'flee') {
      if (B.mode === 'arena') { B.over = 'lose'; break; }
      const hasBoss = B.units.some(u => u.ai === 'boss');
      if (!hasBoss && Math.random() < 0.65) { B.over = 'flee'; break; }
      bmsg(hasBoss ? '头目堵住了去路，逃不掉！' : '逃跑失败！'); await sleep(600); act = { type: 'none' };
    }
    const order = B.units.filter(u => u.alive).map(u => ({ u, k: u.spd * rnd(0.9, 1.1) })).sort((a, b) => b.k - a.k).map(o => o.u);
    for (const u of order) {
      if (!u.alive || B.over) continue;
      u.el.classList.add('turn');
      await perform(u, u === B.player ? act : unitAct(u));
      u.el.classList.remove('turn');
      if (!living('enemy').length) B.over = 'win';
      else if (B.mode === 'arena' ? !living('ally').length : !B.player.alive) B.over = 'lose';
    }
  }
  clearInterval(B.clock); $('bclock').textContent = '';
  await sleep(500); finish();
}

// ---------- 指令 ----------
const tip = t => { $('cmdTip').textContent = t; };
function drawCmd() {
  const names = [['attack', '攻击'], ['skill', '技能'], ['item', '道具'], ['defend', '防御'], ['flee', B.mode === 'arena' ? '认输' : '逃跑'], ['auto', '自动']];
  $('cmdBtns').innerHTML = names.map(n => '<button type="button" class="tbtn' + (n[0] === 'auto' && B.auto ? ' red' : '') + '" data-cmd="' + n[0] + '">' + n[1] + '</button>').join('');
}
$('cmdBtns').addEventListener('click', e => { const b = e.target.closest('[data-cmd]'); if (b) onCmd(b.dataset.cmd); });
$('cmdList').addEventListener('click', e => { const b = e.target.closest('[data-pick]'); if (b && !b.disabled) onPick(b.dataset.pick); });
function chooseAction() {
  if (!B.player.alive) return Promise.resolve({ type: 'none' });      // 竞技场里主角倒下后，队友继续自动打
  tip('请选择行动');
  return new Promise(res => {
    B.resolve = a => { B.resolve = null; B.picking = null; clearInterval(B.clock); living('enemy').forEach(u => u.el.classList.remove('pick')); res(a); };
    // 出手倒计时：到 0 自动行动
    let left = TURN_SECONDS; $('bclock').textContent = left;
    clearInterval(B.clock);
    B.clock = setInterval(() => { left--; $('bclock').textContent = Math.max(0, left); if (left <= 0 && B.resolve) B.resolve(autoAct()); }, 1000);
  });
}
function pickTarget(make) {
  const es = living('enemy');
  if (es.length === 1) return B.resolve(make(es[0]));
  tip('点击要攻击的敌人'); $('cmdList').innerHTML = '';
  es.forEach(u => u.el.classList.add('pick'));
  B.picking = u => B.resolve(make(u));
}
function onCmd(cmd) {
  if (cmd === 'auto') { B.auto = !B.auto; drawCmd(); if (B.auto && B.resolve) B.resolve(autoAct()); return; }
  if (!B.resolve) return;
  B.picking = null; living('enemy').forEach(u => u.el.classList.remove('pick'));
  if (cmd === 'attack') pickTarget(t => ({ type: 'skill', skill: 'attack', target: t }));
  else if (cmd === 'defend') B.resolve({ type: 'defend' });
  else if (cmd === 'flee') B.resolve({ type: 'flee' });
  else if (cmd === 'skill') {
    tip('选择技能');
    $('cmdList').innerHTML = D.playerSkills.map(id => { const s = D.skills[id], lock = B.mode !== 'arena' && S.lv < (s.lv || 1);
      return '<button type="button" class="tbtn" data-pick="skill:' + id + '"' + (lock || B.player.mp < s.mp ? ' disabled' : '') + '>' + s.name + '<small>' + (lock ? s.lv + ' 级解锁' : '法力 ' + s.mp) + '</small></button>'; }).join('');
  } else if (cmd === 'item') {
    const ids = B.mode === 'arena' ? [] : Object.keys(S.items).filter(i => D.items[i].type === 'potion');
    tip(B.mode === 'arena' ? '竞技场里不能用药' : ids.length ? '选择道具' : '背包里没有可用的药');
    $('cmdList').innerHTML = ids.map(id => '<button type="button" class="tbtn" data-pick="item:' + id + '">' + D.items[id].name + '<small>剩余 ' + S.items[id] + '</small></button>').join('');
  }
}
function onPick(v) {
  if (!B.resolve) return;
  const [kind, id] = v.split(':');
  if (kind === 'item') return B.resolve({ type: 'item', item: id });
  const s = D.skills[id];
  if (s.target === 'one') pickTarget(t => ({ type: 'skill', skill: id, target: t }));
  else B.resolve({ type: 'skill', skill: id });
}
// 主角的自动出手
function autoAct() {
  const p = B.player, free = B.mode === 'arena', has = id => (free || S.lv >= D.skills[id].lv) && p.mp >= D.skills[id].mp;
  if (!p.alive) return { type: 'none' };
  if (p.hp < p.maxHp * 0.35) {
    if (has('ningshen')) return { type: 'skill', skill: 'ningshen' };
    if (!free && S.items.hp_potion) return { type: 'item', item: 'hp_potion' };
  }
  if (living('enemy').length >= 3 && has('zongheng')) return { type: 'skill', skill: 'zongheng' };
  if (p.mp >= D.skills.pokong.mp + 80) return { type: 'skill', skill: 'pokong', target: weakestOf('enemy') };
  return { type: 'skill', skill: 'attack', target: weakestOf('enemy') };
}
// 其余单位的自动出手，按 ai 类型决定（见 data.js 里各角色的 ai 字段）
function unitAct(u) {
  const foeSide = other(u.side), foes = living(foeSide), mates = living(u.side);
  const hurt = mates.slice().sort((a, b) => a.hp / a.maxHp - b.hp / b.maxHp)[0];
  const low = weakestOf(foeSide);
  // 随机挑一个目标，aggro 越高越容易被打
  const pickFoe = () => { let sum = 0; foes.forEach(f => { sum += f.aggro; }); let r = Math.random() * sum; for (const f of foes) { r -= f.aggro; if (r <= 0) return f; } return foes[0]; };
  switch (u.ai) {
    case 'heal':    return hurt && hurt.hp < hurt.maxHp * 0.6 ? { type: 'skill', skill: 'heal_ally', target: hurt } : { type: 'skill', skill: 'attack', target: pickFoe() };
    case 'mage':    return B.round % 3 === 0 ? { type: 'skill', skill: 'rain' } : { type: 'skill', skill: 'bolt', target: pickFoe() };
    case 'striker': return { type: 'skill', skill: 'snipe', target: low };
    case 'tank':    return { type: 'skill', skill: 'smash', target: pickFoe() };
    case 'boss':    return B.round % 3 === 0 ? { type: 'skill', skill: 'sweep' } : { type: 'skill', skill: 'attack', target: pickFoe() };
    case 'fox':     return { type: 'skill', skill: Math.random() < 0.35 ? 'foxfire' : 'bite', target: low };
    default:        return { type: 'skill', skill: u.row === 'pet' ? 'bite' : 'attack', target: pickFoe() };
  }
}

// ---------- 出手 ----------
async function perform(u, act) {
  if (!act || act.type === 'none') return;
  if (act.type === 'defend') { u.defending = true; bmsg(u.name + ' 摆出防御架势'); floatText(u, '防御', 'info'); await sleep(420); return; }
  if (act.type === 'item') {
    const it = D.items[act.item]; if (!S.items[act.item]) return;
    addItem(act.item, -1); u.hp = Math.min(u.maxHp, u.hp + (it.hp || 0)); u.mp = Math.min(u.maxMp, u.mp + (it.mp || 0));
    bmsg(u.name + ' 使用了' + it.name); floatText(u, '+' + (it.hp || it.mp), 'heal'); drawUnit(u); await sleep(480); return;
  }
  const s = D.skills[act.skill];
  if (s.mp) { if (u.mp < s.mp) return perform(u, { type: 'skill', skill: 'attack', target: weakestOf(other(u.side)) }); u.mp -= s.mp; drawUnit(u); }
  bmsg(act.skill === 'attack' || act.skill === 'bite' ? '' : u.name + ' · ' + s.name);
  if (s.heal) {
    const t = act.target && act.target.alive ? act.target : u, v = Math.round(t.maxHp * s.heal);
    t.hp = Math.min(t.maxHp, t.hp + v); floatText(t, '+' + v, 'heal'); drawUnit(t); await sleep(480); return;
  }
  const foes = living(other(u.side));
  if (!foes.length) return;
  const targets = s.target === 'all' ? foes : [act.target && act.target.alive && act.target.side !== u.side ? act.target : foes[0]];
  // 冲上去
  const t0 = targets[0], k = s.target === 'all' ? 0.4 : 0.7;
  u.el.style.zIndex = 1500;
  u.el.style.transform = 'translate(' + ((t0.pos.x - u.pos.x) * k) + 'px,' + ((t0.pos.y - u.pos.y) * k) + 'px)';
  await sleep(170);
  for (const t of targets) {
    const crit = Math.random() < 0.1;
    let dmg = u.atk * s.power * rnd(0.9, 1.1) * (crit ? 1.5 : 1) - t.def * 0.6;
    if (t.defending) dmg *= 0.5;
    dmg = Math.max(1, Math.round(dmg));
    t.hp = Math.max(0, t.hp - dmg);
    slashFx(t, !!s.fire); floatText(t, (crit ? '暴击 ' : '') + '-' + dmg, crit ? 'crit' : '');
    t.el.classList.remove('hit'); void t.el.offsetWidth; t.el.classList.add('hit');
    drawUnit(t);
    if (t.hp <= 0) { t.alive = false; t.el.classList.add('dead'); }
    if (targets.length > 1) await sleep(60);
  }
  await sleep(200);
  u.el.style.transform = ''; u.el.style.zIndex = Math.round(u.pos.y);
  await sleep(200);
}

// ---------- 结算 ----------
function finish() {
  const st = B.stage, res = B.over;
  let title, h;
  if (B.mode === 'arena') {
    title = res === 'win' ? '对战胜利' : '对战失败';
    h = '<p class="center">' + (res === 'win' ? D.arena.allyName + ' 击败了 ' + D.arena.foeName + '。' : D.arena.foeName + ' 技高一筹。') + '<br>共 ' + B.round + ' 回合。竞技场是演示对战，不影响等级和背包。</p>' +
      '<p class="center" style="margin:18px 0 0"><button type="button" class="tbtn red" data-act="arena">再来一局</button>　<button type="button" class="tbtn" data-act="leave">离开</button></p>';
    openModal(title, h, leaveBattle); return;
  }
  S.hp = Math.max(1, B.player.hp); S.mp = B.player.mp;
  if (res === 'win') {
    const r = { exp: 0, coin: 0, items: {} };
    st.enemies.forEach(id => { const e = D.enemies[id]; r.exp += e.exp; r.coin += e.coin; e.drops.forEach(d => { if (Math.random() < d[1]) r.items[d[0]] = (r.items[d[0]] || 0) + 1; }); });
    if (!S.cleared.includes(st.id)) S.cleared.push(st.id);
    const q = D.quests[S.step]; let questDone = false;
    if (q && q.type === 'win' && q.stage === st.id) { S.step++; questDone = true; }
    const lv0 = S.lv; B.active = false; const ups = grant(r); B.active = true;
    title = '战斗胜利';
    h = '<div class="gain center">经验 <b>+' + r.exp + '</b>　银两 <b>+' + r.coin + '</b>';
    const drops = Object.keys(r.items);
    if (drops.length) h += '<br>获得 ' + drops.map(id => '<b>' + D.items[id].name + ' ×' + r.items[id] + '</b>').join('、');
    if (ups) h += '<br><b>升级！</b>' + lv0 + ' 级 → ' + S.lv + ' 级，气血法力回满';
    if (questDone) h += '<br><span style="color:var(--jade)">任务完成：' + q.title + '</span>';
    h += '</div>';
    sys('击败' + st.name + '的敌人：' + rewardText(r));
  } else if (res === 'lose') {
    S.hp = Math.round(stats().maxHp * 0.3); title = '战败';
    h = '<p class="center">你被抬回了' + D.maps[M.def.home || M.id].name + '。<br>找人歇一歇可以回满气血，等级和装备不够就先打前面的敌人。</p>';
  } else { title = '成功逃脱'; h = '<p class="center">你带着灵狐退开了几步。</p>'; }
  save();
  h += '<p class="center" style="margin:18px 0 0"><button type="button" class="tbtn" data-act="leave">' + (res === 'lose' ? '回去休整' : '继续') + '</button></p>';
  openModal(title, h, leaveBattle);
}
function leaveBattle() {
  if (!B.active) return;
  const arena = B.mode === 'arena';
  B.active = false; stage.classList.remove('fight'); $('units').innerHTML = ''; B.units = [];
  if (!arena) {
    // 打赢的敌人从地图上消失（重新进图会再出现）；打输回城
    if (B.over === 'win' && B.foe) { B.foe.el.remove(); M.list = M.list.filter(a => a !== B.foe); }
    if (B.over === 'lose') loadMap(M.def.home || M.id);
  }
  B.foe = null;
  last = performance.now(); save(); refresh();
  const q = D.quests[S.step]; if (!arena && q && B.over === 'win') toast('任务更新：' + q.goal);
}

// ============================================================ 启动
sys('点击地面行走，点击人物对话');
say('<span style="color:#ffc860">[世界]</span> <span style="color:#a8e6ff">凌霄阁主</span>：汉中郡外竹林有山贼，组队的来～');
say('<span style="color:#7fe07f">[帮派]</span> <span style="color:#a8e6ff">青锋</span>：今晚八点帮战，全员集合！');
$('btns').querySelectorAll('.dot').forEach(x => { x.hidden = !!S.gift; });
loadMap(D.maps[S.map] ? S.map : D.start.map, S.pos); refresh(); requestAnimationFrame(frame);
// 给自动化测试和调试用
// ---------- 和登录 / 选角界面的接口 ----------
// 进入游戏：换成这个角色自己的存档，名字、职业、头像跟着换
function enterGame(role, serverName) {
  ROLE = role; SAVE_KEY = 'guiqin-save:' + role.id;
  S = load() || newGame(); bagSel = null;
  const c = D.classes[role.cls];
  player.name = role.name; player.title = '【' + c.name + '】'; drawTag(player);
  $('hudName').textContent = role.name; $('hudSect').textContent = c.name; $('hudFace').src = IMG('head_' + role.cls);
  $('btns').querySelectorAll('.dot').forEach(x => { x.hidden = !!S.gift; });
  $('chatLog').innerHTML = '';
  sys('欢迎来到 ' + serverName + '，' + role.name);
  sys('点击地面行走，按住可以拖着走；点击人物对话');
  loadMap(D.maps[S.map] ? S.map : D.start.map, S.pos); refresh(); save(); last = performance.now();
}
function peekLevel(id) { try { const t = localStorage.getItem('guiqin-save:' + id); if (t) return JSON.parse(t).lv || D.start.lv; } catch (e) {} return D.start.lv; }
function wipe(id) { try { localStorage.removeItem('guiqin-save:' + id); } catch (e) {} }
window.GAME = { goTalk, get state() { return S; }, get map() { return M; }, battle: B, player, startBattle, startArena, loadMap, data: D, enter: enterGame, peekLevel, wipe };
})();
