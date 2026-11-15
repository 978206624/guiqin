# 构建：切图 → assets/；生成 js/assets.js（相对路径版）；输出单文件版 wuxia_town.html 和可移植的源码 zip
import base64, json, os, shutil, zipfile
from PIL import Image
G='/home/claude/gen'; R='/home/claude/game'; AD=R+'/assets'
os.makedirs(AD, exist_ok=True)
size={}
def keyed(src):
    # 网关有时给透明底、有时给绿幕底：绿幕的在这里抠掉
    import numpy as np
    from PIL import ImageFilter
    im=Image.open(src)
    if im.mode=='RGBA' and im.getchannel('A').getextrema()[0]<255: return im
    A=np.array(im.convert('RGB')).astype(int); r,g,b=A[:,:,0],A[:,:,1],A[:,:,2]
    green=(g>150)&(g>r*1.4)&(g>b*1.4)
    alpha=Image.fromarray((~green*255).astype('uint8')).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(0.8))
    m=np.maximum(r,b); A[:,:,1]=np.where(g>m+12,m,g)
    out=Image.fromarray(A.astype('uint8')).convert('RGBA'); out.putalpha(alpha); return out
def cut_row(src, names, heights):
    im=keyed(src); a=im.getchannel('A').point(lambda v:255 if v>40 else 0)
    import numpy as np
    cols=(np.array(a)>0).sum(0)>2; runs=[]; s=None
    for x,v in enumerate(cols):
        if v and s is None: s=x
        if not v and s is not None:
            if x-s>40: runs.append((s,x))
            s=None
    if s is not None: runs.append((s,len(cols)))
    cnt=(np.array(a)>0).sum(0)
    while len(runs)<len(names):          # 两个角色挨在一起时，在最宽那段中间最细的地方切开
        i=max(range(len(runs)),key=lambda k:runs[k][1]-runs[k][0]); x0,x1=runs[i]; lo=x0+(x1-x0)*3//10; hi=x0+(x1-x0)*7//10
        c=lo+int(np.argmin(cnt[lo:hi])); runs[i:i+1]=[(x0,c),(c,x1)]
    assert len(runs)==len(names),(src,runs)
    for n,(x0,x1) in zip(names,runs):
        c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
        h=heights[n]*2; w=round(c.width*h/c.height); c.resize((w,h),Image.LANCZOS).save(f'{AD}/{n}.webp',quality=92,method=6)
        size[n]=[w/2,h/2]
# 城镇角色与头像、图标、底图沿用上一轮切好的
for f in os.listdir(G+'/a'):
    if f.startswith('_'): continue
    dst={'bg.jpg':'bg_town.jpg'}.get(f, f.replace('i_','icon_') if f.startswith('i_') else f)
    shutil.copy(f'{G}/a/{f}', f'{AD}/{dst}')
for n in ['player','fox','waiter','smith','girl','vendor']:
    w,h=Image.open(f'{AD}/{n}.webp').size; size[n]=[w/2,h/2]
cut_row(G+'/enemies.png',['grunt','wolf','boss'],dict(grunt=108,wolf=72,boss=150))
cut_row(G+'/npc2.png',['elder','axiu','dummy','boar'],dict(elder=104,axiu=100,dummy=110,boar=78))
cut_row(G+'/backp.png',['b_player','b_axiu','b_girl','b_yan','b_wuchen'],dict(b_player=130,b_axiu=124,b_girl=128,b_yan=132,b_wuchen=132))
cut_row(G+'/backpet.png',['b_fox','b_deer','b_rabbit','b_hawk','b_tiger'],dict(b_fox=66,b_deer=80,b_rabbit=60,b_hawk=74,b_tiger=68))
cut_row(G+'/foep.png',['f_sword','f_tao','f_blade','f_healer','f_spear'],dict(f_sword=130,f_tao=134,f_blade=140,f_healer=130,f_spear=134))
cut_row(G+'/foepet.png',['f_panther','f_crane','f_dragon','f_panda','f_tortoise'],dict(f_panther=62,f_crane=86,f_dragon=78,f_panda=68,f_tortoise=60))
def head(src_png, idx, n_total, name, cx):
    im=Image.open(src_png).convert('RGBA'); import numpy as np
    cols=(np.array(im.getchannel('A'))>40).sum(0)>2; runs=[]; s0=None
    for x,v in enumerate(cols):
        if v and s0 is None: s0=x
        if not v and s0 is not None:
            if x-s0>40: runs.append((s0,x))
            s0=None
    if s0 is not None: runs.append((s0,len(cols)))
    x0,x1=runs[idx]; c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    sz=int(c.width*0.6); l=max(0,min(c.width-sz,int(c.width*cx-sz/2)))
    c.crop((l,0,l+sz,sz)).resize((160,160),Image.LANCZOS).save(f'{AD}/head_{name}.webp',quality=90)
head(G+'/npc2.png',0,4,'elder',0.4); head(G+'/npc2.png',1,4,'axiu',0.5)
v=Image.open(G+'/village2.png').convert('RGB'); v.save(f'{AD}/bg_village.jpg',quality=88,optimize=True); v.resize((512,384),Image.LANCZOS).save(f'{AD}/mini_village.jpg',quality=80)
wm=Image.open(G+'/world.png').convert('RGB'); wm.save(f'{AD}/world.jpg',quality=86,optimize=True); size['world']=list(wm.size)
o=Image.open(G+'/outskirts.jpg').convert('RGB'); o.save(f'{AD}/bg_outskirts.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_outskirts.jpg',quality=80)
o=Image.open(G+'/creek.jpg').convert('RGB'); o.save(f'{AD}/bg_creek.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_creek.jpg',quality=80)
o=Image.open(G+'/mist.jpg').convert('RGB'); o.save(f'{AD}/bg_mist.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_mist.jpg',quality=80)
o=Image.open(G+'/pine.jpg').convert('RGB'); o.save(f'{AD}/bg_pine.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_pine.jpg',quality=80)
o=Image.open(G+'/wolfvalley.jpg').convert('RGB'); o.save(f'{AD}/bg_wolfvalley.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_wolfvalley.jpg',quality=80)
o=Image.open(G+'/tx_forest.png').convert('RGB'); o.save(f'{AD}/bg_taixuforest.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixuforest.jpg',quality=80)
o=Image.open(G+'/tx_peak.png').convert('RGB'); o.save(f'{AD}/bg_taixupeak.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixupeak.jpg',quality=80)
o=Image.open(G+'/tx_valley.png').convert('RGB'); o.save(f'{AD}/bg_taixuvalley.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixuvalley.jpg',quality=80)
b2=Image.open(G+'/battle2.png').convert('RGB'); cw2=round(b2.height*9/16); x2=(b2.width-cw2)//2
b2.crop((x2,0,x2+cw2,b2.height)).save(f'{AD}/bg_battle2.jpg',quality=88,optimize=True)

it=Image.open(G+'/items.png').convert('RGBA'); W,H=it.size
for i,n in enumerate(['hp_potion','mp_potion','iron_sword','steel_sword','cloth_armor','leather_armor','ore','wolf_fang','sachet','coins','lantern','chest']):
    c=it.crop(((i%4)*W//4,(i//4)*H//3,(i%4+1)*W//4,(i//4+1)*H//3)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>60 else 0).getbbox())
    c.resize((128,128),Image.LANCZOS).save(f'{AD}/item_{n}.webp',quality=92)


# 装备图标（十二部位新增）：两张 4x3 图标表，绿幕底，逐格抠出
GEAR={'gear1':['wood_beads','copper_ring','leather_bracer','leather_cap','linen_belt','linen_legs','cloth_boots','bronze_bell','peace_charm','herb_pouch','iron_armor','steel_saber'],
      'gear2':['jade_pendant','silver_ring','steel_bracer','iron_helm','jade_belt','iron_greaves','cloud_boots','bagua_mirror','jade_charm','brocade_pouch','scale_armor','jade_sword']}
for sheet,names in GEAR.items():
    if not os.path.exists(f'{G}/{sheet}.png'): continue
    gi=keyed(f'{G}/{sheet}.png'); W2,H2=gi.size
    for i,n in enumerate(names):
        c=gi.crop(((i%4)*W2//4,(i//4)*H2//3,(i%4+1)*W2//4,(i//4+1)*H2//3)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>60 else 0).getbbox())
        c.resize((128,128),Image.LANCZOS).save(f'{AD}/item_{n}.webp',quality=92)


# 宝石图标：一张 3x3 图标表，绿幕底
if os.path.exists(f'{G}/gems.png'):
    gi=keyed(f'{G}/gems.png'); W3,H3=gi.size
    for i,n in enumerate(['atk','matk','hp','def','mdef','mp','dodge','spd','crit']):
        c=gi.crop(((i%3)*W3//3,(i//3)*H3//3,(i%3+1)*W3//3,(i//3+1)*H3//3)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>60 else 0).getbbox())
        c.resize((128,128),Image.LANCZOS).save(f'{AD}/item_gem_{n}.webp',quality=92)

# 锻造宝石图标：一行 5 个，绿幕底
if os.path.exists(f'{G}/forgegems.png'):
    gi=keyed(f'{G}/forgegems.png'); W5,H5=gi.size
    for i in range(5):
        c=gi.crop((i*W5//5,0,(i+1)*W5//5,H5)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>60 else 0).getbbox())
        c.resize((128,128),Image.LANCZOS).save(f'{AD}/item_forge_{i+1}.webp',quality=92)
# 红装图标：把橙装图标里的青绿色部分改成赤红，金边和深色底保持不变
import numpy as np
def redden(src,dst):
    im=Image.open(f'{AD}/item_{src}.webp').convert('RGBA'); a=np.array(im.convert('RGB').convert('HSV')).astype(float)
    hue=a[...,0]*360/255; m=(hue>70)&(hue<210)&(a[...,1]>60)
    a[...,0]=np.where(m,(352%360)*255/360,a[...,0]); a[...,1]=np.where(m,np.minimum(255,a[...,1]*1.1),a[...,1])
    out=Image.fromarray(a.astype('uint8'),'HSV').convert('RGB').convert('RGBA'); out.putalpha(im.getchannel('A')); out.save(f'{AD}/item_{dst}.webp',quality=92)
redden('jade_sword','crimson_sword'); redden('scale_armor','phoenix_armor')
# 主角行走帧：4 帧一行，统一缩放，脚底对齐、头部水平对齐，拼成一张横向精灵表
def walk_sheet(src, name, stand_h):
    import numpy as np
    im=keyed(src); a=im.getchannel('A').point(lambda v:255 if v>40 else 0); cols=(np.array(a)>0).sum(0)>2
    runs=[]; s0=None
    for x,v in enumerate(cols):
        if v and s0 is None: s0=x
        if not v and s0 is not None:
            if x-s0>40: runs.append((s0,x))
            s0=None
    if s0 is not None: runs.append((s0,len(cols)))
    assert len(runs)==4,(src,runs)
    fr=[]
    for x0,x1 in runs:
        c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
        al=np.array(c.getchannel('A'))>40; top=al[:int(c.height*0.22)]; xs=np.nonzero(top.any(0))[0]
        fr.append((c,(xs.min()+xs.max())/2))          # 头部中心的 x
    k=stand_h*2/((fr[1][0].height+fr[3][0].height)/2)   # 以两张“并脚”帧的身高为准
    fr=[(c.resize((round(c.width*k),round(c.height*k)),Image.LANCZOS),hx*k) for c,hx in fr]
    left=max(hx for c,hx in fr); right=max(c.width-hx for c,hx in fr); cw=int(left+right)+8; ch=max(c.height for c,_ in fr)+4
    sheet=Image.new('RGBA',(cw*4,ch),(0,0,0,0))
    for i,(c,hx) in enumerate(fr): sheet.alpha_composite(c,(i*cw+int(left-hx)+4,ch-c.height))
    sheet.save(f'{AD}/{name}.webp',quality=92,method=6); size[name]=[cw/2,ch/2,left/2+2]   # 每格宽、高、头部中心在格内的 x
walk_sheet(G+'/walkf.png','walk_f',118); walk_sheet(G+'/walkb.png','walk_b',118)
# 登录主视觉、六个职业的立绘和头像
lg=Image.open(G+'/login.png').convert('RGB'); lg.save(f'{AD}/login.jpg',quality=88,optimize=True)
HEAD_X=dict(m_sword=0.55,m_spear=0.66,m_mage=0.47,f_sword=0.57,f_heal=0.51,f_bow=0.49)   # 头在立绘宽度上的位置（武器和飘带会让自动找头不准）
def portraits(src, names):
    import numpy as np
    im=keyed(src); a=im.getchannel('A').point(lambda v:255 if v>40 else 0); cnt=(np.array(a)>0).sum(0); cols=cnt>2
    runs=[]; s0=None
    for x,v in enumerate(cols):
        if v and s0 is None: s0=x
        if not v and s0 is not None:
            if x-s0>40: runs.append((s0,x))
            s0=None
    if s0 is not None: runs.append((s0,len(cols)))
    while len(runs)<len(names):
        i=max(range(len(runs)),key=lambda k:runs[k][1]-runs[k][0]); x0,x1=runs[i]; lo=x0+(x1-x0)*3//10; hi=x0+(x1-x0)*7//10
        c=lo+int(np.argmin(cnt[lo:hi])); runs[i:i+1]=[(x0,c),(c,x1)]
    assert len(runs)==len(names),(src,runs)
    for n,(x0,x1) in zip(names,runs):
        c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
        c.save(f'{AD}/cls_{n}.webp',quality=90,method=6)
        hx=c.width*HEAD_X[n]; sz=int(c.height*0.2); l=int(max(0,min(c.width-sz,hx-sz/2))); t=int(c.height*0.02)
        hd=Image.new('RGBA',(sz,sz),(43,36,64,255)); hd.alpha_composite(c.crop((l,t,l+sz,t+sz)))
        hd.resize((160,160),Image.LANCZOS).save(f'{AD}/head_{n}.webp',quality=90)
portraits(G+'/male.png',['m_sword','m_spear','m_mage']); portraits(G+'/female.png',['f_sword','f_heal','f_bow'])
# 大地图：城镇用整张原图，城外用新图；各配一张小地图用的缩略图
t=Image.open(G+'/town6.jpg').convert('RGB'); t.save(f'{AD}/bg_town.jpg',quality=95); t.resize((512,round(512*t.height/t.width)),Image.LANCZOS).save(f'{AD}/mini_town.jpg',quality=80)
w=Image.open(G+'/wild.png').convert('RGB'); w.save(f'{AD}/bg_wild.jpg',quality=88,optimize=True); w.resize((512,288),Image.LANCZOS).save(f'{AD}/mini_wild.jpg',quality=80)
if os.path.exists(f'{AD}/mini.jpg'): os.remove(f'{AD}/mini.jpg')
b=Image.open(G+'/battle.png').convert('RGB'); cw=round(b.height*9/16); x0=(b.width-cw)//2
b.crop((x0,0,x0+cw,b.height)).save(f'{AD}/bg_battle.jpg',quality=88,optimize=True)

from PIL import ImageFilter
for n,q in [('bg_town',54),('bg_village',78),('bg_wild',78),('world',78),('bg_taixuforest',74),('bg_taixupeak',74),('bg_taixuvalley',74),('bg_outskirts',78),('bg_creek',78),('bg_mist',78),('bg_pine',78),('bg_wolfvalley',78)]:
    p=f'{AD}/{n}.jpg'
    if not os.path.exists(p): continue
    im=Image.open(p).convert('RGB')
    pass
    im.save(f'{AD}/{n}.webp',quality=q,method=6); os.remove(p)
files=sorted(f for f in os.listdir(AD))
keys={f.rsplit('.',1)[0]:f for f in files}
def uri(p):
    mt={'jpg':'image/jpeg','webp':'image/webp','png':'image/png'}[p.rsplit('.',1)[1]]
    return f'data:{mt};base64,'+base64.b64encode(open(p,'rb').read()).decode()
rel='// 素材清单（相对路径版）。单文件版里这一段会被换成内嵌的 data URI。\nwindow.ASSETS = '+json.dumps(dict(img={k:'assets/'+f for k,f in keys.items()},size=size),ensure_ascii=False,indent=1)+';\n'
open(R+'/js/assets.js','w').write(rel)
inl='window.ASSETS = '+json.dumps(dict(img={k:uri(f'{AD}/{f}') for k,f in keys.items()},size=size))+';'
src=open(R+'/index.src.html').read()
single=src.replace('<!--SCRIPTS-->','<script>\n'+inl+'\n</script>\n<script>\n'+open(R+'/js/masks.js').read()+'\n</script>\n<script>\n'+open(R+'/js/water.js').read()+'\n</script>\n<script>\n'+open(R+'/js/data.js').read()+'\n</script>\n<script>\n'+open(R+'/js/chat.js').read()+'\n</script>\n<script>\n'+open(R+'/js/game.js').read()+'\n</script>\n<script>\n'+open(R+'/js/front.js').read()+'\n</script>')
open('/home/claude/wuxia_town.html','w').write(single)
multi='<!doctype html>\n<html lang="zh-CN">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n<style>body{margin:0}[hidden]{display:none!important}</style>\n'+src.replace('<!--SCRIPTS-->','<script src="js/assets.js"></script>\n<script src="js/masks.js"></script>\n<script src="js/water.js"></script>\n<script src="js/data.js"></script>\n<script src="js/chat.js"></script>\n<script src="js/game.js"></script>\n<script src="js/front.js"></script>')+'\n</body>\n</html>\n'
multi=multi.replace('<div id="fit">','</head>\n<body>\n<div id="fit">',1)
open(R+'/index.html','w').write(multi)
z=zipfile.ZipFile('/home/claude/guiqin_source.zip','w',zipfile.ZIP_DEFLATED)
for p in ['index.html','js/assets.js','js/masks.js','js/water.js','js/data.js','js/chat.js','js/game.js','js/front.js','README.txt']:
    if os.path.exists(f'{R}/{p}'): z.write(f'{R}/{p}','guiqin/'+p)
for f in files: z.write(f'{AD}/{f}','guiqin/assets/'+f)
z.close()
print(len(single)//1024,'KB single;',len(files),'assets')
