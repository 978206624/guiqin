import os,subprocess
from concurrent.futures import ThreadPoolExecutor
D=os.path.dirname(os.path.abspath(__file__))
T=open(D+'/chain.txt').read().split(' EFFECT:')[0]
E={
'mo_tiger':"a ferocious tiger-claw strike: three bright golden-orange glowing claw slash marks raking diagonally across the center, with a translucent spectral tiger head roaring and dissolving behind them, embers and sparks.",
'mo_roar':"a thunderous battle roar: expanding concentric golden-white shockwave rings with radiating jagged crack lines and a dragon-like sound burst at the center, dust sparks flying outward.",
'mo_slash':"a triple blade combo: three crossing crescent sword-light arcs in crimson and silver slashing through the center one after another, bright impact flares at the intersections, trailing sparks.",
'mo_blood':"a blood sacrifice: a crimson-red burst of glowing blood-fire with swirling dark-red rune glyphs and flying blood droplets, intense red flames flaring up from the center.",
'mo_pojia':"an armor-breaking strike: a bright golden-orange impact that shatters a glowing round shield glyph into flying armor shards, cracks spreading, sparks.",
'mo_bone':"a bone-crushing blow: a heavy pale-white and crimson impact star with radial crack lines and a pulse of shattering force, small debris and sparks bursting outward.",
'mo_song':"a war song that inspires allies: a golden-red battle banner aura, rising golden rings and glowing ancient bronze bell sound waves with floating musical glyphs and sparks rising upward, warm uplifting glow.",
'mo_dragon':"a dragon spirit awakening: a red-gold spectral Chinese dragon spiraling upward around the center in a vortex of fire-like energy, with golden sparks and a fierce glowing aura.",
}
def job(k):
    out=f'{D}/{k}.png'
    if os.path.exists(out): return
    open(f'{D}/{k}.txt','w').write(T+' EFFECT: '+E[k])
    r=subprocess.run(['python3',os.path.expanduser('~/opt/gpt-image/gpt_image.py'),'generate','--prompt-file',f'{D}/{k}.txt','-o',out,'--size','2048x1024','--quality','high','--retries','5'],capture_output=True,text=True)
    open(out+'.log','w').write(r.stdout+r.stderr); print(k,'ok' if os.path.exists(out) else 'FAIL',flush=True)
with ThreadPoolExecutor(3) as ex: list(ex.map(job,E))
