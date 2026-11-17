import os,subprocess
from concurrent.futures import ThreadPoolExecutor
D=os.path.dirname(os.path.abspath(__file__))
P={'npc07':'a young man with dark blue hair tied in a high ponytail, sharp eyes, dark red robe collar',
'npc13':'a bearded middle-aged scholar wearing a tall grey-brown cloth hat with a taiji symbol and silver band, a short dark beard',
'npc14':'an old village elder with white hair in a topknot with a red ribbon, long white eyebrows and a long white beard, kind wise face, green-red robe collar',
'npc33':'a rugged young man with wild spiky light-brown hair and a red headband, strong jaw, blue-purple tunic collar and brown shoulder straps',
'npc45':'a young woman with long silver-white hair, a huge ornate gold and teal crown-like headdress with curving horns, cool elegant eyes, white fur collar',
'npc47':'a young woman with long pink hair, a small gold crown ornament, gentle sweet face, light blue and white robe collar',
'npc57':'an old sage with white hair and a long white beard, stern wise eyes, a large golden antler-like halo headdress with green gems, green-gold robe and red ribbons',
'npc_zhw':'a young guard with dark blue hair in a high ponytail with a gold hair ring, steady serious eyes, red scarf and silver shoulder armor',
'npc_cyblq':'a very old village doctor with white hair, a long white beard, a green leaf sprout on his head, gentle wrinkled face, green robe collar',
'mo_ms':'a burly mature warrior with spiky brown-red hair going grey, short dark beard, gold winged head plate, fierce stern eyes, red scarf and gold armor shoulders',
'mo_dj':'a calm dignified mature man with a neat short beard, brown hair with grey streaks tied up, gold head plate, steady eyes, red scarf and gold armor shoulders'}
def job(n):
    out=f'{D}/out_{n}.png'
    if os.path.exists(out): return
    pr=f"Game art for an original Chinese wuxia fantasy mobile MMORPG: a character-dialogue AVATAR PORTRAIT in polished smooth hand-painted anime-game style with soft line quality and rich colors, exactly the painterly style of the FIRST reference image (NOT pixel art, no pixelation). Redraw the character from the SECOND reference image (a small low-resolution sprite) as a big-head close-up bust portrait: {P[n]}. Keep the same hairstyle, headdress, beard, face type and colors as the second reference. The head and face fill most of the frame, with only a little of the neck and shoulders, front-facing three-quarter view, expressive detailed face and eyes. Exactly ONE character, centered. Background: completely flat solid pure green (#00FF00) chroma key, no ground, no shadows, no gradients, no text, no borders."
    open(f'{D}/{n}.txt','w').write(pr)
    r=subprocess.run(['python3',os.path.expanduser('~/opt/gpt-image/gpt_image.py'),'edit','--prompt-file',f'{D}/{n}.txt','-i',f'{D}/style.png','-i',f'{D}/ref_{n}.png','-o',out,'--size','1024x1024','--quality','high','--retries','5'],capture_output=True,text=True)
    open(out+'.log','w').write(r.stdout+r.stderr); print(n,'ok' if os.path.exists(out) else 'FAIL',flush=True)
with ThreadPoolExecutor(3) as ex: list(ex.map(job,P))
