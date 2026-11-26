# 道家女 8 帧斜角行走图：切 2×4 网格 → 抠绿 → 统一缩放 → 头部对齐、脚底对齐 → 8 格 + 1 格待机的精灵表
# 只输出到 art-src/dao-walk8/out/，不动 assets/ 里的旧素材。用法：python build8.py walkf_v2.png walkb_v1.png
import sys, os, json
import numpy as np
from PIL import Image

D = os.path.dirname(os.path.abspath(__file__)); OUT = D + '/out'; os.makedirs(OUT, exist_ok=True)
STAND_H = 118        # 与 build.py 的 walk_sheet 一致：并脚帧身高 118（逻辑像素），表按 2 倍存

def clean_rgba(im):
    # 生图接口已自带抠图（RGBA）：用原 alpha，去掉低 alpha 雾点和散落的小噪点块
    from scipy import ndimage
    a = np.array(im).copy(); al = a[..., 3]
    al[al <= 40] = 0
    lab, n = ndimage.label(al > 0)
    if n:
        sz = ndimage.sum(np.ones_like(al), lab, range(1, n + 1))
        small = np.isin(lab, np.nonzero(sz < 400)[0] + 1); al[small] = 0
    a[..., 3] = al
    return Image.fromarray(a, 'RGBA')

def key(im):
    if im.mode == 'RGBA' and (np.array(im.getchannel('A')) == 0).mean() > 0.2:
        return clean_rgba(im)
    a = np.array(im.convert('RGB')).astype(np.float32); r, g, b = a[..., 0], a[..., 1], a[..., 2]
    m = g - np.maximum(r, b)                                   # 绿色优势
    alpha = np.clip(1 - (m - 40) / 80, 0, 1)                   # m<40 全不透明，m>120 全透明
    g2 = np.minimum(g, np.maximum(r, b) + 10)                  # 去绿边
    rgb = np.stack([r, np.where(m > 10, g2, g), b], -1)
    return Image.fromarray(np.dstack([rgb, alpha * 255]).astype(np.uint8), 'RGBA')

def runs(mask, gap):
    cols = mask.sum(0) > 2; out = []; s = None
    for x, v in enumerate(cols):
        if v and s is None: s = x
        if not v and s is not None:
            if x - s > gap: out.append((s, x))
            s = None
    if s is not None: out.append((s, len(cols)))
    return out

def frames(src):
    im = key(Image.open(f'{D}/{src}')); H = im.height; fr = []
    for r in range(2):
        row = im.crop((0, r * H // 2, im.width, (r + 1) * H // 2))
        al = np.array(row.getchannel('A')) > 40
        rs = runs(al, 60)
        if len(rs) != 4:                                       # 飘带连在一起时退回等分
            q = im.width / 4; rs = [(round(i * q), round((i + 1) * q)) for i in range(4)]
            print(src, 'row', r, 'fallback to equal split')
        for x0, x1 in rs:
            c = row.crop((x0, 0, x1, row.height)); bb = c.getchannel('A').point(lambda v: 255 if v > 40 else 0).getbbox(); c = c.crop(bb)
            fr.append(c)
    assert len(fr) == 8, (src, len(fr))
    return fr

def head_x(c):
    al = np.array(c.getchannel('A')) > 40; top = al[:int(c.height * 0.22)]; xs = np.nonzero(top.any(0))[0]
    return (xs.min() + xs.max()) / 2

def sheet(src, name, flip):
    fr = frames(src)
    if flip: fr = [c.transpose(Image.FLIP_LEFT_RIGHT) for c in fr]
    k = STAND_H * 2 / ((fr[2].height + fr[6].height) / 2)         # 以两张“交叉并脚”帧（第 3、7 帧）的身高为准
    fr = [c.resize((round(c.width * k), round(c.height * k)), Image.LANCZOS) for c in fr]
    fr.append(fr[2])                                            # 第 9 格：待机（并脚）
    hx = [head_x(c) for c in fr]
    left = max(hx); right = max(c.width - h for c, h in zip(fr, hx)); cw = int(left + right) + 8; ch = max(c.height for c in fr) + 4
    sh = Image.new('RGBA', (cw * len(fr), ch), (0, 0, 0, 0))
    for i, (c, h) in enumerate(zip(fr, hx)): sh.alpha_composite(c, (i * cw + int(left - h) + 4, ch - c.height))
    sh.save(f'{OUT}/{name}.webp', quality=92, method=6)
    size = [cw / 2, ch / 2, round(left / 2 + 2, 2), 8]
    # 预览：按 10 帧/秒循环的 GIF（原尺寸的一半，灰底）
    gif = []
    for i in range(8):
        f = Image.new('RGBA', (cw, ch), (96, 110, 96, 255)); f.alpha_composite(sh.crop((i * cw, 0, (i + 1) * cw, ch))); gif.append(f.convert('RGB').resize((cw // 2, ch // 2), Image.LANCZOS))
    gif[0].save(f'{OUT}/{name}.gif', save_all=True, append_images=gif[1:], duration=100, loop=0)
    # 对齐检查：头部中心 x、头顶 y（格内，逻辑像素）
    stats = []
    for i in range(len(fr)):
        c = sh.crop((i * cw, 0, (i + 1) * cw, ch)); al = np.array(c.getchannel('A')) > 40; ys, xs = np.nonzero(al)
        stats.append((i, round(head_x(c.crop(c.getbbox())) + c.getbbox()[0], 1) / 2, int(ys.min()) / 2, int(xs.min() == 0 or xs.max() >= cw - 1)))
    print(name, 'size', size, 'sheet', sh.size); [print('  ', s) for s in stats]
    return size

if __name__ == '__main__':
    f_src, b_src = sys.argv[1], sys.argv[2]
    sz = {'walk_f_dao8': sheet(f_src, 'walk_f_dao8', True), 'walk_b_dao8': sheet(b_src, 'walk_b_dao8', False)}
    json.dump(sz, open(f'{OUT}/size.json', 'w'), indent=1)
    # 拼一张对照图：上排正面 8 帧，下排背面 8 帧（缩小，灰底）
    rows = []
    for n in ['walk_f_dao8', 'walk_b_dao8']:
        im = Image.open(f'{OUT}/{n}.webp'); bg = Image.new('RGBA', im.size, (96, 110, 96, 255)); bg.alpha_composite(im); rows.append(bg.convert('RGB'))
    W = max(r.width for r in rows); pv = Image.new('RGB', (W, sum(r.height for r in rows)), (96, 110, 96))
    y = 0
    for r in rows: pv.paste(r, (0, y)); y += r.height
    pv.save(f'{OUT}/preview.png')
