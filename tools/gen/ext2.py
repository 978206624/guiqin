import subprocess
from PIL import Image
T=Image.open('town3.jpg').convert('RGB'); W,H=T.size   # 3677x1426
CW,CH,SH=1500,1125,450          # tile canvas, known strip height
prompt=open('ext2.txt').read()
xs=[0,1090,2177]
res=Image.new('RGB',(W,H+CH-SH),(205,205,205)); res.paste(T,(0,0))
prev=None  # previous tile result (CW x CH)
for i,x0 in enumerate(xs):
    c=Image.new('RGB',(CW,CH),(205,205,205))
    c.paste(T.crop((x0,H-SH,x0+CW,H)),(0,0))
    if prev is not None:
        ov=xs[i-1]+CW-x0
        c.paste(prev.crop((CW-ov,0,CW,CH)),(0,0))
    c.resize((1448,1086),Image.LANCZOS).save(f'ext2_in_{i}.jpg',quality=92)
    subprocess.run(['python3','/root/opt/gpt-image/gpt_image.py','edit','-i',f'ext2_in_{i}.jpg','--prompt-file','ext2.txt','-o',f'ext2_out_{i}.png','--size','2048x1536','--quality','high'],check=True,stdout=open(f'ext2_{i}.log','w'))
    o=Image.open(f'ext2_out_{i}.png').convert('RGB').resize((CW,CH),Image.LANCZOS)
    prev=o
    o.save(f'ext2_tile_{i}.png')
print('done')
