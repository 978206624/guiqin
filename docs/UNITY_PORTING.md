# 移植 Unity 指南

HTML 只是原型。**数据、素材、规则**都已与代码分离，移植时以 `data/` 和 `assets/` 为准，逻辑参照 `js/game.js`。

## 1. 屏幕与坐标
- 设计稿 720×1280 竖屏，整体等比缩放 → Unity 用 Canvas Scaler（Reference 720×1280, Match 0.5）。
- 地图：`data.maps[id]` 的坐标单位是「底图原图像素 / k」。舞台像素 = 坐标 × `scale` × `k`；舞台宽 = `iw × scale`。
  底图文件 `assets/bg_<id>.(jpg|webp)`，缩略图 `mini_<id>`。
- 镜头跟随主角并夹在地图边缘内（game.js `camTarget`）。
- 世界地图：`assets/world.*`（2300×2300），地点位置 `maps[id].world:[x,y]`，门派 `sect:true`；可拖动 + 惯性。

## 2. 碰撞与寻路
- 两种：①位图 `data/collision/village.json|town.json`（`cell` 格大小、`w×h`、`bits` base64 位图，1=可走；格大小在舞台像素 = `scale*k*cell`）；
  ②其它地图用 `roads[{r,pts}]` 道路走廊多边形 + `walk[]` 多边形（`blocks[]` 为障碍）。
- 载入地图时栅格化成 16px 网格，A*（`findPath`）+ 路径拉直。Unity 可烘焙成 Tilemap / NavMesh2D，规则等价即可。
- `portals[{to,name,at,arrive,needStep,lockMsg}]`：站到 `at` 附近触发传送；`needStep` 按主线进度上锁。
- `foes[{stage,at}]` 地图上的遇敌点，`npcs` 为 NPC。

## 3. 战斗（game.js: loop / perform / enemyAct / autoAct / finish）
- 回合制；敌方左上、我方右下，每边 5 人物 + 5 宠物；按速度排序出手；主角手动指令，其余按 `ai` 自动；每回合 30s 倒计时。
- 伤害：物理用 atk−def，法术用 matk−mdef（技能系数见 `data.skills`）；闪避 `random*100 < dodge` 完全躲开；暴击 `random*100 < crit`，伤害 ×1.5。
- 属性：`base + grow × 等级`；灵狐 = 主角裸属性 × `pet` 系数；战力 `powerOf`。
- 装备：12 栏位、5 档品质、强化到 15 星、宝石镶嵌、锻造宝石；背包 `bagSize`。
- 商店 `shops`、任务 `quests`、关卡 `stages`、天气 `weather`（统一 UTC 时段）。

## 4. 特效
- 动态雾 `maps[id].fog`（云雾谷）；恒山村水面动画由 `tools/water.py` 生成的遮罩 + 画布着色（js/water.js）。
- 战斗闪白、位移、浮字全是程序表现，没有逐帧动画。

## 5. 存档
- localStorage：`guiqin-roles-v1`（角色册）、`guiqin-save:<角色id>`（角色存档 JSON，字段见 `newGame()`）。
- Unity：换成 JSON 文件或 PlayerPrefs，结构不变。

## 6. 模拟的部分（需要真实后端替换）
登录/区服/角色创建均为前端演示，无账号校验、无网络。

## 7. 建议流程
1. 导入 `assets/`，按 `data/assets_manifest.json` 建 Sprite 表（webp 需转 png 或用插件）。
2. 用 `data/data.json` 生成 ScriptableObject / 配置表。
3. 先做地图+行走+传送，再做战斗，再做背包/锻造/任务。
4. 换更高清素材时回到 `art-src/`（原图）与 `art-src/prompts/`（提示词）。
