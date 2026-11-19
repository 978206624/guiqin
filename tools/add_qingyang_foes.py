#!/usr/bin/env python3
"""青阳古城的 6 只野怪：原版高清手绘帧（kunkunxunqin pet_handpainted/<pwdId>/cg_001）→ assets/kk_mon_<pwdId>.webp 并登记进 js/assets.js。
用法：python3 tools/add_qingyang_foes.py <帧目录>   帧目录由 tools/extract_hdpet.py <pwdId> <帧目录>/m<pwdId> 事先解出。"""
import sys, json
from PIL import Image
R = __file__.rsplit('/tools/', 1)[0]
src = sys.argv[1]
MON = {'100006': '开山力士', '40010': '恶灵', '100004': '摄魂使者', '50004': '泥石兵俑', '100005': '盾甲兵', '60009': '熔骨血尸', '30005': '噬人妖', '50008': '暴怒尸鬼'}   # 后两只是紫阴古城副本怪
p = R + '/js/assets.js'; s = open(p).read(); i = s.index('{'); j = s.rindex('}'); A = json.loads(s[i:j + 1])
for pid, nm in MON.items():
    im = Image.open(f'{src}/m{pid}/cg_001.png').convert('RGBA'); im = im.crop(im.getchannel('A').getbbox())
    im = im.resize((round(im.width * 1.25), round(im.height * 1.25)), Image.LANCZOS)
    k = 'kk_mon_' + pid; im.save(f'{R}/assets/{k}.webp', quality=92)
    A['img'][k] = f'assets/{k}.webp'; A['size'][k] = [im.width / 2, im.height / 2]; print(k, nm, im.size)
A['img'] = dict(sorted(A['img'].items()))
open(p, 'w').write(s[:i] + json.dumps(A, ensure_ascii=False, separators=(', ', ': ')) + s[j + 1:])
