import subprocess, numpy as np
from PIL import Image
G='/root/opt/gpt-image/gpt_image.py'
def gen(i,o,p): subprocess.run(['python3',G,'edit','-i',i,'--prompt-file',p,'-o',o,'--size','2048x1536','--quality','high'],check=True,stdout=open(o+'.log','w'))
# ---- east
t=Image.open('town2.jpg').convert('RGB'); W,H=t.size
strip=t.crop((W-800,0,W,H)).resize((610,1086),Image.LANCZOS)
c=Image.new('RGB',(1448,1086),(205,205,205)); c.paste(strip,(0,0)); c.save('e3_in.jpg',quality=92)
gen('e3_in.jpg','e3_out.png','ext.txt')
m=Image.open('e3_out.png').convert('RGB').resize((round(1448*H/1086),H),Image.LANCZOS)
off=W-800; mw=m.width; tw=off+mw
out=np.zeros((H,tw,3),np.float32); out[:,:W]=np.asarray(t,np.float32); mm=np.asarray(m,np.float32)
ramp=200; a=np.array([0 if off+x<W-ramp else min(1,(off+x-(W-ramp))/ramp) for x in range(mw)],np.float32)
out[:,off:off+mw]=out[:,off:off+mw]*(1-a[None,:,None])+mm*a[None,:,None]
T=Image.fromarray(out.clip(0,255).astype('uint8')); T.save('town5_east.jpg',quality=92); W,H=T.size; print('east',T.size,flush=True)
# ---- south
CW,CH,SH=1500,1125,450; xs=[0,W//2-CW//2,W-CW]; NH=H+CH-SH
prev=None; tiles=[]
for i,x0 in enumerate(xs):
    cv=Image.new('RGB',(CW,CH),(205,205,205)); cv.paste(T.crop((x0,H-SH,x0+CW,H)),(0,0))
    if prev is not None:
        ov=xs[i-1]+CW-x0; cv.paste(prev.crop((CW-ov,0,CW,CH)),(0,0))
    cv.resize((1448,1086),Image.LANCZOS).save(f's3_in_{i}.jpg',quality=92)
    gen(f's3_in_{i}.jpg',f's3_out_{i}.png','ext2.txt')
    prev=Image.open(f's3_out_{i}.png').convert('RGB').resize((CW,CH),Image.LANCZOS); tiles.append(prev); print('tile',i,flush=True)
acc=np.zeros((NH,W,3),np.float32); ws=np.zeros((NH,W),np.float32)
for i,x0 in enumerate(xs):
    tt=np.asarray(tiles[i],np.float32); wx=np.ones(CW,np.float32)
    if i>0: ov=xs[i-1]+CW-x0; wx[:ov]=np.linspace(0,1,ov)
    if i<len(xs)-1: ov2=x0+CW-xs[i+1]; wx[CW-ov2:]=np.minimum(wx[CW-ov2:],np.linspace(1,0,ov2))
    y0=H-SH; acc[y0:y0+CH,x0:x0+CW]+=tt*wx[None,:,None]; ws[y0:y0+CH,x0:x0+CW]+=wx[None,:]
tile=acc/np.maximum(ws,1e-6)[...,None]
res=np.zeros((NH,W,3),np.float32); res[:H]=np.asarray(T,np.float32)
for y in range(H-200,NH):
    al=min(1,(y-(H-200))/200)
    res[y]=(res[y]*(1-al)+tile[y]*al) if y<H else tile[y]
Image.fromarray(res.clip(0,255).astype('uint8')).save('town5.jpg',quality=90); print('done',W,NH,flush=True)
