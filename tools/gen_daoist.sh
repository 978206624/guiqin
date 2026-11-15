cd /home/claude/gen/daoist
for k in ty qm; do
 for t in walkf walkb; do [ -f ${t}_$k.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file ${t}_$k.txt -i ref.png -o ${t}_$k.png --size 1536x1024 --quality high > ${t}_$k.log 2>&1 &
 done
 [ -f port_$k.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file port_$k.txt -i ref.png -o port_$k.png --size 1024x1536 --quality high > port_$k.log 2>&1 &
done
[ -f back.png ] || nohup python3 ~/opt/gpt-image/gpt_image.py edit --prompt-file back.txt -i ref.png -o back.png --size 1536x1024 --quality high > back.log 2>&1 &
wait
