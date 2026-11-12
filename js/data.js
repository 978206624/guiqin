// ============================================================
// 数据表：数值、物品、技能、敌人、关卡、任务、商店。
// 这里没有任何逻辑，移植到别的引擎时可以原样转成 JSON / 配置表。
// ============================================================
window.DATA = {
  // 主角属性 = base + grow * 等级；灵狐属性 = 主角裸属性 * pet 系数
  base: { hp: 800, mp: 300, atk: 80, def: 30, spd: 90 },
  grow: { hp: 120, mp: 40, atk: 14, def: 6, spd: 2 },
  pet:  { hp: 0.6, atk: 0.7, def: 0.6, spd: 1.2 },
  expNeed: { base: 150, perLevel: 90 },        // 升到下一级所需经验 = base + perLevel * 当前等级
  start: { lv: 1, coin: 100, items: { hp_potion: 3 }, equip: { weapon: null, armor: 'cloth_armor', charm: null }, map: 'village' },

  // 可选职业：男女各三种。stats 是创建界面上的五维条（1-5），只用于展示
  classes: {
    m_sword: { name: '游侠', gender: 'm', role: '近战 · 剑',   desc: '仗剑行走江湖的侠客，攻守均衡，出手干净利落。', stats: { 攻击: 4, 防御: 3, 速度: 4, 法术: 2, 气血: 3 } },
    m_spear: { name: '锐士', gender: 'm', role: '近战 · 戟',   desc: '大秦锐士，披坚执锐。气血和防御最高，适合顶在最前面。', stats: { 攻击: 3, 防御: 5, 速度: 2, 法术: 1, 气血: 5 } },
    m_mage:  { name: '方士', gender: 'm', role: '远程 · 法术', desc: '通晓阴阳五行的方士，以符箓和铜镜施法，擅长群体攻击。', stats: { 攻击: 2, 防御: 2, 速度: 3, 法术: 5, 气血: 2 } },
    f_sword: { name: '剑姬', gender: 'f', role: '近战 · 剑',   desc: '剑舞一脉的传人，身法最快，连击和暴击是她的拿手好戏。', stats: { 攻击: 4, 防御: 2, 速度: 5, 法术: 2, 气血: 3 } },
    f_heal:  { name: '医者', gender: 'f', role: '辅助 · 治疗', desc: '悬壶济世的游方医者，能为队友疗伤解毒，队伍里少不了她。', stats: { 攻击: 1, 防御: 3, 速度: 3, 法术: 4, 气血: 4 } },
    f_bow:   { name: '羽林', gender: 'f', role: '远程 · 弓',   desc: '百步穿杨的神射手，专挑残血的敌人下手，单体伤害最高。', stats: { 攻击: 5, 防御: 2, 速度: 4, 法术: 1, 气血: 2 } }
  },

  items: {
    hp_potion:     { name: '金创药',   icon: 'hp_potion',     type: 'potion', price: 50,  hp: 600, desc: '恢复 600 点气血。' },
    mp_potion:     { name: '回气丹',   icon: 'mp_potion',     type: 'potion', price: 60,  mp: 300, desc: '恢复 300 点法力。' },
    iron_sword:    { name: '精铁剑',   icon: 'iron_sword',    type: 'equip',  slot: 'weapon', price: 300, atk: 40, desc: '王铁匠亲手淬火的好剑。攻击 +40' },
    steel_sword:   { name: '玄铁剑',   icon: 'steel_sword',   type: 'equip',  slot: 'weapon', price: 900, atk: 95, desc: '玄铁所铸，寒光逼人。攻击 +95' },
    cloth_armor:   { name: '布衣',     icon: 'cloth_armor',   type: 'equip',  slot: 'armor',  price: 80,  def: 15, desc: '寻常布衣。防御 +15' },
    leather_armor: { name: '皮甲',     icon: 'leather_armor', type: 'equip',  slot: 'armor',  price: 500, def: 50, hp: 200, desc: '鞣制皮甲。防御 +50，气血上限 +200' },
    sachet:        { name: '百花香囊', icon: 'sachet',        type: 'equip',  slot: 'charm',  price: 400, hp: 300, spd: 10, desc: '逍遥子所赠。气血上限 +300，速度 +10' },
    ore:           { name: '玄铁矿',   icon: 'ore',           type: 'mat',    price: 120, desc: '打造兵器的材料，可以卖给商人。' },
    wolf_fang:     { name: '狼牙',     icon: 'wolf_fang',     type: 'mat',    price: 40,  desc: '野狼的牙，可以卖给商人。' },
    lantern:       { name: '花灯',     icon: 'lantern',       type: 'mat',    price: 0,   desc: '王铁匠托你捎给逍遥子的花灯。' }
  },

  // power：伤害倍率；target：one 单体 / all 全体 / self 自己；heal：按气血上限比例回复
  skills: {
    attack: { name: '攻击',     mp: 0,   power: 1.0, target: 'one',  desc: '普通一击。' },
    pokong: { name: '天剑·破空', mp: 60,  power: 1.9, target: 'one',  desc: '凝气于剑，重创一名敌人。', lv: 1 },
    zongheng:{ name: '剑气纵横', mp: 110, power: 1.15, target: 'all', desc: '剑气四散，攻击全体敌人。', lv: 6 },
    ningshen:{ name: '凝神诀',   mp: 80,  heal: 0.35, target: 'self', desc: '调息凝神，回复 35% 气血。', lv: 7 },
    bite:   { name: '撕咬',     mp: 0,   power: 1.0, target: 'one' },
    foxfire:{ name: '狐火',     mp: 0,   power: 1.6, target: 'one', fire: true },
    // 队友和对手用的招式（由 ai 类型自动选用）
    heal_ally:{ name: '回春术', mp: 0, heal: 0.22, target: 'ally' },
    bolt:   { name: '飞花',     mp: 0,   power: 1.2, target: 'one' },
    rain:   { name: '落英缤纷', mp: 0,   power: 0.7, target: 'all' },
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
    boss:  { name: '山贼头目', sprite: 'boss',  h: 150, hp: 4000, atk: 255, def: 70, spd: 85,  exp: 900, coin: 600, drops: [['ore', 1], ['mp_potion', 1]], boss: true }
  },

  stages: [
    { id: 'dummy',   name: '练武场木桩', rec: 1, enemies: ['dummy'],               desc: '一动不动的稻草人，拿来练手正好。' },
    { id: 'boar',    name: '菜地野猪', rec: 2, enemies: ['boar', 'boar'],          desc: '两头拱菜地的野猪。', need: 'dummy' },
    { id: 'path',    name: '竹林小径', rec: 5, enemies: ['grunt', 'grunt'],          desc: '两个拦路的山贼喽啰。' },
    { id: 'wolves',  name: '狼群出没', rec: 6, enemies: ['wolf', 'wolf', 'wolf'],    desc: '三头饿狼，速度很快。', need: 'path' },
    { id: 'outpost', name: '山寨前哨', rec: 7, enemies: ['grunt', 'wolf', 'grunt'],  desc: '山贼和他们养的狼。',   need: 'wolves' },
    { id: 'boss',    name: '山贼头目', rec: 8, enemies: ['grunt', 'boss', 'grunt'],  desc: '头目每三回合会使出横扫千军。', need: 'path', needStep: 9 }
  ],

  // 主线：talk = 找人对话；win = 打赢指定关卡
  quests: [
    { type: 'talk', npc: 'elder', title: '初出茅庐', goal: '和村长说话',
      say: '孩子，你在恒山学艺也有些年头了，是时候下山闯荡了。走之前，先去练武场拿木桩活动活动筋骨。', reward: { exp: 240, items: { hp_potion: 2 } } },
    { type: 'win', stage: 'dummy', title: '热身', goal: '去村西练武场打倒木桩' },
    { type: 'win', stage: 'boar', title: '菜地之患', goal: '赶走村西菜地边的野猪' },
    { type: 'talk', npc: 'elder', title: '下山', goal: '回去告诉村长',
      say: '好，有模有样了。拿着这封信从村东下山去汉中郡，到聚贤楼找店小二。记住，点右上角的小地图能打开舆图，去过的地方可以直接传送。', reward: { exp: 700, coin: 150 } },
    { type: 'talk', npc: 'waiter', title: '侠客初成', goal: '到汉中郡聚贤楼找店小二',
      say: '少侠是恒山村来的吧？村长的信掌柜的收到了。王铁匠那儿给您留了一把剑，您亲自去取吧。', reward: { exp: 80 } },
    { type: 'talk', npc: 'smith', title: '取剑', goal: '去铁匠铺找王铁匠',
      say: '剑已经淬好了，削铁如泥，记得在背包里装备上。对了，替我把这盏花灯捎给亭子边的逍遥子姑娘。', reward: { exp: 80, items: { iron_sword: 1, lantern: 1 } } },
    { type: 'talk', npc: 'girl', title: '花灯传情', goal: '把花灯交给逍遥子',
      say: '呀，好漂亮的花灯！这枚香囊送你。不过……桥那边的竹林最近有山贼拦路，少侠能去看看吗？', reward: { exp: 100, items: { sachet: 1, hp_potion: 2 } }, take: { lantern: 1 } },
    { type: 'win', stage: 'path', title: '竹林除害', goal: '出城，击败竹林小径上的山贼' },
    { type: 'talk', npc: 'waiter', title: '回报消息', goal: '回聚贤楼告诉店小二',
      say: '少侠好身手！不过喽啰好打，他们的头目还在山寨里。掌柜的出三百两，请少侠除了这个祸害。', reward: { exp: 150, coin: 300 } },
    { type: 'win', stage: 'boss', title: '擒贼擒王', goal: '出城击败山贼头目（建议 8 级）' },
    { type: 'talk', npc: 'girl', title: '灯会如常', goal: '把好消息告诉逍遥子',
      say: '山贼散了？太好了，今晚的灯会终于可以安心办了。少侠，江湖路远，后会有期。', reward: { exp: 400, coin: 500, items: { ore: 2 } } }
  ],

  npcs: {
    elder:  { name: '村长',   title: '【恒山村】', idle: '山下不比村里，凡事多留个心眼。累了就回来歇歇。' },
    axiu:   { name: '阿秀',   color: '#a8e6ff', idle: '我刚采了草药，熬了一锅汤，少侠喝一碗再走吧。', rest: true, restMsg: '喝了一碗草药汤，气血法力回满' },
    waiter: { name: '店小二', idle: '客官辛苦，喝壶热茶歇歇脚吧。', rest: true, restMsg: '喝了一壶热茶，气血法力回满' },
    smith:  { name: '王铁匠', idle: '打铁还需自身硬。少侠看看有什么用得上的。', shop: 'smith' },
    girl:   { name: '逍遥子', title: '【百花谷】', color: '#dca8ff', titleColor: '#ffa8d0', idle: '今晚灯会的灯谜好难……你也是来猜谜的吗？' },
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
      name: '恒山村', img: 'bg_village', mini: 'mini_village', iw: 1672, ih: 941, scale: 1.6, k: 1.3933, start: [560, 358],
      alt: '清晨的山村：茅草屋围着一口老井和一棵大松树，村西是练武场和菜地，村东过桥是下山的寨门',
      world: [250, 330], note: '新手村', home: 'village', battleBg: 'bg_battle2',
      walk: [],
      roads: [
        { r: 22, pts: [[5,228],[70,258],[150,287],[240,300],[340,292],[430,285],[500,310]] },
        { r: 44, pts: [[505,340],[580,356],[655,358]] },
        { r: 20, pts: [[655,358],[770,362],[850,375],[900,385],[960,390],[1020,385],[1075,375],[1110,372]] },
        { r: 18, pts: [[1110,372],[1150,410],[1165,460],[1150,510]] },
        { r: 16, pts: [[520,300],[520,248]] },
        { r: 18, pts: [[530,385],[460,415],[390,438],[330,440]] },
        { r: 30, pts: [[130,425],[220,432],[310,440]] }
      ],
      blocks: [{ at: [557, 322], r: 30 }, { at: [640, 318], r: 34 }],      // 老井、大松树
      glows: [[1068,340],[1140,330]],
      npcs: [{ id: 'elder', at: [548, 272] }, { id: 'axiu', at: [640, 372] }],
      portals: [{ to: 'town', name: '下山·汉中郡', at: [1150, 500], arrive: [110, 845], needStep: 4, lockMsg: '村长还有话要交代，先别急着下山' }],
      foes: [{ stage: 'dummy', at: [215, 428] }, { stage: 'boar', at: [95, 262] }]
    },
    town: {
      name: '汉中郡', world: [467, 765], note: '主城', home: 'town', img: 'bg_town', mini: 'mini_town', iw: 1024, ih: 1536, scale: 1.25, k: 1, petals: true, start: [512, 820],
      alt: '黄昏灯会中的古镇广场：红顶客栈、铁匠铺、凉亭、荷塘和石桥',
      walk: [
        [[330,640],[500,600],[520,560],[640,560],[650,610],[700,640],[690,760],[760,800],[840,830],[850,930],[760,980],[640,990],[420,990],[330,960],[260,980],[250,890],[60,860],[60,830],[330,800]],
        [[262,975],[332,975],[335,1110],[400,1235],[418,1330],[470,1402],[880,1412],[880,1500],[420,1500],[330,1400],[290,1330],[285,1150],[262,1100]]
      ],
      glows: [[75,225],[430,255],[715,410],[985,300],[225,690],[515,545],[1000,560],[665,1010],[520,1170],[150,1050],[300,560]],
      npcs: [{ id: 'waiter', at: [590, 618] }, { id: 'smith', at: [300, 792] }, { id: 'girl', at: [735, 872] }, { id: 'vendor', at: [262, 962] }],
      portals: [{ to: 'wild', name: '出城·竹林', at: [832, 895], arrive: [150, 453] }, { to: 'village', name: '回恒山村', at: [78, 845], arrive: [1085, 376] }]
    },
    wild: {
      name: '城外·竹林', world: [698, 1265], note: '山贼出没', home: 'town', img: 'bg_wild', mini: 'mini_wild', iw: 1672, ih: 941, scale: 2, k: 1.6328, start: [150, 453],
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
      portals: [{ to: 'town', name: '回汉中郡', at: [40, 515], arrive: [790, 900] }],
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
      { name: '云中客', sprite: 'b_player', me: true,      mult: { atk: 1.1 },                                  pet: { name: '灵狐', sprite: 'b_fox',    h: 62, ai: 'fox' } },
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
    smith:  { name: '王记铁匠铺', sell: ['iron_sword', 'steel_sword', 'leather_armor'] }
  }
};
