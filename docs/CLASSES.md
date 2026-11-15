# 职业与技能（道家女主）

主角形象由用户提供（粉发道家女主，`art-src/daoist/ref.png`），其余图为用它做参考生成（提示词在 `art-src/prompts/`，脚本 `tools/gen_daoist.sh`）。

| 分堂 | 武器 | 定位 | 技能（参考 kunkunxunqin） |
|---|---|---|---|
| 天音（`dao_ty`，创角页默认女主） | 琵琶，行走时背在身后，战斗中怀抱 | 辅助、治疗 | 荡魔曲、灵脉、养生、天籁、返魂 |
| 琴魔（`dao_qm`，数据已有，`hidden:true` 暂不在创角页出现） | 古琴 | 法攻、控制 | 荡魔曲、灵脉、魔魂（魔化）、夺魂、浮灵 |

- 创角页只显示 `hidden` 不为 true 的职业：当前男「游侠」`m_sword`、女「天音」`dao_ty`。想开放琴魔，把 `dao_qm` 的 `hidden` 去掉即可。
- 其余旧职业（`m_spear`、`m_mage`、`f_sword`、`f_heal`、`f_bow`）数据和美术仍保留，仅隐藏。
- 战斗状态：魔化、减速、护盾、持续回复；自动战斗按 `auto` 规则表选技能。
- 素材键：`walk_f_<cls>` / `walk_b_<cls>`（行走 4 帧）、`b_<cls>`（战斗背影）、`cls_<cls>`（立绘）、`head_<cls>`（头像）。
