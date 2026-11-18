using System;
using System.Collections.Generic;
using GuiQin.Inventory;
using GuiQin.Net;
using GuiQin.Quest;

namespace GuiQin.Battle
{
    public static class BattleSettlement
    {
        public static BattleEnd Settle(RoleState role, BattleSession b)
        {
            var eng = b.Engine; var p = eng.Player;
            var end = new BattleEnd { result = eng.Over, round = eng.Round, lv = role.lv, step = role.step, cnt = role.cnt, tower = role.tower };
            role.hp = Math.Max(1, p.Hp); role.mp = p.Mp;
            if (eng.Over == "win")
            {
                // 掉落使用独立的随机序列，不受战斗内随机调用次数影响。
                var rng = new Random(b.Seed ^ 0x5EEDB477);
                role.bond++;                      // 亲密度：打赢一场 +1
                int exp = 0, coin = 0;
                foreach (var id in b.Stage.enemies)
                {
                    var e = GameData.Raw["enemies"][id];
                    exp += (int)e["exp"]; coin += (int)e["coin"];
                    foreach (var d in e["drops"])
                    {
                        if (rng.NextDouble() >= (double)d[1]) continue;
                        string k = (string)d[0];
                        if (!new InventoryService(role).Give(k, 1)) { end.note = "背包已满，部分战利品丢失"; continue; }
                        int n; end.items.TryGetValue(k, out n); end.items[k] = n + 1;
                    }
                }
                end.exp = exp; end.coin = coin;
                List<string> lost;
                end.levelUp = Rewards.Grant(role, exp, coin, null, out lost);
                // 任务推进：正好是当前任务的战斗拍就往前走（最后一拍的奖励一并发）
                var qs = new QuestService(role); string killLine; QuestTalkResp done;
                var beat = qs.OnWin(b.Stage, out killLine, out done);
                if (beat != null)
                {
                    end.levelUp |= done.levelUp;
                    if (done.done) { end.questDone = true; end.questTitle = beat.title; end.exp += done.exp; end.coin += done.coin; foreach (var kv in done.items) { int n; end.items.TryGetValue(kv.Key, out n); end.items[kv.Key] = n + kv.Value; } }
                }
                if (killLine != null) end.note = (end.note != null ? end.note + "　" : "") + killLine;
                if (b.Stage.tower && b.Stage.n > role.tower) { role.tower = b.Stage.n; end.note = (end.note != null ? end.note + "　" : "") + (b.Stage.n % 10 == 0 ? "本层首领已破，可以找塔内传送师上楼" : "玄兵塔进度 " + b.Stage.n + "/70"); }
                end.step = role.step; end.cnt = role.cnt; end.tower = role.tower;
                end.lv = role.lv; end.bond = role.bond;
                int heal = new InventoryService(role).PouchHeal();      // 升级已回满时 hp = -1，不会再加
                if (heal > 0) end.note = (end.note != null ? end.note + "　" : "") + "药囊回血 +" + heal;
            }
            else if (eng.Over == "lose")
            {
                role.hp = Math.Max(1, (int)(p.MaxHp * 0.3f));
                MapDef md; end.respawnMap = GameData.Maps.TryGetValue(role.map, out md) && !string.IsNullOrEmpty(md.home) ? md.home : role.map;
                role.map = end.respawnMap; role.x = -1f; role.y = -1f;
            }
            return end;
        }
    }
}
