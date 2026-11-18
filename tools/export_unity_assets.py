#!/usr/bin/env python3
# 把网页版的素材和数据导出成 Unity 工程能直接用的形式：
#   unity/Assets/Resources/GuiQin/Art/*.png|jpg   素材（webp 转 png；内容相同的文件只留一份）
#   unity/Assets/Resources/GuiQin/Data/*.json     data / story / 碰撞位图 / 素材清单（key → 文件、尺寸）
# 用法：先 node tools/export_data.js（刷新 data/*.json），再 python3 tools/export_unity_assets.py
import json, os, hashlib, shutil, sys
from PIL import Image
R = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = R + '/unity/Assets/Resources/GuiQin'
ART, DAT = OUT + '/Art', OUT + '/Data'
os.makedirs(ART, exist_ok=True); os.makedirs(DAT, exist_ok=True)
s = open(R + '/js/assets.js').read(); A = json.loads(s[s.index('{'):s.rindex('}') + 1])
img = A['img']                               # key -> 'assets/xxx.webp'
seen = {}                                    # 内容哈希 -> 输出文件名（不含目录）
out_img = {}; total = 0; conv = 0
for key, rel in sorted(img.items()):
    src = R + '/' + rel
    if not os.path.exists(src): print('缺文件', key, rel); continue
    h = hashlib.md5(open(src, 'rb').read()).hexdigest()
    if h in seen: out_img[key] = seen[h]; continue
    base, ext = os.path.splitext(os.path.basename(rel))
    if ext.lower() == '.webp':
        im = Image.open(src); opaque = im.mode == 'RGB' or (im.mode == 'RGBA' and im.getchannel('A').getextrema()[0] == 255)
        big = max(im.size) >= 800
        name = base + ('.jpg' if opaque and big else '.png'); dst = ART + '/' + name       # 不透明的大图（地图底图）存 jpg，体积小得多
        if not os.path.exists(dst) or os.path.getmtime(dst) < os.path.getmtime(src):
            if name.endswith('.jpg'): im.convert('RGB').save(dst, 'JPEG', quality=92, optimize=True)
            else: im.convert('RGBA').save(dst, 'PNG', optimize=True)
            conv += 1
    else:
        name = base + ext.lower(); dst = ART + '/' + name
        if not os.path.exists(dst) or os.path.getmtime(dst) < os.path.getmtime(src): shutil.copyfile(src, dst); conv += 1
    seen[h] = name; out_img[key] = name; total += os.path.getsize(dst)
meta = {'img': out_img, 'size': A['size'], 'pet': A['pet']}
json.dump(meta, open(DAT + '/assets.json', 'w'), ensure_ascii=False, separators=(',', ':'))
for f in ('data', 'story'): shutil.copyfile(f'{R}/data/{f}.json', f'{DAT}/{f}.json')
for k in os.listdir(R + '/data/collision'): shutil.copyfile(f'{R}/data/collision/{k}', f'{DAT}/mask_{k}')
# 水面蒙版：export/unity-water/<地图>_mask.png → Resources/GuiQin/Water/<地图>.png（流水着色器读它）
WAT = OUT + '/Water'; os.makedirs(WAT, exist_ok=True); nw = 0
if os.path.isdir(R + '/export/unity-water'):
    for fn in os.listdir(R + '/export/unity-water'):
        if fn.endswith('_mask.png'): shutil.copyfile(f'{R}/export/unity-water/{fn}', f'{WAT}/{fn[:-9]}.png'); nw += 1
print(f'水面蒙版 {nw} 张')
print(f'素材 {len(out_img)} 个 key → {len(seen)} 个文件，本次转换/复制 {conv} 个，Art 目录合计 {total/1e6:.1f} MB')
