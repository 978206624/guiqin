#!/usr/bin/env python3
"""把 art-src/kunkun-npc/<n>.png 单独转成 assets/kk_<n>.webp + head_kk_<n>.webp 并登记进 js/assets.js（逻辑同 build.py 的 KK_NPC 循环，开发时不用跑完整 build）。
用法：python3 tools/add_kk_npc.py npc_qxz=104:0.1,0,0.9,0.5 npc_hx=118 ...    （名=显示高度[:头像裁切 x0,y0,x1,y1 占立绘比例，默认 0.1,0,0.9,0.3]）"""
import sys, json, os
from PIL import Image
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'); G = R + '/art-src'; AD = R + '/assets'
KK_HEAD = {}
p = R + '/js/assets.js'; s = open(p).read(); i = s.index('{'); j = s.rindex('}'); A = json.loads(s[i:j + 1])
for arg in sys.argv[1:]:
    n, hh = arg.split('='); hh, _, cr = hh.partition(':'); hh = int(hh); x0, y0, x1, y1 = map(float, cr.split(',')) if cr else (.1, 0, .9, .3)
    im = Image.open(f'{G}/kunkun-npc/{n}.png').convert('RGBA'); c = im.crop(im.getbbox()); h = hh * 2; w = round(c.width * h / c.height)
    c.resize((w, h), Image.LANCZOS).save(f'{AD}/kk_{n}.webp', quality=94, method=6)
    A['img']['kk_' + n] = f'assets/kk_{n}.webp'; A['size']['kk_' + n] = [w / 2, h / 2]
    top = c.crop((round(c.width * x0), round(c.height * y0), round(c.width * x1), round(c.height * y1))); m = max(top.size)
    sq = Image.new('RGBA', (m, m), (0, 0, 0, 0)); sq.paste(top, ((m - top.width) // 2, (m - top.height) // 2))
    sq.resize((160, 160), Image.LANCZOS).save(f'{AD}/head_kk_{n}.webp', quality=92); A['img']['head_kk_' + n] = f'assets/head_kk_{n}.webp'
    print(n, w, h)
A['img'] = dict(sorted(A['img'].items()))
open(p, 'w').write(s[:i] + json.dumps(A, ensure_ascii=False, separators=(', ', ': ')) + s[j + 1:])
