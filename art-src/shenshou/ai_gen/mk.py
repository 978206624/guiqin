import sys,os,glob
from PIL import Image
H='../hd/'
PET={
'1221':dict(name='a serpentine Chinese dragon (逆天魔龙)',pal='MAGENTA-PURPLE scales on the back with BLUE belly scales, a flowing magenta mane and fins, curved black horns, golden-yellow eyes, five glowing VIOLET orbs orbiting its coiled body, white clouds under it, long red-magenta tail',
 body='a coiled floating dragon (no legs on a standing pose; it has small clawed forelegs)',idle='coiled and hovering on white clouds, head raised, orbs orbiting, nearly identical frames',
 atk='frame 1 coils back and lowers the head; frame 2 rears up winding the body; frame 3 lunges forward with the head and claws toward the upper-left, mouth open; frame 4 follows through, body stretched forward',
 hit='frame 1 HIT RECOIL: head snapped back, body jolted; frame 2 stagger, body recoiling low; frame 3 DODGE: body swerving sharply away to the lower-right'),
'bs20':dict(name='a nine-tailed white fox beast (玄晶天狐)',pal='CREAM-WHITE fluffy fur, NINE big fluffy tails fanned behind it each with a RED swirl flame mark, a long glowing CYAN-BLUE ribbon looping over its back, GOLD curved horns on its back and golden anklets with cyan rings on the legs, small orange FLAMES burning around the paws, red-and-white face markings with pointed ears tipped in dark red',
 body='a four-legged fox beast standing on the ground on all four legs',idle='standing calmly on four legs, tails gently fanned, nearly identical frames',
 atk='frame 1 crouches low, head lowered; frame 2 winds up, coiling back with tails flaring; frame 3 pounces forward to the upper-left, mouth open; frame 4 follow-through, body stretched forward',
 hit='frame 1 HIT RECOIL: head snapped back, body jolted; frame 2 stagger, crouching low; frame 3 DODGE: leaping sharply away to the lower-right'),
'1222':dict(name='a fox-spirit maiden (惑天玄姬)',pal='long PURPLE-VIOLET hair, a PALE-BLUE and white fan of nine fox tails behind her, a RED-and-white dress with white sleeves, a floating scroll with glowing purple orbs near her left side, a violet magic circle under her feet',
 body='a floating girl',idle='floating above the violet magic circle with feet together and toes pointed down, tails fanned, arms relaxed',
 atk='frame 1 gathers, scroll raised to her chest; frame 2 winds up, one arm swept back, tails flaring; frame 3 release, arm thrown forward to the upper-left, tails streaming; frame 4 follow-through, arm still extended',
 hit='frame 1 HIT RECOIL: jolted backward, tails flying; frame 2 stagger leaning back; frame 3 DODGE: leaning sharply away to the lower-right'),
'1223':dict(name='a snake-woman mermaid (小青娘子)',pal='long dark PURPLE hair with a teal-and-gold winged headdress, teal-and-white flowing ribbons and robes, a GREEN SCALED SNAKE TAIL (yellow-green with darker green), a green gourd lantern with gold tassels held in one hand, a golden magic circle under her tail',
 body='a hovering snake-tailed woman (no legs, she has a tail)',idle='hovering with the tail curled below, gourd held up, ribbons drifting, nearly identical frames',
 atk='frame 1 gathers, tail coiling, gourd drawn back; frame 2 winds up, gourd arm swept back, torso twisting; frame 3 release, gourd thrust forward to the upper-left, tail whipping; frame 4 follow-through, arm extended',
 hit='frame 1 HIT RECOIL: jolted backward, ribbons flying; frame 2 stagger, tail recoiling; frame 3 DODGE: swaying sharply away to the lower-right'),
'1224':dict(name='a fire-halo goddess (九天玄女)',pal='long BLACK hair, a red mark on the forehead, a layered RED-and-GOLD robe with gold trim, a huge GOLDEN FLAME crescent halo behind her, long red-gold tassels with golden bells hanging below',
 body='a floating woman',idle='floating with feet together hidden under the robe, arms relaxed, tassels and bells swaying slightly',
 atk='frame 1 gathers both hands at the waist; frame 2 winds up, one sleeve swept back; frame 3 release, arm thrown forward to the upper-left, sleeves streaming; frame 4 follow-through, arm extended',
 hit='frame 1 HIT RECOIL: jolted backward, sleeves and hair flying; frame 2 stagger; frame 3 DODGE: leaning sharply away to the lower-right'),
'1225':dict(name='a golden-armored spear general (龙翔天兵)',pal='GOLDEN ornate armor with a horned golden helmet, long RED SCARF ribbons, a long black spear shaft with a SILVER double blade and a red gem, small orange flames at the feet',
 body='a hovering armored warrior',idle='hovering with feet together and small flames under them, spear held horizontally at the waist',
 atk='frame 1 crouches and draws the spear back; frame 2 winds up, spear pulled behind; frame 3 release, thrusting the spear forward to the upper-left, lunging; frame 4 follow-through, spear extended',
 hit='frame 1 HIT RECOIL: jolted backward, scarf flying; frame 2 stagger; frame 3 DODGE: leaping sharply away to the lower-right'),
'1226':dict(name='a flame emperor warrior (龙翔帝君)',pal='a muscular figure engulfed in orange-red FLAMES with flame hair and a purple-and-white crest, ORANGE-GOLD armor, a huge double-bladed battle axe with a dark spiked shaft and silver blades, a red fire ring under his feet',
 body='a standing flame warrior',idle='standing on the fire ring with the axe held upright in one hand, flames licking, nearly identical frames',
 atk='frame 1 crouches and draws the axe back; frame 2 winds up, axe raised behind; frame 3 release, axe swung forward to the upper-left, lunging; frame 4 follow-through, axe extended',
 hit='frame 1 HIT RECOIL: jolted backward, flames flaring; frame 2 stagger; frame 3 DODGE: leaping sharply away to the lower-right'),
}
def ref(k):
    fs=sorted(glob.glob(f'{H}{k}/cg_*.png'));hs=sorted(glob.glob(f'{H}{k}/head_*.png'))
    ims=[Image.open(fs[0]).convert('RGBA')]
    if len(fs)>3: ims.append(Image.open(fs[min(3,len(fs)-1)]).convert('RGBA'))
    if hs and k not in('1221',): h=Image.open(hs[0]).convert('RGBA'); ims.append(h.resize((round(h.width*1.3),round(h.height*1.3))))
    hh=300; ims=[i.resize((round(i.width*hh/i.height),hh)) if i.height>hh else i for i in ims]
    S=Image.new('RGB',(sum(i.width for i in ims)+20*(len(ims)+1),max(i.height for i in ims)+20),(0,255,0));x=20
    for i in ims:S.paste(i,(x,10),i);x+=i.width+20
    S.save(f'ref_{k}.jpg',quality=88)
T="Flat solid pure green (#00FF00) chroma key background, no ground, no shadows, no text, no magic effects, no glow, no energy beams, no projectiles. Evenly spaced left to right with wide empty green gaps, all the same size, same camera angle, same baseline, each fully separated."
def common(k):
    p=PET[k]
    return (f"Reference image: the SAME game character shown from the FRONT in its original game art (it is {p['name']}). Study its exact design and colors and keep them EXACTLY: {p['pal']}. TASK: redraw EXACTLY this same character (same palette, same costume and details, same proportions, same painterly chibi style, same level of detail) as a game battle sprite seen FROM BEHIND in a three-quarter back view, standing at the lower-right of the screen and FACING THE UPPER-LEFT toward enemies (we mostly see its back and a little of the side of its face). Do NOT change any colors or costume details. ")
def prompt(k,kind,style=False):
    p=PET[k];pre="The SECOND input image is an already-approved back-view strip of this exact character: copy its exact palette, costume, scale, camera angle, rendering style. " if style else ""
    if kind=='atk': out=f"OUTPUT: a single horizontal strip of exactly FOUR full-body attack frames: {p['atk']}. "
    elif kind=='idle': out=f"OUTPUT: a single horizontal strip of exactly FOUR full-body IDLE frames, {p['idle']}, only tiny differences between frames. "
    else: out=f"OUTPUT: a single horizontal strip of exactly THREE full-body frames: {p['hit']}. "
    return pre+common(k)+out+T
if __name__=='__main__':
    for k in sys.argv[2:]:
        ref(k)
        for kind in (['atk'] if sys.argv[1]=='atk' else ['idle','hit']):
            open(f'p_{k}_{kind}.txt','w').write(prompt(k,kind,style=(kind!='atk')))
