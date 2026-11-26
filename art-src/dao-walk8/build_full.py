# 道家女行走表（视频完整周期版）：一个步态周期的全部视频帧 + 1 格待机（视频第 1 帧，静止站立）。
# 机位固定，所以不逐帧对齐：所有帧用同一个裁剪框、同一个缩放（并脚身高 118 逻辑像素，表按 2 倍存），保留视频里原有的起伏和摆动。
# 抠绿沿用 build8.key()，另去掉脚下绿色投影和散落噪点。
# size = [每格宽, 每格高, 头部中心在格内的 x, 帧数, 帧率]：第 5 项帧率让引擎按时间播放（视频原速 24fps），而不是按走过的距离。
# 用法：python build_full.py            → out/full/ 下出表和预览；加 --install 再写进 assets/ 与 js/assets.js
import os, sys, re, json
import numpy as np
from PIL import Image
from scipy import ndimage
import build8

D = build8.D; R = os.path.abspath(D + '/../..'); OUT = D + '/out/full'; os.makedirs(OUT, exist_ok=True)
FPS = 24
CYCLE = {
    'f': ('video_in/f2', range(60, 85)),     # 正面：周期 25 帧（60–84，第 85 帧≈第 60 帧）
    'b': ('video_in/f1', range(94, 120)),    # 背面：周期 26 帧（94–119，第 120 帧≈第 94 帧）
}
IDLE = 1                                     # 待机：视频第 1 帧（参考图姿势）
KEYS = ['dao_ty', 'dao_qm', 'dao_dz']        # 道家女三套形象共用同一套行走图（与现有 assets 一致）

def key_full(p):
    a = np.array(build8.key(Image.open(p).convert('RGB')))
    al = a[..., 3].astype(np.float32)
    rgb = np.array(Image.open(p).convert('RGB')).astype(np.int16)
    m = rgb[..., 1] - np.maximum(rgb[..., 0], rgb[..., 2])
    ys = np.nonzero((al > 40).any(1))[0]; y0, y1 = ys.min(), ys.max()
    band = slice(y1 - int((y1 - y0) * 0.06), None)           # 脚下投影：最下 6% 里仍偏绿的像素去掉
    sub = al[band]; sub[m[band] > 20] = 0; al[band] = sub
    al[al <= 40] = 0
    lab, n = ndimage.label(al > 0)
    if n:
        sz = ndimage.sum(np.ones_like(al), lab, range(1, n + 1)); keep = np.argmax(sz) + 1
        al[np.isin(lab, np.nonzero(sz < 400)[0] + 1) & (lab != keep)] = 0
    a[..., 3] = al.astype(np.uint8)
    return Image.fromarray(a, 'RGBA')

def sheet(d, idx):
    ims = [key_full(f'{D}/{d}/{i:03d}.png') for i in list(idx) + [IDLE]]
    als = [np.array(im.getchannel('A')) > 40 for im in ims]
    bbs = [im.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox() for im in ims]
    x0 = min(b[0] for b in bbs); y0 = min(b[1] for b in bbs); x1 = max(b[2] for b in bbs); y1 = max(b[3] for b in bbs)
    hs = [b[3] - b[1] for b in bbs[:-1]]
    k = build8.STAND_H * 2 / float(np.median(hs))
    # 头部中心 x：各帧头顶 22% 的中心取中位数
    hx = []
    for al, b in zip(als, bbs):
        top = al[b[1]:b[1] + int((b[3] - b[1]) * 0.22)]; xs = np.nonzero(top.any(0))[0]; hx.append((xs.min() + xs.max()) / 2)
    hxm = float(np.median(hx))
    pad = 4
    cw = round((x1 - x0) * k) + pad * 2; ch = round((y1 - y0) * k) + pad
    sh = Image.new('RGBA', (cw * len(ims), ch), (0, 0, 0, 0))
    for i, im in enumerate(ims):
        c = im.crop((x0, y0, x1, y1)).resize((round((x1 - x0) * k), round((y1 - y0) * k)), Image.LANCZOS)
        sh.alpha_composite(c, (i * cw + pad, ch - c.height))
    size = [cw / 2, ch / 2, round(((hxm - x0) * k + pad) / 2, 2), len(ims) - 1, FPS]
    edge = [i for i in range(len(ims)) if np.array(sh.crop((i * cw, 0, (i + 1) * cw, ch)).getchannel('A'))[:, [0, -1]].max() > 40]
    print(d, 'frames', len(ims) - 1, '+idle  scale', round(k, 4), 'size', size, 'sheet', sh.size, 'head-x spread', round(max(hx) - min(hx), 1), 'edge-touch', edge)
    return sh, size

def preview(sh, size, name):
    cw, ch = int(size[0] * 2), int(size[1] * 2); n = size[3]; gif = []
    for i in range(n):
        f = Image.new('RGBA', (cw, ch), (96, 110, 96, 255)); f.alpha_composite(sh.crop((i * cw, 0, (i + 1) * cw, ch)))
        gif.append(f.convert('RGB').resize((cw // 2, ch // 2), Image.LANCZOS))
    gif[0].save(f'{OUT}/{name}.gif', save_all=True, append_images=gif[1:], duration=round(1000 / FPS), loop=0)

def main(install):
    res = {}
    for dr, (d, idx) in CYCLE.items():
        sh, size = sheet(d, idx); name = f'walk_{dr}_dao'
        sh.save(f'{OUT}/{name}.webp', quality=92, method=6); preview(sh, size, name); res[dr] = (sh, size)
    json.dump({k: v[1] for k, v in res.items()}, open(f'{OUT}/size.json', 'w'), indent=1)
    if not install: return
    js = open(R + '/js/assets.js', 'rb').read()
    for dr, (sh, size) in res.items():
        for k in KEYS:
            n = f'walk_{dr}_{k}'
            sh.save(f'{R}/assets/{n}.webp', quality=92, method=6)
            new = (f'"{n}": ' + json.dumps(size)).encode()
            js, c = re.subn(rb'"' + n.encode() + rb'": \[[^\]]*\]', new, js)
            assert c == 1, (n, c)
    open(R + '/js/assets.js', 'wb').write(js)
    # 资源清单里同步文件字节数
    mp = R + '/data/assets_manifest.json'; man = json.load(open(mp, encoding='utf-8'))
    for dr in res:
        for k in KEYS:
            n = f'walk_{dr}_{k}'; man[n]['bytes'] = os.path.getsize(f'{R}/assets/{n}.webp')
    json.dump(man, open(mp, 'w', encoding='utf-8'), indent=1, ensure_ascii=False)
    print('installed', [f'walk_{d}_{k}' for d in res for k in KEYS])

if __name__ == '__main__':
    main('--install' in sys.argv)
