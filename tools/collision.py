#!/usr/bin/env python3
"""碰撞图生成器。坐标一律是 data.js 里的“数据坐标”（恒山村 = 2000×1500，汉中郡 = 2000×1107）。
  恒山村：手工描的路面多边形 (ADD) 减去障碍 (SUB)。
  汉中郡：底图颜色分割出路面，和 data.js 里 roads 画的“走廊”取交集，再减去障碍。
输出 js/masks.js（位图，每格 4 个数据单位），并在 /tmp 写预览叠图用来人工检查。
用法：python3 tools/collision.py            （在仓库根目录运行）"""
import re, json, base64, math, os, sys
import cv2, numpy as np
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CELL = 4
PREV = os.environ.get('PREVIEW_DIR', '/tmp')

# ---------------- 恒山村：手工多边形 ----------------
V_ADD = [
 # 左下石板路 → 拱桥左端
 [(590,945),(700,945),(780,980),(790,1060),(730,1110),(670,1170),(615,1190),(600,1260),(570,1330),(560,1390),(600,1440),(680,1500),(0,1500),(0,1330),(120,1300),(260,1250),(380,1200),(440,1150),(520,1135),(590,1090),(610,1000)],
 # 拱桥桥面（中心线 + 半宽，下面代码展开）
 # 桥右 → 中央大片石板、草地台阶
 [(790,640),(900,540),(1000,540),(1080,600),(1200,580),(1300,520),(1400,470),(1520,430),(1520,520),(1440,600),(1400,650),(1300,720),(1230,790),(1180,880),(1090,915),(1040,860),(980,790),(880,730)],
 # 通往下方木平台的石阶
 [(1100,880),(1180,850),(1250,920),(1330,980),(1380,1040),(1350,1100),(1290,1090),(1230,1030),(1150,960)],
 # 东北山道台阶（凉亭左边一路往上）
 [(1450,500),(1500,430),(1530,360),(1560,300),(1540,200),(1520,100),(1600,60),(1680,90),(1700,180),(1660,280),(1620,350),(1570,420),(1560,500),(1520,570),(1440,600)],
 # 村长屋前木平台
 [(880,480),(960,470),(1100,420),(1250,380),(1350,340),(1480,320),(1520,350),(1480,420),(1400,455),(1330,480),(1300,500),(1190,510),(1120,540),(1040,562),(960,562)],
 # 石阶尽头的踏石，连到木平台
 [(1330,1050),(1400,1090),(1455,1165),(1385,1180),(1340,1125)],
 # 右下木平台（出村口）
 [(1390,1150),(1470,1160),(1560,1190),(1640,1230),(1730,1250),(1800,1300),(1900,1380),(1950,1450),(1950,1500),(1560,1500),(1500,1450),(1440,1380),(1390,1300),(1360,1210)],
]
V_BRIDGE = ([(700,948),(790,892),(880,860),(970,860),(1040,882),(1085,905)], 36)    # 中心线, 半宽
V_SUB_CIRC = [  # (x, y, r) 柱子、陶罐、木桶、木箱
 (430,1345,18),(627,1192,20),(650,1238,15),(700,1312,18),
 (1085,498,26),(1180,520,24),(1010,490,18),(1612,1235,22),(1520,1110,45),
]

# ---------------- 汉中郡：自动 + 手工减 ----------------
T_SUB_CIRC = [(1752,888,30),(1745,830,16)]       # 石狮子等
T_SUB_POLY = []

def poly(m, pts, v=1):
    cv2.fillPoly(m, [np.array([(int(x), int(y)) for x, y in pts], np.int32)], v)

def read_roads(name):
    s = open(R + '/js/data.js', encoding='utf-8').read()
    a = s.index(name + ': {'); seg = s[a:]
    b = re.search(r"\n    \w+: \{", seg[10:]); seg = seg[:b.start() + 10] if b else seg
    return [(int(r), eval(p)) for r, p in re.findall(r"\{ r: (\d+), pts: (\[\[.*?\]\]) \}", seg)]

def pack(m):
    h, w = m.shape; small = cv2.resize(m, (w // CELL, h // CELL), interpolation=cv2.INTER_AREA)
    small = (small >= 128).astype(np.uint8)
    bits = np.packbits(small.reshape(-1))
    return dict(cell=CELL, w=small.shape[1], h=small.shape[0], bits=base64.b64encode(bits.tobytes()).decode()), small

def preview(img, m, path):
    ov = img.copy(); sel = m > 0
    ov[sel] = (0.55 * ov[sel] + 0.45 * np.array([255, 160, 0])).astype(np.uint8)
    cnts, _ = cv2.findContours((m > 0).astype(np.uint8), cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
    cv2.drawContours(ov, cnts, -1, (0, 0, 255), 2)
    cv2.imwrite(path, ov)

def village():
    img = cv2.imread(R + '/art-src/village2.png'); H, W = img.shape[:2]
    m = np.zeros((H, W), np.uint8)
    for p in V_ADD: poly(m, p)
    cv2.polylines(m, [np.array(V_BRIDGE[0], np.int32)], False, 255, thickness=V_BRIDGE[1] * 2)
    m = (m > 0).astype(np.uint8) * 255
    for x, y, r in V_SUB_CIRC: cv2.circle(m, (x, y), r, 0, -1)
    return img, m

def town():
    im = cv2.imread(R + '/art-src/town2.jpg'); im = cv2.resize(im, (2000, round(im.shape[0] * 2000 / im.shape[1])), interpolation=cv2.INTER_AREA)
    H, W = im.shape[:2]
    hsv = cv2.cvtColor(im, cv2.COLOR_BGR2HSV); S = hsv[..., 1].astype(int); V = hsv[..., 2].astype(int)
    pav = ((S < 70) & (V > 130)).astype(np.uint8)
    cor = np.zeros((H, W), np.uint8)
    for r, pts in read_roads('town'): cv2.polylines(cor, [np.array(pts, np.int32)], False, 1, thickness=r * 2 + 60)
    m = pav & cor
    m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (15, 15)))
    m = cv2.morphologyEx(m, cv2.MORPH_OPEN, cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (21, 21)))
    n, lab, st, _ = cv2.connectedComponentsWithStats(m); keep = np.zeros_like(m)
    for i in range(1, n):
        if st[i, 4] > 6000: keep[lab == i] = 1
    m = keep * 255
    for x, y, r in T_SUB_CIRC: cv2.circle(m, (x, y), r, 0, -1)
    for p in T_SUB_POLY: poly(m, p, 0)
    return im, m

if __name__ == '__main__':
    out = {}
    for name, fn in (('village', village), ('town', town)):
        img, m = fn(); out[name], small = pack(m)
        preview(img, m, f'{PREV}/collision_{name}.png'); print(name, small.shape, int(small.sum()), 'cells walkable')
    open(R + '/js/masks.js', 'w').write('// 碰撞位图（tools/collision.py 生成）。每格 cell 个数据单位，按行从左到右，1 = 能走；bits 是 base64 的位串。\nwindow.MASKS = ' + json.dumps(out) + ';\n')
