// ============================================================
// 数据表：数值、物品、技能、敌人、关卡、任务、商店。
// 这里没有任何逻辑，移植到别的引擎时可以原样转成 JSON / 配置表。
// ============================================================
window.DATA = {
  // 主角属性 = base + grow * 等级；玄晶天狐属性 = 主角裸属性 * pet 系数
  base: { hp: 800, mp: 300, atk: 80, matk: 70, def: 30, mdef: 25, spd: 90, crit: 10, dodge: 3 },
  grow: { hp: 120, mp: 40, atk: 14, matk: 12, def: 6, mdef: 5, spd: 2, crit: 0, dodge: 0 },
  pet:  { hp: 0.6, atk: 0.7, def: 0.6, spd: 1.2 },
  // 宠物图鉴：玄晶天狐是出生自带的，其余为展示用（获得途径之后接玩法）
  petQuality: { 1: { name: '普通', color: '#c3c9d1', stars: 2 }, 2: { name: '精良', color: '#7ee08a', stars: 3 }, 3: { name: '稀有', color: '#6fb6ff', stars: 4 }, 4: { name: '传说', color: '#ffc857', stars: 5 } },
  bondTiers: [{ at: 0, name: '初识' }, { at: 15, name: '熟悉' }, { at: 40, name: '亲近' }, { at: 80, name: '默契' }, { at: 140, name: '心意相通' }],   // at = 一起打赢的战斗数；每升一档，宠物气血/攻击 +3%
  petDex: {
    fox:      { name: '玄晶天狐', sprite: 'b_ss_bs20', show: 'pd_ss_bs20', h: 112, q: 3, kind: '灵兽 · 近战', desc: '白毛九尾、颈缠青色灵带的天狐，足下燃着狐火，通人性，会在主人身边寸步不离。', skills: ['bite', 'foxfire'], own: true },
    deer:     { name: '小鹿', sprite: 'b_deer',    q: 2, kind: '灵兽 · 辅助', desc: '林间的白鹿，蹄声轻柔，据说能听见草木的心跳。', how: '医者职业任务奖励' },
    rabbit:   { name: '玉兔', sprite: 'b_rabbit',  q: 1, kind: '灵兽 · 敏捷', desc: '月宫来的玉兔，行动极快，总能抢在对手前面出手。', how: '竹林采集活动抽取' },
    hawk:     { name: '苍鹰', sprite: 'b_hawk',    q: 3, kind: '飞禽 · 迅捷', desc: '盘旋云端的猛禽，爪下从无活口。', how: '竞技场赛季奖励' },
    tiger:    { name: '虎崽', sprite: 'b_tiger',   q: 3, kind: '走兽 · 近战', desc: '山君之子，咆哮声虽稚嫩，咬合力却不小。', how: '玄兵塔十层奖励' },
    tortoise: { name: '灵龟', sprite: 'f_tortoise', q: 2, kind: '灵兽 · 防御', desc: '背负山河纹的龟，防御极高，不急不躁。', how: '寄卖行商店兑换' },
    crane:    { name: '仙鹤', sprite: 'f_crane',   q: 3, kind: '飞禽 · 迅捷', desc: '松间白鹤，舞姿清绝，鸣声可传十里。', how: '帮派任务奖励' },
    panther:  { name: '墨豹', sprite: 'f_panther', q: 3, kind: '走兽 · 近战', desc: '夜里才出没的黑豹，爪牙锋利。', how: '城外夜间副本掉落' },
    panda:    { name: '竹熊', sprite: 'f_panda',   q: 2, kind: '走兽 · 防御', desc: '竹林深处的憨态大熊，皮糙肉厚。', how: '城外竹林稀有掉落' },
    dragon:   { name: '赤螭', sprite: 'f_dragon',  q: 4, kind: '神兽 · 法术', desc: '传说中的赤色小龙，吐息如火，是万中无一的灵宠。', how: '节日活动限定' }
  },
  // 物品品质：白装 / 蓝装 / 紫装 / 橙装 / 红装（q = 1..5）
  quality: { 1: { name: '白装', short: '白', color: '#eef0f4' }, 2: { name: '蓝装', short: '蓝', color: '#4aa3ff' }, 3: { name: '紫装', short: '紫', color: '#b36bff' }, 4: { name: '橙装', short: '橙', color: '#ff9a2e' }, 5: { name: '红装', short: '红', color: '#ff4a4a' } },
  // 属性名（暴击、闪避是百分比）
  statName: { atk: '物攻', matk: '法攻', hp: '生命', def: '物防', mdef: '法防', mp: '魔法', dodge: '闪避', spd: '速度', crit: '暴击' },
  // 宝石：九种，每种 1~5 阶，同种 3 合 1 升阶。vals 是各阶镶嵌后加的数值（闪避、暴击为百分点）
  gems: {
    atk:   { stone: '赤焰石', icon: 'gem_atk',   vals: [12, 30, 60, 110, 190] },
    matk:  { stone: '紫霞石', icon: 'gem_matk',  vals: [12, 30, 60, 110, 190] },
    hp:    { stone: '青木石', icon: 'gem_hp',    vals: [100, 250, 500, 900, 1500] },
    def:   { stone: '玄琥石', icon: 'gem_def',   vals: [8, 20, 40, 75, 130] },
    mdef:  { stone: '碧水石', icon: 'gem_mdef',  vals: [8, 20, 40, 75, 130] },
    mp:    { stone: '苍澜石', icon: 'gem_mp',    vals: [60, 150, 300, 540, 900] },
    dodge: { stone: '流云石', icon: 'gem_dodge', vals: [1, 2, 3.5, 5, 7] },
    spd:   { stone: '金风石', icon: 'gem_spd',   vals: [4, 10, 20, 36, 60] },
    crit:  { stone: '绯樱石', icon: 'gem_crit',  vals: [1, 2, 3.5, 5, 7] }
  },
  // 锻造：每个部位独立升星（星级跟着部位走，换装不丢），最高 15 星
  forge: {
    max: 15, perStar: 0.06,                                          // 每星 +6% 装备自带属性，满星 +90%
    coin: [50, 80, 120, 180, 260, 380, 540, 760, 1050, 1450, 2000, 2700, 3600, 4800, 6400],   // 升到第 n+1 星的银两
    rate: [1, 1, 0.95, 0.9, 0.85, 0.75, 0.65, 0.55, 0.5, 0.45, 0.4, 0.35, 0.3, 0.25, 0.2],   // 成功率
    gemBonus: [0.10, 0.20, 0.35, 0.50, 1],                            // 锻造宝石加的成功率；第 5 种（圣锻皇宝石）必成
    dropFrom: 10,                                                     // 已有 ≥10 星时失败会掉 1 星
    qMul: { 1: 1, 2: 1.2, 3: 1.5, 4: 2, 5: 2.6 },                    // 品质越高越贵
    sockets: [0, 5, 10, 15]                                           // 第 1/2/3/4 个孔需要的星级
  },
  bagSize: 20,
  // 装备部位（界面左右各 6 个，从上到下）
  slots: { weapon: '武器', treasure: '法宝', neck: '颈部', wrist: '手腕', finger: '手指', pouch: '药囊', amulet: '护符', head: '头部', chest: '胸部', waist: '腰部', legs: '腿部', feet: '脚部' },
  slotLeft: ['head', 'neck', 'chest', 'waist', 'legs', 'feet'], slotRight: ['weapon', 'treasure', 'wrist', 'finger', 'pouch', 'amulet'],
  expNeed: { base: 150, perLevel: 90 },        // 升到下一级所需经验 = base + perLevel * 当前等级
  start: { lv: 1, coin: 100, items: { hp_potion: 3, forge_1: 5, forge_2: 2 }, equip: { chest: 'cloth_armor' }, map: 'village' },

  // 可选职业：男女各三种。stats 是创建界面上的五维条（1-5），只用于展示
  classes: {
    // ---- 新手：创角页只有这两个（男女各一）。女角色在剧情里拜入道家后，才变成天音 / 琴魔的形象（game.js joinSect）----
    novice_f: { name: '新手', gender: 'f', role: '近战 · 棍', weapon: '木棍', joinSect: ['dao_ty', 'dao_qm'],
      desc: '青带束起马尾的少女，提着一根枯木棍闯荡江湖，什么门派都还没入。到了清溪边，遇到各家接引师，再决定要走哪条路。', stats: { 攻击: 3, 防御: 3, 速度: 3, 法术: 3, 气血: 3 } },
    novice_m: { name: '新手', gender: 'm', role: '近战 · 棍', weapon: '木棍', joinSect: ['dao_ty_m', 'dao_qm_m'],
      desc: '红绳束髻的少年，提着一根枯木棍闯荡江湖，样样都会一点。路还很长，先把清溪的事了了。', stats: { 攻击: 3, 防御: 3, 速度: 3, 法术: 3, 气血: 3 } },
    // ---- 道家（女）两个分堂：技能参考 kunkunxunqin（data_skill_defs 的 qm / ty），数值按本游戏的回合制重新折算 ----
    dao_ty: { hidden: true, name: '天音', sect: '道家', gender: 'f', role: '辅助 · 琵琶', weapon: '琵琶', magicAtk: true,
      desc: '道家天音分堂，怀抱琵琶，以曲音疗伤、护体、返魂。队伍里最可靠的后盾，自己也能用荡魔曲出手。', stats: { 攻击: 1, 防御: 3, 速度: 3, 法术: 4, 气血: 4 },
      mod: { atk: 0.7, matk: 1.2, def: 1.05, mdef: 1.2, hp: 1.1, mp: 1.15 },
      heart: { name: '天音心法', desc: '每级提升法攻 3 点、法防 6 点（原作：智力 +12、精神 +36）', perLv: { matk: 3, mdef: 6 } },
      skills: ['dangmo_ty', 'lingmai_ty', 'yangsheng', 'tianlai', 'fanhun'],
      auto: [['fanhun', 'dead'], ['lingmai_ty', 'selfHurt'], ['yangsheng', 'allyHurt'], ['tianlai', 'allyHurtMany'], ['dangmo_ty', 'always']] },
    dao_qm: { hidden: true, name: '琴魔', sect: '道家', gender: 'f', role: '法攻 · 古琴', weapon: '古琴', magicAtk: true,
      desc: '道家琴魔分堂，抚古琴，以魔音入敌心：魔化、减速、夺魂。输出和控制兼顾，血线要靠自己看着。', stats: { 攻击: 1, 防御: 2, 速度: 3, 法术: 5, 气血: 3 },
      mod: { atk: 0.7, matk: 1.4, def: 0.95, mdef: 1.1, hp: 1.0, mp: 1.2 },
      heart: { name: '琴魔心法', desc: '每级提升法攻 6 点、法防 2 点（原作：智力 +36、精神 +12）', perLv: { matk: 6, mdef: 2 } },
      skills: ['dangmo_qm', 'lingmai_qm', 'mohun', 'duohun', 'fuling'],
      auto: [['lingmai_qm', 'selfHurt'], ['mohun', 'many'], ['duohun', 'marked'], ['fuling', 'fast'], ['dangmo_qm', 'always']] },
    m_sword: { hidden: true, name: '游侠', gender: 'm', role: '近战 · 剑',   desc: '仗剑行走江湖的侠客，攻守均衡，出手干净利落。', stats: { 攻击: 4, 防御: 3, 速度: 4, 法术: 2, 气血: 3 } },
    m_spear: { hidden: true, name: '锐士', gender: 'm', role: '近战 · 戟',   desc: '大秦锐士，披坚执锐。气血和防御最高，适合顶在最前面。', stats: { 攻击: 3, 防御: 5, 速度: 2, 法术: 1, 气血: 5 } },
    m_mage:  { hidden: true, name: '方士', gender: 'm', role: '远程 · 法术', desc: '通晓阴阳五行的方士，以符箓和铜镜施法，擅长群体攻击。', stats: { 攻击: 2, 防御: 2, 速度: 3, 法术: 5, 气血: 2 } },
    f_sword: { hidden: true, name: '剑姬', gender: 'f', role: '近战 · 剑',   desc: '剑舞一脉的传人，身法最快，连击和暴击是她的拿手好戏。', stats: { 攻击: 4, 防御: 2, 速度: 5, 法术: 2, 气血: 3 } },
    f_heal:  { hidden: true, name: '医者', gender: 'f', role: '辅助 · 治疗', desc: '悬壶济世的游方医者，能为队友疗伤解毒，队伍里少不了她。', stats: { 攻击: 1, 防御: 3, 速度: 3, 法术: 4, 气血: 4 } },
    f_bow:   { hidden: true, name: '羽林', gender: 'f', role: '远程 · 弓',   desc: '百步穿杨的神射手，专挑残血的敌人下手，单体伤害最高。', stats: { 攻击: 5, 防御: 2, 速度: 4, 法术: 1, 气血: 2 } }
  },

  items: {
    hp_potion:     { name: '金创药',   icon: 'hp_potion', q: 1,     type: 'potion', price: 50,  hp: 600, desc: '恢复 600 点气血。' },
    mp_potion:     { name: '回气丹',   icon: 'mp_potion', q: 1,     type: 'potion', price: 60,  mp: 300, desc: '恢复 300 点法力。' },
    iron_sword:    { name: '精铁剑',   icon: 'iron_sword', q: 2,    type: 'equip',  slot: 'weapon', price: 300, atk: 40, desc: '王铁匠亲手淬火的好剑。物攻 +40' },
    steel_sword:   { name: '玄铁剑',   icon: 'steel_sword', q: 3,   type: 'equip',  slot: 'weapon', price: 900, atk: 95, desc: '玄铁所铸，寒光逼人。物攻 +95' },
    cloth_armor:   { name: '布衣',     icon: 'cloth_armor', q: 1,   type: 'equip',  slot: 'chest',  price: 80,  def: 15, desc: '寻常布衣。物防 +15' },
    leather_armor: { name: '皮甲',     icon: 'leather_armor', q: 2, type: 'equip',  slot: 'chest',  price: 500, def: 50, hp: 200, desc: '鞣制皮甲。物防 +50，生命 +200' },
    sachet:        { name: '百花香囊', icon: 'sachet', q: 3,        type: 'equip',  slot: 'pouch',  price: 400, hp: 300, spd: 10, desc: '逍遥子所赠。生命 +300，速度 +10' },
    // —— 新增装备：十二个部位 ——
    wood_beads:    { name: '木珠项链',   icon: 'wood_beads',    q: 1, type: 'equip', slot: 'neck',     price: 120,  hp: 120, atk: 6,   desc: '山里人常戴的木珠串，据说能安神。生命 +120，物攻 +6' },
    jade_pendant:  { name: '玉佩项链',   icon: 'jade_pendant',  q: 3, type: 'equip', slot: 'neck',     price: 700,  hp: 320, atk: 14,  desc: '温润的青玉雕成，贴身佩戴养气。生命 +320，物攻 +14' },
    copper_ring:   { name: '铜指环',     icon: 'copper_ring',   q: 1, type: 'equip', slot: 'finger',   price: 100,  atk: 10,          desc: '寻常铜戒，握剑时更稳。物攻 +10' },
    silver_ring:   { name: '银镶蓝宝戒', icon: 'silver_ring',   q: 2, type: 'equip', slot: 'finger',   price: 450,  atk: 22,          desc: '西域来的蓝宝石戒指。物攻 +22' },
    leather_bracer:{ name: '皮护腕',     icon: 'leather_bracer',q: 1, type: 'equip', slot: 'wrist',    price: 110,  def: 8, atk: 4,   desc: '鞣制皮革缝成。物防 +8，物攻 +4' },
    steel_bracer:  { name: '精钢护腕',   icon: 'steel_bracer',  q: 2, type: 'equip', slot: 'wrist',    price: 500,  def: 20, atk: 10,  desc: '精钢打造，镶金边。物防 +20，物攻 +10' },
    leather_cap:   { name: '皮帽',       icon: 'leather_cap',   q: 1, type: 'equip', slot: 'head',     price: 100,  def: 10, hp: 60,  desc: '猎户常戴的皮帽。物防 +10，生命 +60' },
    iron_helm:     { name: '铁盔',       icon: 'iron_helm',     q: 2, type: 'equip', slot: 'head',     price: 500,  def: 26, hp: 150, desc: '红缨铁盔，军中旧物。物防 +26，生命 +150' },
    linen_belt:    { name: '麻布腰带',   icon: 'linen_belt',    q: 1, type: 'equip', slot: 'waist',    price: 90,   hp: 100, def: 5,  desc: '结实的麻布腰带。生命 +100，物防 +5' },
    jade_belt:     { name: '白玉带',     icon: 'jade_belt',     q: 3, type: 'equip', slot: 'waist',    price: 650,  hp: 260, def: 14, desc: '金扣玉带，富贵人家才有。生命 +260，物防 +14' },
    linen_legs:    { name: '麻布护腿',   icon: 'linen_legs',    q: 1, type: 'equip', slot: 'legs',     price: 100,  def: 12, hp: 50,  desc: '赶路时绑在腿上的护腿。物防 +12，生命 +50' },
    iron_greaves:  { name: '铁护腿',     icon: 'iron_greaves',  q: 2, type: 'equip', slot: 'legs',     price: 520,  def: 30, hp: 120, desc: '铁片缀成的护腿。物防 +30，生命 +120' },
    cloth_boots:   { name: '布靴',       icon: 'cloth_boots',   q: 1, type: 'equip', slot: 'feet',     price: 90,   spd: 6, def: 5,   desc: '草底布靴，走山路不累。速度 +6，物防 +5' },
    cloud_boots:   { name: '云纹靴',     icon: 'cloud_boots',   q: 3, type: 'equip', slot: 'feet',     price: 650,  spd: 16, def: 12, desc: '绣着云纹的轻靴，步履生风。速度 +16，物防 +12' },
    bronze_bell:   { name: '青铜铃',     icon: 'bronze_bell',   q: 2, type: 'equip', slot: 'treasure', price: 350,  matk: 18, spd: 4, desc: '古旧的青铜法铃，摇动时嗡嗡作响。法攻 +18，速度 +4' },
    bagua_mirror:  { name: '八卦镜',     icon: 'bagua_mirror',  q: 3, type: 'equip', slot: 'treasure', price: 1100, matk: 42, spd: 8, desc: '方士炼制的法宝，照出邪祟。法攻 +42，速度 +8' },
    peace_charm:   { name: '平安符',     icon: 'peace_charm',   q: 1, type: 'equip', slot: 'amulet',   price: 120,  mdef: 10, hp: 100, desc: '庙里求来的黄纸符。法防 +10，生命 +100' },
    jade_charm:    { name: '玉符',       icon: 'jade_charm',    q: 3, type: 'equip', slot: 'amulet',   price: 700,  mdef: 24, hp: 260, desc: '刻着符文的青玉，隐隐发光。法防 +24，生命 +260' },
    herb_pouch:    { name: '草药袋',     icon: 'herb_pouch',    q: 1, type: 'equip', slot: 'pouch',    price: 120,  hp: 150,         desc: '装着晒干草药的小袋。生命 +150' },
    brocade_pouch: { name: '锦绣药囊',   icon: 'brocade_pouch', q: 3, type: 'equip', slot: 'pouch',    price: 900,  hp: 450, spd: 14, desc: '金线绣的药囊，香气清冽。生命 +450，速度 +14' },
    iron_armor:    { name: '铁甲',       icon: 'iron_armor',    q: 3, type: 'equip', slot: 'chest',    price: 1100, def: 85, hp: 320, desc: '铆钉铁甲，沉甸甸的。物防 +85，生命 +320' },
    scale_armor:   { name: '玄鳞甲',     icon: 'scale_armor',   q: 4, type: 'equip', slot: 'chest',    price: 2000, def: 120, hp: 480, desc: '传说取玄蛇鳞片所制。物防 +120，生命 +480' },
    steel_saber:   { name: '百炼钢刀',   icon: 'steel_saber',   q: 2, type: 'equip', slot: 'weapon',   price: 650,  atk: 70,          desc: '百炼成钢的弯刀，红绸缠柄。物攻 +70' },
    jade_sword:    { name: '碧玉剑',     icon: 'jade_sword',    q: 4, type: 'equip', slot: 'weapon',   price: 2200, atk: 150,         desc: '剑身如碧玉，夜里泛着绿光。物攻 +150' },
    crimson_sword: { name: '赤霄剑',     icon: 'crimson_sword', q: 5, type: 'equip', slot: 'weapon',   price: 6000, atk: 260,         desc: '传说中斩白蛇的赤色神剑，剑出如火。物攻 +260' },
    phoenix_armor: { name: '朱雀战甲',   icon: 'phoenix_armor', q: 5, type: 'equip', slot: 'chest',    price: 6000, def: 190, hp: 800, desc: '朱雀羽炼成的战甲，烈焰不侵。物防 +190，生命 +800' },
    // —— 锻造宝石：锻造时必须消耗一颗，品质越高成功率加得越多，圣锻皇宝石必定成功 ——
    forge_1: { name: '初级锻造宝石', icon: 'forge_1', q: 1, type: 'mat', forge: 1, price: 200,   desc: '锻造时消耗一颗，成功率 +10%。' },
    forge_2: { name: '锻造宝石',     icon: 'forge_2', q: 2, type: 'mat', forge: 2, price: 800,   desc: '锻造时消耗一颗，成功率 +20%。' },
    forge_3: { name: '精锻宝石',     icon: 'forge_3', q: 3, type: 'mat', forge: 3, price: 3000,  desc: '锻造时消耗一颗，成功率 +35%。' },
    forge_4: { name: '锻皇宝石',     icon: 'forge_4', q: 4, type: 'mat', forge: 4, price: 10000, desc: '锻造时消耗一颗，成功率 +50%。' },
    forge_5: { name: '圣锻皇宝石',   icon: 'forge_5', q: 5, type: 'mat', forge: 5, price: 40000, desc: '锻造时消耗一颗，必定锻造成功。' },
    ore:           { name: '玄铁矿',   icon: 'ore', q: 2,           type: 'mat',    price: 120, desc: '打造兵器的材料，可以卖给商人。' },
    wolf_fang:     { name: '狼牙',     icon: 'wolf_fang', q: 1,     type: 'mat',    price: 40,  desc: '野狼的牙，可以卖给商人。' },
    lantern:       { name: '花灯',     icon: 'lantern', q: 2,       type: 'mat',    price: 0,   desc: '王铁匠托你捎给逍遥子的花灯。' }
  },

  // power：伤害倍率；target：one 单体 / all 全体 / self 自己；heal：按气血上限比例回复
  skills: {
    attack: { name: '攻击',     mp: 0,   power: 1.0, target: 'one',  desc: '普通一击。' },
    // ---- 道家·琴魔 ----
    dangmo_qm: { name: '荡魔曲', mp: 40,  power: 1.35, target: 'one', magic: true, lv: 1,  desc: '拨动古琴，以魔音重创一名敌人。' },
    dangmo_fs: { name: '荡魔曲', mp: 40,  power: 1.0, target: 'one', magic: true, lv: 1, desc: '拨动箜篌，以曲音震伤一名敌人。' },
    lingmai_fs:{ name: '灵脉曲', mp: 50,  heal: 0.1, target: 'ally', cleanse: true, lv: 1, desc: '解除一名友方的减速、魔化等状态（原作只解除状态，演示里附带少量回血）。' },
    lingmai_qm:{ name: '灵脉曲', mp: 50,  heal: 0.22, target: 'ally', cleanse: true, lv: 3, desc: '疏通灵脉：为气血最低的友方回复 22% 气血，并解除减速、魔化等状态。' },
    mohun:     { name: '魔魂曲', mp: 90,  power: 0.8, target: 'one', hits: 3, mark: 2, magic: true, lv: 5, desc: '魔音扩散，攻击最多 3 个敌人，并使其魔化 2 回合。' },
    duohun:    { name: '夺魂曲', mp: 70,  power: 1.1, vsMark: 1.7, target: 'one', magic: true, lv: 8, desc: '单体伤害，对魔化状态的敌人伤害 ×1.7。' },
    fuling:    { name: '缚灵曲', mp: 80,  power: 0.95, target: 'one', slow: 2, magic: true, lv: 10, desc: '单体伤害，并使目标出手速度和闪避下降 2 回合。' },
    // ---- 墨家（猛士 / 遁甲，技能参考原作 data_skill_defs 的 ms / dj / zs，数值按本作回合制折算）----
    menghu_zs: { name: '猛虎纵', mp: 30, power: 1.15, target: 'one', melee: true, lv: 1, desc: '纵身扑击，对单体造成物理伤害。' },
    potian_zs: { name: '破天吼', mp: 45, power: 0.9, target: 'one', melee: true, vuln: 3, cd: 1, lv: 1, desc: '一声长啸，使目标 3 回合内受到的伤害 +30%。' },
    menghu_ms: { name: '猛虎纵', mp: 35, power: 1.3, target: 'one', melee: true, bleed: { turns: 3, pct: 0.05 }, lv: 1, desc: '纵身重击，并使目标流血 3 回合，每回合损失 5% 气血。' },
    potian_ms: { name: '破天吼', mp: 50, power: 1.0, target: 'one', melee: true, vuln: 4, cd: 1, lv: 3, desc: '重创单体，并使其 4 回合内受到的伤害 +30%。' },
    lianzhan:  { name: '连斩',   mp: 70, power: 1.95, target: 'one', melee: true, selfCost: 0.05, selfVuln: 1, cd: 1, lv: 5, desc: '三连斩合计 195% 伤害，自身损失 5% 气血，且下回合受伤 +30%。' },
    xueji:     { name: '血祭',   mp: 70, power: 1.45, vsBleed: 1.7, target: 'one', melee: true, selfCost: 0.08, lv: 8, desc: '燃血一击，损失 8% 气血；对流血的敌人伤害 ×1.7。' },
    kuanglong: { name: '狂龙吼', mp: 90, target: 'self', melee: true, rage: 2, cd: 1, lv: 10, desc: '舍弃 80% 防御，换取 2 回合伤害 +60%。' },
    menghu_dj: { name: '猛虎纵', mp: 30, power: 1.15, target: 'one', melee: true, lv: 1, desc: '纵身扑击，对单体造成物理伤害。' },
    potian_dj: { name: '破天吼', mp: 70, power: 0.65, target: 'all', melee: true, vuln: 4, cd: 1, lv: 3, desc: '吼声震慑全体敌人，伤害并使其 4 回合内受伤 +30%。' },
    pojia:     { name: '破甲',   mp: 55, power: 1.2, defDown: 0.3, vuln: 3, target: 'one', melee: true, lv: 5, desc: '破甲一击，降低目标 30% 防御，并使其 3 回合内受伤 +30%。' },
    zhanhun:   { name: '战魂歌', mp: 100, target: 'allies', melee: true, teamBuff: { turns: 3, dmg: 0.2, shield: true }, cd: 3, lv: 8, desc: '鼓舞全队：3 回合内伤害 +20%，所受伤害 -30%。' },
    cuogu:     { name: '挫骨',   mp: 75, power: 1.3, lifesteal: 0.3, selfCost: 0.04, target: 'one', melee: true, cd: 1, lv: 10, desc: '挫骨重击，损失 4% 气血；按伤害的 30% 吸取气血。' },
    // ---- 道家·天音 ----
    dangmo_ty: { name: '荡魔曲', mp: 40,  power: 1.2, target: 'one', magic: true, lv: 1,  desc: '拨动琵琶，以曲音震伤一名敌人。' },
    lingmai_ty:{ name: '灵脉曲', mp: 50,  heal: 0.2, target: 'ally', cleanse: true, shield: 2, lv: 3, desc: '为气血最低的友方回复 20% 气血，解除减速、魔化，并降低 30% 所受伤害（2 回合）。' },
    yangsheng: { name: '养生曲', mp: 60,  healPow: 2.2, target: 'ally', lv: 5, desc: '以法攻为基础，为气血最低的友方恢复大量气血。' },
    tianlai:   { name: '天籁曲', mp: 120, healPow: 1.2, target: 'allies', hot: { pct: 0.35, turns: 4 }, lv: 8, desc: '恢复友方全体气血，随后 4 回合每回合再恢复 35%。' },
    fanhun:    { name: '返魂曲', mp: 150, revive: 0.4, target: 'ally', lv: 10, desc: '复活一名倒下的友方并恢复 40% 气血；无人倒下时为最伤的友方回复气血。' },
    pokong: { name: '天剑·破空', mp: 60,  power: 1.9, target: 'one',  desc: '凝气于剑，重创一名敌人。', lv: 1 },
    zongheng:{ name: '剑气纵横', mp: 110, power: 1.15, target: 'all', magic: true, desc: '剑气四散，攻击全体敌人。', lv: 6 },
    ningshen:{ name: '凝神诀',   mp: 80,  heal: 0.35, target: 'self', desc: '调息凝神，回复 35% 气血。', lv: 7 },
    bite:   { name: '撕咬',     mp: 0,   power: 1.0, target: 'one' },
    foxfire:{ name: '狐火',     mp: 0,   power: 1.6, target: 'one', fire: true, magic: true },
    // 队友和对手用的招式（由 ai 类型自动选用）
    heal_ally:{ name: '回春术', mp: 0, heal: 0.22, target: 'ally' },
    bolt:   { name: '飞花',     mp: 0,   power: 1.2, target: 'one', magic: true },
    rain:   { name: '落英缤纷', mp: 0,   power: 0.7, target: 'all', magic: true },
    snipe:  { name: '穿云一击', mp: 0,   power: 1.3, target: 'one' },
    smash:  { name: '伏魔击',   mp: 0,   power: 0.95, target: 'one' },
    sweep:  { name: '横扫千军', mp: 0,   power: 1.3, target: 'all' }
  },
  playerSkills: ['pokong', 'zongheng', 'ningshen'],

  enemies: {
    dummy: { name: '木桩',     sprite: 'dummy', h: 110, hp: 420,  atk: 28,  def: 8,  spd: 10,  exp: 200, coin: 0,   drops: [] },
    boar:  { name: '野猪',     sprite: 'boar',  pet: true, h: 78,  hp: 360,  atk: 82,  def: 14, spd: 82,  exp: 180, coin: 15,  drops: [['hp_potion', 0.4]] },
    grunt: { name: '山贼喽啰', sprite: 'grunt', h: 108, hp: 780,  atk: 155, def: 32, spd: 70,  exp: 120, coin: 45,  drops: [['hp_potion', 0.35], ['ore', 0.15]] },
    wolf:  { name: '野狼',     sprite: 'wolf',  pet: true, h: 72,  hp: 580,  atk: 180, def: 15, spd: 118, exp: 110, coin: 20,  drops: [['wolf_fang', 0.6]] },
    boss:  { name: '山贼头目', sprite: 'boss',  h: 150, hp: 4000, atk: 255, def: 70, spd: 85,  exp: 900, coin: 600, drops: [['ore', 1], ['mp_potion', 1]], boss: true },
    // ---- 主线剧情专用 ----
    zhw:     { name: '展护卫',   sprite: 'kk_npc_zhw', h: 118, hp: 520,  atk: 62,  def: 14, spd: 78,  exp: 60,  coin: 0,  drops: [], boss: true },
    xinmo:   { name: '心魔',     sprite: 'f_panther',  h: 92,  hp: 760,  atk: 78,  def: 18, spd: 90,  exp: 90,  coin: 0,  drops: [['hp_potion', 1]], boss: true },
    mystery: { name: '神秘强者', sprite: 'f_tao',      h: 132, hp: 1200, atk: 118, def: 28, spd: 86,  exp: 120, coin: 30, drops: [['hp_potion', 1]], boss: true },
    worm:    { name: '食尸虫',   sprite: 'f_tortoise', h: 62,  hp: 380,  atk: 84,  def: 16, spd: 70,  exp: 25,  coin: 10, drops: [['hp_potion', 0.2]], pet: true },
    xieyue:  { name: '邪月',     sprite: 'boss',       h: 150, hp: 1900, atk: 150, def: 38, spd: 84,  exp: 160, coin: 50, drops: [['hp_potion', 1], ['mp_potion', 1]], boss: true },
    tiger:   { name: '利爪幼虎', sprite: 'f_panther',  h: 70,  hp: 520,  atk: 126, def: 22, spd: 108, exp: 30,  coin: 12, drops: [['wolf_fang', 0.3]], pet: true },
    hanxin:  { name: '韩信',     sprite: 'f_spear',    h: 136, hp: 2300, atk: 176, def: 46, spd: 98,  exp: 180, coin: 60, drops: [['hp_potion', 1]], boss: true },
    snake:   { name: '双头毒蛇', sprite: 'f_dragon',   h: 80,  hp: 640,  atk: 150, def: 26, spd: 100, exp: 35,  coin: 14, drops: [['hp_potion', 0.25]], pet: true },
    shadow:  { name: '暗影魔将', sprite: 'grunt',      h: 128, hp: 3100, atk: 204, def: 54, spd: 96,  exp: 200, coin: 70, drops: [['ore', 1]], boss: true },
    mengyuan:{ name: '梦渊尸魔', sprite: 'boss',       h: 156, hp: 3600, atk: 226, def: 60, spd: 88,  exp: 220, coin: 80, drops: [['ore', 1], ['mp_potion', 1]], boss: true },
    tianxing:{ name: '天星子的幻影', sprite: 'f_tao',  h: 136, hp: 4000, atk: 240, def: 64, spd: 104, exp: 250, coin: 90, drops: [['ore', 2], ['mp_potion', 1]], boss: true }
  },

  stages: [
    { id: 'dummy',   name: '山脚木桩', rec: 1, enemies: ['dummy'],               desc: '一动不动的稻草人，拿来练手正好。' },
    { id: 'boar',    name: '山道野猪', rec: 2, enemies: ['boar', 'boar'],          desc: '两头在山道边拱土的野猪。', need: 'dummy' },
    { id: 'hog',     name: '荒径野猪', rec: 3, enemies: ['boar', 'boar', 'boar'],    desc: '三头在村郊荒径上横冲直撞的野猪。', need: 'boar' },
    { id: 'roam',    name: '林间游狼', rec: 4, enemies: ['wolf', 'wolf'],            desc: '两头在林子边游荡的饿狼。', need: 'hog' },
    { id: 'creekwolf', name: '溪畔狼群', rec: 5, enemies: ['wolf', 'wolf', 'wolf'],  desc: '循着水声下山来饮水的三头饿狼。', need: 'roam' },
    { id: 'creekgang', name: '溪边劫匪', rec: 6, enemies: ['grunt', 'grunt', 'wolf'], desc: '躲在竹林里打劫行人的山贼。', need: 'creekwolf' },
    { id: 'mistwolf', name: '谷底狼群', rec: 7, enemies: ['wolf', 'wolf', 'wolf'],  desc: '栖在云雾谷里的狼群，毛色被雾气染得发白。', need: 'creekgang' },
    { id: 'mistgang', name: '雾谷悍匪', rec: 8, enemies: ['grunt', 'wolf', 'grunt'], desc: '躲进云雾谷落脚的山贼，熟悉每一块石头。', need: 'mistwolf' },
    { id: 'pinegang', name: '松林伏兵', rec: 9,  enemies: ['grunt', 'grunt', 'grunt'], desc: '埋伏在石柱后面的三个山贼。', need: 'boss' },
    { id: 'pinewolf', name: '石间狼群', rec: 10, enemies: ['wolf', 'wolf', 'wolf'],   desc: '在石松林里巡游的狼群，爪子磨得发亮。', need: 'pinegang' },
    { id: 'denwolf',  name: '狼窝外围', rec: 11, enemies: ['wolf', 'wolf', 'wolf'],   desc: '守在洞口的狼，嘴边还带着血。', need: 'pinewolf' },
    { id: 'wolfboss', name: '驯狼匪首', rec: 12, enemies: ['wolf', 'boss', 'wolf'],   desc: '把狼群当成打手的山贼头子，每三回合会使出横扫千军。', need: 'denwolf' },
    // ---- 主线剧情关卡（story:true：只在对应任务进行到那一步时出现在地图上）----
    { id: 's_zhw',     story: true, name: '展护卫',   rec: 1, enemies: ['zhw'],                      desc: '展护卫要看看你的身手。点到为止，别伤了自己人。' },
    { id: 's_xinmo',   story: true, name: '心魔',     rec: 2, enemies: ['xinmo'],                    desc: '药气翻涌，梦里追你的声音显出了形。' },
    { id: 's_mystery', story: true, name: '神秘强者', rec: 3, enemies: ['mystery'],                  desc: '赵村的梦境里，有人带着暗红的篆纹拦住了你。' },
    { id: 's_worm',    story: true, name: '坟边虫群', rec: 4, enemies: ['worm', 'worm', 'worm'],      desc: '守在赵村坟地边的食尸虫，是被人引来的。' },
    { id: 's_xieyue',  story: true, name: '邪月',     rec: 5, enemies: ['xieyue'],                   desc: '邪月逼村民滴血续誓，你得先挡住它。' },
    { id: 's_tiger',   story: true, name: '村口幼虎', rec: 6, enemies: ['tiger', 'tiger', 'tiger'],   desc: '野地里聚到村口的利爪幼虎。' },
    { id: 's_hanxin',  story: true, name: '韩信',     rec: 7, enemies: ['hanxin'],                   desc: '韩信认出你身上的暗印余息，误以为你是同伙。' },
    { id: 's_snake',   story: true, name: '双头毒蛇', rec: 8, enemies: ['snake', 'snake', 'snake'],   desc: '汉中城外的双头毒蛇，皮能做衣。' },
    { id: 's_shadow',  story: true, name: '暗影魔将', rec: 9, enemies: ['shadow'],                   desc: '截信的暗影魔将，不让信送到收信人手里。' },
    { id: 's_mengyuan',story: true, name: '梦渊尸魔', rec: 9, enemies: ['mengyuan'],                 desc: '白素梦里的东西跑出来了。' },
    { id: 's_tianxing',story: true, name: '天星子的幻影', rec: 10, enemies: ['tianxing'],             desc: '梦里的天星子许你名字与过去，只要你替他推开那扇门。' },
    { id: 'path',    name: '竹林小径', rec: 5, enemies: ['grunt', 'grunt'],          desc: '两个拦路的山贼喽啰。' },
    { id: 'wolves',  name: '狼群出没', rec: 6, enemies: ['wolf', 'wolf', 'wolf'],    desc: '三头饿狼，速度很快。', need: 'path' },
    { id: 'outpost', name: '山寨前哨', rec: 7, enemies: ['grunt', 'wolf', 'grunt'],  desc: '山贼和他们养的狼。',   need: 'wolves' },
    { id: 'boss',    name: '山贼头目', rec: 8, enemies: ['grunt', 'boss', 'grunt'],  desc: '头目每三回合会使出横扫千军。', need: 'path' }
  ],

  // 主线：talk = 找人对话；win = 打赢指定关卡
  // 主线剧情来自 js/story.js（由 tools/story_import.py 从 kunkunxunqin 的剧本生成），载入时由 game.js 摊平成 D.quests
  quests: [],

  // 天气：各地图按权重随机，每隔 span 秒转一次。演示用，不影响战斗数值。
  weather: {
    clear:  { name: '晴',   desc: '天朗气清，适合赶路。' },
    rain:   { name: '小雨', desc: '细雨绵绵，石板路上泛着水光。' },
    storm:  { name: '雷雨', desc: '电闪雷鸣，雨下得又急又大。' },
    snow:   { name: '飘雪', desc: '雪花悠悠落下，四下一片安静。' },
    span: [90, 200],
    byMap: {
      village: { clear: 4, rain: 2, snow: 1 },
      town:    { clear: 5, rain: 2, storm: 1 },
      pine: { clear: 4, rain: 2 },
      wolfvalley: { clear: 2, storm: 2 },
      taixuforest: { clear: 4, rain: 2 },
      taixupeak: { clear: 5, rain: 1 },
      taixuvalley: { clear: 2, rain: 2 },
      mist: { clear: 2, rain: 2 },
      creek: { clear: 4, rain: 3 },
      outskirts: { clear: 4, rain: 2 },
      wild:    { clear: 3, rain: 3, storm: 1 }
    }
  },
  // 聊天：世界频道里的“路人”和系统公告都是前端模拟的，没有联网。换成真服务器时，把 js/chat.js 里的 simulate* 去掉、
  // 发送处改成发请求、收到消息时调用 CHAT.add('world', {...}) 即可。
  chat: {
    cooldown: 6, maxLen: 40, history: 200, friendLimit: 50, partyLimit: 5,
    ban: ['代充广告', '外挂出售', '私服广告'],
    bots: [
      { name: '沈听雪', cls: 'f_sword', lv: 32 }, { name: '陆长风', cls: 'm_sword', lv: 28 }, { name: '凌霄阁主', cls: 'm_mage', lv: 41 }, { name: '苏晚照', cls: 'f_heal', lv: 25 },
      { name: '燕归来', cls: 'm_spear', lv: 36 }, { name: '顾清歌', cls: 'f_bow', lv: 30 }, { name: '白无咎', cls: 'm_mage', lv: 22 }, { name: '叶知微', cls: 'f_heal', lv: 18 },
      { name: '楚临渊', cls: 'm_spear', lv: 39 }, { name: '温若水', cls: 'f_sword', lv: 15 }, { name: '萧停云', cls: 'm_sword', lv: 12 }, { name: '谢映月', cls: 'f_bow', lv: 27 },
      { name: '江照夜', cls: 'm_sword', lv: 8 }, { name: '青锋', cls: 'm_spear', lv: 44 }
    ],
    lines: [
      '汉中郡外竹林有山贼，组队的来～',
      '玄兵塔第三层有人一起吗？缺个医者[惊]',
      '擂台官那边今天人好多，等了三轮才上去',
      '恒山村的野猪怎么打不过，求带',
      '新人看这里：先找村长，再去山脚打木桩',
      '收[精铁剑]，价格好商量，寄卖行直接挂',
      '出售[百花香囊]，一口价，有意者私聊[笑]',
      '锻造坊的王铁匠今天又在敲敲打打',
      '这{weather}天气，赶路真舒服',
      '{weather}天，窝在聚贤楼喝茶最合适了',
      '有没有人一起刷山贼寨？头目掉的东西不错[酷]',
      '求问：玄晶天狐怎么养？',
      '剑姬的连击是真的快，羡慕',
      '刚升到新等级，气血又涨了不少',
      '竞技场五对五，大家有推荐的阵容吗',
      '帮派招人，在线活跃就行，不看战力',
      '路过的大侠，能带带我吗',
      '今晚有活动吗？看公告说有',
      '店小二的茶是真的管用，喝完满血',
      '谁知道逍遥子在哪？找了半天',
      '哈哈哈，刚才被野狼追了一路[大笑]',
      '收矿石，数量不限',
      '有没有人组队去城外竹林，三缺一',
      '汉中郡的樱花真好看，截图留念[心]',
      '{name}也在这个区？欢迎欢迎',
      '一个人逛街好无聊，有人聊天吗'
    ],
    replies: {
      '组|队|一起': ['来来来，算我一个', '我也想组，等等我', '几级的？我去'],
      '[?？]|怎么|如何|求': ['去问问店小二，他什么都知道', '这个我也在找，帮顶', '看看任务栏，会自动寻路的'],
      '你好|大家好|hi|Hi|哈喽': ['你好呀，欢迎来到归秦', '欢迎欢迎～', '来了来了，大侠好'],
      '收|卖|出售': ['寄卖行看看？', '什么价？', '私聊你了'],
      '新人|新手|萌新': ['新人看右上角小地图，点一下能打开场景地图', '恒山村好好打，木桩野猪都是白送经验']
    },
    guildName: '青云盟',
    guildMembers: ['沈听雪', '陆长风', '苏晚照', '顾清歌', '叶知微', '谢映月'],
    guildLines: [
      '今晚八点帮战，都准时上线啊～[赞]',
      '帮会仓库新到了一批药，要的自取',
      '有人去玄兵塔吗？带我一个[笑]',
      '刚打到一把[精铁剑]，要不要的？',
      '大家早，今天也要元气满满[大笑]',
      '新入帮的兄弟记得去找帮主领见面礼',
      '竞技场我们帮今天赢了三场，不错[酷]',
      '谁有多余的[金创药]，借我两瓶',
      '下雨了，大家别在野外挂机[睡]'
    ],
    partyLines: [
      '前面有野猪，我去拉怪',
      '血不多了，谁带药了？',
      '等我一下，我去买点[金创药]',
      '这个头目不好打，注意躲技能[惊]',
      '我来奶你，放心冲[心]',
      '刚才那一下太帅了[赞]',
      '走吧，去下一个点',
      '稍等，我在回血'
    ],
    pmReplies: ['在的，怎么了？', '好呀[笑]', '哈哈，行', '你也在这个区啊', '稍等，我打完这只怪', '嗯嗯，我看看', '没问题～', '一起一起[大笑]'],
    pmGreets: ['少侠，在吗？想跟你交个朋友[笑]', '看你也是新人，要不要组队练级？', '你好呀，我是{bot}，多多关照[心]'],
    partyDecline: ['{bot} 正忙，拒绝了你的组队邀请', '{bot} 已经在别的队伍里了'],
    fallback: ['哈哈', '+1', '前排围观', '有道理', '确实', '没毛病'],
    notices: [
      '欢迎来到归秦！新服已开，愿各位少侠仗剑江湖。',
      '【活动】竞技场五对五开放中，找汉中郡的擂台官即可报名。',
      '【公告】玄兵塔即将开放，请少侠提前准备。',
      '恭喜玩家「{bot}」在竞技场连胜十场！',
      '恭喜玩家「{bot}」等级突破 40 级！',
      '【维护】本区将于今晚 24:00 例行维护，预计 30 分钟，请提前下线。',
      '【提示】遇到雷雨天气不要在野外久留，当心山贼趁乱出没。',
      '恭喜玩家「{bot}」获得稀有装备「精铁剑」！',
      '【活动】限时双倍经验开启，打山贼最划算。'
    ]
  },
  // 只出现在对白里的角色：对白里的名字 → 头像（没有头像就不显示头像框）
  speakers: { '除魔军士卒': 'head_m_sword', '陨神残念': null, '邪月': null, '心魔': null, '暗影魔将': null, '梦渊尸魔': null, '天星子的身影': null },

  npcs: {
    elder:  { name: '村长',   title: '【恒山村】', sprite: 'kk_npc14', head: 'head_kk_npc14', idle: '山下不比村里，凡事多留个心眼。累了就回来歇歇。' },
    axiu:   { name: '阿秀',   sprite: 'kk_axiu', head: 'head_kk_axiu', color: '#a8e6ff', idle: '我刚采了草药，熬了一锅汤，少侠喝一碗再走吧。', rest: true, restMsg: '喝了一碗草药汤，气血法力回满' },
    waiter: { name: '店小二', idle: '客官辛苦，喝壶热茶歇歇脚吧。', rest: true, restMsg: '喝了一壶热茶，气血法力回满' },
    smith:  { name: '王铁匠', idle: '打铁还需自身硬。兵器铠甲要锻造升星，还是想在孔里镶宝石，都交给老夫。', shop: 'smith' },
    girl:   { name: '逍遥子', title: '【百花谷】', color: '#dca8ff', titleColor: '#ffa8d0', idle: '今晚灯会的灯谜好难……你也是来猜谜的吗？' },
    arenaman: { name: '擂台官', title: '【竞技场】', color: '#ff9a8a', sprite: 'f_blade', head: 'head_m_spear', idle: '擂台已经备好，五对五，人宠齐上。要不要上去练练手？', act: 'arena' },
    towerman: { name: '守塔人', title: '【玄兵塔】', color: '#c9b8ff', sprite: 'f_tao', head: 'head_m_mage', act: 'towerEnter', idle: '百炼玄兵塔一共七层，每层十名守卫，都是百炼成精的神兵法宝。打败本层首领，才能上下一层。' },
    towertp:  { name: '塔内传送师', title: '【玄兵塔】', color: '#c9b8ff', sprite: 'kk_npc07', head: 'head_kk_npc07', act: 'towerTp', idle: '每层的首领一倒，我就能送你上楼。想歇一歇，也可以送你出塔。' },
    trader:   { name: '寄卖商人', title: '【汉中郡】', color: '#ffe08a', sprite: 'kk_npc_jmsr', head: 'head_kk_npc_jmsr', shop: 'trader', idle: '寄卖行的买卖，讲的是个“诚”字。项链、戒指、法宝、护符，各路好货都在这儿挂着，少侠随便看。' },
    pet_jz:   { name: '斗宠小郡主', title: '【斗宠】', color: '#ffb8e0', titleColor: '#8fd0ff', sprite: 'kk_npc20', head: 'head_kk_npc20', act: 'arena', idle: '本郡主的宠物可是汉中第一！你的宠物敢不敢跟它比一比？' },
    // 道家驻地（太虚之林）的三位：形象取自 kunkunxunqin（门主 10000463 / 琴魔堂主 10000464 / 天音堂主 10000465）
    dao_menzhu: { name: '道家门主', title: '【道家】', color: '#bfe8ff', sprite: 'kk_npc57', head: 'head_kk_npc57', act: 'joinSect', idle: '道法自然，无为而无不为。太虚之林乃本门驻地。十级以上、与道有缘者，可在此拜入道家，再去堂主处择天音或琴魔一堂。' },
    dao_qinmo:  { name: '琴魔堂主', title: '【道家·琴魔】', color: '#bfe8ff', act: 'pickHall', hall: 'qm', sprite: 'kk_npc45', head: 'head_kk_npc45', idle: '琴魔一脉，以魔音入敌心。琴声不是用来讨好谁的，是用来镇住心里那头兽的。' },
    dao_tianyin: { name: '天音堂主', title: '【道家·天音】', color: '#bfe8ff', act: 'pickHall', hall: 'ty', sprite: 'kk_npc47', head: 'head_kk_npc47', idle: '天音一脉，琵琶一曲可疗伤、护体、唤回将逝之人。会救人，才算真本事。' },
    taixu_mo:    { name: '墨巨子',   title: '【墨家】', color: '#ffd9a0', sprite: 'f_spear', head: 'head_m_spear', act: 'joinMo', idle: '兼爱非攻，机关为用。太虚之峰的每一道机括，都是墨家弟子亲手所造。' },
    mo_mengshi:  { name: '猛士堂主', title: '【墨家·猛士】', color: '#ffd9a0', act: 'pickMoHall', hall: 'ms', sprite: 'kk_mo_ms', head: 'head_kk_mo_ms', idle: '猛士一脉，双刃开山。流血、易伤、拼命的打法，不怕疼的人才学得会。' },
    mo_dunjia:   { name: '遁甲堂主', title: '【墨家·遁甲】', color: '#ffd9a0', act: 'pickMoHall', hall: 'dj', sprite: 'kk_mo_dj', head: 'head_kk_mo_dj', idle: '遁甲一脉，剑盾为阵。破甲、鼓舞、吸血，站在最前面，也要让身后的人站得住。' },
    taixu_yin:   { name: '邹长老',   title: '【阴阳家】', color: '#ffc6e8', sprite: 'f_healer', head: 'head_f_heal', idle: '阴阳消长，五行流转。太虚之谷里的每一块卦石，都藏着天地的玄机。' },
    zhanhw:   { name: '展护卫',   title: '【恒山村】', color: '#ffd9a0', sprite: 'kk_npc_zhw',  head: 'head_kk_npc_zhw', idle: '拳脚功夫不是一天练成的。去清溪谷之前，多打几场熟悉手感。' },
    bianque:  { name: '扁老鹊',   title: '【村医】',   color: '#b8f0c0', sprite: 'kk_npc_cyblq',   head: 'head_kk_npc_cyblq',  idle: '身子还虚的话，随时来我这儿讨碗药。心里的事，也别一个人扛着。' },
    qingxizi: { name: '清溪子',   title: '【清溪谷】', color: '#bfe8ff', sprite: 'kk_npc13',    head: 'head_kk_npc13',  idle: '溪水听得久了，人心也就静了。你慢慢逛，别踩坏了溪石。' },
    jy_mo:    { name: '墨家接引师', title: '【墨家】', color: '#ffd9a0', sprite: 'kk_npc33',  head: 'head_kk_npc33', idle: '机关用在哪，要看掌机关的人。多看，少信一句定论。' },
    jy_dao:   { name: '道家接引师', title: '【道家】', color: '#bfe8ff', sprite: 'kk_npc13', head: 'head_kk_npc13',  idle: '问来处无妨，别让答案牵着鼻子走。' },
    jy_yin:   { name: '阴阳接引师', title: '【阴阳家】', color: '#ffc6e8', sprite: 'kk_npc07', head: 'head_kk_npc07', idle: '同一道篆线，正看是阻，反看是引。' },
    zc_elder: { name: '赵村村长', title: '【赵村】',   color: '#ffe08a', sprite: 'kk_npc_qxz',    head: 'head_kk_npc_qxz',   idle: '虫清了，乡亲们才敢来上香。日子总算能往下过。' },
    xiaohs:   { name: '萧恨水',   title: '【除魔军】', color: '#ff9a8a', sprite: 'kk_npc_xhs',  head: 'head_kk_npc_xhs', idle: '梦里的事不要全信，也不要全不信。多留个心眼。' },
    hanxin:   { name: '韩信',     title: '【云雾谷】', color: '#ffd56a', sprite: 'kk_npc_hx',  head: 'head_kk_npc_hx', idle: '谷里雾大，别走散。有消息我会让人通知你。' },
    equipman: { name: '装备商人', title: '【汉中】',   color: '#ffffff', sprite: 'kk_npc15',   head: 'head_kk_npc15',  idle: '好兵器自己会说话，客官要不要掂一掂？' },
    baisu:    { name: '白素',     title: '【汉中】',   color: '#ffa8d0', sprite: 'kk_npc_bs',     head: 'head_kk_npc_bs',    idle: '汉中人来人往，信与消息，比银子金贵。' },
    xiaohs2:  { name: '萧恨水',   title: '【除魔军】', color: '#ff9a8a', sprite: 'kk_npc_xhs',  head: 'head_kk_npc_xhs', idle: '这城里的信不好送，你多加小心。' },
    spot_cj1000: { name: '神韵', title: '【采集】', color: '#9ff0ff', spot: true, idle: '溪石间的微光已经散去了。' },
    vendor: { name: '货郎',   color: '#ffffff', idle: '金创药、回气丹，行走江湖少不了——少侠来点儿？', shop: 'vendor' }
  },

  // 地图。坐标都写在“底图原图像素 / k”的单位里：舞台像素 = 坐标 * k * scale。
  //   iw, ih   底图原图尺寸        scale  底图放大倍数（决定地图有多大）
  //   walk     可行走多边形        roads  可行走道路：一串中心点 + 半宽 r（自动展开成多边形）
  //   blocks   道路中间走不过去的东西：圆心 at + 半径 r
  //   npcs / portals / foes       人物、传送点、地图上的敌人（foes 对应 stages 里的关卡）
  //   world    在世界地图图片上的位置（图片原图像素）   note  世界地图上的一句说明
  //   home     在这张图战败后被送回哪里               battleBg  战斗背景
  maps: {
    village: {
      butterflies: 9,
      name: '恒山村', img: 'bg_village', mini: 'mini_village', iw: 2000, ih: 1500, scale: 1.0, k: 1, start: [930, 690],
      alt: '溪水环绕的山村：茅草屋、拱桥、水车、凉亭和村长的瓦顶大屋，青石台阶一路通向山上',
      world: [888, 710], note: '新手村', home: 'village', battleBg: 'bg_battle2',
      water: { dir: [-1, .3], speed: 20, zones: [{ rect: [790, 0, 1000, 300], dir: [.12, 1], speed: 55 }] },   // 遮罩坐标是 1000×750（地图的一半）
      walk: [
        [[1480,1160],[1760,1140],[1850,1300],[1830,1480],[1500,1480],[1400,1330]]      // 右下角木平台（出村口）
      ],
      roads: [
        { r: 46, pts: [[260,1450],[330,1350],[430,1235],[520,1135],[620,1045],[700,960]] },
        { r: 30, pts: [[700,945],[800,885],[900,862],[1010,882],[1075,892]] },                      // 拱桥
        { r: 42, pts: [[1075,892],[1150,830],[1230,765],[1320,690],[1420,605],[1520,525],[1600,430],[1690,335],[1700,210],[1655,120]] },
        { r: 40, pts: [[1075,892],[1010,800],[950,725],[895,645],[875,590]] },                     // 去村长屋
        { r: 34, pts: [[875,575],[1000,530],[1150,505],[1310,470]] },                              // 屋前木平台
        { r: 38, pts: [[1235,800],[1300,900],[1335,1000],[1400,1090],[1500,1165],[1640,1230],[1780,1330]] }
      ],
      blocks: [],
      fade: [   // 遮挡虚化区：凉亭屋顶（角色在其后方时变半透明）
        { poly: [[1700,330],[1752,330],[1762,380],[1905,440],[1908,525],[1860,562],[1790,548],[1700,565],[1612,548],[1602,500],[1602,416],[1690,375]], base: 565 }
      ],
      glows: [[1110,330],[1570,520],[1790,960],[1710,520]],
      opens: [[[1310, 495], [1335, 515], [1415, 478], [1395, 452]],   // 大屋右侧台阶底
        [[1790, 1255], [1885, 1255], [1895, 1305], [1790, 1310]],   // 右下药铺门口的木地板（阿秀站这儿）
        [[110, 1135], [105, 1075], [185, 1050], [200, 990], [260, 965], [470, 950], [570, 935], [585, 960], [395, 1040], [378, 1085], [385, 1170], [450, 1215], [330, 1235], [300, 1200], [225, 1125], [205, 1112]]],   // 桥西草屋的木平台和台阶
      npcs: [{ id: 'elder', at: [1295, 405] }, { id: 'axiu', at: [1735, 1305] }],
      portals: [{ to: 'outskirts', name: '下山·恒山村郊', art: 'portal_array', at: [230, 1295], arrive: [140, 1360], needQuest: '1001', lockMsg: '村长还有话要交代，先别急着下山' }],
      foes: [{ stage: 'dummy', at: [440, 1225] }, { stage: 'boar', at: [1560, 480] }]
    },
    outskirts: {
      name: '恒山村郊', world: [968, 930], note: '山野荒径', home: 'village', img: 'bg_outskirts', mini: 'mini_outskirts', iw: 2000, ih: 1493, scale: 1.2, k: 1, start: [140, 1360],
      alt: '村外的荒野：土路穿过草地和树林，溪水上有踏脚石，路边有篝火圈、树桩、倒木和路牌',
      water: { dir: [1, .6], speed: 16, zones: [{ rect: [190, 140, 270, 250], dir: [.3, 1], speed: 45 }] },
      walk: [],
      roads: [
        { r: 95, pts: [[10,1440],[200,1310],[380,1170],[560,1075],[760,1020],[860,930],[900,830]] },
        { r: 90, pts: [[900,830],[960,700],[1010,620],[1130,570],[1330,500],[1540,470],[1680,390],[1770,270],[1880,170],[1990,90]] },
        { r: 70, pts: [[560,1075],[540,950],[520,860]] },                           // 篝火圈
        { r: 70, pts: [[1330,500],[1420,400],[1470,330]] }                          // 路牌（通清溪谷）
      ],
      blocks: [],
      glows: [[520,860],[1470,330]],
      npcs: [{ id: 'zhanhw', at: [470, 855] }, { id: 'bianque', at: [1070, 685] }],
      portals: [{ to: 'village', name: '回恒山村', art: 'portal_array', at: [170, 1315], arrive: [320, 1285] }, { to: 'town', name: '去汉中郡', at: [1960, 110], arrive: [450, 790] }, { to: 'creek', name: '入清溪谷', at: [1480, 320], arrive: [1100, 1450] }],
      foes: [{ stage: 'hog', at: [560, 1060] }, { stage: 'roam', at: [1330, 505] }]
    },
    creek: {
      name: '清溪谷', world: [470, 1040], note: '碧水幽谷', home: 'village', img: 'bg_creek', mini: 'mini_creek', iw: 2000, ih: 1493, scale: 1.2, k: 1, start: [1100, 1450],
      alt: '碧绿的溪谷：溪水带着几道小瀑布，木桥横跨溪流，红枫、竹林和荷塘相伴，石板路一路通向山上',
      water: { dir: [-.5, 1], speed: 20, zones: [{ rect: [690, 0, 800, 120], dir: [-.1, 1], speed: 55 }, { rect: [585, 145, 700, 255], dir: [-.1, 1], speed: 55 }, { rect: [105, 600, 260, 747], dir: [-.2, 1], speed: 50 }] },
      walk: [],
      roads: [
        { r: 85, pts: [[1100,1480],[1190,1370],[1050,1270],[950,1190],[800,1120],[690,1030],[740,900],[880,840],[1020,790]] },
        { r: 60, pts: [[1020,790],[1170,760],[1290,700]] },                             // 木桥
        { r: 85, pts: [[1290,700],[1420,640],[1500,540],[1600,490],[1740,450],[1860,360],[1830,250],[1760,140],[1800,40]] }
      ],
      blocks: [],
      glows: [[1100,760],[1650,480],[700,1030]],
      npcs: [{ id: 'qingxizi', at: [880, 830] }, { id: 'jy_dao', at: [1240, 670] }, { id: 'jy_yin', at: [1480, 653] }, { id: 'jy_mo', at: [700, 960] }],
      portals: [{ to: 'outskirts', name: '回恒山村郊', at: [1100, 1470], arrive: [1440, 390] }, { to: 'mist', name: '入云雾谷', at: [1800, 50], arrive: [770, 1440] }],
      foes: [{ stage: 'creekwolf', at: [800, 1110] }, { stage: 'creekgang', at: [1600, 490] }]
    },
    mist: {
      name: '云雾谷', world: [1760, 560], note: '云雾缭绕', home: 'village', img: 'bg_mist', mini: 'mini_mist', iw: 2000, ih: 1493, scale: 1.2, k: 1, start: [770, 1440],
      alt: '深山谷底：碧绿的溪水沿着土路蜿蜒，两侧是爬满藤蔓的崖壁和细瀑布，云雾在谷中缓缓飘动',
      fog: { n: 34, lo: .3, hi: .85 },
      water: { dir: [-1, .75], speed: 24, zones: [{ rect: [170, 170, 260, 430], dir: [.15, 1], speed: 60 }, { rect: [720, 0, 880, 110], dir: [-.4, 1], speed: 50 }] },
      walk: [],
      roads: [
        { r: 90, pts: [[760,1480],[900,1380],[1030,1310],[1130,1240],[1250,1140],[1330,1040],[1390,960],[1450,870],[1470,780],[1540,690],[1530,600],[1430,560],[1300,470],[1170,420],[1050,330],[1040,240],[1200,150],[1290,60]] }
      ],
      blocks: [],
      glows: [[1050,330],[1450,870]],
      portals: [{ to: 'creek', name: '回清溪谷', at: [760, 1470], arrive: [1790, 130] }],
      foes: [{ stage: 'mistwolf', at: [1130, 1240] }, { stage: 'mistgang', at: [1170, 430] }]
    },
    pine: {
      name: '石松林', world: [1560, 1480], note: '古松石柱', home: 'town', img: 'bg_pine', mini: 'mini_pine', iw: 2000, ih: 1493, scale: 1.2, k: 1, start: [800, 1440],
      alt: '石柱与古松交错的深林：松针铺成的小路在青苔石柱和盘曲的老松之间蜿蜒向上',
      walk: [],
      roads: [
        { r: 100, pts: [[800,1480],[900,1400],[1100,1290],[1020,1180],[900,1100],[860,1030],[980,900],[1100,780],[1180,740],[1300,660],[1200,540],[1100,480],[1050,400],[1180,300],[1200,200],[1130,120],[1090,20]] }
      ],
      blocks: [],
      glows: [[900,1100],[1200,540]],
      portals: [{ to: 'wild', name: '回城外竹林', at: [800, 1470], arrive: [440, 232] }, { to: 'wolfvalley', name: '入恶狼谷', at: [1090, 30], arrive: [90, 1310] }],
      foes: [{ stage: 'pinegang', at: [1000, 1170] }, { stage: 'pinewolf', at: [1180, 650] }]
    },
    wolfvalley: {
      name: '恶狼谷', world: [1860, 1800], note: '狼群盘踞', home: 'town', img: 'bg_wolfvalley', mini: 'mini_wolfvalley', iw: 2000, ih: 1493, scale: 1.2, k: 1, start: [90, 1310],
      alt: '荒凉的山谷：土路上满是爪印，岩壁里有一个个狼洞，洞口散着兽骨，路边是枯树和荆棘',
      walk: [],
      roads: [
        { r: 100, pts: [[40,1340],[300,1130],[540,990],[760,870],[900,790],[1050,690],[1150,640],[1250,540],[1420,440],[1550,390],[1500,330],[1380,290],[1250,230],[1130,160],[1090,60]] }
      ],
      blocks: [],
      glows: [[540,990],[1420,440]],
      portals: [{ to: 'pine', name: '回石松林', at: [40, 1345], arrive: [1090, 110] }],
      foes: [{ stage: 'denwolf', at: [760, 870] }, { stage: 'wolfboss', at: [1380, 300] }]
    },
    taixuforest: {
      sect: true, name: '太虚之林', world: [380, 1860], note: '道家驻地', home: 'town', img: 'bg_taixuforest', mini: 'mini_taixuforest', iw: 2000, ih: 1616, scale: 1.35, k: 1, start: [1200, 900],
      alt: '竹林深处的道家驻地：莲花形的八卦石台立在水潭之上，青苔石阶通向四周的石台',
      walk: [[[80, 180], [250, 100], [480, 110], [580, 200], [540, 330], [380, 390], [200, 370], [60, 290]],
             [[180, 1300], [330, 1210], [560, 1220], [660, 1330], [620, 1460], [450, 1540], [260, 1520], [150, 1420]],
             [[700, 880], [800, 740], [1000, 640], [1180, 600], [1400, 640], [1560, 720], [1700, 840], [1720, 980], [1600, 1100], [1400, 1180], [1200, 1200], [1000, 1170], [800, 1080]],
             [[1420, 360], [1500, 250], [1700, 200], [1900, 260], [1960, 400], [1900, 520], [1700, 570], [1500, 520]]],
      roads: [
        { r: 70, pts: [[520, 330], [570, 430], [520, 560], [440, 680], [380, 790], [300, 900], [260, 1000], [300, 1100], [340, 1200]] },
        { r: 60, pts: [[380, 1000], [520, 1050], [700, 1060]] },
        { r: 70, pts: [[1480, 720], [1460, 620], [1500, 520]] },
        { r: 90, pts: [[640,1040],[720,1030],[820,1010]] }
      ],
      blocks: [],
      glows: [[1200, 900], [1560, 380]],
      npcs: [{ id: 'dao_menzhu', at: [330, 250] }, { id: 'dao_qinmo', at: [1690, 390] }, { id: 'dao_tianyin', at: [390, 1390] }],
      portals: [{ to: 'town', name: '返回汉中郡', at: [900, 1060], arrive: [850, 575] }]
    },
    taixupeak: {
      sect: true, name: '太虚之峰', world: [1960, 1060], note: '墨家驻地', home: 'town', img: 'bg_taixupeak', mini: 'mini_taixupeak', iw: 2000, ih: 1616, scale: 1.35, k: 1, start: [1000, 620],
      water: { dir: [1, .4], speed: 12, zones: [{ rect: [90, 60, 235, 295], dir: [.25, 1], speed: 55 }, { rect: [465, 435, 560, 600], dir: [0, 1], speed: 55 }, { rect: [880, 225, 920, 305], dir: [0, 1], speed: 50 }, { rect: [0, 762, 1000, 810], dir: [1, -.3], speed: 8 }] },
      alt: '山崖上的墨家驻地：木拱桥、机关石台、齿轮平台和凉亭散布在碧松与瀑布之间',
      walk: [[[900, 330], [1100, 280], [1300, 230], [1560, 220], [1680, 300], [1640, 380], [1400, 420], [1200, 470], [1000, 470]],
             [[800, 520], [1050, 500], [1250, 560], [1230, 680], [1000, 700], [800, 660]],
             [[200, 830], [420, 800], [600, 840], [620, 950], [500, 1010], [300, 1010], [200, 950]],
             [[1100, 900], [1300, 880], [1380, 960], [1340, 1080], [1180, 1110], [1080, 1020]],
             [[1700, 820], [1850, 770], [2000, 820], [2000, 980], [1850, 1000], [1720, 940]]],
      roads: [
        { r: 80, pts: [[100, 1480], [400, 1440], [700, 1450], [1000, 1470], [1100, 1480]] },
        { r: 55, pts: [[1100, 1480], [1220, 1420], [1300, 1360]] },
        { r: 60, pts: [[1300, 1360], [1380, 1250], [1450, 1160], [1560, 1090], [1640, 1010], [1700, 930], [1800, 880]] },
        { r: 60, pts: [[1650, 880], [1560, 800], [1460, 740], [1360, 700], [1250, 640], [1100, 620], [950, 620]] },
        { r: 70, pts: [[1150, 600], [1150, 500], [1100, 430]] },
        { r: 55, pts: [[640, 830], [740, 780], [860, 730], [930, 700]] },
        { r: 70, pts: [[420, 980], [400, 1080], [450, 1180], [520, 1270], [600, 1340], [680, 1400], [740, 1440]] },
        { r: 60, pts: [[1340, 1000], [1420, 1060], [1450, 1140]] }
      ],
      blocks: [],
      glows: [[1450, 320], [1220, 1000]],
      npcs: [{ id: 'taixu_mo', at: [1455, 286] }, { id: 'mo_mengshi', at: [1344, 372] }, { id: 'mo_dunjia', at: [1576, 366] }],
      portals: [{ to: 'town', name: '返回汉中郡', at: [1150, 500], arrive: [850, 575] }]
    },
    taixuvalley: {
      sect: true, name: '太虚之谷', world: [1150, 250], note: '阴阳家驻地', home: 'town', img: 'bg_taixuvalley', mini: 'mini_taixuvalley', iw: 2000, ih: 1504, scale: 1.35, k: 1, start: [1040, 950],
      water: { dir: [-1, .2], speed: 5, zones: [{ rect: [15, 285, 110, 480], dir: [.1, 1], speed: 55 }, { rect: [935, 275, 1000, 480], dir: [-.1, 1], speed: 55 }, { rect: [585, 40, 615, 295], dir: [0, 1], speed: 50 }, { rect: [698, 0, 730, 110], dir: [0, 1], speed: 50 }] },
      alt: '幽谷中的阴阳家驻地：八卦石台、方尖碑和莲池环绕，两侧飞瀑垂落',
      walk: [[[1040, 580], [1600, 860], [1040, 1140], [480, 850]],
             [[260, 500], [420, 440], [700, 440], [820, 520], [760, 640], [520, 700], [330, 640]],
             [[1360, 520], [1580, 430], [1760, 500], [1780, 640], [1560, 700], [1380, 640]]],
      roads: [
        { r: 60, pts: [[620, 640], [760, 720], [880, 760]] },
        { r: 70, pts: [[1440, 780], [1470, 700]] },
        { r: 80, pts: [[1400, 1120], [1520, 1200], [1640, 1260]] },
        { r: 70, pts: [[80, 1470], [200, 1380], [340, 1300], [460, 1230], [560, 1180], [700, 1060]] },
        { r: 80, pts: [[700,1060],[800,1040],[900,1010],[980,980]] },
        { r: 90, pts: [[1400,1120],[1320,1060],[1240,1000],[1180,960]] }
      ],
      blocks: [],
      glows: [[1040, 860], [1520, 540]],
      npcs: [{ id: 'taixu_yin', at: [1040, 780] }],
      portals: [{ to: 'town', name: '返回汉中郡', at: [1040, 1080], arrive: [850, 575] }]
    },
    town: {
      name: '汉中郡', world: [1105, 1145], note: '主城', home: 'town', img: 'bg_town', mini: 'mini_town', iw: 5324, ih: 3042, scale: 1.0359, k: 1.8648, start: [850, 575],
      alt: '青瓦红柱的繁华主城：中央是高耸的玄兵塔，周围有擂台、锻造坊、寄卖行、集市和荷塘石桥',
      walk: [],
      roads: [
        { r: 52, pts: [[700,575],[850,555],[1000,595]] },
        { r: 30, pts: [[850,560],[850,480]] },
        { r: 50, pts: [[1000,595],[1120,540],[1250,480],[1380,420],[1500,335],[1620,235],[1670,190]] },
        { r: 50, pts: [[1000,625],[1100,690],[1200,745],[1300,790],[1420,805],[1520,830],[1620,880],[1740,935]] },
        { r: 45, pts: [[760,580],[700,640],[640,690],[560,730],[470,765],[380,775],[280,795],[200,825],[120,865]] },
        { r: 40, pts: [[470,765],[420,720],[350,735]] },
        { r: 36, pts: [[760,580],[690,520],[650,450],[630,370],[615,290],[585,215],[565,155]] },
        { r: 40, pts: [[1739, 934], [1937, 856], [2059, 714]] },
        { r: 40, pts: [[1739, 934], [1733, 1019], [1733, 1111]] },
      ],
      blocks: [],
      glows: [[660,225],[1215,280],[1750,200],[1620,610],[240,540],[1500,990]],
      npcs: [{ id: 'towerman', at: [850, 535] }, { id: 'waiter', at: [1384, 376] }, { id: 'smith', at: [1290, 745] },
             { id: 'vendor', at: [350, 745] }, { id: 'arenaman', at: [660, 705] }, { id: 'trader', at: [1720, 1060] }, { id: 'girl', at: [1009, 600] }, { id: 'pet_jz', at: [1470, 1230] }],
      portals: [{ to: 'wild', name: '出城·竹林', at: [300, 800], arrive: [150, 453] }, { to: 'outskirts', name: '恒山村郊', at: [570, 160], arrive: [1860, 190] }]
    },
    wild: {
      name: '城外·竹林', world: [1336, 1690], note: '山贼出没', home: 'town', img: 'bg_wild', mini: 'mini_wild', iw: 1672, ih: 941, scale: 2, k: 1.6328, start: [150, 453],
      alt: '城外的竹林：土路从石桥一路通向山贼的寨门，岔路通往狼窝和石龛',
      walk: [],
      roads: [
        { r: 21, pts: [[18,533],[80,478],[150,453],[225,455],[290,425],[380,385],[470,348],[545,308],[625,282],[700,250],[770,205],[850,180],[915,150],[928,108]] },
        { r: 15, pts: [[330,350],[250,315],[215,265],[190,205],[160,150],[128,120]] },
        { r: 12, pts: [[545,308],[515,278],[478,262],[445,232],[440,208]] },
        { r: 17, pts: [[770,215],[820,262],[870,285]] },
        { r: 38, pts: [[295,350],[375,360]] },
        { r: 30, pts: [[805,272],[868,290]] },
        { r: 32, pts: [[855,172],[918,142]] }
      ],
      glows: [[440,165],[75,445],[250,440],[140,490],[165,410],[808,262],[898,298],[872,78],[960,85]],
      portals: [{ to: 'town', name: '回汉中郡', at: [40, 515], arrive: [300, 800] }, { to: 'pine', name: '入石松林', at: [440, 206], arrive: [800, 1440] }],
      foes: [{ stage: 'path', at: [352, 352] }, { stage: 'wolves', at: [150, 140] }, { stage: 'outpost', at: [850, 288] }, { stage: 'boss', at: [926, 116] }]
    }
  },
  // 竞技场演示：两支满编队伍。mult 是相对“同等级主角裸属性”的倍率；
  // ai：heal 奶妈 / mage 群攻法师 / striker 专打残血 / tank 肉盾（aggro 高，更容易挨打）/ 不写就是普通攻击
  // 我方用背影立绘（b_ 开头），对手用正面立绘（f_ 开头）
  arena: {
    lv: 30, allyName: '天剑宗', foeName: '天机阁',
    ally: [
      { name: '阿秀',   sprite: 'b_axiu',   ai: 'heal',    mult: { hp: 0.85, atk: 0.6, def: 0.75, spd: 1.05 }, pet: { name: '小鹿', sprite: 'b_deer',   h: 76, mult: { hp: 0.5, atk: 0.5 } } },
      { name: '逍遥子', sprite: 'b_girl',   ai: 'mage',    mult: { hp: 0.8, atk: 1.1, def: 0.65, spd: 1.1 },   pet: { name: '玉兔', sprite: 'b_rabbit', h: 56, mult: { hp: 0.45, atk: 0.6, spd: 1.3 } } },
      { name: '云中客', sprite: 'b_player', me: true,      mult: { atk: 1.1 },                                  pet: { name: '玄晶天狐', sprite: 'b_ss_bs20', h: 112, ai: 'fox' } },
      { name: '燕归',   sprite: 'b_yan',    ai: 'striker', mult: { hp: 0.85, atk: 1.2, def: 0.7, spd: 1.3 },   pet: { name: '苍鹰', sprite: 'b_hawk',   h: 70, mult: { hp: 0.45, atk: 0.8, spd: 1.5 } } },
      { name: '无尘',   sprite: 'b_wuchen', ai: 'tank', aggro: 3, mult: { hp: 1.5, atk: 0.7, def: 1.5, spd: 0.8 }, pet: { name: '虎崽', sprite: 'b_tiger', h: 64, mult: { hp: 0.7, atk: 0.75 } } }
    ],
    foe: [
      { name: '素心',   sprite: 'f_healer', ai: 'heal',    mult: { hp: 0.85, atk: 0.6, def: 0.75, spd: 1.05 }, pet: { name: '灵龟', sprite: 'f_tortoise', h: 56, mult: { hp: 0.8, atk: 0.45, spd: 0.7 } } },
      { name: '玄真',   sprite: 'f_tao',    ai: 'mage',    mult: { hp: 0.8, atk: 1.1, def: 0.65, spd: 1.1 },   pet: { name: '仙鹤', sprite: 'f_crane',    h: 82, mult: { hp: 0.45, atk: 0.65, spd: 1.4 } } },
      { name: '红绡',   sprite: 'f_sword',  ai: 'striker', mult: { hp: 0.9, atk: 1.2, def: 0.75, spd: 1.25 },  pet: { name: '墨豹', sprite: 'f_panther',  h: 58, mult: { hp: 0.55, atk: 0.8, spd: 1.3 } } },
      { name: '铁狂',   sprite: 'f_blade',  ai: 'tank', aggro: 3, h: 136, mult: { hp: 1.5, atk: 0.8, def: 1.4, spd: 0.8 }, pet: { name: '竹熊', sprite: 'f_panda', h: 64, mult: { hp: 0.75, atk: 0.7 } } },
      { name: '银霜',   sprite: 'f_spear',  mult: { hp: 1.0, atk: 1.1, def: 0.9, spd: 1.1 },                   pet: { name: '赤螭', sprite: 'f_dragon',   h: 74, mult: { hp: 0.5, atk: 0.85, spd: 1.2 } } }
    ]
  },

  shops: {
    vendor: { name: '货郎的担子', sell: ['hp_potion', 'mp_potion'] },
    trader: { name: '百宝摊', sell: ['forge_1', 'forge_2', 'forge_3', 'forge_4', 'forge_5', 'iron_sword', 'steel_saber', 'steel_sword', 'leather_cap', 'iron_helm', 'leather_armor', 'iron_armor', 'linen_legs', 'iron_greaves', 'leather_bracer', 'steel_bracer', 'linen_belt', 'cloth_boots', 'gem_atk_1', 'gem_matk_1', 'gem_hp_1', 'gem_def_1', 'gem_mdef_1', 'gem_mp_1', 'gem_dodge_1', 'gem_spd_1', 'gem_crit_1', 'jade_sword', 'scale_armor', 'crimson_sword', 'phoenix_armor', 'wood_beads', 'jade_pendant', 'copper_ring', 'silver_ring', 'bronze_bell', 'bagua_mirror', 'peace_charm', 'jade_charm', 'herb_pouch', 'brocade_pouch', 'jade_belt', 'cloud_boots'] }
  }
};
// 宝石物品由 D.gems 生成：gem_<类型>_<阶>，品质颜色跟着阶数走（1 白 … 5 红）
(function () {
  const D = window.DATA, price = [150, 450, 1350, 4000, 12000];
  for (const t in D.gems) { const g = D.gems[t];
    g.vals.forEach((v, i) => { D.items['gem_' + t + '_' + (i + 1)] = { name: g.stone + ' Lv.' + (i + 1), icon: g.icon, q: i + 1, type: 'gem', gem: t, lv: i + 1, price: price[i],
      desc: '镶嵌在装备孔里，' + D.statName[t] + ' +' + v + (t === 'crit' || t === 'dodge' ? '%' : '') + '。三颗同阶同种可以合成下一阶。' }; });
  }
})();

// 道家男角色：和女角色同样的两个分堂、同一套技能与心法，只是形象（美术键 *_dao_ty_m / *_dao_qm_m）不同
(function () { const C = window.DATA.classes;
  C.dao_ty_m = Object.assign({}, C.dao_ty, { gender: 'm', hidden: true });
  C.dao_qm_m = Object.assign({}, C.dao_qm, { gender: 'm', hidden: true });
  // 技能修炼：NPC → 能学 / 升级的技能。门主管道家基础技能，分堂技能由堂主管（见 game.js openTrain）
  window.DATA.train = { maxLv: 5, learnCoin: 150, upCoin: 100, upLv: 2, base: ['dangmo_fs', 'lingmai_fs'] };
  // 拜入道家、尚未选分堂：新手的技能与数值不变，形象换成道家服饰、背负并手持箜篌（美术键 *_dao_dz / *_dao_dz_m）
  C.dao_dz   = Object.assign({}, C.novice_f, { name: '道家弟子', sect: '道家', role: '近战 · 箜篌', weapon: '箜篌', hidden: true, magicAtk: true, skills: ['dangmo_fs', 'lingmai_fs'], mod: { atk: 0.8, matk: 1.15, def: 1.0, mdef: 1.1, hp: 1.05, mp: 1.1 },
    heart: { name: '道家心法', desc: '每级提升法攻 2 点、法防 2 点（原作：智力、精神）', perLv: { matk: 2, mdef: 2 } },
    auto: [['lingmai_fs', 'selfHurt'], ['dangmo_fs', 'always']], desc: '已拜入道家、尚未选分堂的弟子，背负箜篌。去琴魔堂主或天音堂主处选定分堂。' });
  C.dao_dz_m = Object.assign({}, C.novice_m, { name: '道家弟子', sect: '道家', role: '近战 · 箜篌', weapon: '箜篌', hidden: true, magicAtk: true, skills: ['dangmo_fs', 'lingmai_fs'], mod: { atk: 0.8, matk: 1.15, def: 1.0, mdef: 1.1, hp: 1.05, mp: 1.1 },
    heart: { name: '道家心法', desc: '每级提升法攻 2 点、法防 2 点（原作：智力、精神）', perLv: { matk: 2, mdef: 2 } },
    auto: [['lingmai_fs', 'selfHurt'], ['dangmo_fs', 'always']], desc: '已拜入道家、尚未选分堂的弟子，背负箜篌。去琴魔堂主或天音堂主处选定分堂。' });
})();

// 墨家：猛士(双刃斧) / 遁甲(剑盾) / 墨家弟子（未分堂）。女角色用默认键，男角色加 _m
(function () { const C = window.DATA.classes;
  C.mo_ms = { hidden: true, name: '猛士', sect: '墨家', gender: 'f', role: '近战 · 双刃斧', weapon: '双刃斧',
    desc: '墨家猛士堂，双刃斧开山劈岭。流血、易伤、拿命换伤害，血线越险，出手越狠。', stats: { 攻击: 5, 防御: 2, 速度: 3, 法术: 1, 气血: 3 },
    mod: { atk: 1.35, matk: 0.6, def: 0.95, mdef: 0.8, hp: 1.05, mp: 0.9 },
    heart: { name: '杀气决', desc: '每级提升物攻 3 点（原作：力量成长）', perLv: { atk: 3 } },
    skills: ['menghu_ms', 'potian_ms', 'lianzhan', 'xueji', 'kuanglong'],
    auto: [['kuanglong', 'rageOK'], ['xueji', 'bleeding'], ['lianzhan', 'hpHigh'], ['potian_ms', 'noVuln'], ['menghu_ms', 'always']] };
  C.mo_dj = { hidden: true, name: '遁甲', sect: '墨家', gender: 'f', role: '近战 · 剑盾', weapon: '剑盾',
    desc: '墨家遁甲堂，剑盾成阵。破甲、鼓舞、吸血，既扛得住也带得动队伍。', stats: { 攻击: 3, 防御: 5, 速度: 2, 法术: 1, 气血: 5 },
    mod: { atk: 1.05, matk: 0.6, def: 1.3, mdef: 1.0, hp: 1.25, mp: 0.95 },
    heart: { name: '运气决', desc: '每级提升物防 4 点、法防 2 点（原作：体质、精神）', perLv: { def: 4, mdef: 2 } },
    skills: ['menghu_dj', 'potian_dj', 'pojia', 'zhanhun', 'cuogu'],
    auto: [['zhanhun', 'teamStart'], ['cuogu', 'selfHurt'], ['pojia', 'noVuln'], ['potian_dj', 'many'], ['menghu_dj', 'always']] };
  C.mo_dz = Object.assign({}, C.novice_f, { name: '墨家弟子', sect: '墨家', role: '近战 · 剑', weapon: '长剑', hidden: true, skills: ['menghu_zs', 'potian_zs'],
    joinSect: ['mo_ms', 'mo_dj'], mod: { atk: 1.1, matk: 0.8, def: 1.05, mdef: 0.9, hp: 1.1, mp: 1.0 },
    heart: { name: '墨家心法', desc: '每级提升物攻 2 点', perLv: { atk: 2 } },
    auto: [['potian_zs', 'noVuln'], ['menghu_zs', 'always']], desc: '已拜入墨家、尚未选分堂的弟子，背负长剑。去猛士堂主或遁甲堂主处选定分堂。' });
  C.mo_ms_m = Object.assign({}, C.mo_ms, { gender: 'm' });
  C.mo_dj_m = Object.assign({}, C.mo_dj, { gender: 'm' });
  C.mo_dz_m = Object.assign({}, C.novice_m, C.mo_dz, { gender: 'm', joinSect: ['mo_ms_m', 'mo_dj_m'] });
  window.DATA.train.baseMo = ['menghu_zs', 'potian_zs'];
})();

// ---------- 神兽（参考原作 data_pet_defs 的 1221~1227 与 pet_skills_special；原作还有进阶形态 x8，这里先记名字） ----------
// 数据驱动：每只 = 图鉴条目 + 战斗倍率 mult + 出手表 act（[技能, 触发几率] 按顺序试）+ 被动 pas。技能描述原样取自原作，演示版数值已折算
(function () {
  const D = window.DATA;
  D.petQuality[5] = { name: '神兽', color: '#ff8a5a', stars: 5 };
  D.items.ss_soul = { name: '神兽之魂', icon: 'jade_charm', q: 4, type: 'mat', price: 10, desc: '神兽遗落世间的精魂，集齐后可到神兽使者处兑换神兽（原作需要 99 个，演示版 ' + 10 + ' 个）。' };
  D.ssCost = 10; D.ssRite = { coin: 800, min: 1, max: 3 };
  Object.assign(D.skills, {
    ss_hit:   { name: '神兽冲击', mp: 0, power: 1.1, target: 'one', ss: true, des: '以 110% 攻击冲击一名敌人。' },
    ss_sl_bw: { name: '神龙摆尾', mp: 0, power: 2.5, target: 'one', fear: 1, ss: true, des: '对目标造成 250% 攻击伤害，使对方恐惧（降低物防，叠加 3 层）。' },
    ss_kl:    { name: '亢龙有悔', mp: 0, power: 4.0, target: 'one', cd: 5, ss: true, des: '对目标造成 400% 攻击伤害，冷却 5 回合。（原作附带燃烧目标魔法）' },
    ss_qk:    { name: '乾坤逆转', mp: 0, power: 1.65, target: 'one', cd: 5, selfCost: 0.15, ss: true, des: '消耗 15% 最大血量，按 150% 转为物攻，以物攻 110% 对单体造成伤害，冷却 5 回合。' }
  });
  const base = { q: 5, ss: true, ai: 'ss', own: false, how: '集齐神兽之魂，到神兽使者处兑换' };
  D.petDex.ss_1227 = Object.assign({}, base, { name: '青龙仙童', adv: '苍龙仙童', sprite: 'b_ss_1227', show: 'pd_ss_1227', h: 142, kind: '神兽 · 物攻 · 迅捷',
    desc: '青龙座下的仙童，发间青鬃随水汽翻飞，周身缠着一缕龙灵。', mult: { hp: 0.75, atk: 0.9, def: 0.7, spd: 1.35 },
    act: [['ss_kl', 1], ['ss_sl_bw', 0.5], ['ss_hit', 1]], pas: { atkMul: 1.35, vsFear: 1.3, dbl: 0.15, revive: 0.6, take: 0.6 },
    pasList: [['龙族本能', '对恐惧状态目标伤害提升 30%，自身攻击提升 35%。'], ['神龙祝福', '致死时恢复 60% 生命（一场一次），每次攻击 15% 几率双倍伤害。'], ['龙鳞护体', '降低所受伤害（演示版 -40%）。原作：降低 60% 受伤，死亡后降低所有敌方物防。']] });
  D.petDex.ss_1221 = Object.assign({}, base, { name: '逆天魔龙', adv: '灭天魔龙', sprite: 'b_ss_1221', show: 'pd_ss_1221', h: 158, kind: '神兽 · 物攻 · 耐久',
    desc: '逆天而行的魔龙，紫鬃蓝鳞，盘踞云端，吐息间天象皆变。', mult: { hp: 0.9, atk: 0.8, def: 0.9, spd: 1.3 },
    act: [['ss_qk', 0.6], ['ss_hit', 1]], pas: { lowAtk: 0.2, ownerAtk: 0.2 },
    pasList: [['万魔归宗', '生命低于 30% 时，提升自身 20% 的攻击。'], ['邪龙附体', '死亡时为主人增加 20% 的攻击力。']] });
  D.npcs.ss_envoy = { name: '神兽使者', title: '【神兽】', color: '#ffb08a', sprite: 'kk_npc07', head: 'head_kk_npc07', act: 'ssEnvoy',
    idle: '神兽遗魂散落人间，聚齐了魂魄，我便能替你唤来神兽。凡人也可以出银两办一场神兽祭，求天神赐下魂魄。' };
  D.maps.town.npcs.push({ id: 'ss_envoy', at: [1156, 1129] });   // 神兽使者在汉中郡
})();

// 神兽技能 → 原作帧序列（序列名见 build.py 的 hd_pet：idle / atk / hit / dodge / skN，图集元数据在 ASSETS.pet）
window.DATA.petSeq = {
  b_ss_bs20: { attack: 'atk', bite: 'atk', foxfire: 'atk' },
  b_ss_1227: { ss_hit: 'atk', ss_sl_bw: 'sk2', ss_kl: 'sk3' },
  b_ss_1221: { ss_hit: 'atk', ss_qk: 'atk' }
};

// ---------- 另外 5 只神兽（原作 1222~1226）：技能 / 被动名称与数值取自原作 pet_skills_special，叠层类效果在演示版里折算 ----------
(function () {
  const D = window.DATA;
  Object.assign(D.skills, {
    ss_sy:  { name: '碎元剑诀', mp: 0, power: 2.0, target: 'one', hpCut: 0.05, ss: true, des: '对目标造成 200% 攻击伤害，并削减对方 5% 生命上限。' },
    ss_sx:  { name: '蛇啸焚天', mp: 0, power: 2.5, magic: true, target: 'one', mfear: 1, ss: true, des: '对目标造成 250% 法术伤害，并使其「离魂」：法防降低 10%，持续 3 回合，最多叠加 3 层。' },
    ss_tsy: { name: '天蛇噬血', mp: 0, power: 4.0, magic: true, target: 'one', cd: 5, guard: 2, ss: true, des: '对目标造成 400% 法术伤害，自身获得 2 次抵挡（免疫 2 次伤害），冷却 5 回合。（原作另有离魂转血量，演示版省略）' },
    ss_ts:  { name: '天魔噬魂', mp: 0, power: 0.8, magic: true, target: 'all', defDown: 0.3, ss: true, des: '对全体敌人造成 80% 法术伤害，并使其物防降低 30%，持续 3 回合。' },
    ss_sg:  { name: '双龙逆天', mp: 0, power: 1.8, target: 'one', critUp: 50, critAtk: true, ss: true, des: '暴击率提高 50%，以 180% 攻击斩向单体；暴击后自身攻击提升 15%，最多叠加 3 次。' },
    ss_zw:  { name: '真武御体', mp: 0, target: 'self', cd: 5, absorb: 2, ss: true, des: '给自己加持血盾：接下来 2 次受到的伤害转化为治疗。冷却 5 回合。' },
    ss_th:  { name: '天火燎原', mp: 0, power: 4.0, magic: true, target: 'one', cd: 5, baoti: true, ss: true, des: '对目标造成 400% 法术伤害，并消耗全部「霸体」层数，每层使伤害提升 10%。冷却 5 回合。' },
    ss_dy:  { name: '都天炎爆', mp: 0, power: 2.5, magic: true, target: 'one', curse: true, ss: true, des: '对目标造成 250% 法术伤害，使其陷入「天罚」：受到伤害 +10%、攻击 -10%，持续 2 回合，最多叠加 2 层。' }
  });
  const base = { q: 5, ss: true, ai: 'ss', own: false, how: '集齐神兽之魂，到神兽使者处兑换' };
  D.petDex.ss_1222 = Object.assign({}, base, { name: '惑天玄姬', adv: '祸天狐仙', sprite: 'b_ss_1222', show: 'pd_ss_1222', h: 160, kind: '神兽 · 物攻 · 迅捷',
    desc: '九尾狐族的玄姬，立于紫色法阵之上，卷轴所指之处，万剑齐发。', mult: { hp: 0.7, atk: 0.95, def: 0.6, spd: 1.2 },
    act: [['ss_sy', 1], ['ss_hit', 1]], pas: { dmgUp: 0.3, deathWard: 1 },
    pasList: [['万剑归宗', '造成的伤害额外提升 30%。'], ['绝杀咒印', '受到致死伤害时保留 1 点生命并无敌一回合，之后伤害提升 50%（一场一次）。']] });
  D.petDex.ss_1223 = Object.assign({}, base, { name: '小青娘子', adv: '千年青蛇', sprite: 'b_ss_1223', show: 'pd_ss_1223', h: 150, kind: '神兽 · 法攻 · 控制', mag: true,
    desc: '蛇身人面的青衣娘子，手持葫芦灯，吐息如焰，眼波能定人魂魄。', mult: { hp: 0.85, atk: 1.0, def: 0.7, spd: 1.1 },
    act: [['ss_tsy', 1], ['ss_sx', 1], ['ss_hit', 1]], pas: { lethalStun: 1 },
    pasList: [['蛊心定影', '受到致死伤害时，使对方定身一回合，期间其受到的伤害翻倍（一场一次；演示版中她自己仍会倒下）。']] });
  D.petDex.ss_1224 = Object.assign({}, base, { name: '九天玄女', adv: '九天圣女', sprite: 'b_ss_1224', show: 'pd_ss_1224', h: 150, kind: '神兽 · 法攻 · 辅助', mag: true,
    desc: '头戴金焰光环的玄女，红袍广袖，挥袖间魔光如潮。', mult: { hp: 0.9, atk: 0.85, def: 0.8, spd: 1.0 },
    act: [['ss_ts', 1], ['ss_hit', 1]], pas: { take: 0.6, dodgeUp: 20, revive: 1, reviveAtk: 0.2, reviveText: '移魂转命' },
    pasList: [['九天魔甲', '受到的伤害减少 40%，闪避提高 20%。'], ['移魂转命', '致死时恢复全部生命并提升 20% 法攻（一场一次）。']] });
  D.petDex.ss_1225 = Object.assign({}, base, { name: '龙翔天兵', adv: '龙翔神将', sprite: 'b_ss_1225', show: 'pd_ss_1225', h: 150, kind: '神兽 · 物攻 · 坚韧',
    desc: '金甲红缨的天兵，长枪所指，火焰随行。', mult: { hp: 0.85, atk: 0.9, def: 0.9, spd: 1.0 },
    act: [['ss_zw', 1], ['ss_sg', 1], ['ss_hit', 1]], pas: { dbl: 0.2, hurtStack: { step: 0.1, max: 4 } },
    pasList: [['神鬼俱焚', '每次攻击有 20% 几率造成双倍伤害。'], ['仙魔真诀', '每受到一次伤害，伤害减免提高 10%，最多叠加 4 次。']] });
  D.petDex.ss_1226 = Object.assign({}, base, { name: '龙翔帝君', adv: '龙翔大帝', sprite: 'b_ss_1226', show: 'pd_ss_1226', h: 165, kind: '神兽 · 法攻 · 霸体', mag: true,
    desc: '浑身燃着烈焰的帝君，一柄巨斧劈开天火，脚下炎环不灭。', mult: { hp: 1.0, atk: 1.0, def: 0.9, spd: 0.9 },
    act: [['ss_th', 1], ['ss_dy', 1], ['ss_hit', 1]], pas: { baoti: 1, baotiStart: 5, atkMul: 1.3, revive: 0.6, reviveText: '混元霸体', hurtAtk: { step: 0.1, max: 10 } },
    pasList: [['烈战狂歌', '开场获得 5 层霸体，攻击提升 30%。'], ['混元霸体', '每回合获得 1 层霸体（最多 5 层），每层使受到的伤害 -10%；致死时恢复 60% 生命（一场一次）。'], ['九域神威', '每受到一次伤害，攻击提升 10%，最多叠加 10 次。']] });
})();

// ---- 百炼玄兵塔：7 层 × 10 名守卫（第 10 名是本层首领）。守卫名字取自原服 blxt_1～blxt_70（封神法宝）。----
;(function () {
  const D = window.DATA;
  const names = ["离火剑", "散瘟鞭", "落宝金钱", "列瘟印", "紫金铃", "撞心杵", "风火轮", "酱油瓶", "乾坤针", "斩仙飞刀", "风袋", "梅花镖", "六根清净竹", "雾露乾坤网", "戳目珠", "照妖鉴", "长生根", "伤不旗", "逆鳞枪", "宝莲灯", "万里起云烟", "钻心钉", "万鸦壶", "紫金钵", "定风珠", "魔神甲", "穿天弩", "咆哮梯", "金光锉", "五行旗", "乱心尘", "阴阳二气瓶", "劈地珠", "杏黄旗", "阴阳刃", "穿心锁", "三尖两刃枪", "浮云", "金霞冠", "伏羲琴", "落魂钟", "焰光旗", "化血神刀", "日月珠", "定海珠", "破军", "开天珠", "混元锤", "火星帖", "翻天印", "四象塔", "天荡", "降魔杵", "乾坤圈", "如意乾坤袋", "捆仙绳", "招妖幡", "杯具", "水火锋", "苍刑逆天枪", "黑砂", "混元幡", "乾坤弓", "落魄镜", "听谛印", "照天印", "遁龙桩", "鸭梨", "缚龙索", "混沌钟"];
  const floors = [
    { name: '炼器大殿', desc: '炉火不熄的炼器大殿，兵器架上插满百炼的神兵。' },
    { name: '云玉阁', desc: '浮在云海里的玉石楼阁，风里带着莲香。' },
    { name: '幽冥魔殿', desc: '紫焰幽幽的黑曜石殿，乌鸦的黑羽落了一地。' },
    { name: '阴阳八卦殿', desc: '地上嵌着巨大的太极八卦，一黑一白，此消彼长。' },
    { name: '日月星辰殿', desc: '一半是金日，一半是银月，头顶星光流转。' },
    { name: '乾坤金塔', desc: '红绸金柱，佛光流转，每一口钟都有人高。' },
    { name: '混沌天阙', desc: '塔顶悬在混沌星海之中，一口古钟镇着整座塔。' }
  ];
  D.tower = { minLv: 15, floors, names };
  // 7 张塔层地图：坐标取自 1448×1086 的原图，换算成 2000×1500。每层一张不同的图，布局一致（左下入口、中间大厅、上方首领台）。
  const K = 2000 / 1448, c = p => [Math.round(p[0] * K), Math.round(p[1] * K)];
  const base = {
    poly: [[220, 420], [480, 320], [640, 310], [1050, 310], [1250, 340], [1350, 420], [1370, 560], [1250, 700], [1030, 860], [800, 950], [600, 930], [440, 880], [330, 800], [180, 700], [70, 560]],
    dais: [[620, 190], [1040, 190], [1060, 330], [620, 330]],
    grid: [[560, 690], [790, 690], [1020, 690], [560, 560], [790, 560], [1020, 560], [560, 430], [790, 430], [1020, 430]],
    boss: [800, 230], start: [520, 840], tp: [420, 800]
  };
  const over = {
    3: { boss: [790, 200] }, 4: { boss: [760, 175], dais: [[640, 130], [900, 130], [920, 330], [620, 330]] }, 5: { boss: [740, 215] }, 7: { boss: [850, 255] }
  };
  floors.forEach((fl, i) => {
    const f = i + 1, s = Object.assign({}, base, over[f] || {});
    D.maps['tower' + f] = { name: '玄兵塔·' + ['一', '二', '三', '四', '五', '六', '七'][i] + '层', note: fl.name, home: 'town', img: 'bg_tower' + f, mini: 'mini_tower' + f, iw: 2000, ih: 1500, scale: 1.2, k: 1,
      indoor: true, tower: f, start: c(s.start), alt: fl.desc, roads: [], blocks: [], glows: [],
      walk: [s.poly.map(c), s.dais.map(c)], slots: s.grid.concat([s.boss]).map(c), npcs: [{ id: 'towertp', at: c(s.tp) }] };
  });
  const pool = ['grunt', 'f_spear', 'f_tao', 'f_panther'], mSpr = ['boar', 'wolf', 'f_tortoise', 'f_dragon', 'boar', 'wolf', 'f_dragon'];
  const H = { grunt: 108, f_spear: 136, f_tao: 132, f_panther: 92, boss: 150 };
  for (let f = 1; f <= 7; f++) D.enemies['blxt_m' + f] = { name: ['火', '云', '幽', '阴阳', '星', '金', '混沌'][f - 1] + '灵', sprite: mSpr[f - 1], pet: true, h: 70, hp: 0, atk: 0, def: 0, spd: 0, exp: 0, coin: 0, drops: [] };
  for (let n = 1; n <= 70; n++) {
    const f = Math.ceil(n / 10), boss = n % 10 === 0, spr = boss ? 'boss' : pool[n % pool.length];
    const hp = Math.round(900 + 140 * n), atk = Math.round(110 + 5.2 * n), def = Math.round(26 + 1.8 * n), spd = 82 + Math.round(n * 0.5), k = boss ? [2.4, 1.25, 1.2] : [1, 1, 1];
    D.enemies['blxt_' + n] = { name: names[n - 1], sprite: spr, h: H[spr], hp: Math.round(hp * k[0]), atk: Math.round(atk * k[1]), def: Math.round(def * k[2]), spd, exp: 300, coin: boss ? 400 : 120,
      drops: boss ? [['ore', 1], ['mp_potion', 1], ['hp_potion', 1]] : [['hp_potion', 0.5]], boss };
    const m = D.enemies['blxt_m' + f]; if (n % 10 === 1) { m.hp = Math.round(hp * 0.4); m.atk = Math.round(atk * 0.6); m.def = Math.round(def * 0.7); m.spd = spd + 8; }
    D.stages.push({ id: 'blxt_' + n, tower: true, n, floor: f, name: names[n - 1], rec: Math.round(14 + n * 0.55),
      enemies: ['blxt_' + n, 'blxt_m' + f, 'blxt_m' + f], desc: '玄兵塔第' + f + '层的守卫「' + names[n - 1] + '」' + (boss ? '，是本层首领。' : '。') });
  }
})();
