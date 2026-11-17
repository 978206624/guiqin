# 从地图图片里按颜色抠出水面，生成 js/water.js（1000x750 的 1 位 PNG 遮罩，base64）
import base64, io, os, sys
import numpy as np
from PIL import Image, ImageFilter
SRC = os.environ.get('VILLAGE_SRC', os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'art-src', 'village2.png'))
OUT = os.path.join(os.path.dirname(__file__), '..', 'js', 'water.js')
ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'art-src')
# 每张有水的地图：地图 id → (底图, 遮罩宽度, 要抹掉的区域 [x0,y0,x1,y1]（1000 宽坐标，抹掉青雾等误判）)
MAPS = {
    'village':   (SRC, 1500, []),
    'creek':     (ART + '/creek.jpg', 1000, [[0, 0, 450, 215]]),
    'mist':      (ART + '/mist.jpg', 1000, []),
    'outskirts': (ART + '/outskirts.jpg', 1000, []),
}
# 手工限定区域（1000 宽坐标的多边形，只在里面做颜色判定，用宽松的蓝青色规则）：瀑布/水潭周围噪点多
ZONES = {
    'taixupeak': [[(95,60),(150,60),(230,215),(235,300),(345,375),(330,395),(200,350),(140,300),(150,200),(95,110)],
                  [(500,375),(630,375),(630,430),(540,440),(500,430)],
                  [(465,430),(560,430),(575,650),(575,770),(500,770),(470,640)],
                  [(880,225),(920,225),(920,305),(880,305)],
                  [(0,765),(1000,765),(1000,808),(0,808)]],
    'taixuvalley': [[(15,285),(110,285),(110,480),(15,480)],
                    [(935,275),(1000,275),(1000,480),(935,480)],
                    [(0,470),(345,470),(345,745),(0,745)],
                    [(200,610),(430,610),(430,745),(200,745)],
                    [(700,650),(900,650),(900,745),(700,745)]],
}
# 直接填充的多边形（颜色判定不可靠的瀑布，如阴影里的深蓝水幕）
DIRECT = {
    'taixupeak': [[(495,440),(538,440),(540,520),(530,610),(482,630),(490,520)],
                  [(472,600),(530,600),(548,680),(540,715),(500,722),(480,680)],
                  [(540,385),(600,385),(625,402),(600,428),(535,425)]],
    'taixuvalley': [[(591,55),(609,55),(607,285),(593,285)],
                    [(704,0),(722,0),(721,105),(706,105)]],
}
def build_poly(src, N, polys, direct=(), ratio=None):
    from PIL import ImageDraw
    im = Image.open(src).convert('RGB')
    if ratio: im = im.crop((0, 0, im.width, round(im.width * ratio)))   # 去掉底部黑边，保证和游戏里的地图同比例
    im = im.resize((N, round(N * im.height / im.width)), Image.LANCZOS)
    a = np.asarray(im).astype(int); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (b > r + 22) & (b > g - 25) & (b > 110)
    mi = Image.fromarray((m * 255).astype('uint8')).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.5)).point(lambda v: 255 if v > 110 else 0)
    pm = Image.new('L', mi.size, 0); d = ImageDraw.Draw(pm)
    for p in polys: d.polygon([(x * N / 1000, y * N / 1000) for x, y in p], fill=255)
    mm = np.minimum(np.asarray(mi), np.asarray(pm))
    dm = Image.new('L', mi.size, 0); d = ImageDraw.Draw(dm)
    for p in direct: d.polygon([(x * N / 1000, y * N / 1000) for x, y in p], fill=255)
    mi = Image.fromarray(np.maximum(mm, np.asarray(dm.filter(ImageFilter.GaussianBlur(1.2)).point(lambda v: 255 if v > 110 else 0)))).filter(ImageFilter.GaussianBlur(2.0))
    buf = io.BytesIO(); mi.save(buf, 'PNG', optimize=True)
    return dict(w=N, h=im.height, mask='data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode()), np.asarray(mi).mean() / 255, mi.point(lambda v: 255 if v > 128 else 0).convert('1')
def build(src, N, drops):
    im = Image.open(src).convert('RGB'); im = im.resize((N, round(N * im.height / im.width)), Image.LANCZOS)
    a = np.asarray(im).astype(int); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (b > r + 40) & (g > r + 40) & (abs(g - b) < 75) & (g > 85)      # 青绿色的水
    mi = Image.fromarray((m * 255).astype('uint8')).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.8))
    mi = mi.point(lambda v: 255 if v > 110 else 0)
    f = N / 1000
    for x0, y0, x1, y1 in drops: mi.paste(0, (round(x0 * f), round(y0 * f), round(x1 * f), round(y1 * f)))
    mi = mi.filter(ImageFilter.GaussianBlur(1.6 * N / 1000 + .4))      # 软边：岸边渐变，游戏里用来画水花线
    buf = io.BytesIO(); mi.save(buf, 'PNG', optimize=True)
    return dict(w=N, h=im.height, mask='data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode()), np.asarray(mi).mean() / 255
out = {}
for k, f in (('taixupeak', 'taixu_peak.jpg'), ('taixuvalley', 'taixu_valley.jpg')):
    d, cov, mi = build_poly(ART + '/' + f, 1000, ZONES[k], DIRECT[k], 0.752 if k == 'taixuvalley' else None); out[k] = d; print(k, 'coverage %.1f%%' % (cov * 100))
    base = Image.open(ART + '/' + f).convert('RGB'); base = (base.crop((0, 0, base.width, round(base.width * .752))) if k == 'taixuvalley' else base).resize(mi.size); ov = np.asarray(base).copy(); mk = np.asarray(mi); ov[mk] = (ov[mk] * .4 + np.array([255, 0, 255]) * .6).astype('uint8')
    Image.fromarray(ov).save(os.environ.get('WDBG', '/tmp') + '/wov_' + k + '.jpg')
for k, (src, N, drops) in MAPS.items():
    d, cov = build(src, N, drops); out[k] = d; print(k, len(d['mask']) // 1024, 'KB', 'coverage %.1f%%' % (cov * 100))
open(OUT, 'w').write('window.WATER={' + ','.join('%s:{w:%d,h:%d,mask:"%s"}' % (k, d['w'], d['h'], d['mask']) for k, d in out.items()) + '};\n')
