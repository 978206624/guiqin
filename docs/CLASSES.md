# 职业与技能

## 创角与入派
创角页只有两个角色：**新手（男）** 和 **新手（女）**，造型参考 kunkunxunqin 创角预览里的「赤绳少年」「青带少女」（参考图 `art-src/ref/kunkun_novice_*.png`，原资源见 kunkunxunqin 仓库 `design/role-create-20261008`），用本游戏的手绘画风重画（美术 `art-src/novice-kk/`，提示词 `art-src/prompts/k*_nf|nm.txt`，脚本 `tools/gen_novice_kk.sh`）。更早的几版新手图留在 `art-src/novice/`，不参与构建。
两步入派：①新手 10 级后找太虚之林的道家门主「拜入道家」（`ROLE.sect='dao'`，存进角色表，形象不变）；②再去琴魔堂主 / 天音堂主处确认分堂（选定不可改），职业和形象（行走图、战斗背影、立绘、头像）此时才切换（`game.js` 的 `joinDaoTalk` / `pickHallTalk` / `changeClass`）。原作入门 15 级、分堂 30 级，演示只做到 10 级所以都按 10 级。男女新手同一流程。

## 道家女主

主角形象由用户提供（粉发道家女主，`art-src/daoist/ref.png`），其余图为用它做参考生成（提示词在 `art-src/prompts/`，脚本 `tools/gen_daoist.sh`）。

| 分堂 | 武器 | 定位 | 技能（参考 kunkunxunqin） |
|---|---|---|---|
| 天音（`dao_ty`，创角页默认女主） | 琵琶，行走时背在身后，战斗中怀抱 | 辅助、治疗 | 荡魔曲、灵脉、养生、天籁、返魂 |
| 琴魔（`dao_qm`，入派时可选） | 古琴 | 法攻、控制 | 荡魔曲、灵脉、魔魂（魔化）、夺魂、浮灵 |

- 创角页只显示 `hidden` 不为 true 的职业：当前是 `novice_m`、`novice_f`。天音 / 琴魔是 `hidden`，只能入派获得。
- 旧职业（`m_sword`、`m_spear`、`m_mage`、`f_sword`、`f_heal`、`f_bow`）数据和美术仍保留，仅隐藏。
- 战斗状态：魔化、减速、护盾、持续回复；自动战斗按 `auto` 规则表选技能。
- 素材键：`walk_f_<cls>` / `walk_b_<cls>`（行走 4 帧）、`b_<cls>`（战斗背影）、`cls_<cls>`（立绘）、`head_<cls>`（头像）。

## 道家驻地 NPC（太虚之林）
道家门主（npc57，对话后可拜入门派）、琴魔堂主（npc45）、天音堂主（npc47），形象取自 kunkunxunqin（10000463/64/65）。
