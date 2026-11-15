# 生成新手男/女角色素材（参考画风：art-src/daoist/ref.png）。需要生图网关凭据，见 docs/ASSETS.md
cd "$(dirname "$0")/.." ; O=art-src/novice; mkdir -p $O
for k in nf nm; do
 for t in port walkf walkb back; do
  [ -f $O/${t}_$k.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file art-src/prompts/${t}_$k.txt -i art-src/daoist/ref.png -o $O/${t}_$k.png --size $([ $t = port ] && echo 1024x1536 || echo 1536x1024) --quality high > $O/${t}_$k.log 2>&1 &
 done
done
wait
