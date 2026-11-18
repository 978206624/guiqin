# 归秦 · Unity 版

网页版（仓库根目录）的 Unity 6 移植。引擎：**Unity 6.3 LTS（6000.3）**，渲染管线：URP 2D。
这个工程目前是**第一阶段：城镇行走切片**，移植进度和后续计划见 [PORTING-PLAN.md](PORTING-PLAN.md)。

## 打开和运行

1. Unity Hub → Add → Add project from disk → 选本仓库的 `unity/` 文件夹，用 6000.3 打开。
2. 第一次导入会比较久（约 330 张贴图，Art 目录 118 MB，导入后做 GPU 压缩）。
3. 菜单 **归秦 → 创建 Main 场景并加入 Build**（可选；游戏对象在运行时由 `Bootstrap` 自动创建，**任何场景按 Play 都能跑**，空场景即可）。
4. 点 Play。点击地面行走，点 NPC 对话，走到传送阵换地图。右上「菜单」可切地图、切角色外观、重置存档。

## 渲染管线（一次性手动，1 分钟）

工程里装了 URP 包，但没有预置管线资产（手写 URP 资产的 YAML 太容易出错）。想用 URP 2D：
`Assets → Create → Rendering → URP Asset (with 2D Renderer)`，建好后到
`Project Settings → Graphics` 和 `Quality` 里把它指定为渲染管线。
这一阶段没用到 2D 灯光，用不用 URP 画面一样，不指定也能跑（走内置管线的 Sprite 渲染）。

## 输入

`PointerInput` 把鼠标/触屏统一成一个指针，新旧输入系统都能编译：
`Project Settings → Player → Active Input Handling` 选 **Input System Package** 或 **Both** 都行。

## 目录

```
Assets/
  Resources/GuiQin/Art/    素材（由 tools/export_unity_assets.py 从网页版 assets/ 转出，别手改）
  Resources/GuiQin/Data/   data.json / story.json / assets.json（key→文件+尺寸）/ mask_*.json（碰撞位图）
  Scripts/Core/            GameData（读 JSON）、Art（出 Sprite）、GameController（主控）、Bootstrap、PointerInput
  Scripts/World/           MapRuntime：可走判定 + 网格 A* 寻路（对应 game.js 的 inside / findPath）
  Scripts/Actors/          Actor / PlayerActor / PetActor / PortalActor
  Scripts/UI/              Ui（uGUI 小工具）、对白框、提示、顶栏、调试菜单
  Editor/                  贴图导入设置、项目设置菜单
Packages/manifest.json     URP 17.3、Input System、Newtonsoft.Json、uGUI
```

## 同步素材和数据

网页版改了素材或 `js/data.js` 之后：

```
node tools/export_data.js              # 刷新 data/*.json
python3 tools/export_unity_assets.py   # 转 png/jpg 并写 Unity 的 Resources
```

## 约定

- 逻辑坐标 = 网页版的「舞台像素」（原点左上，y 向下），和 `js/game.js` 一一对应，移植时逻辑不用改坐标系；显示时 `Stage.ToWorld` 换成世界坐标（1 单位 = 100 像素，y 取反）。
- 精灵表：`assets.json` 的 `size[key] = [格宽, 格高, 头部中心x, (走路帧数)]`；有第 4 项时，表里是 N 张走路帧 + 1 张待机帧（视频抽帧版道家）；没有第 4 项就是老的 4 格。
- 字体用系统中文字体（`Font.CreateDynamicFontFromOSFont`），不依赖 TMP 字库资产。

## 已知风险（没有 Unity 编辑器，这版没编译过）

这个工程是在没有 Unity 编辑器的环境里写的：**没有编译、没有运行过**。打开后如果 Console 有报错，把报错贴给我，我来修。
需要重点留意：包版本能否解析、Newtonsoft 在 IL2CPP 下的裁剪（必要时加 link.xml）、Android 上系统中文字体的回退、Active Input Handling 的设置。
