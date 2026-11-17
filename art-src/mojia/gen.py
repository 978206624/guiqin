import os,sys,subprocess
from concurrent.futures import ThreadPoolExecutor
D=os.path.dirname(os.path.abspath(__file__))
GREEN="Background: completely flat solid pure green (#00FF00) chroma key, no ground, no cast shadows, no gradients, no text, no numbers, no labels, no borders."
CH={
 'ms_m':"a young male Mo-sect fierce warrior ('mengshi'): spiky brown-red hair, a small gold winged head plate on the forehead, red scarf at the neck, ornate gold plate armor with red trim, a long flowing red cape, gold greaves, holding a long dark-blue-handled DOUBLE-BLADED battle axe with two crescent silver blades",
 'ms_f':"a young female Mo-sect fierce warrior ('mengshi'): long wavy red hair with a white streak in the front, a small gold crown, white fur collar, red and purple ornate armor with gold trim, a feathered tattered skirt, a long red cape, gold boots, holding a long dark-blue-handled DOUBLE-BLADED battle axe with two crescent silver blades",
 'dj_m':"a young male Mo-sect guardian ('dunjia'): spiky brown-red hair, a small gold head plate, red scarf, ornate gold plate armor, a long red cape, gold greaves, carrying a large ornate dark-purple and gold KITE SHIELD with a teal gem on one arm and a straight sword on the back/at the hip",
 'dj_f':"a young female Mo-sect guardian ('dunjia'): long wavy red hair with a white streak in the front, a small gold crown, white fur collar, red and purple ornate armor, a feathered skirt, a red cape, carrying a large ornate dark-purple and gold KITE SHIELD with a teal gem on one arm and a straight sword with a ruby pommel on the back",
 'dz_m':"a young male Mo-sect disciple (not yet specialised): spiky brown-red hair, a small gold head plate, red scarf, lighter gold-and-red armor, red cape, a straight sword worn diagonally on the BACK, empty hands",
 'dz_f':"a young female Mo-sect disciple (not yet specialised): long wavy red hair with a white streak, a small gold crown, white fur collar, lighter red and purple armor, feathered skirt, a straight sword worn diagonally on the BACK, empty hands",
}
HEAD="Game art for an original Chinese wuxia fantasy 2.5D isometric mobile MMORPG, hand-painted polished game art in exactly the same smooth painterly style, colors, soft line quality and cute chibi proportions (big head, small body) as the FIRST reference image (NOT pixel art, no pixelation, no dithering), but redrawing the character from the SECOND reference image (a pixel-art chibi shown in three poses): keep the same hairstyle, hair color, face, armor pieces, colors, cape and weapon, redrawn smooth and hand-painted. The character: "
SHEET="Sprite sheet of exactly four frames in ONE horizontal row, evenly spaced with wide clear gaps, none overlapping, identical size and design. Same camera in every frame: slightly elevated three-quarter %s view, walking toward the %s. WALK CYCLE: Frame 1 left foot forward, right arm forward. Frame 2 passing pose feet together, body slightly raised. Frame 3 right foot forward, left arm forward. Frame 4 passing pose again. Full body, feet on the same baseline. "
def prompt(v,kind):
    c=CH[v]
    if kind=='walkf': return HEAD+c+". "+SHEET%('FRONT','lower-right')+GREEN
    if kind=='walkb': return HEAD+c+". "+SHEET%('BACK (we see the back)','upper-left')+GREEN
    if kind=='back': return HEAD+c+". Exactly ONE full-body figure seen FROM BEHIND in a three-quarter back view, facing away toward the upper-left in a ready-to-fight stance, weapon ready, slightly elevated isometric game camera. "+GREEN
    if kind=='port': return HEAD+c+". Full body, standing facing the viewer, relaxed confident pose, character-select portrait. "+GREEN
def cast_prompt(v):
    w={'ms':'swinging the double-bladed axe in a big overhead-to-forward chop','dj':'raising the shield and thrusting the sword forward','dz':'drawing the sword from the back and slashing forward'}[v[:2]]
    return f"The input image is ONE game battle sprite: a Chinese wuxia character seen FROM BEHIND in a three-quarter back view facing the upper-left. TASK: output a single horizontal strip of exactly FOUR full-body frames of this SAME character (identical face/hair, outfit, colors, cape, weapon, proportions, smooth painted art style, and the same camera angle: from behind, facing the upper-left) performing a martial skill motion, {w}, evenly spaced left to right with wide empty gaps between them, all the same size, all standing on the same baseline, each fully separated by green space:\nframe 1 = anticipation: weight shifts back, body crouched, weapon pulled back;\nframe 2 = wind-up: weapon raised high, torso twisting, cape lifted;\nframe 3 = release: the strike lunges forward toward the upper-left, body stepping into it, cape streaming back, the most dynamic pose;\nframe 4 = follow-through: weapon extended but relaxing, body settling back toward the standing pose.\nFlat solid pure green (#00FF00) chroma key background, no ground, no shadows, no text, no effects, no glow."
def job(a):
    v,kind=a; out=f'{D}/{v}/{kind}.png'; os.makedirs(f'{D}/{v}',exist_ok=True)
    if os.path.exists(out): return
    pf=f'{D}/{v}/{kind}.txt'
    if kind=='cast':
        open(pf,'w').write(cast_prompt(v)); ins=['-i',f'{D}/{v}/back_s.png']
    else:
        open(pf,'w').write(prompt(v,kind)); st='style_back.png' if kind=='back' else 'style_walk.png'
        ins=['-i',f'{D}/{st}','-i',f'{D}/kref_{v[:2]}_{v[3]}.png']
    r=subprocess.run(['python3',os.path.expanduser('~/opt/gpt-image/gpt_image.py'),'edit','--prompt-file',pf,*ins,'-o',out,'--size','1536x1024','--quality','high','--retries','5'],capture_output=True,text=True)
    open(out+'.log','w').write(r.stdout+r.stderr); print(v,kind,'ok' if os.path.exists(out) else 'FAIL',flush=True)
if __name__=='__main__':
    kinds=sys.argv[1].split(','); vs=sys.argv[2].split(','); n=int(sys.argv[3])
    if 'cast' in kinds:
        from PIL import Image
        for v in vs:
            im=Image.open(f'{D}/{v}/back.png'); im.thumbnail((1024,1024)); im.save(f'{D}/{v}/back_s.png')
    with ThreadPoolExecutor(n) as ex: list(ex.map(job,[(v,k) for v in vs for k in kinds]))
