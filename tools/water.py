# 从地图图片里按颜色抠出水面，生成 js/water.js（1000x750 的 1 位 PNG 遮罩，base64）
import base64, io, os, sys
import numpy as np
from PIL import Image, ImageFilter
SRC = os.environ.get('VILLAGE_SRC', '/home/claude/guiqin/art-src/village2.png')
OUT = os.path.join(os.path.dirname(__file__), '..', 'js', 'water.js')
im = Image.open(SRC).convert('RGB').resize((1000, 750))
a = np.asarray(im).astype(int); r, g, b = a[..., 0], a[..., 1], a[..., 2]
m = (b > r + 40) & (g > r + 40) & (abs(g - b) < 75) & (g > 85)      # 青绿色的水
mi = Image.fromarray((m * 255).astype('uint8')).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
mi = mi.point(lambda v: 255 if v > 110 else 0).convert('1')
buf = io.BytesIO(); mi.save(buf, 'PNG', optimize=True)
png = base64.b64encode(buf.getvalue()).decode()
open(OUT, 'w').write('window.WATER={village:{w:1000,h:750,mask:"data:image/png;base64,%s"}};\n' % png)
print('water.js', len(png) // 1024, 'KB', 'coverage %.1f%%' % (np.asarray(mi).mean() * 100))
