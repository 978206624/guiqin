# 主线剧情接入

剧情来自 kunkunxunqin 项目的《归秦主线完整剧本》（`story-src/main_story_existing.json`，原 22 章 172 任务；续章在 `main_story_continuation.json`）。
目前接入 **第一章·清溪残梦（1000–1006）** 与 **第二章·赵村血契（1007–1014）**，共 15 个任务、20 拍（`CHAPTER_LAST=1014`）。第一章对应 1–10 级（原项目地图等级：恒山村 3 / 村郊 6 / 清溪谷 9）。第二章的 NPC 摆在城外·竹林（赵村村长、萧恨水）和云雾谷（韩信）；首领/清怪点已用游戏自带碰撞逐一验证可走到。

## 流程
`story-src/*.json` + `story-src/task_targets.json`（原任务目标表）→ `python3 tools/story_import.py` → `js/story.js`（`window.STORY`）→ 载入时 `game.js` 摊平成“节拍”序列 `D.quests`，`S.step` 为节拍序号。

节拍类型：`talk` 找人 / `spot` 采集点 / `fight` 首领战 / `kill` 清怪计数（`S.cnt`）。
每个任务第一拍带接取对白，最后一拍带完成对白与奖励；首领/怪物只在对应任务进行到那一拍时出现在地图上；同名 NPC 在首领战期间隐藏。

## 接入更多章节
1. `CHAPTER_LAST=1014 python3 tools/story_import.py`（只要范围内用到的 NPC / 首领 / 怪物 / 采集物都在脚本顶部映射表里）。
2. `data.js`：补 NPC（已预置 赵村村长/萧恨水/韩信/装备商人/白素 等）、`enemies`、`stages`（`story:true`）、地图 `npcs` 摆位（用 tools 里的碰撞图确认可走）。
3. NPC 形象来自 kunkunxunqin：把 `client/assets/npc_handpainted/<名>/frame-000.png` 放进 `art-src/kunkun-npc/`，加进 `build.py` 的 `KK_NPC`（会生成立绘 `kk_<名>` 与头像 `head_kk_<名>`）。注意单文件版 16MB 上限。
4. 经验奖励：`TOTAL` 表给每个任务的总经验，脚本扣掉战斗经验后写进奖励。第一章主线及必打战斗合计 5,400 经验，正好升到 10 级。`EQUIPMENT` 表为指定任务添加阶段装备。
5. 完成《阴阳两面》后，未入门角色先完成「投师学艺」再进入后续主线。入门关口定义在剧本的 `sectGate` 中，沿用原任务节拍序号。

## 已知取舍
- 食尸虫 / 幼虎 / 毒蛇 / 邪月等后续章节怪物暂借现有美术。
- 旧存档的任务进度会重置（等级、装备保留）。
