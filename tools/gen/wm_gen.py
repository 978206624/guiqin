import subprocess, numpy as np, cv2, os
from PIL import Image
CW,CH=2300,2300; KX,KY=638,380
old=Image.open('world.png').convert('RGB'); print(old.size)
canvas=np.full((CH,CW,3),(205,205,205),np.uint8); known=np.zeros((CH,CW),bool)
o=np.asarray(old); canvas[KY:KY+o.shape[0],KX:KX+o.shape[1]]=o
M=80; known[KY+M:KY+o.shape[0]-M,KX+M:KX+o.shape[1]-M]=True      # 去掉原图画框
# 三个空白椭圆标签位：检测后扩一圈，当成未知区域
im=cv2.cvtColor(o,cv2.COLOR_RGB2BGR); g=cv2.cvtColor(im,cv2.COLOR_BGR2GRAY).astype(np.float32)
mu=cv2.blur(g,(15,15)); sd=np.sqrt(np.maximum(cv2.blur(g*g,(15,15))-mu*mu,0))
b,gg,r=[im[...,i].astype(int) for i in range(3)]
bm=((sd<5)&(r>205)&(gg>180)&(b>120)&(b<190)&(r-b>35)).astype(np.uint8)
bm=cv2.morphologyEx(bm,cv2.MORPH_CLOSE,np.ones((9,9),np.uint8)); bm=cv2.morphologyEx(bm,cv2.MORPH_OPEN,np.ones((25,25),np.uint8))
n,lab,st,cen=cv2.connectedComponentsWithStats(bm); ov=np.zeros((CH,CW),np.uint8); cents=[]
for i in range(1,n):
    if st[i,4]>4000:
        m1=(lab==i).astype(np.uint8); m1=cv2.dilate(m1,cv2.getStructuringElement(cv2.MORPH_ELLIPSE,(61,61)))
        ov[KY:KY+m1.shape[0],KX:KX+m1.shape[1]]|=m1; cents.append((int(cen[i][0])+KX,int(cen[i][1])+KY))
known&=(ov==0); print('ovals',cents,flush=True)
os.makedirs('wm',exist_ok=True)
BASE="""This image is part of one huge ancient Chinese ink-wash and watercolour world map painted on aged parchment (soft misty mountains, winding rivers bright turquoise-grey, bamboo and pine forests, small thatched hamlets, rocky cliffs, drifting clouds and mist, delicate brush linework, warm beige paper tones). The plain flat grey area is EMPTY: paint it by seamlessly CONTINUING the existing painting in exactly the same art style, colour palette, brush technique, scale of features and level of detail, so the join is invisible. Keep the existing painted part exactly unchanged. Roads or rivers that cross the boundary must continue naturally. The whole frame is filled with painted landscape seen from a high 3/4 bird's-eye view: no frame, no border, no text, no labels, no compass, no seal stamps, no people close-up. """
REG={
'L':"Content for the new area: the far west — layered misty mountain ranges, a tall waterfall, pine forest, a quiet mountain stream valley, a few distant pavilions on cliffs.",
'R':"Content for the new area: the far east — a broad river with a stone arch bridge, rocky peaks with a mountain pass, pine woods, terraced farmland and a distant cliff-top workshop with wooden machinery.",
'T':"Content for the new area: the far north — a high mountain range wrapped in clouds, deep misty gorges with waterfalls, ancient twisted trees and winding mountain paths.",
'B':"Content for the new area: the far south — dense bamboo and pine forests, rocky hills, a winding dirt road, wild grassland and a lake shore, a small hidden temple among bamboo.",
'TL':"Content for the new area: the north-west corner — snowy-tipped misty peaks, cloud sea, cliffs and tall pines.",
'TR':"Content for the new area: the north-east corner — rugged mountains with a sea of clouds, and a distant waterfall.",
'BL':"Content for the new area: the south-west corner — a bamboo grove on cliffs, a quiet pond, mossy rocks, pine trees.",
'BR':"Content for the new area: the south-east corner — green hills, a river meeting a lake, reed beds and a few boats, distant mountains."}
REG['O']="Content for the new area: paint natural scenery here — misty hills, a small pond or stream, pine trees, rocks and drifting mist, matching the surrounding landscape. Do not draw any plaque, frame or flat blank parchment patch."
OV=[]
for i,(cx,cy) in enumerate(cents):
    x=min(max(cx-724,0),CW-1448); y=min(max(cy-543,0),CH-1086); OV.append(('O%d'%i,x,y,1448,1086))
W0=[('L',0,380,1086,1448),('R',1214,380,1086,1448),('T',426,0,1448,1086),('B',426,1214,1448,1086),
   ('TL',0,0,1448,1086),('TR',852,0,1448,1086),('BL',0,1214,1448,1086),('BR',852,1214,1448,1086)]
W=OV+W0
for name,x,y,w,h in W:
    crop=canvas[y:y+h,x:x+w].copy(); km=known[y:y+h,x:x+w]; crop[~km]=(205,205,205)
    inp=f'wm/in_{name}.jpg'; Image.fromarray(crop).save(inp,quality=94)
    open(f'wm/p_{name}.txt','w').write(BASE+REG[name[0] if name[0]=='O' else name])
    size='2048x1536' if w>h else '1536x2048'
    for a in range(3):
        r=subprocess.run(['python3','/root/opt/gpt-image/gpt_image.py','edit','-i',inp,'--prompt-file',f'wm/p_{name}.txt','-o',f'wm/out_{name}.png','--size',size,'--quality','high'],stdout=open(f'wm/out_{name}.log','w'),stderr=subprocess.STDOUT)
        if r.returncode==0 and os.path.exists(f'wm/out_{name}.png'): break
    g=np.asarray(Image.open(f'wm/out_{name}.png').convert('RGB').resize((w,h),Image.LANCZOS),np.float32)
    d=cv2.distanceTransform(km.astype(np.uint8),cv2.DIST_L2,3)
    a=np.where(km,np.clip(1-d/110,0,1),1.0).astype(np.float32)
    if name[0]=='O': a=a*(cv2.GaussianBlur(ov[y:y+h,x:x+w].astype(np.float32),(0,0),6))
    # only blend gen into known pixels near the unknown boundary
    reg=canvas[y:y+h,x:x+w].astype(np.float32)
    new=reg*(1-a[...,None])+g*a[...,None]
    canvas[y:y+h,x:x+w]=new.clip(0,255).astype(np.uint8)
    if name[0]=='O': known[y:y+h,x:x+w]|=ov[y:y+h,x:x+w]>0
    else: known[y:y+h,x:x+w]=True
    Image.fromarray(canvas).resize((CW//2,CH//2)).save('wm/progress.jpg',quality=85); print('done',name,flush=True)
Image.fromarray(canvas).save('world2.png'); print('ALL',flush=True)
