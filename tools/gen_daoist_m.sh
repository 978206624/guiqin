# 生成道家男角色（天音 / 琴魔）素材：风格参考 art-src/daoist/ref.png，人物参考 art-src/novice/port_nm.png
cd "$(dirname "$0")/.." ; O=art-src/daoist-m; mkdir -p $O
g(){ [ -f $O/$1.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file art-src/prompts/$2.txt -i art-src/daoist/ref.png -i art-src/novice/port_nm.png -i art-src/ref/male_hair_ref.png -o $O/$1.png --size $3 --quality high > $O/$1.log 2>&1 & }
for k in ty qm; do g port_$k mport_$k 1024x1536; g walkf_$k mwalkf_$k 1536x1024; g walkb_$k mwalkb_$k 1536x1024; done
g back mback 1536x1024
wait
