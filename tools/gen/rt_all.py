import subprocess, numpy as np, os
from concurrent.futures import ThreadPoolExecutor
from PIL import Image
T=Image.open('town3.jpg').convert('RGB'); W,H=T.size
TW,TH=1000,750; xs=[0,892,1785,2677]; ys=[0,675,1351]
SC=1448/TW; os.makedirs('rt',exist_ok=True)
jobs=[(i,j) for j in range(3) for i in range(4)]
def run(ij):
    i,j=ij; x0,y0=xs[i],ys[j]; inp=f'rt/in_{i}_{j}.jpg'; out=f'rt/out_{i}_{j}.png'
    T.crop((x0,y0,x0+TW,y0+TH)).resize((1448,1086),Image.LANCZOS).save(inp,quality=94)
    for a in range(3):
        r=subprocess.run(['python3','/root/opt/gpt-image/gpt_image.py','edit','-i',inp,'--prompt-file','rt.txt','-o',out,'--size','2048x1536','--quality','high'],stdout=open(out+'.log','w'),stderr=subprocess.STDOUT)
        if r.returncode==0 and os.path.exists(out): break
    print('tile',i,j,flush=True)
with ThreadPoolExecutor(4) as ex: list(ex.map(run,jobs))
OW,OH=round(W*SC),round(H*SC)
acc=np.zeros((OH,OW,3),np.float32); ws=np.zeros((OH,OW),np.float32)
for i,j in jobs:
    t=np.asarray(Image.open(f'rt/out_{i}_{j}.png').convert('RGB').resize((1448,1086),Image.LANCZOS),np.float32)
    h,w=t.shape[:2]; x0=round(xs[i]*SC); y0=round(ys[j]*SC)
    def ramp(n,lo,hi,ov):
        r=np.ones(n,np.float32)
        if lo: r[:ov]=np.linspace(0.02,1,ov)
        if hi: r[n-ov:]=np.minimum(r[n-ov:],np.linspace(1,0.02,ov))
        return r
    ovx=round((xs[0]+TW-xs[1])*SC) ; ovy=round((TH-(ys[1]-ys[0]))*SC)
    wx=ramp(w,i>0,i<3,ovx); wy=ramp(h,j>0,j<2,ovy)
    wgt=wy[:,None]*wx[None,:]
    acc[y0:y0+h,x0:x0+w]+=t*wgt[...,None]; ws[y0:y0+h,x0:x0+w]+=wgt
out=(acc/np.maximum(ws,1e-6)[...,None]).clip(0,255).astype('uint8')
Image.fromarray(out).save('town6.png'); print('done',OW,OH,flush=True)
