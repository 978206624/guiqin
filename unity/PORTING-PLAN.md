# 移植计划（网页版 → Unity 6）

原则：**逻辑照搬，数据和素材共用**。网页版 `js/game.js` 的每一块在 Unity 里有对应模块；数据只有一份（`data/*.json` → 导出到 Unity）。
每个阶段结束都应该是「能在 Unity 里跑、能玩一段」，不攒大爆炸。通用说明见 [docs/UNITY_PORTING.md](../docs/UNITY_PORTING.md)。

| 阶段 | 内容（网页版位置） | Unity 模块 | 状态 |
|---|---|---|---|
| R1 | 地图与行走：底图、可走判定、A* 寻路、点击/拖动行走、镜头、传送点、NPC 站位与对话、敌人站位、道家视频帧动画（game.js 543–815 / 1227–1331） | `MapRuntime`、`GameController`、`Actors`、`GameUi` | 已写完，待在 Unity 里验证 |
| R2 | 存档与属性、主线剧情「节拍」、任务显示与完成、NPC 对话分支（game.js 38–132 / 957–1225） | `SaveData`/`PlayerState`、`StoryRunner`、`QuestUi` | 未开始 |
| R3 | 回合制战斗：敌我布阵、出手顺序、伤害/暴击/闪避、技能与状态、宠物、结算（game.js 1690–2350） | `Battle/*` | 未开始 |
| R4 | 背包 / 装备 / 强化 / 宝石 / 锻造、商店、玄兵塔、竞技场、门派选择（game.js 150–542 / 1141–1173） | `Inventory/*`、`ShopUi`、`ForgeUi`、`TowerUi` | 未开始 |
| R5 | 世界地图传送、天气、动态雾、流水水面（js/water.js + export/unity-water）、蝴蝶、落花 | `WorldMapUi`、`Weather`、`WaterRenderer`（Shader）| 未开始 |
| R6 | 登录 / 选区 / 创角（front.js）、设置、音效（WebAudio 合成 → AudioClip 或程序合成） | `FrontUi`、`Settings`、`Sfx` | 未开始 |
| R7 | 道家武器层（背在身后可换：琵琶/琴盒/箜篌，按帧锚点和前后层级）、移动端适配、出包 | `WeaponLayer`、真机测试 | 未开始 |

## 验证方式

- **有 Unity 编辑器时**：每阶段列一份「怎么点一遍」的清单（见各阶段完成时的说明），报错贴回来修。
- **没有编辑器时（当前）**：写完只能做静态检查（括号配平、字段/类型和 JSON 对照、素材 key 全部存在），逻辑和网页版逐行对照。
  素材/数据覆盖用脚本审过：所有地图的底图、NPC、头像、传送阵贴图、敌人精灵都能在 `assets.json` 里找到。

## 与网页版保持一致的地方

- 坐标系、速度（230 px/s）、起步加速、朝向切换阈值、步幅（26 / 道家背面 24）、点击判定范围、NPC 站位候选点、镜头偏移——都是原样搬的数字。
- 道家男女行走图是视频帧：女 正面 28 帧 / 背面 30 帧，男 正面/背面各 26 帧，另带一张待机帧。

## 暂时不做 / 取舍

- 任务锁（`needQuest`/`needStep`）：R2 前先忽略，`GameController.IgnoreQuestLocks`。
- 商店/锻造/塔/竞技场/门派 NPC：对话结束后只弹一条「待移植」提示。
- 美术：网页版素材原样用；高清替换回 `art-src/`。
