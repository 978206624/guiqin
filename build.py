# 构建：切图 → assets/；生成 js/assets.js（相对路径版）；输出单文件版 dist/guiqin.html 和可移植的源码 zip
import base64, json, os, shutil, zipfile
from PIL import Image
R=os.path.dirname(os.path.abspath(__file__)); G=R+'/art-src'; AD=R+'/assets'; DIST=R+'/dist'      # 全部相对仓库根目录，换机器也能直接构建
os.makedirs(DIST, exist_ok=True)
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
for f in os.listdir(G+'/sprites-cut'):
    if f.startswith('_'): continue
    dst={'bg.jpg':'bg_town.jpg'}.get(f, f.replace('i_','icon_') if f.startswith('i_') else f)
    shutil.copy(f'{G}/sprites-cut/{f}', f'{AD}/{dst}')
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
OCC={}
wm=Image.open(G+('/world2.png' if os.path.exists(G+'/world2.png') else '/world.png')).convert('RGB'); wm.save(f'{AD}/world.jpg',quality=86,optimize=True); size['world']=list(wm.size)
o=Image.open(G+'/outskirts.jpg').convert('RGB'); o.save(f'{AD}/bg_outskirts.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_outskirts.jpg',quality=80)
o=Image.open(G+'/creek.jpg').convert('RGB'); o.save(f'{AD}/bg_creek.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_creek.jpg',quality=80)
o=Image.open(G+'/mist.jpg').convert('RGB'); o.save(f'{AD}/bg_mist.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_mist.jpg',quality=80)
o=Image.open(G+'/pine.jpg').convert('RGB'); o.save(f'{AD}/bg_pine.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_pine.jpg',quality=80)
o=Image.open(G+'/wolfvalley.jpg').convert('RGB'); o.save(f'{AD}/bg_wolfvalley.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_wolfvalley.jpg',quality=80)
o=Image.open(G+'/tx_forest.png').convert('RGB'); o.save(f'{AD}/bg_taixuforest.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixuforest.jpg',quality=80)
o=Image.open(G+'/tx_peak.png').convert('RGB'); o.save(f'{AD}/bg_taixupeak.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixupeak.jpg',quality=80)
o=Image.open(G+'/tx_valley.png').convert('RGB'); o.save(f'{AD}/bg_taixuvalley.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_taixuvalley.jpg',quality=80)
for _k in range(1,8):    # 百炼玄兵塔 1～7 层
    o=Image.open(G+f'/tower{_k}.jpg').convert('RGB'); o.save(f'{AD}/bg_tower{_k}.jpg',quality=95); o.resize((512,round(512*o.height/o.width)),Image.LANCZOS).save(f'{AD}/mini_tower{_k}.jpg',quality=80)
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
def walk_sheet(src, name, stand_h, flip=False):      # flip：逐帧水平翻转（引擎要求正面图朝右下；生成的正面图朝左下，所以要翻，帧顺序不变）
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
        if flip: c=c.transpose(Image.FLIP_LEFT_RIGHT)
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
        c.resize((max(1,c.width//2),max(1,c.height//2)),Image.LANCZOS).save(f'{AD}/cls_{n}.webp',quality=40,method=6)   # 已隐藏的旧职业立绘，缩半以省体积
        hx=c.width*HEAD_X[n]; sz=int(c.height*0.2); l=int(max(0,min(c.width-sz,hx-sz/2))); t=int(c.height*0.02)
        hd=Image.new('RGBA',(sz,sz),(43,36,64,255)); hd.alpha_composite(c.crop((l,t,l+sz,t+sz)))
        hd.resize((160,160),Image.LANCZOS).save(f'{AD}/head_{n}.webp',quality=90)
portraits(G+'/male.png',['m_sword','m_spear','m_mage']); portraits(G+'/female.png',['f_sword','f_heal','f_bow'])

# 道家女主（用户提供形象）：天音（琵琶，行走时背在身后）/ 琴魔（古琴）。行走正/背面 4 帧表、战斗背影（back.png 左天音右琴魔）、立绘与头像
DA=G+'/daoist'
walk_sheet(DA+'/walkf_ty.png','walk_f_dao_ty',118,flip=True); walk_sheet(DA+'/walkb_ty.png','walk_b_dao_ty',118)
walk_sheet(DA+'/walkf_qm.png','walk_f_dao_qm',118,flip=True); walk_sheet(DA+'/walkb_qm.png','walk_b_dao_qm',118)
cut_row(DA+'/back.png',['b_dao_ty','b_dao_qm'],dict(b_dao_ty=126,b_dao_qm=126))
def dao_head(c,name):
    sz=int(c.height*0.34); l=int(max(0,min(c.width-sz,c.width*0.5-sz/2))); t=int(c.height*0.02)
    hd=Image.new('RGBA',(sz,sz),(43,36,64,255)); hd.alpha_composite(c.crop((l,t,l+sz,t+sz))); hd.resize((160,160),Image.LANCZOS).save(f'{AD}/head_{name}.webp',quality=90)
for n,src in [('dao_ty',DA+'/port_ty.png' if os.path.exists(DA+'/port_ty.png') else DA+'/ref.png'),('dao_qm',DA+'/port_qm.png')]:
    c=keyed(src); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    c=c.resize((round(c.width*900/c.height),900),Image.LANCZOS); c.save(f'{AD}/cls_{n}.webp',quality=64,method=6); dao_head(c,n)
# 新手男 / 女：参考 kunkunxunqin 创角预览里的赤绳少年 / 青带少女，用本游戏的手绘画风重画（art-src/novice-kk/）
NK=G+'/novice-kk'
for k,n in [('nf','novice_f'),('nm','novice_m')]:
    walk_sheet(f'{NK}/walkf_{k}.png','walk_f_'+n,118,flip=True); walk_sheet(f'{NK}/walkb_{k}.png','walk_b_'+n,118)
    cut_row(f'{NK}/back_{k}.png',['b_'+n],{'b_'+n:126})
    c=keyed(f'{NK}/port_{k}.png'); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    c=c.resize((round(c.width*900/c.height),900),Image.LANCZOS); c.save(f'{AD}/cls_{n}.webp',quality=64,method=6); dao_head(c,n)
# 道家男角色（天音 / 琴魔）：人物参考新手男，风格参考女主
DM=G+'/daoist-m'
for k in ['ty','qm']:
    n='dao_'+k+'_m'
    walk_sheet(f'{DM}/walkf_{k}.png','walk_f_'+n,118,flip=True); walk_sheet(f'{DM}/walkb_{k}.png','walk_b_'+n,118)
    c=keyed(f'{DM}/port_{k}.png'); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    c=c.resize((round(c.width*900/c.height),900),Image.LANCZOS); c.save(f'{AD}/cls_{n}.webp',quality=64,method=6); dao_head(c,n)
cut_row(DM+'/back.png',['b_dao_ty_m','b_dao_qm_m'],dict(b_dao_ty_m=126,b_dao_qm_m=126))
# 道家弟子（拜入道家、尚未选分堂）：道家服饰 + 背负箜篌，art-src/dao-base/（f=女，m=男）
DB=G+'/dao-base'
for k,n in [('f','dao_dz'),('m','dao_dz_m')]:
    walk_sheet(f'{DB}/walkf_{k}.png','walk_f_'+n,118,flip=True); walk_sheet(f'{DB}/walkb_{k}.png','walk_b_'+n,118)
    cut_row(f'{DB}/back_{k}.png',['b_'+n],{'b_'+n:126})
    c=keyed(f'{DB}/port_{k}.png'); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    c=c.resize((round(c.width*900/c.height),900),Image.LANCZOS); c.save(f'{AD}/cls_{n}.webp',quality=52,method=6); dao_head(c,n)

# 施法帧动画（art-src/cast-anim，图像生成：背影施法 4 帧）→ bc_<id>：4 格等宽精灵表，脚底对齐、按首帧身高缩放到与 b_ 立绘同尺寸
def cast_anim(src,name,stand_h=126):
    import numpy as np
    im=keyed(src); a=im.getchannel('A').point(lambda v:255 if v>40 else 0); cols=(np.array(a)>0).sum(0)>2
    runs=[]; s0=None
    for x,v in enumerate(cols):
        if v and s0 is None: s0=x
        if not v and s0 is not None:
            if x-s0>30: runs.append((s0,x))
            s0=None
    if s0 is not None: runs.append((s0,len(cols)))
    assert len(runs)==4,(src,runs)
    fr=[]
    for x0,x1 in runs:
        c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
        al=np.array(c.getchannel('A'))>40; low=al[int(c.height*0.6):]; xs=np.nonzero(low.any(0))[0]
        fr.append((c,(xs.min()+xs.max())/2))               # 下半身中心的 x（站位锚点）
    eff=[fr[0][0].height,fr[1][0].height*0.88,fr[2][0].height,fr[3][0].height]      # 第 2 帧举臂过头，不算身高；各帧逐帧归一，避免生成时的大小漂移
    ks=[stand_h*2/e for e in eff]
    fr=[(c.resize((round(c.width*k),round(c.height*k)),Image.LANCZOS),ax*k) for (c,ax),k in zip(fr,ks)]
    left=max(ax for c,ax in fr); right=max(c.width-ax for c,ax in fr); cw=int(left+right)+8; ch=max(c.height for c,_ in fr)+4
    sheet=Image.new('RGBA',(cw*4,ch),(0,0,0,0))
    for i,(c,ax) in enumerate(fr): sheet.alpha_composite(c,(i*cw+int(left-ax)+4,ch-c.height))
    sheet.save(f'{AD}/{name}.webp',quality=90,method=6); size[name]=[cw/2,ch/2]
CA=G+'/cast-anim'
for n in ['dao_dz','dao_dz_m','dao_qm','dao_qm_m','dao_ty','dao_ty_m']:
    if os.path.exists(f'{CA}/{n}.png'): cast_anim(f'{CA}/{n}.png','bc_'+n)
# 墨家（参考 kunkunxunqin 的 mojia_hd_v1 像素角色，用本作平滑手绘风重画，art-src/mojia/<堂>_<m|f>/）：猛士(ms,双刃斧) / 遁甲(dj,剑盾) / 墨家弟子(dz,背剑)
MJ=G+'/mojia'
for h in ['ms','dj','dz']:
    for g in ['f','m']:
        n='mo_'+h+('_m' if g=='m' else ''); d=f'{MJ}/{h}_{g}'
        if not os.path.exists(d+'/walkf.png'): continue
        walk_sheet(d+'/walkf.png','walk_f_'+n,118,flip=True); walk_sheet(d+'/walkb.png','walk_b_'+n,118)
        cut_row(d+'/back.png',['b_'+n],{'b_'+n:126})
        c=keyed(d+'/port.png'); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
        c=c.resize((round(c.width*900/c.height),900),Image.LANCZOS); c.save(f'{AD}/cls_{n}.webp',quality=58,method=6); dao_head(c,n)
        if os.path.exists(d+'/cast.png'): cast_anim(d+'/cast.png','bc_'+n)
# 神兽：直接用原作高清手绘帧（kunkunxunqin client/assets/pet_handpainted，tools/extract_hdpet.py 解出 PNG，tools/pet_cfg.py 从 aef/manifest 生成 art-src/shenshou/pets.json）。
# 各帧按 manifest 的 region 摆回 1000×600 画布（身体原点 (500,560)），所有用到的帧裁成同一大小格子、水平翻转（原图朝右，本作我方朝左），拼成网格图集 bp_ss_<id>；
# b_ss_<id> = 待机首帧的紧裁剪静图（地图 / 面板用）。图集元数据（格子大小、锚点、帧序列、释放帧）写进 ASSETS.pet。
petmeta={}
def hd_pet(k, H, q=0.75):
    cfg=json.load(open(f'{G}/shenshou/pets.json'))[k]; A=cfg['anims']; reg=cfg['region']; hd=f'{G}/shenshou/hd/{k}'
    def png(f): return f'{hd}/zd_{int(f):03d}.png' if str(f).isdigit() else f'{hd}/zd_{f.split("-")[0]}-{int(f.split("-")[1]):03d}.png'
    if k=='1221':      # 逆天魔龙：原作 manifest 的战斗兼容动画（待机 / 攻击 / 受击 / 闪避）
        seq={'idle':['idle-1','idle-2','idle-3','idle-4','idle-5','idle-4','idle-3','idle-2'],'atk':['attack-1','attack-2','attack-3','attack-4'],'hit':['hit-1','hit-2','hit-3'],'dodge':['dodge-1','dodge-2','dodge-3','dodge-4']}
    else:
        A=[[str(x) for x in a] for a in A]; idle=A[0]; w0=reg[idle[0]][2]
        seq={'idle':idle,'atk':A[1]+A[2],'hit':A[4]}
        n=0
        for a in A[3:]:
            if a is A[4]: continue
            if max(reg[f][2] for f in a)>w0*1.3: n+=1; seq[f'sk{n}']=a
        seq['dodge']=seq['hit']
    used=[]
    for v in seq.values():
        for f in v:
            if f not in used: used.append(f)
    x0=min(reg[f][0] for f in used); x1=max(reg[f][0]+reg[f][2] for f in used); y0=min(reg[f][1] for f in used); y1=max(reg[f][1]+reg[f][3] for f in used)
    x0=min(x0,500-60); x1=max(x1,500+60); cw,ch=x1-x0,y1-y0
    i0=reg[seq['idle'][0]]; ls=H/i0[3]       # 以待机首帧的身高为准：身高 H 个游戏像素
    cols=min(6,len(used)); rows=(len(used)+cols-1)//cols; sc=q
    tw,th=round(cw*sc),round(ch*sc); sheet=Image.new('RGBA',(tw*cols,th*rows),(0,0,0,0)); idx={}
    for n,f in enumerate(used):
        im=Image.open(png(f)).convert('RGBA'); rx,ry,rw,rh=reg[f]; assert im.size==(rw,rh),(k,f,im.size,rw,rh)
        cv=Image.new('RGBA',(cw,ch),(0,0,0,0)); cv.alpha_composite(im,(rx-x0,ry-y0)); cv=cv.transpose(Image.FLIP_LEFT_RIGHT).resize((tw,th),Image.LANCZOS)
        sheet.alpha_composite(cv,((n%cols)*tw,(n//cols)*th)); idx[f]=n
    sheet.save(f'{AD}/bp_ss_{k}.webp',quality=88,method=6)
    st=Image.open(png(seq['idle'][0])).convert('RGBA'); st=st.transpose(Image.FLIP_LEFT_RIGHT); st=st.crop(st.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox())
    st=st.resize((round(st.width*q),round(st.height*q)),Image.LANCZOS); st.save(f'{AD}/b_ss_{k}.webp',quality=92,method=6)
    size[f'b_ss_{k}']=[round(st.width/q*ls*0.0+st.width/q*ls,1),H]
    ms={'idle':130,'hit':95,'dodge':90}
    out={}; rel={}
    for name,v in seq.items():
        out[name]=[[idx[f],ms.get(name,115)] for f in v]
        if name!='idle' and name not in('hit','dodge'):
            rel[name]=max(range(len(v)),key=lambda i:reg[v[i]][2]*reg[v[i]][3])
    petmeta[f'b_ss_{k}']=dict(sheet=f'bp_ss_{k}',cols=cols,rows=rows,cw=round(cw*ls,1),ch=round(ch*ls,1),ox=round((x1-500)*ls,1),by=round((y1-(i0[1]+i0[3]))*ls,1),H=H,seq=out,rel=rel)
    size[f'bp_ss_{k}']=[round(cw*ls,1),round(ch*ls,1)]
if os.path.exists(f'{G}/shenshou/pets.json'):
    for k,H in [('1221',158),('1222',160),('1223',150),('1224',150),('1225',150),('1226',165),('1227',142)]:
        if os.path.isdir(f'{G}/shenshou/hd/{k}'): hd_pet(k,H)
# 神兽斜向重绘（art-src/shenshou/ai/<id>/{idle,atk,hit}.png：以原作帧为参考、用生图重画成"背面三四视角、朝左上"的连帧条，绿幕或透明底）。
# 有这套图的神兽用它替换上面的原作侧视帧：同样输出 bp_ss_<id> 图集与 ASSETS.pet 元数据（序列 idle / atk / hit / dodge），技能特效由战斗里的通用特效补。
def ai_strip(src,n):
    import numpy as np
    im=keyed(src).convert('RGBA'); a=np.array(im.getchannel('A'))>40; cols=a.sum(0)>2; runs=[]; s0=None
    for x,v in enumerate(cols):
        if v and s0 is None: s0=x
        if not v and s0 is not None:
            if x-s0>40: runs.append((s0,x))
            s0=None
    if s0 is not None: runs.append((s0,len(cols)))
    while len(runs)>n:                      # 碎块并入最近的大块（飘带、小龙可能与主体断开）
        i=min(range(len(runs)-1),key=lambda k:runs[k+1][0]-runs[k][1]); runs[i:i+2]=[(runs[i][0],runs[i+1][1])]
    assert len(runs)==n,(src,runs)
    fr=[]
    for x0,x1 in runs:
        c=im.crop((x0,0,x1,im.height)); c=c.crop(c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox()); al=np.array(c.getchannel('A'))>40
        low=al[int(c.height*0.8):]; xs=np.nonzero(low.any(0))[0]; fr.append((c,float(xs.mean()) if len(xs) else c.width/2))
    return fr
def ai_pet(k,H,q=0.9):
    d=f'{G}/shenshou/ai/{k}'; spec=[('idle',4),('atk',4),('hit',3)]; strips={}
    for nm,n in spec:
        if os.path.exists(f'{d}/{nm}.png'): strips[nm]=ai_strip(f'{d}/{nm}.png',n)
    if 'idle' not in strips: return
    med=lambda L:sorted(L)[len(L)//2]
    h0=med([c.height for c,_ in strips['idle']]); frames=[]; seq={}
    for nm,fr in strips.items():
        k2=h0/med([c.height for c,_ in fr]) if nm!='idle' else 1.0        # 各条按身高中位数对齐到待机
        ids=[]
        for c,ax in fr: frames.append((c.resize((round(c.width*k2),round(c.height*k2)),Image.LANCZOS),ax*k2)); ids.append(len(frames)-1)
        seq[nm]=ids
    seq['idle']=seq['idle'][:1]                                              # 待机只用首帧（生图的待机帧之间姿势、体型有漂移，循环播会抖），呼吸感交给 CSS 的 .pa 动画
    if 'hit' in seq: seq['dodge']=[seq['hit'][-1]]; seq['hit']=seq['hit'][:2]
    L=max(ax for c,ax in frames); Rr=max(c.width-ax for c,ax in frames); half=int(max(L,Rr))+6; cw=half*2; ch=max(c.height for c,_ in frames)+4
    ls=H/h0; cols=min(6,len(frames)); rows=(len(frames)+cols-1)//cols; tw,th=round(cw*q),round(ch*q)
    sheet=Image.new('RGBA',(tw*cols,th*rows),(0,0,0,0))
    for n,(c,ax) in enumerate(frames):
        cv=Image.new('RGBA',(cw,ch),(0,0,0,0)); cv.alpha_composite(c,(int(half-ax),ch-c.height)); cv=cv.resize((tw,th),Image.LANCZOS); sheet.alpha_composite(cv,((n%cols)*tw,(n//cols)*th))
    sheet.save(f'{AD}/bp_ss_{k}.webp',quality=88,method=6)
    c0,_=frames[seq['idle'][0]]; c0.resize((round(c0.width*q),round(c0.height*q)),Image.LANCZOS).save(f'{AD}/b_ss_{k}.webp',quality=92,method=6); size[f'b_ss_{k}']=[round(c0.width*ls,1),H]
    ms={'idle':150,'hit':110,'dodge':110}; out={nm:[[i,ms.get(nm,120)] for i in v] for nm,v in seq.items()}
    petmeta[f'b_ss_{k}']=dict(sheet=f'bp_ss_{k}',cols=cols,rows=rows,cw=round(cw*ls,1),ch=round(ch*ls,1),ox=round(half*ls,1),by=0,H=H,seq=out,rel={'atk':2})
    size[f'bp_ss_{k}']=[round(cw*ls,1),round(ch*ls,1)]
for k,H in [('1227',142),('1222',160),('1223',150),('1224',150),('1225',150),('1226',165),('1221',158),('bs20',112)]:
    if os.path.isdir(f'{G}/shenshou/ai/{k}'): ai_pet(k,H)
# 神兽弹道特效：从原作技能帧里切出身体以外的光束 / 龙焰（x≥650 的部分，原图朝右、保持朝右），战斗里按目标方向旋转、飞向目标。
def pfx_pet(k,H,sets,q=0.9,boost=1.5):
    cfg=json.load(open(f'{G}/shenshou/pets.json'))[k]; reg=cfg['region']; hd=f'{G}/shenshou/hd/{k}'; ls=H/reg[str(cfg['anims'][0][0])][3]
    crops=[]; amap={}; scm={}
    for sk,(ids,xcut,sc) in sets.items():
        amap[sk]=[]; scm[sk]=sc
        for f in ids:
            fn=f'{hd}/zd_{int(f):03d}.png' if str(f).isdigit() else f'{hd}/zd_{str(f).split("-")[0]}-{int(str(f).split("-")[1]):03d}.png'
            im=Image.open(fn).convert('RGBA'); rx,ry,rw,rh=reg[str(f)]
            cv=Image.new('RGBA',(1000,600),(0,0,0,0)); cv.alpha_composite(im,(rx,ry)); c=cv.crop((xcut,0,1000,600)); bb=c.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox()
            c=c.crop(bb); amap[sk].append(len(crops)); crops.append(c)
    cw=max(c.width for c in crops); ch=max(c.height for c in crops)
    sheet=Image.new('RGBA',(cw*len(crops),ch),(0,0,0,0))
    for i,c in enumerate(crops): sheet.alpha_composite(c,(i*cw,(ch-c.height)//2))
    sheet=sheet.resize((round(sheet.width*q),round(sheet.height*q)),Image.LANCZOS); sheet.save(f'{AD}/pfx_ss_{k}.webp',quality=90,method=6)
    petmeta[f'b_ss_{k}']['pfx']=dict(sheet=f'pfx_ss_{k}',n=len(crops),cw=round(cw*ls*boost,1),ch=round(ch*ls*boost,1),map=amap,sc=scm)
    size[f'pfx_ss_{k}']=[round(cw*ls*boost,1),round(ch*ls*boost,1)]
# 各神兽弹道：{技能: ([原作帧], 裁切起点 x, 放大倍数)}
PFX={'1227':{'ss_hit':([9,10],650,1),'ss_sl_bw':([15,16],650,1),'ss_kl':([25,15,16],650,1.3)},
 '1222':{'ss_hit':([35,36],650,1),'ss_sy':([28,29],650,1.2)},
 '1223':{'ss_hit':([18,19],650,1),'ss_sx':([22,23],690,1.1),'ss_tsy':([22,23],690,1.3)},
 '1224':{'ss_hit':([18],640,1),'ss_ts':([17,18],640,1.2)},
 '1225':{'ss_hit':([18,19],730,1),'ss_sg':([18,19],730,1.3)},
 '1221':{'ss_hit':(['attack-2','attack-3'],650,0.9),'ss_qk':(['attack-2','attack-3'],650,1.2)},
 'bs20':{'attack':([19,20],650,1),'bite':([19,20],650,1),'foxfire':([19,20],650,1.3)},
 '1226':{'ss_hit':([17,18,19,20],700,1),'ss_th':([17,18,19],700,1.5),'ss_dy':([18,19,20],700,1.2)}}
for k,H in [('1227',142),('1222',160),('1223',150),('1224',150),('1225',150),('1226',165),('1221',158),('bs20',112)]:
    if f'b_ss_{k}' in petmeta and os.path.isdir(f'{G}/shenshou/hd/{k}'): pfx_pet(k,H,PFX[k])
# 神兽展示图（面板 / 图鉴 / 地图正面）：原作待机首帧，保持原作朝向，紧裁剪，身高与战斗图一致
for k,H,f in [('1221',158,'idle-001'),('1222',160,'009'),('1223',150,'001'),('1224',150,'009'),('1225',150,'009'),('1226',165,'005'),('1227',142,'001'),('bs20',112,'013')]:
    p=f'{G}/shenshou/hd/{k}/zd_{f}.png'
    if os.path.exists(p):
        im=Image.open(p).convert('RGBA'); im=im.crop(im.getchannel('A').point(lambda v:255 if v>40 else 0).getbbox()); sc=H*2/im.height
        im.resize((round(im.width*sc),round(im.height*sc)),Image.LANCZOS).save(f'{AD}/pd_ss_{k}.webp',quality=92,method=6); size[f'pd_ss_{k}']=[round(im.width*sc/2,1),H]
# 大地图：城镇用整张原图，城外用新图；各配一张小地图用的缩略图
t=Image.open(G+'/town6.jpg').convert('RGB'); t.save(f'{AD}/bg_town.jpg',quality=95); t.resize((512,round(512*t.height/t.width)),Image.LANCZOS).save(f'{AD}/mini_town.jpg',quality=80)
w=Image.open(G+'/wild.png').convert('RGB'); w.save(f'{AD}/bg_wild.jpg',quality=88,optimize=True); w.resize((512,288),Image.LANCZOS).save(f'{AD}/mini_wild.jpg',quality=80)
if os.path.exists(f'{AD}/mini.jpg'): os.remove(f'{AD}/mini.jpg')
b=Image.open(G+'/battle.png').convert('RGB'); cw=round(b.height*9/16); x0=(b.width-cw)//2
b.crop((x0,0,x0+cw,b.height)).save(f'{AD}/bg_battle.jpg',quality=88,optimize=True)

from PIL import ImageFilter
# 多文件版（assets/）用高清质量；单文件版有 16MB 上限，拼 HTML 时再把大图按 LOWQ 重压一遍（见下面 uri()）
HQ=dict(bg_town=82,bg_village=80,bg_wild=80,world=82,bg_battle=80,bg_battle2=80,login=82,bg_taixuforest=80,bg_taixupeak=80,bg_taixuvalley=80,bg_outskirts=80,bg_creek=80,bg_mist=80,bg_pine=80,bg_wolfvalley=80,bg_tower1=80,bg_tower2=80,bg_tower3=80,bg_tower4=80,bg_tower5=80,bg_tower6=80,bg_tower7=80)
LOWQ=dict(bg_town=24,bg_village=60,bg_wild=78,world=40,bg_battle=72,bg_battle2=72,login=74,bg_taixuforest=74,bg_taixupeak=74,bg_taixuvalley=74,bg_outskirts=70,bg_creek=70,bg_mist=70,bg_pine=56,bg_wolfvalley=78,bg_tower1=64,bg_tower2=64,bg_tower3=64,bg_tower4=64,bg_tower5=64,bg_tower6=64,bg_tower7=64)
for n,q in HQ.items():
    p=f'{AD}/{n}.jpg'
    if not os.path.exists(p): continue
    im=Image.open(p).convert('RGB')
    pass
    im.save(f'{AD}/{n}.webp',quality=q,method=6); os.remove(p)
# 技能高清光效：art-src/fx/<名>.png 是 4 列×2 行、黑底的 8 帧图（图像生成，每格方形），缩成 1024×512 的精灵表，战斗里用 screen 混合播放
for k in ['wave','mist','soul','chain','ring','heal','notes','revive','mo_tiger','mo_roar','mo_slash','mo_blood','mo_pojia','mo_bone','mo_song','mo_dragon']:
    sp=f'{G}/fx/{k}.png'
    if os.path.exists(sp):                       # 黑底转透明：alpha = 三通道最大值，颜色按 alpha 还原（等价于加色发光，不依赖混合模式）
        import numpy as np
        a=np.asarray(Image.open(sp).convert('RGB').resize((1024,512),Image.LANCZOS)).astype(np.float32)/255; al=a.max(2); al=np.clip((al-0.02)/0.98,0,1)
        rgb=np.where(al[...,None]>0.003,a/np.maximum(al[...,None],0.003),0).clip(0,1)
        Image.fromarray((np.dstack([rgb,al])*255).astype('uint8'),'RGBA').save(f'{AD}/fx_{k}.webp',quality=85,method=6)
# 原作形象特效（art-src/fx/cast/<名>.png：2×2 四帧、黑底）→ fxc_<名>.webp（黑底转透明）
for k in ['wave','mist','soul','chain','heal','notes','revive']:
    sp=f'{G}/fx/cast/{k}.png'
    if os.path.exists(sp):                       # 生成图尺寸不一：按各自的 2×2 切开，每格等比缩进 768×512 的格子再拼回 1536×1024
        import numpy as np
        src_im=Image.open(sp).convert('RGB'); W0,H0=src_im.size; sheet=Image.new('RGB',(1536,1024),(0,0,0))
        for ci in range(4):
            cx,cy=(ci%2)*W0//2,(ci//2)*H0//2; cell=src_im.crop((cx,cy,cx+W0//2,cy+H0//2)); r=min(768/cell.width,512/cell.height)
            cell=cell.resize((round(cell.width*r),round(cell.height*r)),Image.LANCZOS); sheet.paste(cell,((ci%2)*768+(768-cell.width)//2,(ci//2)*512+(512-cell.height)//2))
        a=np.asarray(sheet).astype(np.float32)/255; fl=0.16 if k=='wave' else 0.04; al=np.clip((a.max(2)-fl)/(1-fl),0,1)
        rgb=np.where(al[...,None]>0.003,a/np.maximum(al[...,None],0.003),0).clip(0,1)
        Image.fromarray((np.dstack([rgb,al])*255).astype('uint8'),'RGBA').save(f'{AD}/fxc_{k}.webp',quality=85,method=6)
# kunkunxunqin 项目的 NPC 静态图（art-src/kunkun-npc/<资源名>.png，1000×600 画布里一个小人）：裁掉空白 → kk_<名> 立绘（显示高度见表，2 倍存储）+ head_kk_<名> 头像
# 当前接入第一章用到的；后续章节的（npc_qxz 赵村村长 / npc_xhs 萧恨水 / npc_hx 韩信 / npc15 装备商人 / npc_bs 白素）原图已在 art-src/kunkun-npc，用到时加进来（单文件版有 16MB 上限）
KK_HEAD = dict(npc07=(0.1,0,0.9,0.36), npc13=(0.05,0,0.95,0.42), npc14=(0.05,0,0.9,0.40), npc33=(0.08,0,0.92,0.38), npc45=(0.15,0.02,0.85,0.48), npc47=(0.3,0,0.85,0.40), npc57=(0.25,0.05,0.75,0.50), npc_zhw=(0.1,0,0.9,0.38), npc_cyblq=(0.12,0.02,0.65,0.55), mo_ms=(0.30,0.02,0.75,0.34), mo_dj=(0.30,0,0.72,0.30))   # 各 NPC 头部在立绘上的范围（占身高/身宽的比例）
KK_NPC = dict(npc14=104, npc_zhw=118, npc_cyblq=132, npc13=104, npc07=104, npc33=104, npc57=150, npc45=150, npc47=156, mo_ms=150, mo_dj=150)   # mo_ms/mo_dj 墨家猛士/遁甲堂主（平滑重绘）； npc57 道家门主 / npc45 琴魔堂主 / npc47 天音堂主
for n,hh in KK_NPC.items():
    sp=f'{G}/kunkun-npc/{n}.png'
    if not os.path.exists(sp): continue
    im=Image.open(sp).convert('RGBA'); bb=im.getbbox(); c=im.crop(bb); h=hh*2; w=round(c.width*h/c.height)
    c.resize((w,h),Image.LANCZOS).save(f'{AD}/kk_{n}.webp',quality=94,method=6); size['kk_'+n]=[w/2,h/2]
    x0,y0,x1,y1=KK_HEAD.get(n,(0.1,0,0.9,0.3)); top=c.crop((round(c.width*x0),round(c.height*y0),round(c.width*x1),round(c.height*y1)))   # 对话头像：只截“大头”（头发/头饰到下巴）
    s=max(top.width,top.height); sq=Image.new('RGBA',(s,s),(0,0,0,0)); sq.paste(top,((s-top.width)//2,(s-top.height)//2))
    sq.resize((160,160),Image.LANCZOS).save(f'{AD}/head_kk_{n}.webp',quality=92)
files=sorted(f for f in os.listdir(AD))
keys={f.rsplit('.',1)[0]:f for f in files}
def uri(p):
    mt={'jpg':'image/jpeg','webp':'image/webp','png':'image/png'}[p.rsplit('.',1)[1]]
    nm=os.path.basename(p).rsplit('.',1)[0]
    if nm in LOWQ and p.endswith('.webp'):                      # 单文件版：大背景重压到 LOWQ，控制在 16MB 内
        import io; b=io.BytesIO(); Image.open(p).convert('RGB').save(b,'WEBP',quality=LOWQ[nm],method=6); return f'data:{mt};base64,'+base64.b64encode(b.getvalue()).decode()
    return f'data:{mt};base64,'+base64.b64encode(open(p,'rb').read()).decode()
rel='// 素材清单（相对路径版）。单文件版里这一段会被换成内嵌的 data URI。\nwindow.ASSETS = '+json.dumps(dict(img={k:'assets/'+f for k,f in keys.items()},size=size,pet=petmeta,occ=OCC),ensure_ascii=False,indent=1)+';\n'
open(R+'/js/assets.js','w').write(rel)
inl='window.ASSETS = '+json.dumps(dict(img={k:uri(f'{AD}/{f}') for k,f in keys.items()},size=size,pet=petmeta,occ=OCC))+';'
src=open(R+'/index.src.html').read()
single=src.replace('<!--SCRIPTS-->','<script>\n'+inl+'\n</script>\n<script>\n'+open(R+'/js/masks.js').read()+'\n</script>\n<script>\n'+open(R+'/js/water.js').read()+'\n</script>\n<script>\n'+open(R+'/js/data.js').read()+'\n</script>\n<script>\n'+open(R+'/js/story.js').read()+'\n</script>\n<script>\n'+open(R+'/js/chat.js').read()+'\n</script>\n<script>\n'+open(R+'/js/game.js').read()+'\n</script>\n<script>\n'+open(R+'/js/front.js').read()+'\n</script>')
open(DIST+'/guiqin.html','w').write(single)
if os.environ.get('GUIQIN_PUBLISH_COPY'): open(os.environ['GUIQIN_PUBLISH_COPY'],'w').write(single)      # 可选：同时复制到别处（比如要发布的位置）
multi='<!doctype html>\n<html lang="zh-CN">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">\n<style>body{margin:0}[hidden]{display:none!important}</style>\n'+src.replace('<!--SCRIPTS-->','<script src="js/assets.js"></script>\n<script src="js/masks.js"></script>\n<script src="js/water.js"></script>\n<script src="js/data.js"></script>\n<script src="js/story.js"></script>\n<script src="js/chat.js"></script>\n<script src="js/game.js"></script>\n<script src="js/front.js"></script>')+'\n</body>\n</html>\n'
multi=multi.replace('<div id="fit">','</head>\n<body>\n<div id="fit">',1)
open(R+'/index.html','w').write(multi)
# 发布用：不带 doctype / head 的页面片段（Artifact 会自己包一层），脚本和图片作为旁边的文件
pub=src.replace('<!--SCRIPTS-->','<script src="js/assets.js"></script>\n<script src="js/masks.js"></script>\n<script src="js/water.js"></script>\n<script src="js/data.js"></script>\n<script src="js/story.js"></script>\n<script src="js/chat.js"></script>\n<script src="js/game.js"></script>\n<script src="js/front.js"></script>')
open(DIST+'/pub_index.html','w').write(pub)
z=zipfile.ZipFile(DIST+'/guiqin_source.zip','w',zipfile.ZIP_DEFLATED)
for p in ['index.html','js/assets.js','js/masks.js','js/water.js','js/data.js','js/story.js','js/chat.js','js/game.js','js/front.js']:
    if os.path.exists(f'{R}/{p}'): z.write(f'{R}/{p}','guiqin/'+p)
for f in files: z.write(f'{AD}/{f}','guiqin/assets/'+f)
z.close()
print(len(single)//1024,'KB single;',len(files),'assets')
