# 导出 Unity 用的水面数据包：export/unity-water/{地图}_mask.png（8 位软边蒙版）+ water.json（流向/流速/分区）+ README.md
import base64, json, os, re, subprocess
R = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..'); OUT = os.path.join(R, 'export', 'unity-water'); os.makedirs(OUT, exist_ok=True)
js = open(os.path.join(R, 'js', 'water.js')).read()
data = {}
for m in re.finditer(r'(\w+):\{w:(\d+),h:(\d+),mask:"data:image/png;base64,([^"]+)"\}', js):
    k, w, h, b = m.groups(); open(os.path.join(OUT, k + '_mask.png'), 'wb').write(base64.b64decode(b)); data[k] = {'maskFile': k + '_mask.png', 'maskSize': [int(w), int(h)]}
node = r"""const fs=require('fs'),vm=require('vm');const s=fs.readFileSync(process.argv[1],'utf8');const re=/^    (\w+): \{\n(?:.*\n){0,3}?\s+water: (\{.*\}),/gm;let o={},m;while((m=re.exec(s))){try{o[m[1]]=vm.runInNewContext('('+m[2]+')')}catch(e){}}console.log(JSON.stringify(o))"""
defs = json.loads(subprocess.check_output(['node', '-e', node, os.path.join(R, 'js', 'data.js')]))
for k, d in defs.items():
    if k in data: data[k]['flow'] = d
json.dump(data, open(os.path.join(OUT, 'water.json'), 'w'), ensure_ascii=False, indent=1)
open(os.path.join(OUT, 'README.md'), 'w').write("""# 归秦 水面数据包（给 Unity 用）

- `<map>_mask.png`：水面蒙版，灰度 8 位，亮度 = 水的不透明度，边缘是软边（岸边水花线就靠它）。导入时关掉 sRGB，格式选 R8 / Single Channel。
- `water.json`：每张图的蒙版尺寸和流动参数。
  - `flow.dir` / `flow.speed`：全图默认流向（向量）和流速（蒙版像素/秒，蒙版宽 1000 为准，村庄是 1500）。
  - `flow.zones[]`：分区 `{rect:[x0,y0,x1,y1], dir, speed}`，坐标和蒙版同系。`|dir.y| > 1.5|dir.x|` 的是瀑布（竖向）。

## 在 Unity 里的做法
1. 水面一张 quad，叠在地图上方，材质用 Shader Graph（URP 2D Unlit/Sprite）。
2. 折射：对地图底图采样，UV 加 `sin` 扰动（幅度约 1.1 蒙版像素，水平方向 `sin(y*0.16 + t*1.7)`、垂直方向 `sin(x*0.13 + t*1.35)`）；瀑布区只做水平方向的高频抖动 `sin(y*0.5 - t*9)`。
3. 流动：滚动法线或水纹贴图，三层（1x / 1.55x 缩放 + 0.62 倍速 / 0.7x 缩放 + 1.35 倍速）；蒙版内部（离岸远）再叠一层 1.7 倍速。
4. 岸边水花：蒙版亮度在 0.15–0.85 之间的像素（`1 - |2a - 1|`）混白色，随 `sin(t*1.8 + x*0.09 + y*0.07)` 脉动。
5. 瀑布落点：每个竖向分区的底部放粒子（水雾：上升 + 变大 + 淡出；水珠：抛物线）。涟漪、闪光、飘叶也是粒子。
6. 流向可以直接把 `zones` 烘成一张 flow map（RG 通道存方向），这样不用在 shader 里写分区判断。

遮罩由 `tools/water.py` 从地图颜色抠出再叠手工多边形生成。换成更高清的地图后重新跑一遍，或者直接在 PSD 里画一张蒙版。
""")
print({k: ('flow' in v) for k, v in data.items()})
