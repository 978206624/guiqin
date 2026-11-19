#!/usr/bin/env python3
# 50 级主线 8 张新地图：把 art-src/maps/<id>.png 放大到 2400 宽，生成 assets/bg_<id>.webp + mini 图，
# 用下面手工描的可走区域 / 障碍 / 传送点生成 js/data.js 里的地图配置（夹在 // <story-maps> 标记之间），并做可达性检查。
# 用法：python3 tools/story_maps.py            生成素材 + 配置 + 检查（叠图预览写到 $PREVIEW_DIR，默认 /tmp/story-maps）
#       python3 tools/story_maps.py --check    只检查 + 出叠图，不写文件
# 规格里的坐标一律是“预览坐标”（图宽 1200 的缩略图坐标），脚本乘以 2 变成数据坐标（= 放大后的底图像素）。
import os, sys, json, re
from PIL import Image, ImageDraw
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
PREV = os.environ.get('PREVIEW_DIR', '/tmp/story-maps'); os.makedirs(PREV, exist_ok=True)
W = 2400; F = W / 1200.0
CHECK = '--check' in sys.argv

# road: {r, pts} 走廊（半宽 r）；walk: 多边形；blocks: (x, y, r) 椭圆障碍（竖向压扁 0.6，和游戏一致）
# portals: (to, name, at, arrive)；start 出生点；exit/back 只是备注
MAPS = {
 'hejiang': dict(
  name='合江村', note='两河交汇', world=[1180, 1900],
  alt='两河交汇的小村：木拱桥横跨河面，岸边是茅屋、船坞和菜地，土路从河岸斜向远处',
  walk=[
   [(0,900),(0,700),(215,690),(240,650),(300,590),(330,520),(400,520),(500,545),(560,580),(640,620),(740,640),(800,660),(900,690),(1000,740),(1060,790),(1130,830),(1200,850),(1200,900)],
   [(440,330),(470,235),(560,172),(900,172),(1000,110),(1100,45),(1200,30),(1200,330),(1100,385),(960,435),(840,420),(740,395),(700,380),(600,350),(520,330)],
  ],
  roads=[dict(r=48, pts=[(400,505),(560,460),(690,410)])],
  blocks=[(885,830,32),(360,830,75),(1040,255,75),(1120,255,60),(870,322,36)],
  start=(150,790),
  portals=[('mist','回云雾谷',(80,830),None),('pine','回石松林',(250,850),None),('qianling','入黔岭古道',(1130,75),None)],
  arrive={'mist':(150,800),'pine':(300,830),'qianling':(1060,110)}),
 'qianling': dict(
  name='黔岭古道', note='险道通幽', world=[1180, 1900],
  alt='云雾缭绕的险峻山岭：土路沿崖壁盘旋，栈道和索桥横跨深谷，对面是一片开阔的高原草甸',
  walk=[[(684,114),(768,84),(912,72),(1080,48),(1200,36),(1200,897),(924,897),(888,828),(858,744),(834,672),(798,600),(780,528),(816,474),(894,446),(912,396),(840,360),(768,324),(720,276),(672,240),(648,180)]],
  roads=[dict(r=42, pts=[(0,800),(120,750),(230,692),(330,648),(390,610),(380,560),(340,500),(380,455),(470,430),(560,430),(680,415),(780,395),(880,390),(912,396)])],
  blocks=[(954,54,25),(1116,258,36),(1152,204,30),(1140,540,34),(1152,618,36),(1128,804,42),(1140,40,40)],
  start=(110,740), portals=[], arrive={}),
 'anyang': dict(
  name='安阳废墟', note='残垣断壁', world=[1180, 1900],
  alt='倾颓的古城废墟：断墙、倒柱和焦木之间一条黄土路，右上角是强盗扎下的营寨',
  walk=[
   [(0,0),(240,0),(262,112),(392,142),(412,262),(562,285),(602,338),(722,388),(862,388),(872,300),(882,172),(1200,162),(1200,282),(1002,292),(992,402),(902,437),(962,547),(1200,567),(1200,900),(880,900),(842,792),(782,690),(710,590),(620,545),(500,572),(482,592),(360,600),(250,548),(180,470),(0,440)],
  ],
  roads=[],
  blocks=[(155,320,50),(205,410,130),(440,520,80),(45,190,55),(45,262,50),(510,215,110),(765,340,40),(1055,195,55),(1000,240,45),(1050,440,70)],
  start=(1060,790),
  portals=[('qianling','回黔岭古道',(1120,820),None),('handan','入邯郸城',(70,50),None)],
  arrive={'qianling':(1060,790),'handan':(70,50)}),
 'handan': dict(
  name='邯郸城', note='城内繁华', world=[1180, 1900],
  alt='',
  walk=[
   [(470,190),(520,150),(700,150),(850,170),(880,300),(860,380),(700,420),(400,420),(350,380),(340,310),(470,300)],
   [(700,500),(900,480),(1180,470),(1200,684),(740,684),(715,600)],
   [(880,0),(1060,0),(1030,130),(1000,300),(990,400),(900,470),(830,420),(850,300),(870,150)],
  ],
  roads=[dict(r=60, pts=[(0,455),(300,455),(600,455),(720,440),(800,380),(850,300),(900,200),(960,100),(1000,0)])],
  blocks=[(545,300,55),(690,215,50),(720,345,45),(480,335,30),(530,170,40),(395,330,18),(790,285,20)],
  start=(100,455), portals=[], arrive={}),
 'shangshan': dict(
  name='殇山', note='祭地阴森', world=[1180, 1900],
  alt='',
  walk=[
   [(0,150),(120,140),(250,190),(330,280),(380,330),(330,420),(240,490),(130,520),(0,520)],
   [(380,300),(500,290),(600,330),(740,350),(840,400),(860,480),(760,560),(620,640),(430,690),(260,700),(190,640),(150,540),(250,500),(330,420)],
  ],
  roads=[dict(r=70, pts=[(1130,40),(1060,130),(1010,230),(950,320),(890,400),(850,480),(780,540),(660,600),(520,640),(380,680),(250,720),(120,760),(0,790)])],
  blocks=[(270,215,50),(50,450,50),(580,565,65),(630,240,45),(985,520,15)],
  start=(1040,160), portals=[], arrive={}),
 'baishou': dict(
  name='百兽谷', note='兽骨遍野', world=[1180, 1900],
  alt='',
  walk=[[(960,60),(1200,50),(1200,200),(1100,215),(1000,175)]],
  roads=[dict(r=95, pts=[(20,840),(180,740),(330,650),(470,570),(600,500),(720,430),(800,360),(900,310),(1000,260),(1090,190),(1150,110),(1200,70)])],
  blocks=[(1110,350,60),(440,700,70)],
  start=(60,810), portals=[], arrive={}),
 'yinlong': dict(
  name='隐龙山', note='荒山', world=[1180, 1900],
  alt='光秃秃的荒山：风化的灰褐岩石和枯草之间只有一条被踩实的土路，盘旋向上通往山顶的岩台',
  walk=[[(0,0),(390,0),(380,70),(300,110),(200,150),(100,130),(0,130)]],
  roads=[dict(r=65, pts=[(1200,760),(1100,720),(1000,665),(900,610),(800,555),(700,515),(590,490),(510,430),(440,365),(380,295),(345,235),(320,175),(300,130),(250,95),(100,55)])],
  blocks=[],
  start=(1090,750), portals=[], arrive={}),
 'qingyang': dict(
  name='青阳古城', note='古城外街', world=[1180, 1900],
  alt='',
  walk=[
   [(0,0),(60,0),(130,55),(400,185),(560,290),(700,355),(860,420),(1000,465),(1200,540),(1200,590),(1050,545),(900,520),(780,555),(700,610),(560,684),(440,684),(420,600),(330,560),(300,470),(330,420),(300,330),(200,240),(0,230)],
  ],
  roads=[],
  blocks=[(75,140,20),(335,380,20),(555,200,20)],
  start=(120,80), portals=[], arrive={}),

 # ---- 紫阴古城（50 级副本，4 层）：底图 art-src/maps/zy<n>.png，World 无坐标 → 不上世界地图 ----
 'zy1': dict(
  name='紫阴古城·古城外围', note='时空缝隙', world=None, indoor=True,
  alt='悬浮在紫黑虚空里的石板长道，两侧断裂坠落，兽首石门通向更深处',
  walk=[[(0,10),(60,0),(180,60),(300,125),(420,180),(500,225),(585,285),(660,325),(750,332),(870,330),(1000,325),(1100,340),(1200,345),(1200,684),(600,684),(560,600),(545,520),(545,430),(480,380),(420,340),(340,290),(260,250),(190,200),(100,150),(0,110)]],
  roads=[], blocks=[(1150,385,35),(655,575,38)],
  start=(90,80), portals=[], arrive={}),
 'zy2': dict(
  name='紫阴古城·紫阴废墟', note='断壁残垣', world=None, indoor=True,
  alt='坍塌的殿宇和石牢，裂缝里冒出黑烟，街道断断续续悬在虚空上',
  walk=[[(0,330),(150,325),(300,310),(420,270),(520,248),(610,225),(660,175),(720,130),(800,95),(900,70),(1000,60),(1100,40),(1200,50),(1200,190),(1090,170),(1000,160),(900,175),(850,215),(790,260),(700,330),(660,440),(690,480),(780,510),(880,520),(960,545),(1000,570),(980,600),(880,605),(790,595),(700,585),(620,570),(500,568),(400,545),(300,520),(200,520),(100,520),(0,540)]],
  roads=[], blocks=[(560,520,45),(765,125,35),(330,385,25),(940,140,25)],
  start=(60,430), portals=[], arrive={}),
 'zy3': dict(
  name='紫阴古城·怨魂之地', note='荒坟野地', world=None, indoor=True,
  alt='悬在虚空里的荒坟，歪倒的石碑和铁链，苍白的幽魂在枯树间游荡',
  walk=[[(60,520),(120,440),(200,400),(300,430),(330,330),(430,330),(560,300),(640,330),(700,300),(760,240),(820,150),(820,60),(850,0),(1000,0),(1050,60),(1100,140),(1200,170),(1200,360),(1080,420),(1000,480),(900,490),(760,500),(700,550),(580,590),(500,640),(400,684),(0,684),(0,560)]],
  roads=[], blocks=[(1100,430,40),(730,440,35),(785,445,22),(940,230,22),(1005,250,30),(1070,115,22),(1165,165,22),(925,420,20)],
  start=(100,600), portals=[], arrive={}),
 'zy4': dict(
  name='紫阴古城·紫阴中枢', note='封印之心', world=None, indoor=True,
  alt='圆形祭坛上血红与紫色的法阵，黑色方尖碑环立，兽角王座高踞一侧',
  walk=[[(0,290),(120,330),(220,350),(260,300),(330,240),(430,195),(560,175),(700,165),(820,185),(900,230),(980,290),(1060,360),(1100,430),(1060,520),(960,580),(840,610),(700,625),(560,620),(450,600),(350,550),(290,500),(250,430),(160,400),(60,360),(0,340)]],
  roads=[], blocks=[(290,415,35),(945,585,35),(490,575,40)],
  start=(60,320), portals=[], arrive={}),
}

LINKS = {
 'hejiang':  dict(start=(150,790), portals=[('mist','回云雾谷',(60,840),None),('pine','回石松林',(190,870),None),('qianling','入黔岭古道',(1130,75),None)],
                  arrive={'mist':(150,790),'pine':(240,800),'qianling':(1060,110)}),
 'qianling': dict(start=(110,740), portals=[('hejiang','回合江村',(40,800),None),('anyang','入安阳废墟',(1100,125),None)],
                  arrive={'hejiang':(110,745),'anyang':(1020,140)}),
 'anyang':   dict(start=(1060,780), portals=[('qianling','回黔岭古道',(1120,820),None),('handan','入邯郸城',(70,50),None)],
                  arrive={'qianling':(1060,780),'handan':(150,100)}),
 'handan':   dict(start=(130,455), portals=[('anyang','回安阳废墟',(50,455),None),('shangshan','入殇山',(960,50),None)],
                  arrive={'anyang':(130,455),'shangshan':(960,120)}),
 'shangshan':dict(start=(1040,160), portals=[('handan','回邯郸城',(1110,70),None),('baishou','入百兽谷',(40,775),None)],
                  arrive={'handan':(1040,160),'baishou':(110,760)}),
 'baishou':  dict(start=(110,790), portals=[('shangshan','回殇山',(40,820),None),('yinlong','入隐龙山',(1140,110),None)],
                  arrive={'shangshan':(110,790),'yinlong':(1060,140)}),
 'yinlong':  dict(start=(1090,750), portals=[('baishou','回百兽谷',(1160,790),None),('qingyang','入青阳古城',(80,50),None)],
                  arrive={'baishou':(1060,740),'qingyang':(160,90)}),
 'qingyang': dict(start=(230,150), portals=[('yinlong','回隐龙山',(70,50),None)],
                  arrive={'yinlong':(230,150)}),
 'zy1': dict(start=(90,80), portals=[('qingyang','出古城',(40,45),None),('zy2','入紫阴废墟',(1090,352),None)],
             arrive={'zy2':(1090,360)}),
 'zy2': dict(start=(60,430), portals=[('zy1','回古城外围',(30,430),None),('zy3','入怨魂之地',(920,575),None)],
             arrive={'zy1':(60,430),'zy3':(900,570)}),
 'zy3': dict(start=(100,600), portals=[('zy2','回紫阴废墟',(60,600),None),('zy4','入紫阴中枢',(920,50),None)],
             arrive={'zy2':(100,600),'zy4':(900,60)}),
 'zy4': dict(start=(60,320), portals=[('zy3','回怨魂之地',(30,330),None)],
             arrive={'zy3':(60,320)}),
}
for _k, _v in LINKS.items(): MAPS[_k].update(_v)
# 世界地图上的位置（world.webp 2300×2300）：沿东侧田舍往北、再往西，和老地图的圆点错开
FOES = {'qingyang': [{'stage': 'qy47', 'at': [900, 560]}, {'stage': 'qy47b', 'at': [1000, 1250]}, {'stage': 'qy49', 'at': [1450, 830]}, {'stage': 'qy49b', 'at': [1250, 1120]}, {'stage': 'qy49c', 'at': [1850, 990]}]}  # 关卡定义在 data.js 的 stages
ZY_FOES = {   # 紫阴古城四层：怪物与首领（预览坐标×2 = 数据坐标）
  'zy1': [{'stage': 'zy1a', 'at': [800, 470]}, {'stage': 'zy1b', 'at': [1900, 1050]}, {'stage': 'zy1boss', 'at': [1700, 1180]}],
  'zy2': [{'stage': 'zy2a', 'at': [700, 800]}, {'stage': 'zy2b', 'at': [1000, 900]}, {'stage': 'zy2boss', 'at': [1980, 180]}],
  'zy3': [{'stage': 'zy3a', 'at': [500, 1120]}, {'stage': 'zy3b', 'at': [1000, 880]}, {'stage': 'zy3c', 'at': [1700, 560]}],
  'zy4': [{'stage': 'zy4a', 'at': [700, 560]}, {'stage': 'zy4b', 'at': [1300, 960]}, {'stage': 'zy4c', 'at': [1700, 640]}, {'stage': 'zy4boss', 'at': [1300, 780]}]}
FOES.update(ZY_FOES)
NPCS = {'qingyang': [{'id': 'fb50_guard', 'at': [2090, 1015]}],
        'zy1': [{'id': 'fb50_probe', 'at': [380, 200]}], 'zy2': [{'id': 'fb50_general', 'at': [1180, 830]}]}  # 守将站在遗迹城门前
WORLD = {'hejiang': [2045, 1431], 'qianling': [2134, 843], 'anyang': [2096, 435], 'handan': [1636, 307],
         'shangshan': [1380, 153], 'baishou': [767, 383], 'yinlong': [332, 588], 'qingyang': [281, 843]}
for _k, _w in WORLD.items(): MAPS[_k]['world'] = _w
WEATHER = {'hejiang': 'clear: 4, rain: 2', 'qianling': 'clear: 2, rain: 3', 'anyang': 'clear: 3, rain: 1', 'handan': 'clear: 5, rain: 2',
           'shangshan': 'clear: 2, storm: 2', 'baishou': 'clear: 4, storm: 1', 'yinlong': 'clear: 3, snow: 1', 'qingyang': 'clear: 4, rain: 2'}

def S(p): return [round(p[0] * F), round(p[1] * F)]
# 连到老地图的传送：到对方地图后的落点（数据坐标，对方地图自己的坐标系）。老地图那一头的传送点在 js/data.js 里手写。
# 副本层间传送：要先打赢指定关卡才能过（游戏里 needStages）
PORTAL_NEED = {('zy1', 'zy2'): (['zy1boss'], '噬魂魔将还在镇守，击败他才能进入下一层。'),
               ('zy2', 'zy3'): (['zy2boss'], '魔帅挡在前面，击败他才能进入下一层。'),
               ('zy3', 'zy4'): (['zy3a', 'zy3b', 'zy3c'], '怨魂挡在去路上，先清掉这一层的鬼魂。')}
EXT = {('hejiang', 'mist'): [1110, 205], ('hejiang', 'pine'): [1190, 545], ('zy1', 'qingyang'): [2040, 1040]}
def build_def(k, m):
    ih = round(W * m['_h'] / m['_w'])
    d = dict(name=m['name'], world=m['world'], note=m['note'], home='town', img='bg_' + k, mini='mini_' + k, iw=W, ih=ih, scale=1, k=1, start=S(m['start']), alt=m['alt'])
    d['walk'] = [[S(p) for p in poly] for poly in m['walk']]
    d['roads'] = [dict(r=round(r['r'] * F), pts=[S(p) for p in r['pts']]) for r in m['roads']]
    d['blocks'] = [dict(at=S((b[0], b[1])), r=round(b[2] * F)) for b in m['blocks']]
    d['glows'] = []
    d['portals'] = []
    for to, name, at, _ in m['portals']:
        pd = dict(to=to, name=name, at=S(at), arrive=None)
        if (k, to) in PORTAL_NEED: pd['needStages'], pd['lockMsg'] = PORTAL_NEED[(k, to)]
        d['portals'].append(pd)
    if d['world'] is None: del d['world']              # 副本图不上世界地图
    if m.get('indoor'): d['indoor'] = True              # 室内：天气恒晴
    if k.startswith('zy'): d['battleBg'] = 'bg_' + k     # 副本战斗用本层底图当背景
    d['npcs'] = NPCS.get(k, [])  # 手写 NPC，重跑脚本时保留
    d['foes'] = FOES.get(k, [])
    return d

def raster(d, step=8):
    iw, ih = d['iw'], d['ih']; gw, gh = iw // step + 1, ih // step + 1
    im = Image.new('L', (gw, gh), 0); dr = ImageDraw.Draw(im)
    for poly in d['walk']: dr.polygon([(x / step, y / step) for x, y in poly], fill=255)
    for r in d['roads']:
        pts = r['pts']; rr = r['r'] / step
        for a, b in zip(pts, pts[1:]): dr.line([(a[0]/step, a[1]/step), (b[0]/step, b[1]/step)], fill=255, width=max(1, round(rr * 2)))
        for p in pts: dr.ellipse([p[0]/step-rr, p[1]/step-rr, p[0]/step+rr, p[1]/step+rr], fill=255)
    for b in d['blocks']:
        x, y, r = b['at'][0] / step, b['at'][1] / step, b['r'] / step
        dr.ellipse([x - r, y - r * 0.6, x + r, y + r * 0.6], fill=0)
    return im

def reach(d, im, step=8):
    gw, gh = im.size; px = im.load()
    s = (d['start'][0] // step, d['start'][1] // step)
    if px[min(s[0], gw-1), min(s[1], gh-1)] == 0: return None
    seen = {s}; q = [s]
    while q:
        x, y = q.pop()
        for dx, dy in ((1,0),(-1,0),(0,1),(0,-1)):
            n = (x + dx, y + dy)
            if 0 <= n[0] < gw and 0 <= n[1] < gh and n not in seen and px[n] > 0: seen.add(n); q.append(n)
    return seen

def main():
    sizes = {}
    for k, m in MAPS.items():
        p = f'{R}/art-src/maps/{k}.png'
        if not os.path.exists(p): continue
        im = Image.open(p).convert('RGB'); m['_w'], m['_h'] = im.size; m['_im'] = im
    defs = {}
    for k, m in MAPS.items():
        if '_im' not in m or not (m['walk'] or m['roads']): continue
        d = build_def(k, m)
        for i, (to, name, at, _) in enumerate(m['portals']):
            arr = MAPS[to]['arrive'].get(k) if to in MAPS else None
            d['portals'][i]['arrive'] = S(arr) if arr else EXT.get((k, to))
        defs[k] = d
        im = raster(d); seen = reach(d, im)
        up = m['_im'].resize((W, d['ih']), Image.LANCZOS).convert('RGBA'); ov = Image.new('RGBA', up.size, (0, 0, 0, 0)); od = ImageDraw.Draw(ov)
        st = 8; g = im.load()
        for y in range(im.size[1]):
            for x in range(im.size[0]):
                if g[x, y] > 0: od.rectangle([x*st, y*st, x*st+st-1, y*st+st-1], fill=(0, 255, 0, 70) if (seen and (x, y) in seen) else (255, 160, 0, 90))
        for b in d['blocks']: od.ellipse([b['at'][0]-b['r'], b['at'][1]-b['r']*0.6, b['at'][0]+b['r'], b['at'][1]+b['r']*0.6], outline=(255, 0, 0, 255), width=4)
        s = d['start']; od.ellipse([s[0]-14, s[1]-14, s[0]+14, s[1]+14], fill=(255, 255, 0, 255))
        ok = bool(seen); msgs = []
        if not seen: msgs.append('出生点不可走')
        for pt in d['portals']:
            a = pt['at']; od.ellipse([a[0]-18, a[1]-18, a[0]+18, a[1]+18], fill=(0, 120, 255, 255)); od.text((a[0]+22, a[1]-8), pt['name'], fill=(255, 255, 255, 255))
            c = (a[0] // 8, a[1] // 8)
            if not seen or c not in seen: msgs.append('传送点不可达: ' + pt['name']); ok = False
            ar = pt.get('arrive')
            if ar:
                od.ellipse([ar[0]-10, ar[0]*0+ar[1]-10, ar[0]+10, ar[1]+10], outline=(255, 255, 255, 255), width=3)
        Image.alpha_composite(up, ov).convert('RGB').resize((1200, d['ih'] // 2)).save(f'{PREV}/ov_{k}.jpg', quality=85)
        print(k, '可走', len(seen) if seen else 0, '格', 'OK' if ok and not msgs else msgs)
    if CHECK: return
    # 素材
    for k, m in MAPS.items():
        if '_im' not in m: continue
        im = m['_im']; ih = round(W * im.height / im.width); big = im.resize((W, ih), Image.LANCZOS)
        big.save(f'{R}/assets/bg_{k}.webp', quality=80, method=6)
        big.resize((512, round(512 * ih / W)), Image.LANCZOS).save(f'{R}/assets/mini_{k}.jpg', quality=80)
    p = f'{R}/js/assets.js'; s = open(p).read()
    for k, m in MAPS.items():
        if '_im' not in m: continue
        for key, fn in (('bg_' + k, f'assets/bg_{k}.webp'), ('mini_' + k, f'assets/mini_{k}.jpg')):
            if f'"{key}"' not in s: s = s.replace('"boar": "assets/boar.webp",', f'"{key}": "{fn}", "boar": "assets/boar.webp",', 1)
    open(p, 'w').write(s)
    # 地图配置
    lines = []
    for k, d in defs.items(): lines.append(f'    {k}: ' + json.dumps(d, ensure_ascii=False, separators=(',', ':')) + ',')
    block = '    // <story-maps>\n' + '\n'.join(lines) + '\n    // </story-maps>\n'
    p = f'{R}/js/data.js'; s = open(p).read()
    if '// <story-maps>' in s: s = re.sub(r'    // <story-maps>.*?// </story-maps>\n', lambda _: block, s, flags=re.S)
    else: s = s.replace('\n    taixuforest: {\n', '\n' + block + '    taixuforest: {\n', 1)
    if '// <story-weather>' not in s:
        s = s.replace("      outskirts: { clear: 4, rain: 2 },", "      outskirts: { clear: 4, rain: 2 },\n      // <story-weather>\n      // </story-weather>", 1)
    wx = '      // <story-weather>\n' + ''.join(f'      {k}: {{ {v} }},\n' for k, v in WEATHER.items()) + '      // </story-weather>'
    s = re.sub(r'      // <story-weather>.*?// </story-weather>', lambda _: wx, s, flags=re.S)
    open(p, 'w').write(s); print('写入 js/data.js、assets/、js/assets.js')

if __name__ == '__main__': main()
