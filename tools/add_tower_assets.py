# 玄兵塔 7 张地图图：把 art-src/tower/out/t<k>.png 放大成 2000x1500 → art-src/tower<k>.jpg（build.py 的输入）
# 加 --quick 时，顺手生成 assets/bg_tower<k>.webp 和 mini 图并补进 js/assets.js（开发时不用跑完整 build.py）
import os, re, sys
from PIL import Image
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..')
quick = '--quick' in sys.argv
for k in range(1, 8):
    src = f'{R}/art-src/tower/out/t{k}.png'
    if not os.path.exists(src): continue
    im = Image.open(src).convert('RGB').resize((2000, 1500), Image.LANCZOS); im.save(f'{R}/art-src/tower{k}.jpg', quality=95)
    if quick:
        im.save(f'{R}/assets/bg_tower{k}.webp', quality=80, method=6)
        im.resize((512, 384), Image.LANCZOS).save(f'{R}/assets/mini_tower{k}.jpg', quality=80)
if quick:
    p = f'{R}/js/assets.js'; s = open(p).read()
    for k in range(1, 8):
        if os.path.exists(f'{R}/assets/bg_tower{k}.webp') and f'"bg_tower{k}"' not in s:
            s = s.replace('  "boar": "assets/boar.webp",', f'  "bg_tower{k}": "assets/bg_tower{k}.webp",\n  "mini_tower{k}": "assets/mini_tower{k}.jpg",\n  "boar": "assets/boar.webp",', 1)
    open(p, 'w').write(s)
print('ok')
