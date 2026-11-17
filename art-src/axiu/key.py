import sys
from PIL import Image
import numpy as np
from scipy import ndimage as ndi
src,dst=sys.argv[1],sys.argv[2]
im=Image.open(src);a=np.asarray(im.convert('RGBA')).astype(np.float32)
r,g,b,al0=[a[...,i] for i in range(4)];gd=g-np.maximum(r,b);bgm=(gd>40)|(al0<10)
lab,n=ndi.label(bgm);edge=set(np.unique(np.concatenate([lab[0],lab[-1],lab[:,0],lab[:,-1]])))-{0}
mask=np.isin(lab,list(edge))|(al0<10)
H=a.shape[0];low=np.zeros_like(mask);low[int(H*0.86):]=True;mask|=(bgm&low)
alpha=ndi.gaussian_filter(np.where(mask,0,255).astype(np.float32),0.8)
fr=ndi.binary_dilation(mask,iterations=3)&~mask;g2=np.where(fr,np.minimum(g,np.maximum(r,b)),g)
low2=np.zeros_like(mask);low2[int(H*0.88):]=True;fix=low2&((g-np.maximum(r,b))>8)&~mask
g2=np.where(fix,np.maximum(r,b),g2);alpha=np.where(fix&((g-np.maximum(r,b))>25),0,alpha)
res=Image.fromarray(np.dstack([r,g2,b,alpha]).clip(0,255).astype('uint8'),'RGBA');res=res.crop(res.getbbox());res.save(dst)
bg=Image.new('RGBA',res.size,(90,110,90,255));bg.alpha_composite(res);bg.convert('RGB').resize((res.width//2,res.height//2)).save('chk3.png');print(res.size)
