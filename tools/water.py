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
def build(src, N, drops):
    im = Image.open(src).convert('RGB'); im = im.resize((N, round(N * im.height / im.width)), Image.LANCZOS)
    a = np.asarray(im).astype(int); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = (b > r + 40) & (g > r + 40) & (abs(g - b) < 75) & (g > 85)      # 青绿色的水
    mi = Image.fromarray((m * 255).astype('uint8')).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.8))
    mi = mi.point(lambda v: 255 if v > 110 else 0)
    f = N / 1000
    for x0, y0, x1, y1 in drops: mi.paste(0, (round(x0 * f), round(y0 * f), round(x1 * f), round(y1 * f)))
    mi = mi.convert('1')
    buf = io.BytesIO(); mi.save(buf, 'PNG', optimize=True)
    return dict(w=N, h=im.height, mask='data:image/png;base64,' + base64.b64encode(buf.getvalue()).decode()), np.asarray(mi).mean()
out = {}
for k, (src, N, drops) in MAPS.items():
    d, cov = build(src, N, drops); out[k] = d; print(k, len(d['mask']) // 1024, 'KB', 'coverage %.1f%%' % (cov * 100))
open(OUT, 'w').write('window.WATER={' + ','.join('%s:{w:%d,h:%d,mask:"%s"}' % (k, d['w'], d['h'], d['mask']) for k, d in out.items()) + '};\n')
