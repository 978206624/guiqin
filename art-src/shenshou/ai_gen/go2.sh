#!/bin/bash
# 用法: go2.sh <id>  —— 以该宠物的 atk 基准图为风格参考，出 idle 和 hit
k=$1
python3 - <<P
from PIL import Image
im=Image.open('g_${k}_atk.png').convert('RGBA');bg=Image.new('RGB',im.size,(0,255,0));bg.paste(im,mask=im.getchannel('A'));bg.thumbnail((1200,400));bg.save('style_${k}.jpg',quality=85)
P
python3 mk.py idlehit $k
for n in idle hit; do (nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file p_${k}_$n.txt -i ref_$k.jpg -i style_$k.jpg -o g_${k}_$n.png --size 1536x1024 --quality high > g_${k}_$n.log 2>&1 &); done
