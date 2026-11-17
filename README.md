# 归秦

竖屏武侠手游的前端小样（HTML / CSS / JS，无框架、无后端）。

- 登录 → 选区 → 创建 / 选择角色（创角页男女各一个「新手」，女角色剧情里拜入道家后变天音 / 琴魔；每区三个栏位）
- 十一张可滚动大地图：恒山村、恒山村郊、清溪谷、云雾谷、石松林、恶狼谷、汉中郡、城外竹林，加道/墨/阴阳三家门派驻地（太虚之林/峰/谷，只能经天下舆图到达）；天下舆图可手指拖动，点地点传送
- 点击或按住拖动行走，A* 寻路，主角有四方向行走帧
- 回合制战斗：敌方左上、我方右下，每边最多 5 个人物 + 5 只宠物；“竞技”按钮是 5v5 演示对战
- 主线：接入 kunkunxunqin 的剧本第一章（任务 1000–1006，对应 1–10 级），见 docs/STORY.md；升级、背包装备、商店，进度存在浏览器本地

登录是纯前端演示：账号密码不校验、不保存、不上传。美术素材由 AI 生成。

## 运行

直接用浏览器打开 `index.html`（多文件版），或 `dist/guiqin.html`（单文件版，构建产物，不入库）。

## 重新构建

改了 `index.src.html` 或换了 `art-src/` 里的原图之后：

```
pip install pillow numpy
python3 build.py
```

会重新切图到 `assets/`，生成 `js/assets.js` 和 `index.html`，并在 `dist/` 输出单文件版 `guiqin.html` 和源码 zip。构建只用仓库内相对路径，任何机器 clone 下来都能直接跑。
只改 `js/data.js`、`js/game.js`、`js/front.js` 的话不用构建，刷新页面即可。

`art-src/` 是生成的原图，`art-src/prompts/` 是对应的提示词，`art-src/sprites-cut/` 是最早一批已经切好的素材。

## 源码说明

### 文件结构

```
  index.src.html  页面结构和全部样式（720x1280 竖屏舞台，等比缩放）；index.html 由它生成
  js/data.js      数据表：属性成长、物品、技能、敌人、关卡、主线任务、NPC、商店。没有逻辑。
  js/game.js      全部逻辑：存档与属性 → 界面/弹窗 → 城镇行走 → 任务 → 回合制战斗
  js/front.js     进游戏之前的界面：登录 → 选区 → 角色列表 / 创建角色。纯前端演示，账号密码不校验不保存；
                  角色列表存在浏览器本地（guiqin-roles-v1），每个角色一份独立存档（guiqin-save:<角色 id>）。
                  职业定义在 data.js 的 classes 里；区服列表是 front.js 顶部的演示数据。
  js/chat.js      聊天面板（六个页签、表情、公告条），发言和回话全是前端定时器模拟，没有联网
  js/story.js     主线剧本数据，由 tools/story_import.py 从 story-src/ 生成，不要手改
  js/masks.js     碰撞位图，由 tools/collision.py 生成，不要手改
  js/water.js     水面区域位图，由 tools/water.py 生成
  js/assets.js    素材清单：素材名 → 文件路径，以及角色图片的显示尺寸
  assets/         图片素材（底图 jpg，角色/图标 webp，均为 2 倍图）
```

### 移植到别的平台时

  - data.js 可以原样转成 JSON 或配置表，数值和任务都在这里改。
  - game.js 里和浏览器绑定的只有三类：DOM 元素的创建与样式、点击事件、localStorage 存档。
    战斗的回合流程（loop / perform / enemyAct / autoAct / finish）和属性计算（stats / grant）
    是纯逻辑，可以直接照着改写。
  - 地图系统：data.js 的 maps 里每张地图有底图、放大倍数 scale、可行走区域（walk 多边形 + roads 道路）、
    NPC、传送点 portals 和地图上的敌人 foes。坐标写在“底图原图像素 / k”的单位里，舞台像素 = 坐标 * k * scale。
    加新地图只需要加一张底图和一段 maps 配置，不用改逻辑。
  - 世界地图：点小地图打开（game.js 的 openWorld）。每张地图在 maps 里用 world:[x,y] 写它在 2300×2300 舆图上的位置（门派地图另加 sect:true），
    所有地点可直接传送，无访问限制；舆图可拖动。传送点可以用 needStep 按主线进度上锁。
  - 战斗站位：敌方左上、我方右下，每边后排 5 个人物位、前排 5 个宠物位（game.js 的 ROWS / slotPos）。
    我方用背影立绘（b_ 开头），竞技场对手用正面立绘（f_ 开头），野外敌人的图会水平翻转。
  - 竞技场（“竞技”按钮）：data.js 的 arena 配了两支 5 人 + 5 宠的队伍，按同一等级对打，不影响存档。
    只有主角手动下指令，其余单位按各自的 ai 类型自动出手（unitAct）；每回合 30 秒倒计时，超时自动行动。
  - 主角行走：两张 4 帧精灵表 walk_f（正面，朝右下）和 walk_b（背影，朝左上），另外两个方向靠水平翻转；
    按走过的距离换帧（STEP_LEN），见 game.js 的 stepPlayer / placePlayer。按住拖动会持续朝手指位置走。
  - 寻路：game.js 载入地图时把可行走区域栅格化成 16 像素的网格（buildGrid），点击后用 A*（findPath）
    找路再拉直。镜头在主循环里平滑跟随主角并夹在地图边缘内（camTarget）。
  - 屏幕固定是 720x1280 设计稿像素，界面按这个尺寸摆放后整体等比缩放。

### 已知限制

  - 每张地图是一整张底图，角色不会被建筑和树遮挡。
  - 城外的野外地图底图放大显示（城外·竹林 2 倍，其余 1.2–1.3 倍），比城里略糊。
  - 逐帧动画只覆盖一部分：主角有四方向行走帧，道家各职业有战斗施法帧（bc_ 开头），技能有序列帧特效；
    其余角色、宠物和敌人仍是单张立绘，行走和攻击靠位移、颠动和闪白表现。
  - 存档在浏览器本地（localStorage），换设备或清缓存会丢。

## 碰撞图
`js/masks.js` 是自动生成的碰撞位图（恒山村、汉中郡），不要手改。改路面或障碍时编辑 `tools/collision.py` 里的多边形，再运行：

    python3 tools/collision.py && python3 build.py

预览叠图会写到 `/tmp/collision_<地图名>.png`（用环境变量 `PREVIEW_DIR` 改目录），用来对照底图检查。


## 项目目录（资源永不丢失）

```
art-src/            所有原始美术：AI 生成的整图、用户提供的地图、sprites-cut 早期切好的素材
art-src/prompts/    生成每张图用的提示词
assets/             构建产物：切好的游戏资源（webp/jpg），已提交
data/               语言无关导出：data.json（全部数值/地图/NPC/关卡/任务）、collision/*.json、assets_manifest.json
tools/              collision.py 碰撞图、water.py 水面、export_data.js 导出、gen/ 生图脚本
tests/smoke.js      Playwright 冒烟测试
docs/UNITY_PORTING.md  移植 Unity 指南     docs/ASSETS.md  素材来源与流程
```

导出数据：`node tools/export_data.js`（改过 js/data.js 或重新生成碰撞后运行）。
冒烟测试：`node tests/smoke.js [html路径]`（默认 `dist/guiqin.html`）。
