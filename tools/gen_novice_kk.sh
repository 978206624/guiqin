# 新手男 / 女（参考 kunkunxunqin 创角预览里的赤绳少年 / 青带少女）：风格参考 art-src/daoist/ref.png，造型参考 art-src/ref/kunkun_novice_*.png
cd "$(dirname "$0")/.." ; O=art-src/novice-kk; mkdir -p $O
for k in nf nm; do
 for t in port walkf walkb back; do
  [ -f $O/${t}_$k.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file art-src/prompts/k${t}_$k.txt -i art-src/daoist/ref.png -i art-src/ref/kunkun_novice_$([ $k = nm ] && echo m || echo f).png -o $O/${t}_$k.png --size $([ $t = port ] && echo 1024x1536 || echo 1536x1024) --quality high > $O/${t}_$k.log 2>&1 &
 done
done
wait
