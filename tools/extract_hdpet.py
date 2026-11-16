#!/usr/bin/env python3
"""把 kunkunxunqin 的高清手绘宠物（client/assets/pet_handpainted/<id>/{cg,zd,head}/*.res，Godot3 RSCC+zstd 的 ImageTexture）转成 PNG。
用法：extract_hdpet.py <宠物目录名> <输出目录>   依赖：node>=22.15（内置 zstd）、Pillow"""
import sys, os, glob, struct, subprocess, tempfile
from PIL import Image
SRC='/home/claude/kunkunxunqin/client/assets/pet_handpainted'
JS=os.path.join(os.path.dirname(os.path.abspath(__file__)),'unres.js')
def read_res(path):
    with tempfile.NamedTemporaryFile(suffix='.rsrc') as t:
        subprocess.run(['node',JS,path,t.name],check=True,stdout=subprocess.DEVNULL); d=open(t.name,'rb').read()
    w=struct.unpack('<I',d[d.find(b'width\0')+10:][:4])[0]; h=struct.unpack('<I',d[d.find(b'height\0')+11:][:4])[0]
    k=d.find(b'data\0\x1f\0\0\0',d.find(b'mipmaps')); n=struct.unpack('<I',d[k+9:k+13])[0]
    raw=d[k+13:k+13+n]; assert n==w*h*4,(w,h,n)
    return Image.frombytes('RGBA',(w,h),raw)
if __name__=='__main__':
    pid,out=sys.argv[1],sys.argv[2]; os.makedirs(out,exist_ok=True)
    for f in sorted(glob.glob(f'{SRC}/{pid}/*/*.res')):
        g=os.path.basename(os.path.dirname(f)); name=f'{g}_{os.path.basename(f)[6:-4]}.png'
        read_res(f).save(os.path.join(out,name)); print(name,end=' ')
