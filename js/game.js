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
$('hudFace').src = IMG('head_player'); $('hudPetImg').src = IMG('b_fox');

// ============================================================ 存档与属性
let SAVE_KEY = 'guiqin-save:guest';      // 每个角色一份存档，进游戏时换成 guiqin-save:<角色 id>
let ROLE = { id: 'guest', name: '云中客', cls: 'novice_m' };
function newGame() {
  const s = JSON.parse(JSON.stringify(D.start));
  return { lv: s.lv, exp: 0, hp: null, mp: null, coin: s.coin, items: s.items, equip: s.equip, step: 0, cnt: 0, sv: 2, cleared: [], gift: false, map: s.map, pos: null, visited: [] };
}
function load() {
  try { const t = localStorage.getItem(SAVE_KEY); if (t) { const s = JSON.parse(t); if (s && typeof s.lv === 'number') { if (s.sv !== 2) { s.step = 0; s.cnt = 0; s.sv = 2; }      // 换了新主线：旧存档的任务进度从头来，等级装备保留
        const e = s.equip || (s.equip = {}); if ('armor' in e) { e.chest = e.chest || e.armor; delete e.armor; } if ('charm' in e) { e.pouch = e.pouch || e.charm; delete e.charm; } for (const k of Object.keys(e)) if (!D.slots[k]) delete e[k]; return s; } } } catch (e) {}
  return null;
}
function save() { try { localStorage.setItem(SAVE_KEY, JSON.stringify(S)); } catch (e) {} }
let S = load() || newGame();

// 属性：物攻 atk / 法攻 matk / 生命 maxHp / 物防 def / 法防 mdef / 魔法 maxMp / 闪避 dodge% / 速度 spd / 暴击 crit%
const STATKEY = { atk: 'atk', matk: 'matk', hp: 'maxHp', def: 'def', mdef: 'mdef', mp: 'maxMp', dodge: 'dodge', spd: 'spd', crit: 'crit' };
const PCT = { dodge: 1, crit: 1 };
const fmtStat = (k, v) => PCT[k] ? (Math.round(v * 10) / 10) + '%' : fmt(v);
function getGear(slot) { return (S.gear && S.gear[slot]) || { star: 0, gems: [] }; }
const socketCount = star => D.forge.sockets.filter(n => star >= n).length;
// 某个部位的加成：装备自带属性 ×(1+星级加成)，再加宝石
function slotBonus(slot) {
  const out = {}, it = D.items[S.equip[slot]]; if (!it) return out;
  const gr = getGear(slot), mul = 1 + gr.star * D.forge.perStar;
  for (const k in STATKEY) { if (it[k]) out[k] = (out[k] || 0) + (PCT[k] ? it[k] : Math.round(it[k] * mul)); }
  (gr.gems || []).forEach(gid => { const g = gid && D.items[gid]; if (g) out[g.gem] = (out[g.gem] || 0) + D.gems[g.gem].vals[g.lv - 1]; });
  return out;
}
function gearSum() { const t = {}; for (const slot in S.equip) { const o = slotBonus(slot); for (const k in o) t[k] = (t[k] || 0) + o[k]; } return t; }
const curCls = () => D.classes[ROLE.cls] || D.classes.novice_f;
const clsName = c => c.name === '道家弟子' ? c.name : (c.sect ? c.sect + '·' : '') + c.name;
// 职业倍率（按基础+成长算）和心法加成（每级固定值，对应原作的“心法”被动）
const clsMul = k => (curCls().mod || {})[k] || 1;
const heartBonus = k => { const h = curCls().heart; return h && h.perLv[k] ? h.perLv[k] * S.lv : 0; };
const playerSkills = () => curCls().skills || D.playerSkills;
// 技能修炼：S.skl = { 技能id: 等级 }。有门派技能表的职业必须“学过”才能用；新手用通用技能，仍按人物等级解锁
const skl = id => (S.skl && S.skl[id]) || 0;
const skillUsable = id => !!D.skills[id] && (curCls().skills ? skl(id) > 0 : S.lv >= (D.skills[id].lv || 1));
const skillMul = id => 1 + 0.12 * (Math.max(1, skl(id)) - 1);          // 技能每升 1 级，威力 / 治疗 +12%
function ensureSkl() {
  if (!S.skl) { S.skl = {}; if (curCls().skills) curCls().skills.forEach(id => { if (S.lv >= (D.skills[id].lv || 1)) S.skl[id] = 1; }); }   // 旧存档：按等级把已解锁的算作已学
}
function grantInitSkills(id) { ensureSkl(); const c = D.classes[id]; if (c && c.skills) c.skills.slice(0, 2).forEach(k => { if (!S.skl[k]) S.skl[k] = 1; }); }   // 入门送前两个技能 1 级

function stats() {
  const b = D.base, g = D.grow, L = S.lv, m = clsMul;
  const st = { maxHp: (b.hp + g.hp * L) * m('hp'), maxMp: (b.mp + g.mp * L) * m('mp'), atk: (b.atk + g.atk * L) * m('atk'), matk: (b.matk + g.matk * L) * m('matk') + heartBonus('matk'), def: (b.def + g.def * L) * m('def'), mdef: (b.mdef + g.mdef * L) * m('mdef') + heartBonus('mdef'), spd: b.spd + g.spd * L, crit: b.crit, dodge: b.dodge };
  for (const k of ['maxHp', 'maxMp', 'atk', 'matk', 'def', 'mdef']) st[k] = Math.round(st[k]);
  const gs = gearSum(); for (const k in gs) st[STATKEY[k]] += gs[k];
  return st;
}
function bondInfo() {
  const n = S.bond || 0, T = D.bondTiers; let i = 0; while (i + 1 < T.length && n >= T[i + 1].at) i++;
  return { n, i, name: T[i].name, from: T[i].at, to: i + 1 < T.length ? T[i + 1].at : null, bonus: i * 0.03 };
}
const ownPet = id => id === 'fox' || !!(D.petDex[id] && D.petDex[id].own) || !!(S.petOwn && S.petOwn[id]);
const petAct = () => (S.petAct && D.petDex[S.petAct] && ownPet(S.petAct)) ? S.petAct : 'fox';
function petStats(id) {
  const dx = D.petDex[id || petAct()] || {}, b = D.base, g = D.grow, L = S.lv, p = dx.mult || D.pet, k = 1 + bondInfo().bonus;
  return { maxHp: Math.round((b.hp + g.hp * L) * p.hp * k), atk: Math.round((b.atk + g.atk * L) * p.atk * k), matk: Math.round((b.matk + g.matk * L) * p.atk * k), def: Math.round((b.def + g.def * L) * p.def), mdef: Math.round((b.mdef + g.mdef * L) * p.def), crit: 10, dodge: 3, spd: Math.round((b.spd + g.spd * L) * p.spd) };
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
    S.bond = (S.bond || 0) + 1;   // 每打赢一场，灵狐的亲密度 +1
    S.exp += r.exp;
    while (S.exp >= expNeed()) { S.exp -= expNeed(); S.lv++; ups++; }
    if (ups) { S.hp = null; S.mp = null; if (S.lv % 5 === 0) window.CHAT.notice('恭喜玩家「' + ROLE.name + '」等级突破 ' + S.lv + ' 级！'); }
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
const sys = t => window.CHAT.sys(t);

// ============================================================ 弹窗
let modalOnClose = null;
function openModal(title, html, onClose) { $('mTitle').textContent = title; $('mBody').innerHTML = html; $('modal').hidden = false; modalOnClose = onClose || null; }
function closeModal() { $('modal').hidden = true; const f = modalOnClose; modalOnClose = null; if (f) f(); }
$('mClose').addEventListener('click', closeModal);
$('modal').addEventListener('click', e => { e.stopPropagation(); if (e.target === $('modal')) closeModal(); });
// 弹窗里的所有按钮都用 data-act="动作:参数"
$('mBody').addEventListener('click', e => {
  const el = e.target.closest('[data-act]'); if (!el || el.disabled) return;
  const _i = el.dataset.act.indexOf(':'), act = _i < 0 ? el.dataset.act : el.dataset.act.slice(0, _i), arg = _i < 0 ? undefined : el.dataset.act.slice(_i + 1);
  ACTS[act] && ACTS[act](arg);
});
const itemImg = id => '<img alt="" src="' + IMG('item_' + D.items[id].icon) + '">';
function itemBonus(it) {
  return Object.keys(STATKEY).filter(k => it[k]).map(k => D.statName[k] + ' +' + it[k] + (PCT[k] ? '%' : '')).join('　');
}

let bagSel = null;
let bagTab = 'all', lastPanel = 'bag', eqSel = null;
const BAGCAT = { all: '全部', potion: '药品', equip: '装备', gem: '宝石', mat: '材料' }, TYPENAME = { potion: '药品', equip: '装备', gem: '宝石', mat: '材料' }, SLOTNAME = D.slots;
const itemQ = id => D.quality[(D.items[id] && D.items[id].q) || 1];
const qHi = id => ((D.items[id] && D.items[id].q) || 1) >= 4 ? ' hi' : '';
function bagDelta(it) {   // 对比当前同部位装备（只比装备自带属性）
  const cur = D.items[S.equip[it.slot]] || {}, out = [];
  Object.keys(STATKEY).forEach(k => { const dv = (it[k] || 0) - (cur[k] || 0); if (dv) out.push('<em class="' + (dv > 0 ? 'up' : 'dn') + '">' + D.statName[k] + (dv > 0 ? ' ▲' : ' ▼') + Math.abs(dv) + '</em>'); });
  return out.join(' ');
}
function openBag() {
  lastPanel = 'bag';
  const all = Object.keys(S.items), ids = all.filter(id => bagTab === 'all' || D.items[id].type === bagTab);
  if (!ids.includes(bagSel)) bagSel = ids[0] || null;
  const cap = D.bagSize;
  let h = '<div class="bagtop"><div class="rtabs">' + Object.keys(BAGCAT).map(k => '<button type="button" class="rtab' + (bagTab === k ? ' on' : '') + '" data-act="bagTab:' + k + '">' + BAGCAT[k] + '</button>').join('') + '</div>' +
    '<div class="bagcap"><span>' + all.length + ' / ' + cap + '</span><button type="button" class="tbtn" data-act="bagSort">整理</button></div></div><div class="grid">';
  ids.forEach(id => { const it = D.items[id], c = itemQ(id).color, eq = it.type === 'equip';
    h += '<button type="button" class="tile q' + qHi(id) + (id === bagSel ? ' sel' : '') + '" style="--qc:' + c + '" data-act="sel:' + id + '" aria-label="' + it.name + '">' + itemImg(id) + (S.items[id] > 1 || !eq ? '<b>' + S.items[id] + '</b>' : '') + (eq && it.slot ? '<u>' + SLOTNAME[it.slot] + '</u>' : '') + '</button>'; });
  for (let i = ids.length; i < (bagTab === 'all' ? cap : Math.max(10, Math.ceil(ids.length / 5) * 5)); i++) h += '<div class="tile empty"></div>';
  h += '</div>';
  if (bagSel) {
    const it = D.items[bagSel], q = itemQ(bagSel), n = S.items[bagSel];
    let btns = '';
    if (it.type === 'potion') btns += '<button type="button" class="tbtn red" data-act="use:' + bagSel + '">使用</button>';
    if (it.type === 'equip') btns += '<button type="button" class="tbtn red" data-act="equip:' + bagSel + '">装备</button>';
    if (it.price > 0 && it.type !== 'potion') btns += '<button type="button" class="tbtn" data-act="bagSell:' + bagSel + '">出售 ' + it.price + '</button>';
    h += '<div class="idetail"><div class="itile" style="--qc:' + q.color + '">' + itemImg(bagSel) + '</div><div class="ibody"><h5>' + it.name + ' <span class="qual" style="color:' + q.color + ';border-color:' + q.color + '">' + q.name + '</span></h5>' +
      '<p class="meta">' + TYPENAME[it.type] + (it.slot ? ' · ' + SLOTNAME[it.slot] : '') + '　持有 ' + n + (it.price > 0 ? '　单价 ' + it.price + ' 两' : '　<b>任务物品</b>') + '</p>' +
      (it.type === 'equip' ? '<p class="bonus">' + itemBonus(it) + '</p><p class="cmp">' + (S.equip[it.slot] ? '对比当前「' + D.items[S.equip[it.slot]].name + '」：' + (bagDelta(it) || '无差别') : '该部位空着，装上就是净提升') + '</p>' : '') +
      '<p>' + it.desc + '</p></div></div><div class="ibtns">' + btns + '</div>';
  } else h += '<div class="idetail"><div class="ibody"><p>' + (all.length ? '这个分类下没有物品。' : '背包是空的。') + '</p></div></div>';
  // 已装备
  h += '<div class="eqbar"><small>已装备</small>' + Object.keys(SLOTNAME).map(sl => { const id = S.equip[sl], it = id && D.items[id];
    return '<div class="eqs' + (id ? '' : ' empty') + '" style="--qc:' + (id ? itemQ(id).color : 'rgba(226,176,74,.25)') + '">' + (id ? itemImg(id) : '') + '<span>' + (it ? it.name : SLOTNAME[sl]) + '</span></div>'; }).join('') +
    '<b class="coinb">银两 ' + fmt(S.coin) + '</b></div>';
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
const powerOf = st => Math.round(st.atk * 6 + (st.matk || 0) * 4 + st.def * 5 + (st.mdef || 0) * 3 + st.maxHp / 2 + (st.maxMp || 0) / 4 + st.spd * 3 + (st.crit || 0) * 12 + (st.dodge || 0) * 12);
function reopen() { if (lastPanel === 'role') openRole(); else openBag(); }
// 装备页：上面人物预览 + 装备栏，下面背包格子，点格子看详情、对比、穿戴
function equipView() {
  const st = stats(), IT = D.items, ids = Object.keys(S.items);
  if (!eqSel || (eqSel.t === 'bag' && !S.items[eqSel.v]) || (eqSel.t === 'slot' && !S.equip[eqSel.v] && false)) eqSel = null;
  if (!eqSel) { const first = ids.find(i => IT[i].type === 'equip') || ids[0]; eqSel = first ? { t: 'bag', v: first } : null; }
  const slotTile = sl => { const id = S.equip[sl], q = id ? itemQ(id).color : 'rgba(226,176,74,.3)', on = eqSel && eqSel.t === 'slot' && eqSel.v === sl;
    return '<button type="button" class="dslot' + (id ? qHi(id) : ' empty') + (on ? ' sel' : '') + '" style="--qc:' + q + '" data-act="eqSlot:' + sl + '" aria-label="' + SLOTNAME[sl] + '">' + (id ? itemImg(id) : '') + (getGear(sl).star ? '<i class="sstar">★' + getGear(sl).star + '</i>' : '') + '<span>' + SLOTNAME[sl] + '</span></button>'; };
  let h = '<div class="doll"><div class="dglow"></div><div class="dcol dl">' + D.slotLeft.map(slotTile).join('') + '</div><div class="dcol dr">' + D.slotRight.map(slotTile).join('') + '</div>' +
    '<div class="dname"><b>' + ROLE.name + '</b><small>Lv.' + S.lv + ' · ' + clsName(D.classes[ROLE.cls]) + '</small></div><div class="dpow">战力 <b>' + fmt(powerOf(st)) + '</b></div>' +
    '<img alt="" src="' + IMG('cls_' + ROLE.cls) + '">' +
    '<div class="dstats"><div>攻击<b>' + Math.round(st.atk) + '</b></div><div>防御<b>' + Math.round(st.def) + '</b></div><div>速度<b>' + Math.round(st.spd) + '</b></div><div>气血<b>' + Math.round(st.maxHp) + '</b></div></div></div>';
  h += '<div class="grid eqgrid">';
  ids.forEach(id => { const it = IT[id], on = eqSel && eqSel.t === 'bag' && eqSel.v === id;
    h += '<button type="button" class="tile q' + qHi(id) + (on ? ' sel' : '') + (it.type === 'equip' ? '' : ' dim') + '" style="--qc:' + itemQ(id).color + '" data-act="eqBag:' + id + '" aria-label="' + it.name + '">' + itemImg(id) + (it.type !== 'equip' || S.items[id] > 1 ? '<b>' + S.items[id] + '</b>' : '') + (it.slot ? '<u>' + SLOTNAME[it.slot] + '</u>' : '') + '</button>'; });
  for (let i = ids.length; i < Math.max(10, Math.ceil(ids.length / 5) * 5); i++) h += '<div class="tile empty"></div>';
  h += '</div>';
  if (eqSel) {
    const id = eqSel.t === 'slot' ? S.equip[eqSel.v] : eqSel.v, it = id && IT[id];
    if (!it) h += '<div class="idetail"><div class="ibody"><p>「' + SLOTNAME[eqSel.v] + '」还空着。点下面背包里带「' + SLOTNAME[eqSel.v] + '」标记的装备就能穿上，铁匠铺和百宝摊也有卖。</p></div></div>';
    else { const q = itemQ(id); let btn = '';
      if (eqSel.t === 'slot') btn = '<button type="button" class="tbtn" data-act="unequip:' + eqSel.v + '">卸下</button>';
      else if (it.type === 'equip') btn = '<button type="button" class="tbtn red" data-act="equip:' + id + '">装备</button>';
      else if (it.type === 'potion') btn = '<button type="button" class="tbtn red" data-act="use:' + id + '">使用</button>';
      h += '<div class="idetail"><div class="itile" style="--qc:' + q.color + '">' + itemImg(id) + '</div><div class="ibody"><h5>' + it.name + ' <span class="qual" style="color:' + q.color + ';border-color:' + q.color + '">' + q.name + '</span></h5>' +
        '<p class="meta">' + TYPENAME[it.type] + (it.slot ? ' · ' + SLOTNAME[it.slot] : '') + (eqSel.t === 'slot' ? '　已装备' : '　持有 ' + S.items[id]) + '</p>' +
        (it.type === 'equip' ? '<p class="bonus">' + itemBonus(it) + (eqSel.t === 'slot' && getGear(eqSel.v).star ? '　<span class="gold2">★' + getGear(eqSel.v).star + ' 锻造 +' + Math.round(getGear(eqSel.v).star * D.forge.perStar * 100) + '%</span>' : '') + '</p>' + (eqSel.t === 'bag' ? '<p class="cmp">' + (S.equip[it.slot] ? '对比当前「' + IT[S.equip[it.slot]].name + '」：' + (bagDelta(it) || '无差别') : '该部位空着，装上就是净提升') + '</p>' : '') : '') +
        '<p>' + it.desc + '</p>' + (eqSel.t === 'slot' ? '<p class="hint" style="margin:4px 0 0">锻造升星、镶嵌宝石：去汉中郡找王铁匠。</p>' : '') + '</div><div class="ibtn1">' + btn + '</div></div>'; }
  }
  return h + '<p class="center" style="margin:12px 0 0"><button type="button" class="tbtn" data-act="openBag">打开完整背包（整理 / 出售）</button></p>';
}
let forgeSlot = null, gemPick = null, forgeGem = null;
const forgeable = () => D.slotLeft.concat(D.slotRight).filter(sl => S.equip[sl]);
function starsRow(n) { let h = ''; for (let i = 1; i <= D.forge.max; i++) h += '<i class="fs' + (i <= n ? ' on' : '') + (i % 5 === 0 ? ' m' : '') + '">★</i>'; return h; }
function forgeView(mode) {
  const IT = D.items, F = D.forge, list = forgeable();
  if (!forgeSlot || !S.equip[forgeSlot]) forgeSlot = list[0] || null;
  let h = '<div class="fstrip">' + D.slotLeft.concat(D.slotRight).map(sl => { const id = S.equip[sl], gr = getGear(sl);
    return '<button type="button" class="fchip' + (sl === forgeSlot ? ' sel' : '') + (id ? qHi(id) : ' empty') + '" style="--qc:' + (id ? itemQ(id).color : 'rgba(226,176,74,.3)') + '" data-act="forgeSlot:' + sl + '" aria-label="' + D.slots[sl] + '">' + (id ? itemImg(id) : '') + '<span>' + D.slots[sl] + '</span>' + (gr.star ? '<i>★' + gr.star + '</i>' : '') + '</button>'; }).join('') + '</div>';
  if (!forgeSlot) return h + '<div class="idetail"><div class="ibody"><p>还没有穿任何装备。先到「装备」页穿上一件，再回来锻造。</p></div></div>';
  const id = S.equip[forgeSlot], it = IT[id], q = itemQ(id), gr = getGear(forgeSlot), s0 = gr.star, mx = s0 >= F.max;
  const base = Object.keys(STATKEY).filter(k => it[k]).map(k => D.statName[k] + ' ' + fmtStat(k, it[k]) + (PCT[k] || !s0 ? '' : ' → <b>' + fmtStat(k, Math.round(it[k] * (1 + s0 * F.perStar))) + '</b>')).join('　');
  h += '<div class="fcard"><div class="itile" style="--qc:' + q.color + '">' + itemImg(id) + '</div><div class="ibody"><h5 style="color:' + q.color + '">' + it.name + ' <span class="qual" style="color:' + q.color + ';border-color:' + q.color + '">' + q.name + '</span></h5>' +
    '<p class="meta">' + D.slots[forgeSlot] + '　' + (s0 ? '★' + s0 + '　' : '') + '星级和宝石都跟着部位走，换装不丢</p><p class="bonus">' + base + '</p></div></div>';
  if (mode === 'forge') h += '<div class="fstars">' + starsRow(s0) + '</div><p class="fsub">' + s0 + ' / ' + F.max + ' 星　基础属性 +' + Math.round(s0 * F.perStar * 100) + '%' + (mx ? '　<b>已满星</b>' : '') + '</p>';
  if (mode === 'forge' && !mx) {
    const mul = F.qMul[it.q || 1] || 1, coin = Math.round(F.coin[s0] * mul), base = F.rate[s0], drop = s0 >= F.dropFrom;
    const fids = ['forge_1', 'forge_2', 'forge_3', 'forge_4', 'forge_5'];
    if (!forgeGem || !S.items[forgeGem]) forgeGem = fids.find(i => S.items[i]) || forgeGem || 'forge_1';
    const have = S.items[forgeGem] || 0, gi = D.items[forgeGem], rate = gi.forge >= 5 ? 1 : Math.min(1, base + F.gemBonus[gi.forge - 1]);
    const ok = S.coin >= coin && have >= 1;
    h += '<h4 class="fh">选一颗锻造宝石<small>　锻造必须消耗一颗，品质越高成功率越高</small></h4><div class="fgems">' + fids.map(i => { const n = S.items[i] || 0, g = D.items[i], q = itemQ(i);
      return '<button type="button" class="fgem' + (i === forgeGem ? ' sel' : '') + (n ? '' : ' none') + qHi(i) + '" style="--qc:' + q.color + '" data-act="forgeGem:' + i + '" aria-label="' + g.name + '">' + itemImg(i) + '<b>' + n + '</b><small>' + (g.forge >= 5 ? '必成' : '+' + Math.round(F.gemBonus[g.forge - 1] * 100) + '%') + '</small></button>'; }).join('') + '</div>' +
      '<div class="fcost"><div><small>锻造到 ' + (s0 + 1) + ' 星</small></div><div class="' + (S.coin >= coin ? '' : 'lack') + '">银两 ' + fmt(coin) + ' <small>（有 ' + fmt(S.coin) + '）</small></div><div class="' + (have >= 1 ? '' : 'lack') + '" style="color:' + itemQ(forgeGem).color + '">' + gi.name + ' ×1 <small>（有 ' + have + '）</small></div>' +
      '<div>成功率 <b class="' + (rate >= .7 ? 'okc' : rate >= .4 ? 'midc' : 'badc') + '">' + Math.round(rate * 100) + '%</b> <small>' + (gi.forge >= 5 ? '必定成功' : '基础 ' + Math.round(base * 100) + '% + 宝石 ' + Math.round(F.gemBonus[gi.forge - 1] * 100) + '%') + '</small></div>' +
      '<div class="fwide"><small>' + (gi.forge >= 5 ? '' : drop ? '失败掉 1 星，宝石和银两都会消耗' : '失败不掉星，宝石和银两仍会消耗') + '</small></div></div>' +
      '<p class="center" style="margin:10px 0 0"><button type="button" class="tbtn red forgebtn" data-act="forge:' + forgeSlot + '"' + (ok ? '' : ' disabled') + '>锻造</button></p>';
  }
  if (mode !== 'inlay') return h;
  // 宝石孔
  const nSock = socketCount(s0);
  h += '<h4 class="fh">宝石孔　<small>5 / 10 / 15 星各解锁一个孔</small></h4><div class="sockets">';
  for (let i = 0; i < F.sockets.length; i++) {
    if (i >= nSock) { h += '<div class="sock lock"><span>🔒</span><small>' + F.sockets[i] + '星</small></div>'; continue; }
    const gid = gr.gems && gr.gems[i], g = gid && IT[gid];
    h += g ? '<button type="button" class="sock on' + qHi(gid) + '" style="--qc:' + itemQ(gid).color + '" data-act="gemOff:' + forgeSlot + ':' + i + '">' + itemImg(gid) + '<small>' + D.statName[g.gem] + ' +' + D.gems[g.gem].vals[g.lv - 1] + (PCT[g.gem] ? '%' : '') + '</small></button>'
      : '<button type="button" class="sock' + (gemPick && gemPick.slot === forgeSlot && gemPick.i === i ? ' sel' : '') + '" data-act="gemPick:' + forgeSlot + ':' + i + '"><span>＋</span><small>空孔</small></button>';
  }
  h += '</div>';
  if (gemPick && gemPick.slot === forgeSlot) {
    const gems = Object.keys(S.items).filter(i => IT[i].type === 'gem');
    h += '<p class="fsub">选一颗宝石镶进第 ' + (gemPick.i + 1) + ' 个孔：</p>' + (gems.length ? '<div class="grid gemgrid">' + gems.map(i => '<button type="button" class="tile q' + qHi(i) + '" style="--qc:' + itemQ(i).color + '" data-act="gemSet:' + i + '" aria-label="' + IT[i].name + '">' + itemImg(i) + '<b>' + S.items[i] + '</b><u>' + D.statName[IT[i].gem] + '</u></button>').join('') + '</div>' : '<p class="hint">背包里没有宝石。百宝摊（寄卖行掌柜）有卖一阶宝石。</p>');
  } else h += '<p class="hint">点空孔选宝石镶嵌；点已镶的宝石可以摘下放回背包。</p>';
  // 合成
  const merge = Object.keys(S.items).filter(i => IT[i].type === 'gem' && IT[i].lv < 5 && S.items[i] >= 3);
  h += '<h4 class="fh">宝石合成　<small>三颗同种同阶 → 一颗高一阶</small></h4>' + (merge.length ? merge.map(i => '<div class="row mrow">' + itemImg(i) + '<div><h5 style="color:' + itemQ(i).color + '">' + IT[i].name + ' ×' + S.items[i] + '</h5><p>→ ' + IT[i.replace(/_\d$/, '_' + (IT[i].lv + 1))].name + '</p></div><button type="button" class="tbtn" data-act="gemMerge:' + i + '">3 合 1</button></div>').join('') : '<p class="hint">还没有凑够三颗的宝石。</p>');
  return h;
}
function forgeDo(slot) {
  const F = D.forge, id = S.equip[slot], it = D.items[id]; if (!it) return;
  const gr = S.gear && S.gear[slot] || ((S.gear = S.gear || {})[slot] = { star: 0, gems: [] }), s0 = gr.star;
  if (s0 >= F.max) return;
  const mul = F.qMul[it.q || 1] || 1, coin = Math.round(F.coin[s0] * mul), gid = forgeGem, gi = D.items[gid];
  if (!gi) { toast('先选一颗锻造宝石'); return; }
  if (S.coin < coin) { toast('银两不够'); return; }
  if (!S.items[gid]) { toast('没有' + gi.name + '了'); return; }
  S.coin -= coin; addItem(gid, -1);
  const rate = gi.forge >= 5 ? 1 : Math.min(1, F.rate[s0] + F.gemBonus[gi.forge - 1]);
  if (Math.random() < rate) { gr.star++; toast('锻造成功！' + it.name + ' ' + gr.star + ' 星');
    sys('「' + it.name + '」锻造到了 ' + gr.star + ' 星');
    if (gr.star >= 10) window.CHAT.notice('恭喜玩家「' + ROLE.name + '」将' + it.name + '锻造至 ' + gr.star + ' 星！');
  } else if (s0 >= F.dropFrom) { gr.star--; toast('锻造失败……' + it.name + ' 掉到了 ' + gr.star + ' 星'); }
  else toast('锻造失败，星级没有变化');
  clampVitals(); save(); refresh(); openSmith('forge');
}
function openRole() {
  lastPanel = 'role';
  const st = stats(), cls = D.classes[ROLE.cls], need = expNeed();
  const bar = (cur, max, cls2) => '<div class="rbar ' + cls2 + '"><i style="width:' + Math.max(0, Math.min(100, cur / max * 100)) + '%"></i><span>' + Math.round(cur) + ' / ' + max + '</span></div>';
  let h = '<div class="who"><img alt="" src="' + IMG('cls_' + ROLE.cls) + '"><div><h5>' + ROLE.name + '</h5><p>' + clsName(cls) + ' · ' + cls.role + ' · ' + (cls.gender === 'm' ? '男' : '女') + '</p>' +
    '<p>等级 <b class="gold">' + S.lv + '</b>　战力 <b class="gold">' + fmt(powerOf(st)) + '</b></p>' +
    '<p class="small">经验 ' + S.exp + ' / ' + need + '</p><div class="rbar exp"><i style="width:' + Math.min(100, S.exp / need * 100) + '%"></i></div></div></div>' +
    '<div class="rtabs"><button type="button" class="rtab' + (roleTab === 'attr' ? ' on' : '') + '" data-act="roleTab:attr">属性</button><button type="button" class="rtab' + (roleTab === 'equip' ? ' on' : '') + '" data-act="roleTab:equip">装备</button><button type="button" class="rtab' + (roleTab === 'skill' ? ' on' : '') + '" data-act="roleTab:skill">技能</button></div>';
  if (roleTab === 'attr') {
    // 每项属性拆成 基础 + 等级成长 + 装备（含星级和宝石），装备加成用绿色标出来
    const gear = gearSum(), base = D.base, grow = D.grow, L = S.lv;
    const line = (k, tip) => { const sk = STATKEY[k], tot = st[sk], e = gear[k] || 0, mu = clsMul(k), hb = heartBonus(k);
      return '<div class="arow"><span>' + D.statName[k] + '</span><b>' + fmtStat(k, tot) + '</b><small>' + (tip || '基础 ' + fmtStat(k, Math.round(base[k] * mu)) + (grow[k] ? ' + 成长 ' + fmtStat(k, Math.round(grow[k] * L * mu)) : '') + (hb ? ' + 心法 ' + hb : '')) + (e ? ' <em>+ 装备 ' + fmtStat(k, e) + '</em>' : '') + '</small></div>'; };
    h += '<div class="vit"><div><small>生命</small>' + bar(S.hp, st.maxHp, 'hp') + '</div><div><small>魔法</small>' + bar(S.mp, st.maxMp, 'mp') + '</div></div>' +
      '<div class="alist">' + ['atk', 'matk', 'def', 'mdef', 'hp', 'mp', 'spd'].map(k => line(k)).join('') +
      line('crit', '暴击时伤害 ×1.5') + line('dodge', '被攻击时有几率完全躲开') +
      '<div class="arow"><span>银两</span><b>' + fmt(S.coin) + '</b><small></small></div></div>' +
      '<p class="hint">物攻用于普通招式，法攻用于剑气纵横、狐火这类法术招式；物防、法防分别抵挡对应伤害。升级固定成长，装备、星级和宝石提供额外加成。</p>';
  } else if (roleTab === 'skill') {
    h += skillsHTML();
  } else {
    h = h.slice(h.indexOf('<div class="rtabs">')) + equipView();
    openModal('角色', h); return;
  }
  h += '<p class="center" style="margin:16px 0 0"><button type="button" class="tbtn" data-act="switchRole">切换角色</button>　<button type="button" class="tbtn red" data-act="reset">重新开始</button></p>';
  openModal('角色', h);
}
function skillsHTML() {
  let h = '';
  const hc = curCls().heart;
  if (hc) h += '<div class="row"><div><h5>' + hc.name + '</h5><p>' + hc.desc + '</p></div><span>被动</span></div>';
  playerSkills().forEach(id => {
    const x = D.skills[id], ok = skillUsable(id), lvTxt = curCls().skills && ok ? ' <small>Lv.' + skl(id) + '</small>' : '';
    h += '<div class="row" style="' + (ok ? '' : 'opacity:.5') + '"><div><h5>' + x.name + lvTxt + '</h5><p>' + x.desc + '</p></div><span>' + (ok ? '法力 ' + x.mp : curCls().skills ? '未学习' : x.lv + ' 级解锁') + '</span></div>';
  });
  return h + '<p class="hint" style="margin-top:14px">' + (curCls().skills ? (ROLE.sect === 'dao' ? (curCls().sect === '道家' && curCls().name !== '道家弟子' ? '分堂技能找本堂堂主学习、升级。' : '基础技能找道家门主学习、升级。') + '　' : '') : '') + '技能在战斗中使用。' + (curCls().weapon ? '武器：' + curCls().weapon + '。' : '') + '</p>';
}
let petTab = 'attr', petSel = 'fox';
function petPower(p) { return Math.round(p.maxHp / 10 + p.atk * 2 + p.def * 1.5 + p.spd); }
function stars(n, color) { return '<span class="stars" style="color:' + color + '">' + '★'.repeat(n) + '<i>' + '★'.repeat(5 - n) + '</i></span>'; }
function openPet() {
  const pid = petAct(), dex = D.petDex[pid], q = D.petQuality[dex.q], p = petStats(pid), bd = bondInfo(), b = D.base, g = D.grow, L = S.lv, m = dex.mult || D.pet, kb = 1 + bd.bonus;
  let h = '<div class="pethero"><div class="pglow"></div><img alt="" src="' + IMG(dex.show || dex.sprite) + '"><div class="pinfo"><h5>' + dex.name + ' <span class="qual" style="color:' + q.color + ';border-color:' + q.color + '">' + q.name + '</span></h5>' +
    '<p>' + stars(q.stars, q.color) + '　' + dex.kind + '</p><p>等级 <b class="gold">' + L + '</b>（随主人成长）　战力 <b class="gold">' + fmt(petPower(p)) + '</b></p>' +
    '<p class="small">亲密度 · <b style="color:#ffd9a0">' + bd.name + '</b>　' + bd.n + (bd.to ? ' / ' + bd.to : '') + '</p><div class="rbar exp bond"><i style="width:' + (bd.to ? Math.min(100, (bd.n - bd.from) / (bd.to - bd.from) * 100) : 100) + '%"></i></div></div></div>' +
    '<div class="rtabs">' + Object.keys(D.petDex).filter(ownPet).map(id => '<button type="button" class="rtab' + (id === pid ? ' on' : '') + '" data-act="petUse:' + id + '">' + D.petDex[id].name + (id === pid ? ' · 出战' : '') + '</button>').join('') + '</div>' +
    '<div class="rtabs">' + [['attr', '属性'], ['skill', '技能'], ['dex', '图鉴']].map(t => '<button type="button" class="rtab' + (petTab === t[0] ? ' on' : '') + '" data-act="petTab:' + t[0] + '">' + t[1] + '</button>').join('') + '</div>';
  if (petTab === 'attr') {
    const row = (name, total, base, grow, note) => '<div class="arow"><span>' + name + '</span><b>' + total + '</b><small>' + note + '</small></div>';
    h += '<div class="alist">' +
      row('气血', p.maxHp, 0, 0, '(基础 ' + Math.round(b.hp * m.hp) + ' + 成长 ' + Math.round(g.hp * L * m.hp) + ')' + (bd.i ? ' <em>× 亲密 ' + (kb).toFixed(2) + '</em>' : '')) +
      row('攻击', p.atk, 0, 0, '(基础 ' + Math.round(b.atk * m.atk) + ' + 成长 ' + Math.round(g.atk * L * m.atk) + ')' + (bd.i ? ' <em>× 亲密 ' + (kb).toFixed(2) + '</em>' : '')) +
      row('防御', p.def, 0, 0, '(基础 ' + Math.round(b.def * m.def) + ' + 成长 ' + Math.round(g.def * L * m.def) + ')') +
      row('速度', p.spd, 0, 0, '(基础 ' + Math.round(b.spd * m.spd) + ' + 成长 ' + Math.round(g.spd * L * m.spd) + ')') +
      '</div><div class="bondrow">' + D.bondTiers.map((t, i) => '<div class="' + (i <= bd.i ? 'on' : '') + '"><b>' + t.name + '</b><small>' + (i ? '≥ ' + t.at + ' 战 · +' + i * 3 + '%' : '初始') + '</small></div>').join('') + '</div>' +
      '<p class="hint">' + dex.name + '的属性取主人裸属性的一部分，随等级成长；每打赢一场战斗亲密度 +1，亲密度升档后气血和攻击提高。</p>';
  } else if (petTab === 'skill') {
    const sk = id => { const x = D.skills[id]; return x; };
    if (dex.ss) {
      h += '<div class="pskills">' + dex.act.filter(a => a[0] !== 'ss_hit').map(a => { const x = D.skills[a[0]]; return '<div class="pskill"><div class="pico fire">' + x.name[0] + '</div><div><h6>' + x.name + '<small>主动' + (x.cd ? ' · 冷却 ' + x.cd + ' 回合' : '') + '</small></h6><p>' + x.des + '</p></div></div>'; }).join('') +
        dex.pasList.map(a => '<div class="pskill"><div class="pico bond">' + a[0][0] + '</div><div><h6>' + a[0] + '<small>被动</small></h6><p>' + a[1] + '</p></div></div>').join('') +
        '</div><p class="hint">技能描述取自原作；演示版数值已按本作难度折算。宠物在战斗中自动出手。进阶形态「' + dex.adv + '」暂未开放。</p>';
    } else
    h += '<div class="pskills">' +
      '<div class="pskill"><div class="pico">咬</div><div><h6>撕咬<small>普通招式</small></h6><p>扑上去撕咬一名敌人，造成 ' + Math.round(p.atk * 1.0) + ' 点基础伤害。出手时有 65% 的几率使用。</p></div></div>' +
      '<div class="pskill"><div class="pico fire">火</div><div><h6>狐火<small>主动 · 35% 触发</small></h6><p>喷出一团幽蓝狐火，对一名敌人造成 ' + Math.round(p.atk * 1.6) + ' 点伤害（约 1.6 倍攻击），并优先攻击气血最低的敌人。</p></div></div>' +
      '<div class="pskill"><div class="pico bond">灵</div><div><h6>灵犀<small>被动</small></h6><p>与主人越亲近越强：当前「' + bd.name + '」档，气血、攻击 ' + (bd.i ? '+' + bd.i * 3 + '%' : '尚无加成') + '。' + (bd.to ? '再打赢 ' + (bd.to - bd.n) + ' 场升到「' + D.bondTiers[bd.i + 1].name + '」。' : '已满级。') + '</p></div></div>' +
      '</div><p class="hint">宠物在战斗中自动出手，不需要手动指挥。</p>';
  } else {
    const ids = Object.keys(D.petDex), own = ownPet, sel = D.petDex[petSel] || D.petDex.fox, sq = D.petQuality[sel.q];
    h += '<div class="petgrid">' + ids.map(id => { const x = D.petDex[id], c = D.petQuality[x.q].color;
      return '<button type="button" class="pcard' + (id === petSel ? ' sel' : '') + (own(id) ? '' : ' lock') + '" style="--qc:' + c + '" data-act="petSel:' + id + '"><img alt="" src="' + IMG(x.show || x.sprite) + '"><span>' + (own(id) ? x.name : '？？？') + '</span></button>'; }).join('') + '</div>' +
      '<div class="pdetail"><div class="pdhead"><h6>' + (sel.own ? sel.name : sel.name) + ' <span class="qual" style="color:' + sq.color + ';border-color:' + sq.color + '">' + sq.name + '</span></h6>' + stars(sq.stars, sq.color) + '</div>' +
      '<p>' + sel.kind + '</p><p>' + sel.desc + '</p><p class="how">' + (ownPet(petSel) ? '<b>已拥有</b>　' + (sel.ss ? '可在上方切换出战。' : '出生自带，一直跟随主人。') : '获得途径：' + sel.how + (sel.ss ? '' : '（暂未开放）')) + '</p></div>' +
      '<p class="hint">已收集 ' + ids.filter(own).length + ' / ' + ids.length + '</p>';
  }
  openModal('宠物', h);
}
let shopId = null;
function shopHTML(id) {
  const shop = D.shops[id];
  let h = '<p class="center" style="margin:0;color:var(--amber)">银两 ' + fmt(S.coin) + '</p>';
  shop.sell.forEach(iid => {
    const it = D.items[iid];
    h += '<button type="button" class="row" data-act="buy:' + iid + '"' + (S.coin < it.price ? ' disabled' : '') + '>' + itemImg(iid) + '<div><h5 style="color:' + itemQ(iid).color + '">' + it.name + (S.items[iid] ? '　<small style="color:var(--muted)">已有 ' + S.items[iid] + '</small>' : '') + '</h5><p>' + it.desc + '</p></div><span>' + it.price + ' 两</span></button>';
  });
  const sellable = Object.keys(S.items).filter(i => D.items[i].type === 'mat' && D.items[i].price > 0);
  if (sellable.length) {
    h += '<p style="margin:16px 0 0;color:var(--muted)">出售材料</p>';
    sellable.forEach(iid => { const it = D.items[iid]; h += '<button type="button" class="row" data-act="sell:' + iid + '">' + itemImg(iid) + '<div><h5>' + it.name + ' ×' + S.items[iid] + '</h5><p>' + it.desc + '</p></div><span>卖 ' + it.price + ' 两</span></button>'; });
  }
  return h;
}
function openShop(id) {
  shopId = id;
  openModal(D.shops[id].name, shopHTML(id));
}
// 铁匠铺：锻造升星 / 镶嵌宝石 / 买卖
let smithTabNow = 'forge';
function openSmith(tab) {
  smithTabNow = tab || smithTabNow; if (smithTabNow !== 'inlay') smithTabNow = 'forge';
  const tabs = '<div class="rtabs"><button type="button" class="rtab' + (smithTabNow === 'forge' ? ' on' : '') + '" data-act="smithTab:forge">锻造</button><button type="button" class="rtab' + (smithTabNow === 'inlay' ? ' on' : '') + '" data-act="smithTab:inlay">镶嵌</button></div>';
  openModal('王记铁匠铺', tabs + forgeView(smithTabNow));
}
const ACTS = {
  petUse(id) { if (!ownPet(id)) return; S.petAct = id; petSel = id; save(); applyPetLook(); toast(D.petDex[id].name + ' 出战'); openPet(); },
  ssExch(id) { const dx = D.petDex[id]; if (!dx || ownPet(id)) return; if ((S.items.ss_soul || 0) < D.ssCost) { toast('神兽之魂不够'); return; } addItem('ss_soul', -D.ssCost); S.petOwn = S.petOwn || {}; S.petOwn[id] = true; S.petAct = id; save(); refresh(); applyPetLook(); toast('神兽「' + dx.name + '」跟随了你'); openEnvoy(); },
  ssRite() { if (S.coin < D.ssRite.coin) { toast('银两不够'); return; } S.coin -= D.ssRite.coin; const n = Math.round(rnd(D.ssRite.min, D.ssRite.max)); addItem('ss_soul', n); save(); refresh(); toast('神兽祭：天神赐下神兽之魂 ×' + n); openEnvoy(); },
  sel(id) { bagSel = id; openBag(); },
  eqBag(id) { eqSel = { t: 'bag', v: id }; openRole(); },
  eqSlot(sl) { eqSel = { t: 'slot', v: sl }; openRole(); },
  use(id) { usePotion(id); reopen(); },
  equip(id) {
    const it = D.items[id], old = S.equip[it.slot];
    addItem(id, -1); if (old) addItem(old, 1); S.equip[it.slot] = id; S.hp = Math.min(S.hp, stats().maxHp);
    save(); refresh(); toast('装备了' + it.name); eqSel = { t: 'slot', v: it.slot }; reopen();
  },
  buy(id) { const it = D.items[id]; if (S.coin < it.price) return; S.coin -= it.price; addItem(id, 1); save(); refresh(); toast('买下了' + it.name); openShop(shopId); },
  sell(id) { const it = D.items[id]; if (!S.items[id]) return; addItem(id, -1); S.coin += it.price; save(); refresh(); openShop(shopId); },
  fight(id) { $('modal').hidden = true; modalOnClose = null; startBattle(D.stages.find(s => s.id === id)); },
  reset() { openModal('重新开始', '<p class="center">会清掉等级、背包和任务进度，从头再来。</p><p class="center"><button type="button" class="tbtn red" data-act="resetYes">确定清档</button>　<button type="button" class="tbtn" data-act="close">再想想</button></p>'); },
  resetYes() { try { localStorage.removeItem(SAVE_KEY); } catch (e) {} S = newGame(); bagSel = null; loadMap(S.map); closeModal(); refresh(); toast('已重新开始'); },
  arena() { $('modal').hidden = true; modalOnClose = null; if (B.active) { B.active = false; $('units').innerHTML = ''; } startArena(); },
  wx(k) { wxLock = k; wxUpdate(true); openWeather(); },
  wxAuto() { wxLock = null; wxUpdate(true); openWeather(); },
  forgeSlot(sl) { forgeSlot = sl; gemPick = null; openSmith('forge'); },
  smithTab(t) { openSmith(t); },
  forgeGem(id) { forgeGem = id; openSmith('forge'); },
  forge(sl) { forgeDo(sl); },
  gemPick(arg) { const [sl, i] = arg.split(':'); gemPick = { slot: sl, i: +i }; openSmith('forge'); },
  gemSet(id) { if (!gemPick || !S.items[id]) return; const gr = (S.gear = S.gear || {})[gemPick.slot] || (S.gear[gemPick.slot] = { star: 0, gems: [] }); gr.gems = gr.gems || []; gr.gems[gemPick.i] = id; addItem(id, -1); gemPick = null; clampVitals(); save(); refresh(); toast('镶嵌了' + D.items[id].name); openSmith('forge'); },
  gemOff(arg) { const [sl, i] = arg.split(':'); const gr = getGear(sl), id = gr.gems && gr.gems[+i]; if (!id) return; S.gear[sl].gems[+i] = null; addItem(id, 1); clampVitals(); save(); refresh(); toast('摘下了' + D.items[id].name); openSmith('forge'); },
  gemMerge(id) { if ((S.items[id] || 0) < 3) return; const nx = id.replace(/_\d$/, '_' + (D.items[id].lv + 1)); addItem(id, -3); addItem(nx, 1); save(); toast('合成了' + D.items[nx].name); openSmith('forge'); },
  roleTab(t) { roleTab = t; gemPick = null; openRole(); },
  petTab(t) { petTab = t; openPet(); },
  petSel(id) { petSel = id; petTab = 'dex'; openPet(); },
  unequip(slot) { if (!S.equip[slot]) return; const id = S.equip[slot]; S.equip[slot] = null; addItem(id, 1); clampVitals(); save(); refresh(); toast('卸下了' + D.items[id].name); eqSel = { t: 'bag', v: id }; openRole(); },
  openBag() { openBag(); },
  bagTab(t) { bagTab = t; openBag(); },
  bagSort() { const order = { equip: 0, gem: 1, potion: 2, mat: 3 }, ids = Object.keys(S.items).sort((a, b) => order[D.items[a].type] - order[D.items[b].type] || (D.items[b].q || 1) - (D.items[a].q || 1)), o = {}; ids.forEach(i => o[i] = S.items[i]); S.items = o; save(); toast('已整理背包'); openBag(); },
  bagSell(id) { const it = D.items[id]; if (!S.items[id] || !(it.price > 0)) return; if (it.type === 'equip' && S.items[id] === 1 && Object.values(S.equip).includes(id)) return; addItem(id, -1); S.coin += it.price; save(); refresh(); toast('卖出' + it.name + '，银两 +' + it.price); openBag(); },
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
  if (a.kind === 'portal' || a.kind === 'spot') { a.tag.innerHTML = (a.mark ? '<span class="mk">!</span>' : '') + '<span>' + a.name + (a.kind === 'spot' ? ' ✦' : ' ▶') + '</span>'; return; }
  a.tag.innerHTML = (a.mark ? '<span class="mk">!</span>' : '') + (auto ? '<span class="auto">自动寻路中…</span>' : '') +
    (a.title ? '<small style="color:' + a.titleColor + '">' + a.title + '</small>' : '') + '<span style="color:' + a.color + '">' + a.name + '</span>';
}
const player = makeActor('player', 'player', 'player', '云中客', { x: 0, y: 0 }, { color: '#8affb0', title: '【游侠】', hop: 7 });
const fox = makeActor('pet', 'fox', 'fox', '灵狐', { x: 0, y: 0 }, { color: '#a8e6ff', hop: 5 });
// 主角用行走帧：正面一套（朝右下）、背面一套（朝左上），各 4 帧；另外两个方向靠水平翻转
// 精灵表一行 4 格，AS.size.walk_f = [每格宽, 每格高, 头部中心在格内的 x]
const WALK_SHEET = { f: 'walk_f', b: 'walk_b' }, STEP_LEN = 26;
function setPlayerLook(cls) {      // 按职业换行走精灵表（没有专属图就用默认的）
  WALK_SHEET.f = AS.size['walk_f_' + cls] ? 'walk_f_' + cls : 'walk_f'; WALK_SHEET.b = AS.size['walk_b_' + cls] ? 'walk_b_' + cls : 'walk_b';
  player.shown = ''; const sz = AS.size[WALK_SHEET.f]; player.tag.style.bottom = (sz[1] + 2) + 'px'; player.h = sz[1];
  Object.values(WALK_SHEET).forEach(k => { const im = new Image(); im.src = IMG(k); });
}
player.walk = document.createElement('div'); player.walk.className = 'walk';
player.el.insertBefore(player.walk, player.tag); player.img.hidden = true;
player.dir = 'f'; player.shown = ''; player.dist = 0; player.mt = 0;
player.tag.style.bottom = (AS.size.walk_f[1] + 2) + 'px'; player.h = AS.size.walk_f[1];
Object.values(WALK_SHEET).forEach(k => { const im = new Image(); im.src = IMG(k); });
// 灵狐多一张背影，往上走时切过去
fox.imgB = document.createElement('img'); fox.imgB.alt = ''; fox.imgB.src = IMG('b_fox');
{ const sz = AS.size.b_fox, h = fox.h + 4, w = h * sz[0] / sz[1]; fox.imgB.style.cssText = 'width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px'; }
fox.imgB.hidden = true; fox.el.insertBefore(fox.imgB, fox.tag); fox.dir = 'f';
function applyPetLook() {          // 出战宠物是神兽时，地图上跟随的形象换成它的图：正面用原作立绘，背面用斜向重绘的背面图（没有时共用一张）
  const id = petAct(), dx = D.petDex[id];
  if (dx.ss && AS.size[dx.sprite]) {
    const mk = (spr) => { const sz = AS.size[spr], h = dx.h * 0.8, w = h * sz[0] / sz[1]; return 'width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px'; };
    const fs = dx.show && AS.size[dx.show] ? dx.show : dx.sprite;
    fox.img.src = IMG(fs); fox.img.style.cssText = mk(fs); fox.imgB.src = IMG(dx.sprite); fox.imgB.style.cssText = mk(dx.sprite); fox.h = dx.h * 0.8; fox.name = dx.name;
    $('hudPetImg').src = IMG(fs);
  } else {
    const sz = AS.size.fox; fox.img.src = IMG('fox'); fox.img.style.cssText = 'width:' + sz[0] + 'px;height:' + sz[1] + 'px;left:' + (-sz[0] / 2) + 'px'; fox.h = sz[1];
    const sb = AS.size.b_fox, h = fox.h + 4, w = h * sb[0] / sb[1]; fox.imgB.src = IMG('b_fox'); fox.imgB.style.cssText = 'width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px'; fox.name = '灵狐';
    $('hudPetImg').src = IMG('b_fox');
  }
  if (player && player.pos && fox.pos) { const fp = { x: player.pos.x + (fox.h > 70 ? 96 : 46), y: player.pos.y + 18 }; if (!inside || inside(fp)) fox.pos = fp; }   // 换宠物时站到主角身旁，别叠在身上
  fox.tag.style.bottom = (fox.h + 2) + 'px'; drawTag(fox);
}

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
  waterSetup();
  $('glows').innerHTML = '';
  { const fg = $('fog'); fg.innerHTML = '';                      // 动态雾：一批缓慢漂移、忽浓忽淡的大雾团
    if (def.fog) { const n = def.fog.n || 26, W = M.W, H = M.H; let seed = 7;
      const rnd = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
      for (let i = 0; i < n; i++) { const s = 380 + rnd() * 520, e = document.createElement('i');
        e.style.cssText = 'width:' + s + 'px;height:' + s * (0.55 + rnd() * 0.3) + 'px;left:' + (rnd() * (W + s) - s * .5) + 'px;top:' + (rnd() * (H + s) - s * .5) + 'px;--dx:' + (60 + rnd() * 160) + 'px;--dy:' + (-30 + rnd() * 60) + 'px;--d:' + (14 + rnd() * 18) + 's;--o1:' + (def.fog.lo ?? .25) + ';--o2:' + (def.fog.hi ?? .8) + ';animation-delay:' + (-rnd() * 20) + 's';
        fg.appendChild(e); } } }
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
  buildGrid(); drawMiniDots(); wxSnap();
  const p = at && inside(at) ? at : I(def.start[0], def.start[1]);
  player.pos = { x: p.x, y: p.y }; player.face = 1;
  const fp = { x: p.x + (fox.h > 70 ? 96 : 46), y: p.y + 18 }; fox.pos = inside(fp) ? fp : { x: p.x, y: p.y };
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
[['act', '活动', 0], ['gift', '福利', 1], ['mall', '商城', 0], ['rank', '竞技', 0], ['bag', '背包', 0], ['guild', '帮派', 0], ['friend', '好友', 0]].forEach((r, i) => addBtn($('btns'), r[0], r[1], 676, 246 + i * 76, 54, r[0], r[2]));
const autoBtn = addBtn($('btns'), 'auto', '', 652, 1186, 86, 'auto');
addBtn($('btns'), 'ride', '', 562, 1214, 56, 'ride'); addBtn($('btns'), 'potion', '', 594, 1144, 56, 'potion');
// 点头像开角色面板，点宠物头像开宠物面板
$('hudFace').parentNode.addEventListener('click', e => { e.stopPropagation(); if (!B.active) openRole(); });
$('hudPet').addEventListener('click', e => { e.stopPropagation(); if (!B.active) openPet(); });

let wander = false, wanderWait = 0;
function onBtn(key, label, b) {
  if (key === 'bag') openBag();
  else if (key === 'pet') openPet();
  else if (key === 'mall') openShop('vendor');
  else if (key === 'guild') window.CHAT.open('guild');
  else if (key === 'friend') window.CHAT.open('pm');
  else if (key === 'auto') { wander = !wander; wanderWait = 0; autoBtn.classList.toggle('on', wander); if (!wander) stop(); toast(wander ? '开始挂机闲逛，再点一次停止' : '停止挂机'); }
  else if (key === 'potion') { if (!S.items.hp_potion) toast('没有金创药了，找货郎买点吧'); else usePotion('hp_potion'); }
  else if (key === 'gift') {
    if (!S.ssGift) { S.ssGift = true; S.petOwn = Object.assign(S.petOwn || {}, { ss_1227: true, ss_1221: true }); S.petAct = 'ss_1227'; addItem('ss_soul', 20); save(); refresh(); applyPetLook(); toast('神兽体验福利：青龙仙童、逆天魔龙已入队，在宠物面板切换出战；另送神兽之魂 ×20'); }
    else if (S.gift) toast('今日福利已领取');
    else { S.gift = true; grant({ coin: 200, items: { hp_potion: 2 } }); toast('领取福利：银两 +200，金创药 ×2'); const d = b.querySelector('.dot'); if (d) d.hidden = true; }
  }
  else if (key === 'rank') openModal('竞技场', '<p class="center">' + D.arena.allyName + ' 对 ' + D.arena.foeName + '<br>双方各 5 名侠客、5 只宠物，等级相同。<br>你操作自己的角色，队友和宠物自动出手；每回合 ' + TURN_SECONDS + ' 秒，超时自动行动。<br>演示对战，不影响等级和背包。</p><p class="center"><button type="button" class="tbtn red" data-act="arena">开始 5v5 对战</button></p>');
  else if (key === 'act') toast('中秋灯会将在戌时三刻开始');
  else toast('「' + (label || '坐骑') + '」暂未开放');
}
['hud', 'coin', 'chat'].forEach(id => $(id).addEventListener('click', e => e.stopPropagation()));

// ---------- 世界地图：点小地图打开，点任意地点可以直接传送 ----------
// 世界地图：一张大图，手指拖动浏览（带惯性）；地点坐标是 D.maps[id].world = [x, y]（大图原图像素），新增地点只要给一个坐标
const WP = { x: 0, y: 0, vx: 0, vy: 0, drag: false, moved: 0, raf: 0 };
function wpClamp() {
  const iw = AS.size.world[0], ih = AS.size.world[1];
  WP.x = Math.min(0, Math.max(720 - iw, WP.x)); WP.y = Math.min(0, Math.max(1280 - ih, WP.y));
  $('wmapPan').style.transform = 'translate3d(' + WP.x.toFixed(1) + 'px,' + WP.y.toFixed(1) + 'px,0)';
}
function wpCenter(id) { const d = D.maps[id]; WP.x = 360 - d.world[0]; WP.y = 640 - d.world[1] + 40; wpClamp(); }
function openWorld() {
  if (B.active) return;
  stop(); wander = false; autoBtn.classList.remove('on');
  const img = $('wmapImg'); img.src = IMG('world'); img.style.width = AS.size.world[0] + 'px'; img.style.height = AS.size.world[1] + 'px';
  $('wmapNodes').innerHTML = Object.keys(D.maps).map(id => {
    const d = D.maps[id], here = id === M.id;
    return '<button type="button" class="wnode' + (here ? ' here' : '') + (d.sect ? ' sect' : '') + '" data-go="' + id + '" style="left:' + d.world[0] + 'px;top:' + d.world[1] + 'px">' + d.name +
      '<small>' + (here ? '你在这里' : d.note) + '</small></button>';
  }).join('');
  $('wmap').hidden = false; WP.vx = WP.vy = 0; wpCenter(M.id);
}
{ const wm = $('wmap'); let last = null, pid = null;
  const stepInertia = () => {
    if (WP.drag) return;
    WP.x += WP.vx * 16; WP.y += WP.vy * 16; WP.vx *= 0.93; WP.vy *= 0.93; wpClamp();
    if (Math.abs(WP.vx) + Math.abs(WP.vy) > 0.01 && !wm.hidden) WP.raf = requestAnimationFrame(stepInertia);
  };
  wm.addEventListener('pointerdown', e => {
    if (e.target.closest('.tbtn')) return;
    cancelAnimationFrame(WP.raf); WP.drag = true; WP.moved = 0; WP.vx = WP.vy = 0; pid = e.pointerId; last = { x: e.clientX, y: e.clientY, t: performance.now() };
  });
  window.addEventListener('pointermove', e => {
    if (!WP.drag || e.pointerId !== pid) return;
    const dx = (e.clientX - last.x) / scale, dy = (e.clientY - last.y) / scale, now = performance.now(), dt = Math.max(1, now - last.t);
    WP.moved += Math.abs(dx) + Math.abs(dy);
    if (WP.moved > 10) { wm.classList.add('drag'); WP.x += dx; WP.y += dy; wpClamp(); WP.vx = WP.vx * 0.6 + dx / dt * 0.4; WP.vy = WP.vy * 0.6 + dy / dt * 0.4; }
    last = { x: e.clientX, y: e.clientY, t: now };
  });
  const end = e => { if (!WP.drag || e.pointerId !== pid) return; WP.drag = false; wm.classList.remove('drag'); if (WP.moved > 10) WP.raf = requestAnimationFrame(stepInertia); };
  window.addEventListener('pointerup', end); window.addEventListener('pointercancel', end);
  wm.addEventListener('click', e => {
    e.stopPropagation();
    if (WP.moved > 10) { WP.moved = 0; return; }              // 拖动不算点击
    const b = e.target.closest('[data-go]'); if (!b) return;
    const id = b.dataset.go;
    if (id === M.id) { toast('你已经在' + D.maps[id].name); return; }
    wm.hidden = true; loadMap(id); save(); toast('传送到 ' + D.maps[id].name);
  });
  $('wmapHome').addEventListener('click', e => { e.stopPropagation(); WP.vx = WP.vy = 0; wpCenter(M.id); });
}
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
  $('dFace').style.visibility = 'visible'; $('dFace').src = IMG((D.npcs[n.id] && D.npcs[n.id].head) || 'head_' + n.id); $('dName').textContent = n.name; $('dialog').hidden = false;
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

// ---------- 主线剧情：STORY.quests 摊平成“节拍”序列，S.step 就是节拍序号 ----------
// 节拍类型：talk 找人对话 / spot 采集点 / fight 首领战 / kill 清怪计数（S.cnt）。每个任务第一拍带接取对白，最后一拍带完成对白和奖励。
(function flattenStory() {
  const out = [], first = {};
  (window.STORY ? window.STORY.quests : []).forEach(q => q.beats.forEach((b, i) => {
    const beat = Object.assign({}, b, { qid: q.id, title: q.title, chapter: q.chapter, summary: q.summary });
    if (i === 0) { beat.first = true; beat.accept = q.accept; first[q.id] = out.length; }
    if (i === q.beats.length - 1) { beat.last = true; beat.outro = q.complete; beat.reward = q.reward; }
    out.push(beat);
  }));
  D.quests = out; D.questFirst = first;
  Object.values(D.maps).forEach(m => (m.portals || []).forEach(p => { if (p.needQuest) p.needStep = first[p.needQuest]; }));
  // 剧情里的采集点：当成一个只在对应任务时出现的“人物”
  out.forEach(b => { if (b.type === 'spot' && !D.npcs[b.npc]) D.npcs[b.npc] = { name: b.spot, spot: true, color: '#9ff0ff', idle: '' }; });
})();
const faceOf = who => {
  if (who === '你') return 'head_player';
  const id = Object.keys(D.npcs).find(k => D.npcs[k].name === who && !D.npcs[k].spot);
  if (id) return D.npcs[id].head || 'head_' + id;
  return D.speakers && D.speakers[who] ? D.speakers[who] : null;
};
function speak(who, text, after) {
  const f = faceOf(who), im = $('dFace');
  if (f) { im.src = IMG(f); im.style.visibility = 'visible'; } else im.style.visibility = 'hidden';
  $('dName').textContent = who === '你' ? ROLE.name : (who || '');
  $('dialog').hidden = false;
  const p = $('dText'); p.textContent = ''; let i = 0; clearInterval(typing);
  if (reduce) p.textContent = text; else typing = setInterval(() => { p.textContent = text.slice(0, ++i); if (i >= text.length) clearInterval(typing); }, 26);
  dialogFull = text; dialogAfter = after || null;
}
// pages = [[说话人, 内容], …]；说话人为空是旁白
function showPages(pages, done) {
  let i = 0;
  const next = () => { if (i >= pages.length) { if (done) done(); return; } const pg = pages[i++]; speak(pg[0], pg[1], next); };
  next();
}
function changeClass(id) {         // 拜入门派：换职业、形象、头像，存档
  ROLE.cls = id; const c = D.classes[id]; grantInitSkills(id);
  setPlayerLook(id); player.title = '【' + clsName(c) + '】'; drawTag(player);
  $('hudSect').textContent = clsName(c); $('hudFace').src = IMG(AS.img['head_' + id] ? 'head_' + id : 'head_player');
  S.hp = stats().maxHp; S.mp = stats().maxMp; if (id.indexOf('dao_dz') === 0) { sys('你拜入道家，背起箜篌。请去太虚之林找琴魔堂主或天音堂主选择分堂。'); toast('拜入道家'); } else { sys('你选定' + c.name + '分堂，成为「' + c.name + '」。'); toast('拜入' + c.sect + '·' + c.name); }
  if (window.GAME.onRoleChange) window.GAME.onRoleChange(); save(); refresh();
}
function offerSect() {
  const opts = (curCls().joinSect || []).map(id => D.classes[id]); if (!opts.length) return;
  openModal('拜入门派', '<p class="center">道家门主说：你与道家有缘。选一个分堂吧。</p>' + (curCls().joinSect).map(id => { const c = D.classes[id];
    return '<div class="q"><img alt="" src="' + IMG('head_' + id) + '" style="width:56px;height:56px;border-radius:50%;float:left;margin-right:10px"><b>' + c.name + '</b>（' + c.weapon + '）<br><small>' + c.desc + '</small><br><button type="button" class="tbtn red" data-act="sect:' + id + '">拜入' + c.name + '</button></div>'; }).join(''));
}
// 门派两步：10 级找门主入道家（ROLE.sect），再去对应堂主处选分堂（职业形象此时才变）。原作入门 15 级、分堂 30 级，演示只做到 10 级所以都按 10 级
const SECT_LV = 10;
function openEnvoy() {
  const have = S.items.ss_soul || 0;
  let h = '<p class="center" style="margin:0">神兽之魂 <b class="gold">' + have + '</b>　银两 <b class="gold">' + fmt(S.coin) + '</b></p><div class="shop">' +
    Object.keys(D.petDex).filter(id => D.petDex[id].ss).map(id => { const x = D.petDex[id], own = ownPet(id);
      return '<button type="button" class="row" data-act="ssExch:' + id + '"' + (own || have < D.ssCost ? ' disabled' : '') + '><img alt="" src="' + IMG(x.show || x.sprite) + '" style="height:54px;width:auto"><div><h5 style="color:' + D.petQuality[x.q].color + '">' + x.name + '<small style="color:var(--muted)">　进阶：' + x.adv + '</small></h5><p>' + x.kind + '　技能：' + x.act.filter(a => a[0] !== 'ss_hit').map(a => D.skills[a[0]].name).join('、') + '</p></div><span>' + (own ? '已拥有' : '魂 ×' + D.ssCost) + '</span></button>'; }).join('') +
    '</div><p class="center" style="margin:14px 0 0"><button type="button" class="tbtn red" data-act="ssRite">办神兽祭　银两 ' + D.ssRite.coin + '</button></p><p class="hint">神兽祭：奉上祭品，天神会赐下 ' + D.ssRite.min + '~' + D.ssRite.max + ' 枚神兽之魂。（原作兑换需要 99 枚，演示版 ' + D.ssCost + ' 枚）</p>';
  openModal('神兽使者', h);
}
function joinDaoTalk() {
  const c = curCls();
  if (ROLE.sect === 'dao') {            // 已入道家：门主管基础技能；选了分堂的，基础技能不再用，分堂技能去堂主处
    if (c.name === '道家弟子') openTrain('menzhu'); else toast('分堂技能请找本堂堂主学习、升级');
    return;
  }
  if (!c.joinSect) { toast('你已是道家弟子'); return; }
  if (S.lv < SECT_LV) { toast('需达到 ' + SECT_LV + ' 级方可入门（现在 ' + S.lv + ' 级）'); return; }
  openModal('拜入道家', '<p class="center">道家门主说：道法自然。入我门下，再去琴魔、天音两位堂主处择一堂修行。</p><p class="center"><button type="button" class="tbtn red" data-act="joinDao">拜入道家</button>　<button type="button" class="tbtn" data-act="close">再想想</button></p>');
}
// 技能修炼（参考原作：门派心法不在此升级；分堂技能只能在本堂堂主处学）。演示简化：只耗银两，等级上限 5，升级要求人物等级随技能等级升高
function trainList(who) { return who === 'menzhu' ? D.train.base : (curCls().skills || []); }
function trainCost(id) {
  const n = skl(id) + 1, x = D.skills[id], T = D.train;
  return n === 1 ? { lvReq: x.lv || 1, coin: T.learnCoin * (x.lv || 1) } : { lvReq: (x.lv || 1) + T.upLv * (n - 1), coin: T.upCoin * n * n };
}
function openTrain(who) {
  const T = D.train, name = who === 'menzhu' ? '道家门主' : (curCls().name + '堂主');
  let h = '<p class="center" style="margin:0;color:var(--amber)">银两 ' + fmt(S.coin) + '</p><p class="hint center">' + name + '：学习新技能、把学过的技能升级（技能每升一级，威力与疗效 +12%）。</p>';
  trainList(who).forEach(id => {
    const x = D.skills[id], cur = skl(id), maxed = cur >= T.maxLv, ct = trainCost(id);
    let btn;
    if (maxed) btn = '<span class="hint">已满级</span>';
    else if (S.lv < ct.lvReq) btn = '<span class="hint">需 ' + ct.lvReq + ' 级</span>';
    else btn = '<button type="button" class="tbtn' + (S.coin < ct.coin ? '' : ' red') + '" data-act="train:' + who + ':' + id + '"' + (S.coin < ct.coin ? ' disabled' : '') + '>' + (cur ? '升到 ' + (cur + 1) + ' 级' : '学习') + '<br><small>银两 ' + fmt(ct.coin) + '</small></button>';
    h += '<div class="row"><div><h5>' + x.name + (cur ? ' <small>Lv.' + cur + '</small>' : ' <small>未学习</small>') + '</h5><p>' + x.desc + '</p></div>' + btn + '</div>';
  });
  if (who === 'menzhu') h += '<div class="row"><div><h5>' + curCls().heart.name + ' <small>被动</small></h5><p>' + curCls().heart.desc + '。随人物等级成长，不需要修炼。</p></div></div>';
  openModal('技能修炼', h);
}
ACTS.train = v => { const [who, id] = v.split(':'), ct = trainCost(id);
  if (skl(id) >= D.train.maxLv || S.lv < ct.lvReq || S.coin < ct.coin || trainList(who).indexOf(id) < 0) return;
  S.coin -= ct.coin; S.skl[id] = skl(id) + 1; save(); refresh(); toast(D.skills[id].name + ' 升到 ' + S.skl[id] + ' 级'); openTrain(who); };
ACTS.joinDao = () => { closeModal(); ROLE.sect = 'dao'; changeClass(curCls().gender === 'm' ? 'dao_dz_m' : 'dao_dz'); };
function pickHallTalk(hall) {          // hall: 'qm' | 'ty'
  const c = curCls(), name = hall === 'qm' ? '琴魔' : '天音';
  if (!c.joinSect) { if (c.sect === '道家' && c.name === name) openTrain('hall'); else toast('你已入另一堂，分堂不可改选'); return; }
  if (ROLE.sect !== 'dao') { toast('先去找道家门主入门（' + SECT_LV + ' 级）'); return; }
  const id = c.joinSect.find(x => x.indexOf('dao_' + hall) === 0), d = D.classes[id];
  openModal('选择分堂', '<div class="q"><img alt="" src="' + IMG('head_' + id) + '" style="width:56px;height:56px;border-radius:50%;float:left;margin-right:10px"><b>' + d.name + '</b>（' + d.weapon + '）<br><small>' + d.desc + '</small></div><p class="center">分堂选定后不能改选。</p><p class="center"><button type="button" class="tbtn red" data-act="sect:' + id + '">拜入' + name + '</button>　<button type="button" class="tbtn" data-act="close">再想想</button></p>');
}
ACTS.sect = id => { closeModal(); changeClass(id); };
// 完成当前这一拍：进入下一拍；任务最后一拍发奖励并播完成对白（下一个任务不是“找人”时，顺带播它的接取对白）
function finishBeat(q, extra) {
  const wasLow = S.lv < SECT_LV;
  S.step++; S.cnt = 0;
  let ups = 0, pages = [];
  if (q.last) {
    const r = Object.assign({}, q.reward || {}); if (extra) { r.exp = (r.exp || 0) + (extra.exp || 0); r.coin = (r.coin || 0) + (extra.coin || 0); r.items = Object.assign({}, r.items, extra.items); }
    ups = grant(r); sys('完成「' + q.title + '」：' + rewardText(r));
    pages = (q.outro || []).slice();
    const nx = D.quests[S.step]; if (nx && nx.first && nx.type !== 'talk' && nx.type !== 'spot') pages = pages.concat(nx.accept);
  }
  toast(ups ? '升到 ' + S.lv + ' 级！气血法力已回满' : (D.quests[S.step] ? '任务更新：' + D.quests[S.step].goal : '第一章已完成，后续剧情敬请期待'));
  if (wasLow && S.lv >= SECT_LV && curCls().joinSect) sys('你已达 ' + SECT_LV + ' 级，可前往太虚之林，找道家门主入门，再去堂主处选分堂。');
  save(); refresh();
  if (pages.length) showPages(pages);
}
function talk(n) {
  const q = D.quests[S.step];
  if (q && (q.type === 'talk' || q.type === 'spot') && q.npc === n.id) {
    showPages((q.first ? q.accept : []).concat(q.pages), () => finishBeat(q));
    return;
  }
  const info = D.npcs[n.id];
  openDialog(n, info.idle, () => {
    if (info.shop === 'smith') openSmith('forge');
    else if (info.shop) openShop(info.shop);
    else if (info.act === 'ssEnvoy') openEnvoy();
    else if (info.act === 'joinSect') joinDaoTalk();
    else if (info.act === 'pickHall') pickHallTalk(info.hall);
    else if (info.act === 'arena') openModal('竞技场', '<p class="center">五对五模拟对战（不影响存档）。</p><p class="center"><button type="button" class="tbtn red" data-act="arena">上擂台</button>　<button type="button" class="tbtn" data-act="close">再看看</button></p>');
    else if (info.rest) { const st = stats(); if (S.hp < st.maxHp || S.mp < st.maxMp) { S.hp = st.maxHp; S.mp = st.maxMp; save(); refresh(); toast(info.restMsg || '歇了一会儿，气血法力回满'); } }
  });
}
// 地图上的敌人：走过去后弹出确认
function openFoe(a) {
  const q0 = D.quests[S.step];
  if (a.stage.story && q0 && q0.stage === a.stage.id && q0.pre && q0.pre.length && S.preSeen !== S.step) { S.preSeen = S.step; save(); showPages(q0.pre, () => openFoe(a)); return; }
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
  return M.list.find(a => a.sb === S.step) || hopTo(q.map);
}
// 剧情首领 / 采集点：只在对应任务进行到那一拍时出现在地图上
function syncStory() {
  const q = D.quests[S.step];
  Object.values(M.npcs).forEach(n => { n.el.style.display = q && q.type === 'fight' && q.map === M.id && D.stages.find(s => s.id === q.stage).name === n.name ? 'none' : ''; });   // 剧情首领和同名 NPC 是同一个人：战斗前后只显示一个
  const stale = M.list.filter(a => a.sb !== undefined && !(q && a.sb === S.step));
  stale.forEach(a => { a.el.remove(); delete M.npcs[a.id]; }); if (stale.length) M.list = M.list.filter(a => !stale.includes(a));
  if (!q || q.map !== M.id || M.list.some(a => a.sb === S.step)) return;
  if (q.type === 'fight' || q.type === 'kill') {
    const st = D.stages.find(s => s.id === q.stage), lead = D.enemies[st.enemies[0]];
    const a = makeActor('foe', 'foe_' + st.id, lead.sprite, st.name, I(q.at[0], q.at[1]), { color: '#ff9a8a', title: '建议 ' + st.rec + ' 级', titleColor: '#ffd56a' });
    a.stage = st; a.sb = S.step; M.list.push(a);
  } else if (q.type === 'spot') {
    const a = makePortal({ to: '', name: q.spot, at: q.at }); a.kind = 'spot'; a.id = q.npc; a.name = q.spot; a.color = '#9ff0ff'; a.sb = S.step; a.el.classList.add('spot');
    drawTag(a); M.list.push(a); M.npcs[a.id] = a;
  }
}
function showQuest() {
  if (!M) return;
  syncStory();
  const q = D.quests[S.step], tgt = questTarget();
  let ch = false; M.list.forEach(a => { const m = a === tgt; if (a.mark !== m) { a.mark = m; drawTag(a); ch = true; } }); if (ch) drawMiniDots();
  $('qTitle').textContent = q ? q.title : '江湖路远'; $('qGoal').textContent = q ? q.goal + (q.type === 'kill' ? '（' + Math.min(S.cnt || 0, q.n) + '/' + q.n + '）' : '') : '第一章已完成，去各处历练升级吧';
  $('qSide').textContent = q ? q.chapter : '待续';
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
  const cands = n.kind === 'portal' || n.kind === 'spot' ? [{ x: n.pos.x, y: n.pos.y }] :
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
  const nr = fox.h > 70 ? 104 : 62;
  if (dist > nr + 22) fox.moving = true; if (dist < nr) fox.moving = false;
  if (dist > 600) { fox.pos.x = player.pos.x; fox.pos.y = player.pos.y; return; }
  if (!fox.moving) return;
  const len = Math.min(dist - (nr - 4), (SPEED + 30) * dt), sx = tx / dist * len;
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
  for (const n of M.list) if (Math.abs(p.x - n.pos.x) < 54 && p.y < n.pos.y + 20 && p.y > n.pos.y - (n.kind === 'portal' || n.kind === 'spot' ? n.h : n.h * 1.35) - 40) hit = n;
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

// ---------- 天气 ----------
// 每张地图有自己的天气（kind + 到期时间），到点按权重随机换一个。画面全在一张 720×1280 的 canvas 上：
// 各种效果各有一个 0~1 的“强度”，朝目标值缓缓靠拢，所以换天气是渐变的。演示面板可以把天气固定住。
const WXD = D.weather, wxCv = $('wx'), wxCtx = wxCv.getContext('2d');
const CLOUD = 'M7 18a4 4 0 0 1 .5-7.97A5.5 5.5 0 0 1 18 11a3.5 3.5 0 0 1-.5 7z';
const wxSvg = (inner) => '<svg viewBox="0 0 24 24" aria-hidden="true">' + inner + '</svg>';
const WXICON = {
  clear:  wxSvg('<circle cx="12" cy="12" r="5" fill="#f7b928" stroke="#b9750d"/><g stroke="#e59a12" stroke-width="1.8" stroke-linecap="round"><path d="M12 2.5v2.5M12 19v2.5M2.5 12H5M19 12h2.5M5.3 5.3l1.8 1.8M16.9 16.9l1.8 1.8M5.3 18.7l1.8-1.8M16.9 7.1l1.8-1.8"/></g>'),
  cloudy: wxSvg('<path d="' + CLOUD + '" fill="#dfe5f0" stroke="#7d8aa3" stroke-width="1.2" stroke-linejoin="round"/>'),
  fog:    wxSvg('<path d="M7 14a4 4 0 0 1 .5-6.97A5.5 5.5 0 0 1 18 8a3.5 3.5 0 0 1-.5 6z" fill="#e6eaf1" stroke="#8d98ad" stroke-width="1.2"/><g stroke="#8d98ad" stroke-width="1.8" stroke-linecap="round"><path d="M4 17.5h16M6 21h12"/></g>'),
  rain:   wxSvg('<path d="M7 15a4 4 0 0 1 .5-6.97A5.5 5.5 0 0 1 18 9a3.5 3.5 0 0 1-.5 6z" fill="#c4cfe2" stroke="#6d7b97" stroke-width="1.2"/><g stroke="#3f8fe0" stroke-width="1.8" stroke-linecap="round"><path d="M8 17.5l-1 3M12.5 17.5l-1 3M17 17.5l-1 3"/></g>'),
  storm:  wxSvg('<path d="M7 15a4 4 0 0 1 .5-6.97A5.5 5.5 0 0 1 18 9a3.5 3.5 0 0 1-.5 6z" fill="#8f9ab3" stroke="#4d5a78" stroke-width="1.2"/><path d="M12.8 13l-3 4.6h2.6l-1.4 4.4 4.4-5.6h-2.7l1.3-3.4z" fill="#ffd23f" stroke="#c7900a" stroke-width=".8" stroke-linejoin="round"/>'),
  snow:   wxSvg('<path d="M7 15a4 4 0 0 1 .5-6.97A5.5 5.5 0 0 1 18 9a3.5 3.5 0 0 1-.5 6z" fill="#e8eef9" stroke="#8d9bb8" stroke-width="1.2"/><g fill="#7fb4ee"><circle cx="8" cy="18.5" r="1.4"/><circle cx="12.5" cy="20.5" r="1.4"/><circle cx="17" cy="18.5" r="1.4"/></g>')
};
const WXKINDS = ['clear', 'cloudy', 'fog', 'rain', 'storm', 'snow'];
const WXTARGET = {            // 各效果强度的目标值
  clear:  { rain: 0, storm: 0, snow: 0, fog: 0,   cloud: 0,  dark: 0 },
  cloudy: { rain: 0, storm: 0, snow: 0, fog: 0,   cloud: .9, dark: .35 },
  fog:    { rain: 0, storm: 0, snow: 0, fog: 1,   cloud: .3, dark: .12 },
  rain:   { rain: .7, storm: 0, snow: 0, fog: .25, cloud: 1, dark: .55 },
  storm:  { rain: 1, storm: 1, snow: 0, fog: .2,  cloud: 1, dark: 1 },
  snow:   { rain: 0, storm: 0, snow: 1, fog: .3,  cloud: .6, dark: .2 }
};
const wxLv = { rain: 0, storm: 0, snow: 0, fog: 0, cloud: 0, dark: 0 };
const wxSt = {}; let wxLock = null, wxShown = null, wxFlash = 0, wxFlashAt = 4, wxT = 0;
const rainP = Array.from({ length: 420 }, () => ({ x: Math.random() * 820, y: Math.random() * 1280, v: .8 + Math.random() * .5, l: 20 + Math.random() * 28 }));
const snowP = Array.from({ length: 130 }, () => ({ x: Math.random() * 720, y: Math.random() * 1280, v: 30 + Math.random() * 50, r: 1.4 + Math.random() * 2.6, ph: Math.random() * 10 }));
const fogP = Array.from({ length: 7 }, (_, i) => ({ x: i * 130, y: 160 + (i % 4) * 280 + Math.random() * 120, r: 260 + Math.random() * 160, v: 10 + Math.random() * 14, ph: Math.random() * 10 }));
function wxPick(mapId, not) {
  const w = Object.assign({}, WXD.byMap[mapId] || { clear: 1 }); if (not && Object.keys(w).length > 1) delete w[not];
  let sum = 0; for (const k in w) sum += w[k]; let r = Math.random() * sum;
  for (const k in w) { r -= w[k]; if (r <= 0) return k; } return 'clear';
}
const wxRoll = (mapId, not) => ({ kind: wxPick(mapId, not), until: Date.now() + (WXD.span[0] + Math.random() * (WXD.span[1] - WXD.span[0])) * 1000 });
const wxKind = () => wxLock || (wxSt[M.id] && wxSt[M.id].kind) || 'clear';
function wxUpdate(announce) {          // 到期就换；换了就刷新图标并提示
  if (!M) return;
  if (!wxSt[M.id]) wxSt[M.id] = wxRoll(M.id);
  else if (!wxLock && Date.now() > wxSt[M.id].until) wxSt[M.id] = wxRoll(M.id, wxSt[M.id].kind);
  const k = wxKind();
  if (k !== wxShown) {
    const first = wxShown === null; wxShown = k;
    $('wxBtn').innerHTML = WXICON[k]; $('wxBtn').title = WXD[k].name;
    if (announce && !first) toast('天气转为 ' + WXD[k].name);
  }
}
function wxSnap() { wxShown = null; wxUpdate(false); Object.assign(wxLv, WXTARGET[wxKind()]); }   // 进新地图：不渐变，直接是那里的天气
function wxDraw(dt) {
  wxT += dt; const g = wxCtx, tg = WXTARGET[wxKind()], e = 1 - Math.exp(-1.1 * dt);
  for (const k in wxLv) wxLv[k] += (tg[k] - wxLv[k]) * e;
  g.clearRect(0, 0, 720, 1280);
  const wet = Math.min(1, wxLv.rain + wxLv.snow * .8 + wxLv.fog * .5);
  $('petals').style.opacity = (1 - wet).toFixed(2);
  if (wxLv.dark > .01) { g.fillStyle = 'rgba(24,34,60,' + (wxLv.dark * .5).toFixed(3) + ')'; g.fillRect(0, 0, 720, 1280); }
  if (reduce) return;
  if (wxLv.cloud > .02) {              // 云影：几块缓缓飘过的暗斑
    for (let i = 0; i < 4; i++) {
      const x = ((wxT * (14 + i * 5) + i * 260) % 1100) - 190, y = 180 + i * 290, r = 230 + i * 30, gr = g.createRadialGradient(x, y, 0, x, y, r);
      gr.addColorStop(0, 'rgba(20,28,45,' + (wxLv.cloud * .2).toFixed(3) + ')'); gr.addColorStop(1, 'rgba(20,28,45,0)'); g.fillStyle = gr; g.fillRect(x - r, y - r, r * 2, r * 2);
    }
  }
  if (wxLv.fog > .02) {
    for (const f of fogP) {
      const x = ((f.x + wxT * f.v) % 1000) - 140, y = f.y + Math.sin(wxT * .25 + f.ph) * 30, gr = g.createRadialGradient(x, y, 0, x, y, f.r);
      gr.addColorStop(0, 'rgba(232,238,244,' + (wxLv.fog * .34).toFixed(3) + ')'); gr.addColorStop(1, 'rgba(232,238,244,0)'); g.fillStyle = gr; g.fillRect(x - f.r, y - f.r, f.r * 2, f.r * 2);
    }
    g.fillStyle = 'rgba(226,232,238,' + (wxLv.fog * .1).toFixed(3) + ')'; g.fillRect(0, 0, 720, 1280);
  }
  if (wxLv.rain > .02) {               // 雨丝：雷雨更密更斜
    const n = Math.floor(rainP.length * Math.min(1, wxLv.rain)), slant = .16 + wxLv.storm * .16;
    g.lineWidth = 1.7; g.strokeStyle = 'rgba(214,228,250,' + (.36 + wxLv.rain * .24).toFixed(2) + ')'; g.beginPath();
    for (let i = 0; i < n; i++) {
      const p = rainP[i]; p.y += 1050 * p.v * dt * (1 + wxLv.storm * .3); p.x -= 1050 * p.v * dt * slant * (1 + wxLv.storm * .3);
      if (p.y > 1300 || p.x < -40) { p.y = -40; p.x = Math.random() * 820; }
      g.moveTo(p.x, p.y); g.lineTo(p.x + p.l * slant, p.y - p.l);
    }
    g.stroke();
  }
  if (wxLv.snow > .02) {
    const n = Math.floor(snowP.length * wxLv.snow); g.fillStyle = 'rgba(255,255,255,.85)'; g.beginPath();
    for (let i = 0; i < n; i++) {
      const p = snowP[i]; p.y += p.v * dt; p.x += Math.sin(wxT * 1.1 + p.ph) * 22 * dt - 8 * dt;
      if (p.y > 1300) { p.y = -10; p.x = Math.random() * 720; } if (p.x < -10) p.x = 730;
      g.moveTo(p.x + p.r, p.y); g.arc(p.x, p.y, p.r, 0, 6.2832);
    }
    g.fill();
  }
  if (wxLv.storm > .5) {               // 闪电：隔几秒闪一两下
    wxFlashAt -= dt; if (wxFlashAt <= 0) { wxFlash = 1; wxFlashAt = 4 + Math.random() * 7; if (Math.random() < .5) setTimeout(() => { wxFlash = .8; }, 170); }
  }
  if (wxFlash > .01) { g.fillStyle = 'rgba(236,242,255,' + (wxFlash * .62).toFixed(3) + ')'; g.fillRect(0, 0, 720, 1280); wxFlash *= Math.exp(-7 * dt); }
}
function openWeather() {
  if (B.active) return;
  const k = wxKind(), d = WXD[k], st = wxSt[M.id];
  let eta = '天气会一直保持';
  if (!wxLock && st) { const m = Math.max(1, Math.round((st.until - Date.now()) / 60000)); eta = '约 ' + m + ' 分钟后转天气'; }
  openModal('天气', '<div class="wxnow">' + WXICON[k].replace('<svg ', '<svg class="big" ') + '<div><h5>' + M.def.name + ' · ' + d.name + '</h5><p>' + d.desc + '</p><small>' + eta + '</small></div></div>' +
    '<p style="margin:10px 0 4px;color:var(--muted)">演示：直接切换这张地图的天气</p><div class="wxgrid">' +
    WXKINDS.map(x => '<button type="button" class="' + (x === k && wxLock ? 'on' : '') + '" data-act="wx:' + x + '">' + WXICON[x] + WXD[x].name + '</button>').join('') + '</div>' +
    '<p class="center" style="margin:14px 0 0"><button type="button" class="tbtn' + (wxLock ? '' : ' on') + '" data-act="wxAuto">' + (wxLock ? '恢复自动天气' : '自动天气中') + '</button></p>');
}
$('wxBtn').addEventListener('click', e => { e.stopPropagation(); openWeather(); });

// ============================================================ 流水（水面遮罩 + 滚动高光 + 涟漪 + 落叶）
// 水面遮罩由 tools/water.py 按颜色抠出；每帧在遮罩内滚动两层纹理，不改原图。
const WT = { on: false, cv: null, cx: null, mask: null, zones: [], leaves: [], rip: [], pts: null, acc: 0, tiles: null };
function wtTiles() {
  const mk = vert => { const c = document.createElement('canvas'); c.width = c.height = 256; const x = c.getContext('2d'); x.lineCap = 'round';
    let seed = vert ? 11 : 5; const R = () => (seed = (seed * 16807) % 2147483647) / 2147483647;
    const wrap = fn => { for (const ox of [-256, 0, 256]) for (const oy of [-256, 0, 256]) fn(ox, oy); };
    // 细密的亮纹 + 暗纹（短、细、弯）
    for (let i = 0; i < 150; i++) { const px = R() * 256, py = R() * 256, L = 6 + R() * 20, bright = R() > .4, bend = (R() - .5) * 5;
      x.strokeStyle = bright ? 'rgba(238,255,250,' + (.3 + R() * .4).toFixed(2) + ')' : 'rgba(5,75,85,' + (.14 + R() * .16).toFixed(2) + ')'; x.lineWidth = bright ? .7 + R() * .9 : 1 + R() * 1.1;
      wrap((ox, oy) => { x.beginPath(); if (vert) { x.moveTo(px + ox, py + oy); x.quadraticCurveTo(px + ox + bend, py + oy + L * .7, px + ox + bend * .4, py + oy + L * 1.6); } else { x.moveTo(px + ox, py + oy); x.quadraticCurveTo(px + ox + L / 2, py + oy + bend, px + ox + L, py + oy + L * .1); } x.stroke(); }); }
    // 焦散：小段网状亮线
    for (let i = 0; i < 34; i++) { const px = R() * 256, py = R() * 256, r = 4 + R() * 8, a0 = R() * 6.28;
      x.strokeStyle = 'rgba(255,255,240,' + (.22 + R() * .25).toFixed(2) + ')'; x.lineWidth = .6 + R() * .6;
      wrap((ox, oy) => { x.beginPath(); x.arc(px + ox, py + oy, r, a0, a0 + 1.2 + R() * 1.2); x.stroke(); }); }
    return c; };
  return { h: mk(false), v: mk(true) };
}
function waterSetup() {
  const W = window.WATER && window.WATER[M.id], def = M.def.water;
  WT.on = false; if (WT.cv) { WT.cv.remove(); WT.cv = null; }
  if (!W || !def) return;
  WT.f = W.w / 1000;
  if (!WT.tiles) WT.tiles = wtTiles();
  const cv = document.createElement('canvas'); cv.id = 'water'; cv.width = W.w; cv.height = W.h; cv.style.width = M.W + 'px'; cv.style.height = M.H + 'px';
  bgMap.after(cv); WT.cv = cv; WT.cx = cv.getContext('2d'); WT.leaves = []; WT.rip = []; WT.zones = [];
  const img = new Image(); const id = M.id;
  img.onload = () => {
    if (!M || M.id !== id) return;
    const sc = c => { const o = document.createElement('canvas'); o.width = W.w; o.height = W.h; return o; };
    const base = sc(); const bx = base.getContext('2d'); bx.drawImage(img, 0, 0);
    const id2 = bx.getImageData(0, 0, W.w, W.h), px = id2.data; for (let i = 0; i < W.w * W.h; i++) { px[i * 4 + 3] = px[i * 4] > 128 ? 255 : 0; } bx.putImageData(id2, 0, 0);   // 黑白图 → 白色=不透明、黑色=透明
    WT.pts = []; WT.W = W.w; WT.H = W.h; WT.solid = new Uint8Array(W.w * W.h);
    for (let i = 0; i < W.w * W.h; i++) if (px[i * 4 + 3] > 128) { WT.solid[i] = 1; if (i % 37 === 0) WT.pts.push(i); }
    // 按区域拆分遮罩：每个区域有自己的流向和流速
    const list = (def.zones || []).concat([{ rest: true, dir: def.dir, speed: def.speed }]);
    WT.zones = list.map(z => { const o = sc(), x = o.getContext('2d'); x.drawImage(base, 0, 0); x.globalCompositeOperation = z.rest ? 'destination-out' : 'destination-in';
      if (z.rest) { (def.zones || []).forEach(q => x.fillRect(q.rect[0] * WT.f, q.rect[1] * WT.f, (q.rect[2] - q.rect[0]) * WT.f, (q.rect[3] - q.rect[1]) * WT.f)); } else x.fillRect(z.rect[0] * WT.f, z.rect[1] * WT.f, (z.rect[2] - z.rect[0]) * WT.f, (z.rect[3] - z.rect[1]) * WT.f);
      const d = z.dir || [-1, .3], L = Math.hypot(d[0], d[1]) || 1; return { box: z.rest ? null : z.rect.map(v => v * WT.f), mask: o, dx: d[0] / L, dy: d[1] / L, sp: (z.speed || 20) * WT.f, vert: Math.abs(d[1]) > Math.abs(d[0]) * 1.5, buf: sc() }; });
    // 把水面按 40px 网格连通成几块，只在这些包围框里绘制（省掉大片岸上的无用计算）
    const C = 40, gw = Math.ceil(W.w / C), gh = Math.ceil(W.h / C), cell = new Uint8Array(gw * gh);
    for (let y = 0; y < W.h; y++) for (let x = 0; x < W.w; x++) if (WT.solid[y * W.w + x]) cell[((y / C) | 0) * gw + ((x / C) | 0)] = 1;
    const seen = new Uint8Array(gw * gh); WT.regions = [];
    for (let i = 0; i < cell.length; i++) if (cell[i] && !seen[i]) { const st = [i]; seen[i] = 1; let x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1;
      while (st.length) { const c = st.pop(), cx_ = c % gw, cy_ = (c / gw) | 0; x0 = Math.min(x0, cx_); x1 = Math.max(x1, cx_); y0 = Math.min(y0, cy_); y1 = Math.max(y1, cy_);
        for (let dy = -1; dy <= 1; dy++) for (let dx = -1; dx <= 1; dx++) { const nx = cx_ + dx, ny = cy_ + dy; if (nx < 0 || ny < 0 || nx >= gw || ny >= gh) continue; const n = ny * gw + nx; if (cell[n] && !seen[n]) { seen[n] = 1; st.push(n); } } }
      WT.regions.push([x0 * C, y0 * C, Math.min(W.w, (x1 + 1) * C), Math.min(W.h, (y1 + 1) * C)]); }
    WT.on = true;
  };
  img.src = W.mask;
}
function waterDraw(dt, t) {
  if (!WT.on || B.active || reduce) return;
  WT.acc += dt; if (WT.acc < 1 / 24) return; const step = WT.acc; WT.acc = 0;
  const cx = WT.cx, W = WT.W, H = WT.H, f = WT.f; cx.clearRect(0, 0, W, H);
  for (const z of WT.zones) {
    const b = z.buf.getContext('2d'), tile = z.vert ? WT.tiles.v : WT.tiles.h;
    for (const rg of WT.regions) {
      const x0 = z.box ? Math.max(rg[0], z.box[0]) : rg[0], y0 = z.box ? Math.max(rg[1], z.box[1]) : rg[1], x1 = z.box ? Math.min(rg[2], z.box[2]) : rg[2], y1 = z.box ? Math.min(rg[3], z.box[3]) : rg[3];
      if (x1 <= x0 || y1 <= y0) continue; const w = x1 - x0, h = y1 - y0;
      b.save(); b.beginPath(); b.rect(x0, y0, w, h); b.clip(); b.globalCompositeOperation = 'source-over'; b.clearRect(x0, y0, w, h);
      for (const [sc, sp, al] of [[1, 1, .8], [1.55, .62, .55], [.7, 1.35, .3]]) {
        b.save(); b.globalAlpha = al; b.scale(sc, sc);
        const ox = ((t * z.sp * z.dx * sp) % 256 + 256) % 256, oy = ((t * z.sp * z.dy * sp) % 256 + 256) % 256;
        b.fillStyle = b.createPattern(tile, 'repeat'); b.translate(ox, oy); b.fillRect(x0 / sc - ox, y0 / sc - oy, w / sc, h / sc); b.restore();
      }
      b.globalCompositeOperation = 'destination-in'; b.drawImage(z.mask, 0, 0); b.restore();
      cx.drawImage(z.buf, x0, y0, w, h, x0, y0, w, h);
    }
  }
  // 涟漪
  if (WT.pts.length && Math.random() < step * 2.2 && WT.rip.length < 8) { const i = WT.pts[(Math.random() * WT.pts.length) | 0]; WT.rip.push({ x: i % W, y: (i / W) | 0, a: 0, life: 1.6 + Math.random() }); }
  cx.save(); cx.lineWidth = 1 * f;
  for (let i = WT.rip.length - 1; i >= 0; i--) { const r = WT.rip[i]; r.a += step; const k = r.a / r.life; if (k >= 1) { WT.rip.splice(i, 1); continue; }
    cx.strokeStyle = 'rgba(240,255,252,' + (.55 * (1 - k)).toFixed(2) + ')'; cx.beginPath(); cx.ellipse(r.x, r.y, (3 + k * 16) * f, (3 + k * 16) * f * .45, 0, 0, 6.283); cx.stroke(); }
  cx.restore();
  // 闪光点：随机亮点一闪一闪
  if (WT.pts.length) { cx.save(); for (const sp of WT.spk || (WT.spk = Array.from({ length: 46 }, () => ({ i: WT.pts[(Math.random() * WT.pts.length) | 0], ph: Math.random() * 6.28, fr: .8 + Math.random() * 1.8 })))) {
    const v = Math.sin(t * sp.fr * 2 + sp.ph); if (v < .55) { if (v < -.98) sp.i = WT.pts[(Math.random() * WT.pts.length) | 0]; continue; }
    const x = sp.i % W, y = (sp.i / W) | 0, a = (v - .55) / .45, r = (1 + a * 1.4) * f; cx.fillStyle = 'rgba(255,255,245,' + (.85 * a).toFixed(2) + ')'; cx.fillRect(x - r, y - r * .5, r * 2, r); cx.fillRect(x - r * .5, y - r * 1.4, r, r * 2.8); } cx.restore(); }
  // 落叶：顺着水流漂，漂出水面就换个位置重新出现
  if (WT.pts.length && WT.leaves.length < 6 && Math.random() < step * .8) { const i = WT.pts[(Math.random() * WT.pts.length) | 0]; WT.leaves.push({ x: i % W, y: (i / W) | 0, ph: Math.random() * 6, hue: Math.random() > .5 ? '#d9a441' : '#7fb04a', life: 10 + Math.random() * 8, a: 0 }); }
  for (let i = WT.leaves.length - 1; i >= 0; i--) { const l = WT.leaves[i]; l.a += step;
    const def = M.def.water, zones = def.zones || [];
    let d = def.dir || [-1, .3], sp = def.speed || 20; for (const q of zones) if (l.x / f >= q.rect[0] && l.x / f <= q.rect[2] && l.y / f >= q.rect[1] && l.y / f <= q.rect[3]) { d = q.dir; sp = q.speed; break; }
    const L = Math.hypot(d[0], d[1]) || 1; l.x += d[0] / L * sp * f * .8 * step; l.y += d[1] / L * sp * f * .8 * step;
    const xi = l.x | 0, yi = l.y | 0;
    if (l.a > l.life || xi < 0 || yi < 0 || xi >= W || yi >= H || !WT.solid[yi * W + xi]) { WT.leaves.splice(i, 1); continue; }
    const fade = Math.min(1, l.a * 2, (l.life - l.a) * 2); cx.save(); cx.globalAlpha = fade * .9; cx.translate(l.x, l.y + Math.sin(t * 2 + l.ph) * 1.2 * f); cx.scale(f * .8, f * .8); cx.rotate(Math.sin(t * 1.4 + l.ph) * .8); cx.fillStyle = l.hue; cx.beginPath(); cx.ellipse(0, 0, 5, 2.4, 0, 0, 6.283); cx.fill(); cx.strokeStyle = 'rgba(60,40,10,.5)'; cx.lineWidth = .8; cx.beginPath(); cx.moveTo(-5, 0); cx.lineTo(5, 0); cx.stroke(); cx.restore(); }
}

function frame(now) {
  const dt = Math.min(.05, (now - last) / 1000), t = now / 1000; last = now;
  if (!B.active && M) {
    const busy = !$('dialog').hidden || !$('modal').hidden;
    if (wander && !player.moving && !busy) {
      wanderWait -= dt;
      if (wanderWait <= 0) { wanderWait = .6 + Math.random() * .8; const p = { x: player.pos.x + rnd(-320, 320), y: player.pos.y + rnd(-260, 260) }; if (inside(p)) moveTo(p, true); }
    }
    stepPlayer(dt); stepFox(dt);
    placePlayer(); place(fox, t); for (const a of M.list) if (a.kind !== 'portal' && a.kind !== 'spot') place(a, t);
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
  wxUpdate(true); wxDraw(dt); waterDraw(dt, t);
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
  const el = document.createElement('div'); el.className = 'unit ' + side + ' ' + row; el.style.setProperty('--ib', (-Math.random() * 2.6).toFixed(2) + 's');
  el.innerHTML = '<div class="sh"></div><img alt="" src="' + IMG(def.sprite) + '" style="width:' + w + 'px;height:' + h + 'px;left:' + (-w / 2) + 'px' + (def.flip ? ';scale:-1 1' : '') + '">' +
    '<div class="tap" style="left:' + (-w / 2) + 'px;bottom:0;width:' + w + 'px;height:' + h + 'px"></div>' +
    '<div class="hpb" style="bottom:' + (h + 4) + 'px"><i></i></div><div class="stt"></div>' + (row === 'person' ? '<div class="nm" style="bottom:' + (h + 13) + 'px">' + def.name + '</div>' : '');
  el.style.left = pos.x + 'px'; el.style.top = pos.y + 'px'; el.style.zIndex = Math.round(pos.y);
  $('units').appendChild(el);
  const st = def.stats;
  const u = { side, row, sprite: def.sprite, name: def.name, el, pos, h, hp: st.hp, maxHp: st.maxHp, mp: st.mp || 0, maxMp: st.maxMp || 0, atk: st.atk, def: st.def, matk: st.matk != null ? st.matk : st.atk, mdef: st.mdef != null ? st.mdef : st.def, crit: st.crit != null ? st.crit : 10, dodge: st.dodge || 0, spd: st.spd,
    kit: def.kit, pas: def.pas, cd: {}, alive: true, defending: false, ai: def.ai || 'attack', aggro: def.aggro || 1, data: def.data || null, magicAtk: !!def.magicAtk, st: {} };
  if (side === 'enemy') el.addEventListener('click', () => { if (B.picking && u.alive) { const f = B.picking; B.picking = null; f(u); } });
  if (u.pas && u.pas.atkMul) { u.atk = Math.round(u.atk * u.pas.atkMul); u.matk = Math.round(u.matk * u.pas.atkMul); }
  B.units.push(u); petAnimInit(u); drawUnit(u); return u;
}

// 神兽逐帧动画（原作高清帧，图集元数据见 ASSETS.pet[sprite]）：待机循环；攻击 / 技能 / 受击 / 闪避时插播，播完回到待机
function petAnimInit(u) {
  const m = AS.pet && AS.pet[u.sprite]; if (!m || !AS.img[m.sheet]) return;
  const k = u.h / m.H, w = m.cw * k, h = m.ch * k, d = document.createElement('div'), img = u.el.querySelector('img');
  d.className = 'pa'; d.style.cssText = 'left:' + (-m.ox * k) + 'px;bottom:' + (-4 - m.by * k) + 'px;width:' + w + 'px;height:' + h + 'px;background-image:url(' + IMG(m.sheet) + ');background-size:' + (m.cols * 100) + '% ' + (m.rows * 100) + '%';
  u.el.insertBefore(d, img); img.style.visibility = 'hidden';
  u.pa = { d, m, tm: 0, gen: 0, i: 0 }; petIdle(u);
}
function petShow(u, f) { const m = u.pa.m, c = f % m.cols, r = Math.floor(f / m.cols); u.pa.d.style.backgroundPosition = (m.cols > 1 ? c * 100 / (m.cols - 1) : 0) + '% ' + (m.rows > 1 ? r * 100 / (m.rows - 1) : 0) + '%'; }
function petIdle(u) {
  const p = u.pa, g = ++p.gen, s = p.m.seq.idle; clearTimeout(p.tm);
  const loop = () => { if (p.gen !== g || !u.el.isConnected) return; if (p.i >= s.length) p.i = 0; const [f, ms] = s[p.i++]; petShow(u, f); p.tm = setTimeout(loop, ms); };
  loop();
}
// 播一段帧序列；到释放帧时调用 onRel；整段播完（回到待机）后 resolve
function petPlay(u, name, onRel) {
  const p = u.pa; if (!p || !p.m.seq[name]) { onRel && onRel(); return Promise.resolve(); }
  const g = ++p.gen, s = p.m.seq[name], rel = p.m.rel[name]; clearTimeout(p.tm);
  return new Promise(res => {
    let i = 0;
    const run = () => {
      if (p.gen !== g || !u.el.isConnected) { onRel && i <= rel && onRel(); return res(); }
      if (i >= s.length) { p.i = 0; petIdle(u); return res(); }
      const [f, ms] = s[i]; petShow(u, f); if (i === rel && onRel) onRel(); i++; p.tm = setTimeout(run, ms);
    };
    run();
  });
}

// 神兽弹道特效：原作光束 / 龙焰帧（图集 ASSETS.pet[...].pfx），原图朝右，按施法者→目标的方向旋转、飞向目标
function petFx(u, skill, t) {
  const m = u.pa && u.pa.m.pfx; if (!m || !m.map[skill] || !AS.img[m.sheet]) return 0;
  const fr = m.map[skill], k = u.h / u.pa.m.H, w = m.cw * k, h = m.ch * k;
  const sx = u.pos.x - u.h * 0.12, sy = u.pos.y - u.h * 0.55, ex = t.pos.x, ey = t.pos.y - t.h * 0.5, ang = Math.atan2(ey - sy, ex - sx) * 180 / Math.PI, dist = Math.hypot(ex - sx, ey - sy);
  const e = document.createElement('div'); e.className = 'pfx';
  e.style.cssText = 'left:' + sx + 'px;top:' + (sy - h / 2) + 'px;width:' + w + 'px;height:' + h + 'px;background-image:url(' + IMG(m.sheet) + ');background-size:' + (m.n * 100) + '% 100%;transform-origin:0 50%;transform:rotate(' + ang + 'deg) translateX(0);z-index:1999';
  $('units').appendChild(e); const dur = Math.max(160, Math.min(320, dist * 1.1));
  let i = 0; const step = () => { const f = fr[i++ % fr.length]; e.style.backgroundPosition = (m.n > 1 ? f * 100 / (m.n - 1) : 0) + '% 0'; }; step(); const tm = setInterval(step, 70);
  requestAnimationFrame(() => requestAnimationFrame(() => { e.style.transition = 'transform ' + dur + 'ms cubic-bezier(.4,0,.8,.6),opacity ' + dur + 'ms'; e.style.transform = 'rotate(' + ang + 'deg) translateX(' + Math.max(0, dist - w * 0.6) + 'px)'; }));
  setTimeout(() => { clearInterval(tm); e.style.opacity = 0; setTimeout(() => e.remove(), 160); }, dur);
  return dur;
}
function petSeqFor(u, skill) { if (!u.pa) return null; const t = D.petSeq && D.petSeq[u.sprite], s = t && t[skill], sq = u.pa.m.seq; return s && sq[s] ? s : sq.atk && (skill === 'attack' || D.skills[skill] && D.skills[skill].ss) ? 'atk' : null; }
function drawUnit(u) {
  u.el.querySelector('.hpb i').style.width = Math.max(0, u.hp / u.maxHp * 100) + '%';
  if (u === B.player) { setBar('hp', u.hp, u.maxHp); setBar('mp', u.mp, u.maxMp); }
}
// ---------- 战斗手感：合成音效（WebAudio，无外部素材）/ 顿帧 / 震屏 ----------
const SND = (() => {
  let ac = null, master = null, muted = false;
  try { muted = localStorage.getItem('gq_mute') === '1'; } catch (e) {}
  const ctx = () => { if (muted) return null; try { if (!ac) { ac = new (window.AudioContext || window.webkitAudioContext)(); master = ac.createGain(); master.gain.value = 0.32; master.connect(ac.destination); } if (ac.state === 'suspended') ac.resume(); return ac; } catch (e) { return null; } };
  const noise = (a, dur, f0, f1, vol, type) => { const c = ctx(); if (!c) return; const n = c.sampleRate * dur | 0, b = c.createBuffer(1, n, c.sampleRate), d = b.getChannelData(0); for (let i = 0; i < n; i++) d[i] = Math.random() * 2 - 1;
    const s = c.createBufferSource(); s.buffer = b; const f = c.createBiquadFilter(); f.type = type || 'lowpass'; f.frequency.setValueAtTime(f0, c.currentTime + a); f.frequency.exponentialRampToValueAtTime(Math.max(40, f1), c.currentTime + a + dur);
    const g = c.createGain(); g.gain.setValueAtTime(vol, c.currentTime + a); g.gain.exponentialRampToValueAtTime(0.001, c.currentTime + a + dur); s.connect(f); f.connect(g); g.connect(master); s.start(c.currentTime + a); };
  const tone = (a, dur, f0, f1, vol, type) => { const c = ctx(); if (!c) return; const o = c.createOscillator(), g = c.createGain(); o.type = type || 'sine'; o.frequency.setValueAtTime(f0, c.currentTime + a); if (f1 !== f0) o.frequency.exponentialRampToValueAtTime(Math.max(30, f1), c.currentTime + a + dur);
    g.gain.setValueAtTime(0.0001, c.currentTime + a); g.gain.exponentialRampToValueAtTime(vol, c.currentTime + a + 0.008); g.gain.exponentialRampToValueAtTime(0.001, c.currentTime + a + dur); o.connect(g); g.connect(master); o.start(c.currentTime + a); o.stop(c.currentTime + a + dur + 0.05); };
  const pluck = (a, f, vol) => { tone(a, 0.55, f, f * 0.995, vol, 'triangle'); tone(a, 0.3, f * 2, f * 2, vol * 0.35, 'sine'); };
  return {
    get muted() { return muted; }, toggle() { muted = !muted; try { localStorage.setItem('gq_mute', muted ? '1' : '0'); } catch (e) {} if (!muted) this.hit(false); return muted; },
    unlock() { ctx(); },
    slash() { noise(0, 0.14, 5200, 900, 0.5, 'bandpass'); },
    hit(crit) { noise(0, 0.12, 1800, 160, 0.9); tone(0, 0.14, 150, 55, 0.8, 'sine'); if (crit) { tone(0.01, 0.35, 1320, 880, 0.35, 'square'); tone(0.01, 0.5, 2640, 2000, 0.18, 'sine'); noise(0, 0.2, 3000, 300, 0.5); } },
    dodge() { noise(0, 0.18, 800, 3200, 0.35, 'bandpass'); },
    cast() { [392, 523, 659, 784].forEach((f, i) => pluck(i * 0.07, f, 0.32)); tone(0, 0.7, 196, 294, 0.14, 'sine'); },
    release() { noise(0, 0.35, 700, 5200, 0.38, 'bandpass'); tone(0, 0.5, 330, 660, 0.2, 'sawtooth'); },
    heal() { [523, 659, 784, 1046].forEach((f, i) => tone(i * 0.08, 0.5, f, f, 0.2, 'sine')); },
    die() { tone(0, 0.55, 220, 55, 0.5, 'sawtooth'); noise(0, 0.4, 900, 80, 0.5); },
    win() { [523, 659, 784, 1046, 1318].forEach((f, i) => { pluck(i * 0.11, f, 0.3); }); },
    lose() { [392, 330, 262, 196].forEach((f, i) => tone(i * 0.2, 0.6, f, f * 0.97, 0.3, 'triangle')); },
    ui() { tone(0, 0.08, 880, 880, 0.15, 'sine'); }
  };
})();
function shake(power, ms) {            // 震屏：对整个舞台做衰减抖动（Web Animations，不占用 transform）
  if (reduce || !stage.animate) return;
  const n = 7, kf = [{ translate: '0 0' }];
  for (let i = 1; i < n; i++) { const a = power * (1 - i / n); kf.push({ translate: (i % 2 ? 1 : -1) * a * rnd(0.6, 1) + 'px ' + (i % 3 ? -1 : 1) * a * 0.6 + 'px' }); }
  kf.push({ translate: '0 0' }); stage.animate(kf, { duration: ms || 260, easing: 'ease-out' });
}
async function hitstop(ms) { const u = $('units'); u.classList.add('frozen'); await sleep(ms); u.classList.remove('frozen'); }
function impactAt(u, crit) { const e = document.createElement('div'); e.className = 'impact' + (crit ? ' crit' : ''); e.style.left = u.pos.x + 'px'; e.style.top = (u.pos.y - u.h * 0.5) + 'px'; $('units').appendChild(e); setTimeout(() => e.remove(), 280); }
function knock(t, from, px) {          // 受击后仰：沿“攻击者→目标”方向退一小步再回位
  const dx = t.pos.x - from.pos.x, dy = t.pos.y - from.pos.y, L = Math.hypot(dx, dy) || 1;
  t.el.classList.add('kb'); t.el.style.transform = 'translate(' + (dx / L * px) + 'px,' + (dy / L * px * 0.5) + 'px)';
  setTimeout(() => { t.el.classList.remove('kb'); t.el.classList.add('ret'); t.el.style.transform = ''; setTimeout(() => t.el.classList.remove('ret'), 280); }, 90);
}
function dodgeStep(t, from) {          // 闪避：向侧后方滑开一步
  petPlay(t, 'dodge');
  const s = t.side === 'ally' ? 1 : -1;
  t.el.classList.add('kb'); t.el.style.transform = 'translate(' + (s * 26) + 'px,-8px)';
  setTimeout(() => { t.el.classList.remove('kb'); t.el.classList.add('ret'); t.el.style.transform = ''; setTimeout(() => t.el.classList.remove('ret'), 280); }, 150);
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
// 技能特效（代码绘制，见 index.src.html 的 .sfx）：施法时在出手者脚下起声波，命中 / 治疗时在目标身上起对应效果
const SKILL_FX = { dangmo_qm: 'wave', dangmo_ty: 'wave', dangmo_fs: 'wave', mohun: 'mist', duohun: 'soul', fuling: 'chain', lingmai_qm: 'ring', lingmai_ty: 'ring', lingmai_fs: 'ring', yangsheng: 'heal', tianlai: 'notes', fanhun: 'revive' };
function fxAt(cls, u, ms, dy, style) {
  const e = document.createElement('div'); e.className = 'sfx ' + cls; e.style.left = u.pos.x + 'px'; e.style.top = (u.pos.y - (dy == null ? u.h * 0.45 : dy)) + 'px';
  if (style) e.style.cssText += style; $('units').appendChild(e); setTimeout(() => e.remove(), ms || 900);
}
async function castPose(u, onRelease) {   // 施法动作：有逐帧施法图（bc_*，图像生成）就播帧动画，没有就退回整图摆动
  const bc = u.sprite && u.sprite.startsWith('b_') ? 'bc_' + u.sprite.slice(2) : null;
  if (bc && AS.size[bc] && u.el.querySelector('img')) {
    const img = u.el.querySelector('img'), sz = AS.size[bc], k = u.h / 126, w = sz[0] * k, h = sz[1] * k, d = document.createElement('div');
    d.className = 'castanim'; d.style.cssText = 'left:' + (-w / 2) + 'px;width:' + w + 'px;height:' + h + 'px;background-image:url(' + IMG(bc) + ')' + (u.side === 'ally' ? '' : ';scale:-1 1');
    u.el.insertBefore(d, img); img.style.visibility = 'hidden';
    const seq = [[0, 130], [1, 170], [2, 260], [3, 200]];
    SND.cast();
    for (const [f, ms] of seq) { d.style.backgroundPosition = (f * 100 / 3) + '% 0'; if (f === 2) { SND.release(); onRelease && onRelease(); } await sleep(ms); }
    d.remove(); img.style.visibility = ''; return;
  }
  u.el.style.setProperty('--d', u.side === 'ally' ? -1 : 1); u.el.classList.add('cast'); SND.cast(); await sleep(300); SND.release(); onRelease && onRelease(); await sleep(420); u.el.classList.remove('cast');
}
function castFx(u, id) {                       // 出手者一侧：乐声起势
  const k = SKILL_FX[id]; if (!k) return;
  const ck = CAST_FX[id];
  if (ck && AS.img['fxc_' + ck] && (ck === 'wave' || ck === 'mist' || ck === 'soul' || ck === 'chain')) { castSheet(ck, u, { face: u.side === 'ally' ? 1 : -1, dx: u.side === 'ally' ? -34 : 34, up: 1.6, w: ck === 'chain' ? 260 : 320 }); }
  ['wave', 'wave b', 'wave c'].forEach(c => fxAt(c, u, 800, 6));
}
// 高清光效：4×2 = 8 帧的精灵表（art-src/fx，图像生成重画），黑底，用 screen 混合叠在战场上；缺图时退回下面的代码绘制版
function sheetFx(k, u, scale) {
  const w = 200 * (scale || 1), h = 200 * (scale || 1), e = document.createElement('div'); e.className = 'sfx hd';
  e.style.cssText = 'left:' + u.pos.x + 'px;top:' + (u.pos.y - u.h * 0.5) + 'px;width:' + w + 'px;height:' + h + 'px;background-image:url(' + IMG('fx_' + k) + ')';
  $('units').appendChild(e);
  let i = 0; const step = () => { e.style.backgroundPosition = ((i % 4) * 100 / 3) + '% ' + (i >= 4 ? 100 : 0) + '%'; if (++i >= 8) { clearInterval(tm); setTimeout(() => e.remove(), 90); } };
  const tm = setInterval(step, 95); step();
}
// 原作形象特效（art-src/fx/cast，参考原作帧、去掉人物）：2×2 共 4 帧、黑底转透明。魔影放在施法者身后朝向敌人，仙影出现在被治疗者身上
const CAST_FX = { dangmo_qm: 'wave', dangmo_ty: 'wave', dangmo_fs: 'wave', mohun: 'mist', duohun: 'soul', fuling: 'chain', yangsheng: 'heal', tianlai: 'notes', fanhun: 'revive' };
function castSheet(k, u, opts) {
  opts = opts || {}; const w = (opts.w || 330), h = w * 2 / 3, e = document.createElement('div'); e.className = 'sfx hd c4';
  const face = opts.face ? ' scaleX(' + opts.face + ')' : '';
  e.style.cssText = 'left:' + (u.pos.x + (opts.dx || 0)) + 'px;top:' + (u.pos.y - u.h * (opts.up == null ? 0.55 : opts.up)) + 'px;width:' + w + 'px;height:' + h + 'px;z-index:' + (opts.behind ? Math.round(u.pos.y) - 1 : 1998) + ';background-image:url(' + IMG('fxc_' + k) + ');transform:translate(-50%,-50%)' + face;
  $('units').appendChild(e);
  let i = 0; const step = () => { e.style.backgroundPosition = ((i % 2) * 100) + '% ' + (i >= 2 ? 100 : 0) + '%'; e.style.opacity = i === 3 ? 0.75 : 1; if (++i >= 4) { clearInterval(tm); e.style.transition = 'opacity .25s'; e.style.opacity = 0; setTimeout(() => e.remove(), 260); } };
  const tm = setInterval(step, 190); step();
}
function skillHitFx(id, t) {                   // 目标一侧
  const k = SKILL_FX[id]; if (!k) return false;
  const ck = CAST_FX[id];
  if (ck && AS.img['fxc_' + ck] && (ck === 'heal' || ck === 'notes' || ck === 'revive')) { castSheet(ck, t, { w: 340, up: 0.7 }); return true; }
  if (AS.img['fx_' + k]) { sheetFx(k, t, k === 'revive' ? 1.15 : 1); return true; }
  if (k === 'wave') { fxAt('wave', t, 700); fxAt('wave b', t, 800); }
  else if (k === 'notes') { for (let i = 0; i < 4; i++) { fxAt('note', t, 1100, t.h * 0.3 + i * 4, '--dx:' + rnd(-26, 26) + 'px;animation-delay:' + (i * 0.1) + 's;opacity:0'); const el = $('units').lastChild; el.textContent = i % 2 ? '♫' : '♪'; } }
  else fxAt(k, t, 1000);
  return true;
}
const bmsg = t => { $('bmsg').textContent = t; };
const living = side => B.units.filter(u => u.alive && u.side === side);
const other = side => side === 'ally' ? 'enemy' : 'ally';
const weakestOf = side => living(side).sort((a, b) => a.hp - b.hp)[0];

// teams = { ally: { persons: [...], pets: [...] }, enemy: { persons: [...], pets: [...] } }，每个成员是 makeUnit 用的 def
function beginBattle(mode, title, teams, names) {
  stop(); wander = false; autoBtn.classList.remove('on');
  $('units').innerHTML = '';
  SND.unlock(); if (!$('sndBtn')) { const b = document.createElement('button'); b.type = 'button'; b.id = 'sndBtn'; b.className = 'sndbtn'; b.textContent = SND.muted ? '🔇' : '🔊'; b.addEventListener('click', e => { e.stopPropagation(); b.textContent = SND.toggle() ? '🔇' : '🔊'; }); stage.appendChild(b); }
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
  const ps = stats(), fs = petStats(petAct());
  B.stage = st;
  const foes = { persons: [], pets: [] };
  st.enemies.forEach(id => {
    const e = D.enemies[id];
    (e.pet ? foes.pets : foes.persons).push({ name: e.name, sprite: e.sprite, h: e.h * 1.12, flip: true, data: e, ai: e.boss ? 'boss' : 'attack',
      stats: { hp: e.hp, maxHp: e.hp, atk: e.atk, def: e.def, spd: e.spd } });
  });
  beginBattle('pve', st.name, {
    ally: { persons: [{ name: ROLE.name, sprite: AS.size['b_' + ROLE.cls] ? 'b_' + ROLE.cls : 'b_player', h: 126, me: true, magicAtk: !!curCls().magicAtk, stats: { hp: Math.max(1, S.hp), maxHp: ps.maxHp, mp: S.mp, maxMp: ps.maxMp, atk: ps.atk, matk: ps.matk, def: ps.def, mdef: ps.mdef, crit: ps.crit, dodge: ps.dodge, spd: ps.spd } }],
            pets: [{ name: (D.petDex[petAct()] || D.petDex.fox).name, sprite: (D.petDex[petAct()] || D.petDex.fox).sprite, h: (D.petDex[petAct()] || D.petDex.fox).h || 62, ai: (D.petDex[petAct()] || D.petDex.fox).ai || 'fox', kit: (D.petDex[petAct()] || {}).act, pas: (D.petDex[petAct()] || {}).pas, stats: { hp: fs.maxHp, maxHp: fs.maxHp, atk: fs.atk, matk: fs.matk, def: fs.def, mdef: fs.mdef, crit: fs.crit, dodge: fs.dodge, spd: fs.spd } }] },
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
      statusStart(u);
      await perform(u, u === B.player ? act : unitAct(u));
      statusEnd(u);
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
    $('cmdList').innerHTML = playerSkills().map(id => { const s = D.skills[id], lock = B.mode !== 'arena' && !skillUsable(id);
      return '<button type="button" class="tbtn" data-pick="skill:' + id + '"' + (lock || B.player.mp < s.mp ? ' disabled' : '') + '>' + s.name + '<small>' + (lock ? (curCls().skills ? '未学习' : s.lv + ' 级解锁') : '法力 ' + s.mp + (curCls().skills && B.mode !== 'arena' ? ' · Lv.' + skl(id) : '')) + '</small></button>'; }).join('');
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
// 主角的自动出手：有职业规则（data.js 的 classes[x].auto：[技能, 条件] 依次试）就按规则，否则走通用逻辑
function autoAct() {
  const p = B.player, free = B.mode === 'arena', cls = curCls();
  if (!p.alive) return { type: 'none' };
  const ok = id => D.skills[id] && (free || skillUsable(id)) && p.mp >= D.skills[id].mp;
  const foes = living('enemy'), mates = B.units.filter(x => x.side === 'ally');
  if (cls.auto) {
    if (p.hp < p.maxHp * 0.3 && !free && S.items.hp_potion) return { type: 'item', item: 'hp_potion' };
    const cond = {
      selfHurt: () => p.hp < p.maxHp * 0.5, allyHurt: () => mates.some(x => x.alive && x.hp < x.maxHp * 0.55),
      allyHurtMany: () => mates.filter(x => x.alive && x.hp < x.maxHp * 0.7).length >= 2, dead: () => mates.some(x => !x.alive),
      many: () => foes.length >= 3, marked: () => foes.some(f => f.st.mark), fast: () => foes.some(f => !f.st.slow) && B.round % 3 === 2, always: () => true };
    for (const [id, c] of cls.auto) {
      if (!ok(id) || !cond[c]()) continue;
      const sk = D.skills[id];
      if (sk.target === 'one') return { type: 'skill', skill: id, target: (c === 'marked' && foes.find(f => f.st.mark)) || weakestOf('enemy') };
      return { type: 'skill', skill: id };
    }
    return { type: 'skill', skill: 'attack', target: weakestOf('enemy') };
  }
  const has = id => (free || skillUsable(id)) && p.mp >= D.skills[id].mp;
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
    case 'ss': { const pick = (u.kit || []).find(a => !u.cd[a[0]] && Math.random() < a[1]); return { type: 'skill', skill: pick ? pick[0] : 'ss_hit', target: pickFoe() }; }
    case 'fox':     return { type: 'skill', skill: Math.random() < 0.35 ? 'foxfire' : 'bite', target: low };
    default:        return { type: 'skill', skill: u.row === 'pet' ? 'bite' : 'attack', target: pickFoe() };
  }
}

// ---------- 状态：魔化 mark / 减速 slow / 护体 shield / 持续回复 hot ----------
const FX_BADGE = { fear: ['惧', '#7a4bd1'], mark: ['魔', '#a64bff'], slow: ['缓', '#3d8bff'], shield: ['盾', '#e0a526'], hot: ['愈', '#33b36b'] };
function refreshFx(u) { u.el.querySelector('.stt').innerHTML = Object.keys(u.st).map(k => '<b style="background:' + FX_BADGE[k][1] + '">' + FX_BADGE[k][0] + u.st[k].turns + '</b>').join(''); }
function clearStatus(u, k) { if (!u.st[k]) return; if (k === 'fear' && u._df0) { u.def = u._df0; u._df0 = 0; } if (k === 'slow') { u.spd = u._spd0; u.dodge = u._dg0; } delete u.st[k]; refreshFx(u); }
function statusStart(u) { for (const k of Object.keys(u.cd || {})) { if (--u.cd[k] <= 0) delete u.cd[k]; } if (u.alive && u.st.hot) { const v = u.st.hot.v; u.hp = Math.min(u.maxHp, u.hp + v); floatText(u, '+' + v, 'heal'); drawUnit(u); } }
function statusEnd(u) { for (const k of Object.keys(u.st)) { if (--u.st[k].turns <= 0) clearStatus(u, k); } refreshFx(u); }

// ---------- 出手 ----------
async function perform(u, act) {
  if (!act || act.type === 'none') return;
  if (act.type === 'defend') { u.defending = true; bmsg(u.name + ' 摆出防御架势'); floatText(u, '防御', 'info'); await sleep(420); return; }
  if (act.type === 'item') {
    const it = D.items[act.item]; if (!S.items[act.item]) return;
    addItem(act.item, -1); u.hp = Math.min(u.maxHp, u.hp + (it.hp || 0)); u.mp = Math.min(u.maxMp, u.mp + (it.mp || 0));
    bmsg(u.name + ' 使用了' + it.name); floatText(u, '+' + (it.hp || it.mp), 'heal'); drawUnit(u); await sleep(480); return;
  }
  const s0 = D.skills[act.skill], km = u === B.player && B.mode !== 'arena' && curCls().skills ? skillMul(act.skill) : 1;
  const s = km === 1 ? s0 : Object.assign({}, s0, { power: s0.power && s0.power * km, heal: s0.heal && s0.heal * km, healPow: s0.healPow && s0.healPow * km });
  if (s.mp) { if (u.mp < s.mp) return perform(u, { type: 'skill', skill: 'attack', target: weakestOf(other(u.side)) }); u.mp -= s.mp; drawUnit(u); }
  bmsg(act.skill === 'attack' || act.skill === 'bite' ? '' : u.name + ' · ' + s.name);
  castFx(u, act.skill);
  if (s.heal || s.healPow || s.revive) {
    let relH; const releasedH = new Promise(r => { relH = r; });
    const poseH = SKILL_FX[act.skill] ? castPose(u, relH) : (relH(), null);
    await releasedH;
    const mates = B.units.filter(x => x.side === u.side), lowest = () => mates.filter(x => x.alive).sort((x, y) => x.hp / x.maxHp - y.hp / y.maxHp)[0] || u;
    if (s.revive) {
      const dead = mates.find(x => !x.alive);
      if (dead) { SND.heal(); dead.alive = true; dead.hp = Math.round(dead.maxHp * s.revive); dead.el.classList.remove('dead'); floatText(dead, '复活', 'heal'); skillHitFx(act.skill, dead); drawUnit(dead); await sleep(560); return; }
    }
    const ts = s.target === 'allies' ? mates.filter(x => x.alive) : s.target === 'self' ? [u] : [act.target && act.target.alive && act.target.side === u.side ? act.target : lowest()];
    for (const t of ts) {
      const v = s.healPow ? Math.round(u.matk * s.healPow * rnd(0.9, 1.1)) : Math.round(t.maxHp * (s.heal || 0.3));
      t.hp = Math.min(t.maxHp, t.hp + v); floatText(t, '+' + v, 'heal'); SND.heal();
      skillHitFx(act.skill, t);
      if (s.cleanse) { clearStatus(t, 'slow'); clearStatus(t, 'mark'); }
      if (s.shield) { t.st.shield = { turns: s.shield }; floatText(t, '护体', 'info'); }
      if (s.hot) t.st.hot = { turns: s.hot.turns, v: Math.round(v * s.hot.pct) };
      drawUnit(t); refreshFx(t);
    }
    if (poseH) await poseH; await sleep(260); return;
  }
  const foes = living(other(u.side));
  if (!foes.length) return;
  const primary = act.target && act.target.alive && act.target.side !== u.side ? act.target : foes[0];
  let targets = s.target === 'all' ? foes : [primary];
  if (s.hits > 1) targets = [primary].concat(foes.filter(x => x !== primary).sort(() => Math.random() - 0.5).slice(0, s.hits - 1));
  const magic = !!s.magic || (act.skill === 'attack' && u.magicAtk);
  if (s.cd) u.cd[act.skill] = s.cd + 1;
  if (s.selfCost) { const c = Math.round(u.maxHp * s.selfCost); u.hp = Math.max(1, u.hp - c); floatText(u, '-' + c, 'info'); drawUnit(u); }
  // 时间轴：预备（后撤）→ 冲刺 → 释放点（特效/伤害/受击/顿帧/震屏/音效同帧）→ 收招回位
  const pseq = petSeqFor(u, act.skill), music = !!SKILL_FX[act.skill] || !!pseq, t0 = targets[0], k = pseq ? 0 : music ? 0.22 : s.target === 'all' ? 0.4 : 0.7;
  const ux = t0.pos.x - u.pos.x, uy = t0.pos.y - u.pos.y, UL = Math.hypot(ux, uy) || 1;
  u.el.style.zIndex = 1500;
  if (!music) {                                   // 预备动作：向后蓄一下
    u.el.classList.add('kb'); u.el.style.transform = 'translate(' + (-ux / UL * 16) + 'px,' + (-uy / UL * 8) + 'px) scale(1.02,.96)'; await sleep(110);
    u.el.classList.remove('kb');
  }
  u.el.classList.add('mv'); u.el.style.transform = 'translate(' + (ux * k) + 'px,' + (uy * k) + 'px)';
  await sleep(pseq ? 60 : music ? 170 : 130); u.el.classList.remove('mv');
  let relA; const releasedA = new Promise(r => { relA = r; });
  const poseA = pseq ? petPlay(u, pseq, relA) : music ? castPose(u, relA) : (relA(), null);
  await releasedA;
  if (pseq) { const dly = petFx(u, act.skill, t0); if (dly) await sleep(dly * 0.85); }
  if (!music) { if (s.fire || magic) SND.release(); else SND.slash(); }
  let anyCrit = false, anyHit = false;
  for (const t of targets) {
    if (Math.random() * 100 < (t.dodge || 0)) { floatText(t, '闪避', 'miss'); SND.dodge(); dodgeStep(t, u); if (targets.length > 1) await sleep(60); continue; }
    const crit = Math.random() * 100 < (u.crit != null ? u.crit : 10);
    let dmg = (magic ? u.matk : u.atk) * s.power * rnd(0.9, 1.1) * (crit ? 1.5 : 1) - (magic ? t.mdef : t.def) * 0.6;
    if (s.vsMark && t.st.mark) dmg *= s.vsMark;
    if (t.st.shield) dmg *= 0.7;
    if (t.defending) dmg *= 0.5;
    const up = u.pas || {}, tp = t.pas || {};
    if (up.vsFear && t.st.fear) dmg *= up.vsFear;
    if (up.lowAtk && u.hp < u.maxHp * 0.3) dmg *= 1 + up.lowAtk;
    if (up.dbl && Math.random() < up.dbl) { dmg *= 2; floatText(u, '双倍', 'info'); }
    if (tp.take) dmg *= tp.take;
    dmg = Math.max(1, Math.round(dmg));
    t.hp = Math.max(0, t.hp - dmg); anyHit = true; anyCrit = anyCrit || crit;
    if (t.hp <= 0 && tp.revive && !t._rv) { t._rv = true; t.hp = Math.round(t.maxHp * tp.revive); floatText(t, '神龙祝福', 'heal'); SND.heal(); }
    if (!skillHitFx(act.skill, t)) slashFx(t, !!s.fire);
    impactAt(t, crit); SND.hit(crit);
    floatText(t, (crit ? '暴击 ' : '') + '-' + dmg, crit ? 'crit' : dmg >= t.maxHp * 0.25 ? 'big' : '');
    t.el.classList.remove('hit'); void t.el.offsetWidth; t.el.classList.add('hit'); petPlay(t, 'hit');
    knock(t, u, crit ? 30 : 15);
    drawUnit(t);
    if (t.hp <= 0 && tp.ownerAtk && B.player && B.player.alive && t.side === B.player.side) { B.player.atk = Math.round(B.player.atk * (1 + tp.ownerAtk)); B.player.matk = Math.round(B.player.matk * (1 + tp.ownerAtk)); floatText(B.player, '攻击 +' + Math.round(tp.ownerAtk * 100) + '%', 'info'); }
    if (t.hp <= 0) { t.alive = false; t.st = {}; setTimeout(() => { t.el.classList.add('dead'); SND.die(); }, 160); }
    else {
      if (s.fear) { const f = t.st.fear || { turns: 3, n: 0 }; f.n = Math.min(3, f.n + s.fear); f.turns = 3; if (!t._df0) t._df0 = t.def; t.def = Math.round(t._df0 * (1 - 0.12 * f.n)); t.st.fear = f; floatText(t, '恐惧', 'info'); }
      if (s.mark) { t.st.mark = { turns: s.mark }; floatText(t, '魔化', 'info'); }
      if (s.slow) { if (!t.st.slow) { t._spd0 = t.spd; t._dg0 = t.dodge; t.spd = Math.round(t.spd * 0.8); t.dodge = Math.max(0, t.dodge - 10); } t.st.slow = { turns: s.slow }; floatText(t, '减速', 'info'); }
    }
    refreshFx(t);
    if (targets.length > 1) await sleep(60);
  }
  if (anyHit) { shake(anyCrit ? 9 : targets.length > 1 ? 6 : 4, anyCrit ? 340 : 240); await hitstop(anyCrit ? 120 : 70); }
  if (poseA) await poseA;
  await sleep(120);
  u.el.classList.add('ret'); u.el.style.transform = ''; u.el.style.zIndex = Math.round(u.pos.y);
  await sleep(260); u.el.classList.remove('ret');
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
  if (res === 'win') SND.win(); else if (res === 'lose') SND.lose();
  if (res === 'win') {
    const r = { exp: 0, coin: 0, items: {} };
    st.enemies.forEach(id => { const e = D.enemies[id]; r.exp += e.exp; r.coin += e.coin; e.drops.forEach(d => { if (Math.random() < d[1]) r.items[d[0]] = (r.items[d[0]] || 0) + 1; }); });
    if (!S.cleared.includes(st.id)) S.cleared.push(st.id);
    const q = D.quests[S.step]; let questDone = false, killLine = '';
    if (q && (q.type === 'fight' || q.type === 'kill') && q.stage === st.id) {
      let done = true;
      if (q.type === 'kill') { S.cnt = (S.cnt || 0) + st.enemies.length; done = S.cnt >= q.n; if (!done) killLine = q.goal + ' ' + S.cnt + '/' + q.n; }
      if (done) { S.step++; S.cnt = 0; B.after = q; if (q.last) { questDone = true; const rw = q.reward || {}; r.exp += rw.exp || 0; r.coin += rw.coin || 0; for (const id in (rw.items || {})) r.items[id] = (r.items[id] || 0) + rw.items[id]; } }
    }
    const lv0 = S.lv; B.active = false; const ups = grant(r); B.active = true;
    title = '战斗胜利';
    h = '<div class="gain center">经验 <b>+' + r.exp + '</b>　银两 <b>+' + r.coin + '</b>';
    const drops = Object.keys(r.items);
    if (drops.length) h += '<br>获得 ' + drops.map(id => '<b>' + D.items[id].name + ' ×' + r.items[id] + '</b>').join('、');
    if (ups) h += '<br><b>升级！</b>' + lv0 + ' 级 → ' + S.lv + ' 级，气血法力回满';
    if (killLine) h += '<br><span style="color:var(--jade)">' + killLine + '</span>';
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
  B.active = false; stage.classList.remove('fight'); $('units').innerHTML = ''; B.units = []; { const sb = $('sndBtn'); if (sb) sb.remove(); }
  if (!arena) {
    // 打赢的敌人从地图上消失（重新进图会再出现）；打输回城
    if (B.over === 'win' && B.foe) { B.foe.el.remove(); M.list = M.list.filter(a => a !== B.foe); }
    if (B.over === 'lose') loadMap(M.def.home || M.id);
  }
  B.foe = null;
  last = performance.now(); save(); refresh();
  const bq = B.after; B.after = null;
  if (!arena && bq && bq.last) {                 // 这场战斗打完了一个任务：播完成对白，下一个任务不是“找人”就接着播它的接取对白
    let pages = (bq.outro || []).slice(); const nx = D.quests[S.step];
    if (nx && nx.first && nx.type !== 'talk' && nx.type !== 'spot') pages = pages.concat(nx.accept);
    if (pages.length) showPages(pages);
  }
  const q = D.quests[S.step]; if (!arena && B.over === 'win') toast(q ? '任务更新：' + q.goal : '第一章已完成，后续剧情敬请期待');
}

// ============================================================ 启动
window.CHAT.bind(() => ({
  name: ROLE.name, cls: ROLE.cls, lv: S.lv, weather: WXD[wxKind()].name, battle: B.active,
  bagItems: () => [...new Set(Object.keys(S.items).concat(Object.values(S.equip).filter(Boolean)))].filter(id => D.items[id]),
  showItem(id) {                                                        // 点聊天里的物品链接 → 物品详情
    const it = D.items[id]; if (!it) return;
    openModal(it.name, '<div class="who"><div class="tile" style="width:96px;flex:none">' + itemImg(id) + '</div><div><h5>' + it.name + '</h5><p>' + ({ equip: '装备', potion: '药品' }[it.type] || '物品') + (it.price ? ' · 售价 ' + it.price + ' 银两' : '') + '</p>' + (itemBonus(it) ? '<p style="color:#7ee08a">' + itemBonus(it) + '</p>' : '') + '</div></div><p style="color:var(--muted);margin:0">' + (it.desc || '') + '</p>');
  },
  showInfo(name, cls, lv) {                                              // 点名字 → 查看
    const c = D.classes[cls];
    openModal('玩家信息', '<div class="who">' + (c ? '<img alt="" src="' + IMG('cls_' + cls) + '">' : '') + '<div><h5>' + name + '</h5><p>' + (c ? clsName(c) + ' · ' + c.role : '路人') + '</p><p>等级 ' + (lv || '?') + '</p><p style="color:#7ee08a">在线</p></div></div>');
  }
}));
// 左侧任务栏的“队伍”页签：显示自己和聊天里拉进来的队友
function drawParty() {
  const ps = window.CHAT.party(), row = (n, c, l, me) => '<div class="q"><em style="color:' + (me ? 'var(--amber)' : 'var(--sky)') + '">' + (me ? '队长' : '队友') + '</em> ' + n + '<p>' + (c ? D.classes[c].name + ' · ' : '') + (l || '?') + ' 级</p></div>';
  $('qParty').innerHTML = row(ROLE.name, ROLE.cls, S.lv, true) + (ps.length ? ps.map(n => { const b = (D.chat.bots.find(x => x.name === n) || {}); return row(n, b.cls, b.lv, false); }).join('') : '<div class="q"><small>还没有队友。点聊天里的名字，选“邀请组队”。</small></div>');
}
let qTab = 0;
function showQuestTab(i) { qTab = i; $('quest').querySelectorAll('h3 span').forEach((s, j) => s.classList.toggle('on', j === i)); $('quest').querySelectorAll('.q').forEach(q => { if (!q.closest('#qParty')) q.hidden = i === 1; }); $('qParty').hidden = i === 0; if (i === 1) drawParty(); }
$('quest').querySelectorAll('h3 span').forEach((s, i) => s.addEventListener('click', e => { e.stopPropagation(); showQuestTab(i); }));
window.addEventListener('chat-party', () => { if (qTab === 1) drawParty(); });
$('btns').querySelectorAll('.dot').forEach(x => { x.hidden = !!(S.gift && S.ssGift); });
loadMap(D.maps[S.map] ? S.map : D.start.map, S.pos); refresh(); requestAnimationFrame(frame);
// 给自动化测试和调试用
// ---------- 和登录 / 选角界面的接口 ----------
// 进入游戏：换成这个角色自己的存档，名字、职业、头像跟着换
function enterGame(role, serverName) {
  ROLE = role; SAVE_KEY = 'guiqin-save:' + role.id;
  S = load() || newGame(); bagSel = null; ensureSkl();
  const c = D.classes[role.cls];
  setPlayerLook(role.cls);
  const sectTxt = c.joinSect && role.sect === 'dao' ? '道家' : clsName(c);
  player.name = role.name; player.title = '【' + sectTxt + '】'; drawTag(player);
  $('hudName').textContent = role.name; $('hudSect').textContent = sectTxt; $('hudFace').src = IMG(AS.img['head_' + role.cls] ? 'head_' + role.cls : 'head_player');
  $('btns').querySelectorAll('.dot').forEach(x => { x.hidden = !!(S.gift && S.ssGift); });
  window.CHAT.reset(serverName, role.name);
  applyPetLook();
  loadMap(D.maps[S.map] ? S.map : D.start.map, S.pos); refresh(); save(); last = performance.now();
}
function peekLevel(id) { try { const t = localStorage.getItem('guiqin-save:' + id); if (t) return JSON.parse(t).lv || D.start.lv; } catch (e) {} return D.start.lv; }
function wipe(id) { try { localStorage.removeItem('guiqin-save:' + id); } catch (e) {} }
window.GAME = { goTalk, get state() { return S; }, get map() { return M; }, battle: B, player, startBattle, startArena, loadMap, data: D, enter: enterGame, peekLevel, wipe };
})();
