#!/usr/bin/env python3
"""从原作 aef（帧序列）+ pet_handpainted manifest（帧区域）生成 art-src/shenshou/pets.json，build.py 据此拼图集。
每只神兽：frames（用到的帧 id）、region（每帧在 1000×600 画布的矩形）、anims（原作动画序列：0 待机 / 1 起手 / 2 出击 / 3.. 技能，hit 为 a,b,a 三帧）。"""
import struct, glob, os, re, json
K='/home/claude/kunkunxunqin/client/assets/'
PETS={'1221':'1220','1222':'1221','1223':'cw_sj','1224':'1223','1225':'1224','1226':'1225','1227':'cw_xlr'}
def aef(path):
    d=open(path,'rb').read(); p=[0]
    def s(): v=struct.unpack('>H',d[p[0]:p[0]+2])[0]; p[0]+=2; return v
    def i(): v=struct.unpack('>I',d[p[0]:p[0]+4])[0]; p[0]+=4; return v
    n=s()
    for _ in range(n):
        s(); c=i()
        for _ in range(c): s();s();s();s()
    return [[s() for _ in range(s())] for _ in range(s())]
out={}
for k,pwd in PETS.items():
    f=(glob.glob(K+f'pet/{pwd}/*zd.aef')+glob.glob(K+f'pet/{pwd}/*zhandou.aef'))[0]
    mf=open(K+f'pet_handpainted/{pwd}/manifest.gd').read()
    zd=mf[mf.index('"zd"'):]; zd=zd[:zd.index('"head"')] if '"head"' in zd else zd
    reg={m.group(1):list(map(int,m.group(2,3,4,5))) for m in re.finditer(r'"([\w-]+)": \{"texture": preload\("[^"]+/zd/frame-[\w-]+\.res"\), "region": Rect2\((\d+), (\d+), (\d+), (\d+)\)',zd)}
    out[k]={'pwd':pwd,'anims':aef(f),'region':reg}
json.dump(out,open(os.path.join(os.path.dirname(os.path.abspath(__file__)),'..','art-src','shenshou','pets.json'),'w'),ensure_ascii=False,indent=1)
for k,v in out.items(): print(k,v['anims'],len(v['region']))
