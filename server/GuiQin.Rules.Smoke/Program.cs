using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Net;
using GuiQin.Quest;
using GuiQin.Pets;
using GuiQin.Sect;
using GuiQin;

internal static class Program
{
    const int Seed = 12346;

    static int Main(string[] args)
    {
        bool printOnly = args.Length == 1 && args[0] == "--print";
        if (args.Length > 0 && !printOnly)
        {
            Console.Error.WriteLine("用法：GuiQin.Rules.Smoke [--print]");
            return 1;
        }

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Console.OutputEncoding = new UTF8Encoding(false);
        GameData.Load(new FileDataSource(Path.Combine(AppContext.BaseDirectory, "Data")));

        string actual = RunBattle();
        if (!string.Equals(actual, RunBattle(), StringComparison.Ordinal))
        {
            Console.Error.WriteLine("同一种子两次战斗的输出不一致。");
            return 1;
        }

        Console.Write(actual);
        if (printOnly) return 0;

        string snapshot = Path.Combine(AppContext.BaseDirectory, "Snapshots", "battle-seed-12346.txt");
        if (!File.Exists(snapshot) || !string.Equals(actual, File.ReadAllText(snapshot).Replace("\r\n", "\n"), StringComparison.Ordinal))
        {
            Console.Error.WriteLine("战斗输出与快照不一致：" + snapshot);
            return 1;
        }
        VerifySectStory();
        VerifyPetSurvivesOwner();
        Console.WriteLine("宠物续战：主角倒下后继续行动，宠物独力可获胜，全员倒下才判负。");
        VerifyPetManaAndPouch();
        Console.WriteLine("宠物法力：旧档从零开始，药囊补蓝，施法扣蓝，换宠和下一场保留各自法力。");
        return 0;
    }

    static void VerifySectStory()
    {
        foreach (var sect in new[] { "dao", "mo" })
        {
            var role = new RoleState { cls = "novice_f", lv = 1, bag = InventoryService.NewBag() };
            var quest = new QuestService(role);
            int earned = 0;
            while (!quest.Current.IsSectGate)
            {
                var beat = quest.Current;
                if (beat.IsTalk)
                {
                    var result = quest.Talk(beat.npc);
                    if (!result.ok || result.exp != beat.exp) throw new InvalidOperationException("主线对话未推进或重复补发了经验。");
                    earned += result.exp;
                }
                else
                {
                    var stage = GameData.Stages[beat.stage];
                    if (quest.CheckStage(stage) != null) throw new InvalidOperationException("第一章战斗被意外阻止。");
                    int exp = stage.enemies.Sum(id => (int)GameData.Raw["enemies"][id]["exp"]);
                    Rewards.Grant(role, exp, 0, null, out _);
                    quest.OnWin(stage, out _, out var result);
                    earned += exp + result.exp;
                }
            }
            var inventory = new InventoryService(role);
            if (role.lv != 10 || role.exp != 0 || earned != 5400 ||
                inventory.ItemCount("cloth_armor") != 1 || inventory.ItemCount("iron_sword") != 1 ||
                inventory.ItemCount("bronze_bell") != 1)
                throw new InvalidOperationException("第一章的自然升级或装备发奖不正确。");

            int step = role.step;
            var boss = GameData.Stages["s_mystery"];
            if (quest.CheckStage(boss) == null || quest.Talk("zc_elder").ok ||
                !new SectService(role).Join(sect).ok || role.step != step ||
                quest.Current.qid != "1007" || quest.CheckStage(boss) != null)
                throw new InvalidOperationException("入门应解除主线门槛并延续原进度。");

            var old = new RoleState { cls = "novice_f", lv = 4, exp = 123, coin = 55, step = step + 2, cnt = 3, bag = InventoryService.NewBag() };
            if (QuestService.EnsureSectLevel(old) <= 0 || old.lv != 10 || old.exp != 0 ||
                old.step != step + 2 || old.cnt != 3 || old.coin != 55 ||
                new InventoryService(old).ItemCount("cloth_armor") != 0 || QuestService.EnsureSectLevel(old) != 0)
                throw new InvalidOperationException("旧角色的经验补足不应重发奖励或改动进度。");
        }
        Console.WriteLine("主线入门：第一章升到 10 级并发放装备，两派均能解锁赵村，旧存档保留节拍。");
    }

    static void VerifyPetSurvivesOwner()
    {
        var skills = GameData.Raw["skills"].ToObject<Dictionary<string, SkillDef>>();
        var names = new Dictionary<string, string>();
        foreach (bool petWins in new[] { true, false })
        {
            var player = new BUnit { Id = 1, Side = "ally", Me = true, Hp = 1, MaxHp = 1, Spd = 40, Crit = 0 };
            var pet = new BUnit { Id = 2, Side = "ally", Row = "pet", Hp = 2000, MaxHp = 2000, Atk = 150, Spd = 20, Aggro = 0, Crit = 0 };
            var enemy = new BUnit { Id = 3, Side = "enemy", Hp = 350, MaxHp = 350, Atk = 200, Spd = 200, Crit = 0 };
            var engine = new BattleEngine(new List<BUnit> { player, pet, enemy }, skills, names, Seed);
            if (!engine.AcceptsPetCommand("bite") || engine.AcceptsPetCommand("pokong"))
                throw new InvalidOperationException("宠物技能白名单无效。");
            var first = engine.RunRound(new BattleCmd { type = "defend", petSkill = "bite", petTarget = enemy.Id });
            if (player.Alive || !pet.Alive || engine.Over != null || !first.Any(e => e.t == "act" && e.u == pet.Id))
                throw new InvalidOperationException("主角倒下时，宠物应在当前回合继续出手。");

            if (!petWins) pet.Hp = 1;
            var next = engine.RunRound(new BattleCmd { type = "flee", petSkill = "bite", petTarget = enemy.Id });
            if (next.Any(e => e.t == "flee"))
                throw new InvalidOperationException("倒下的主角不能提交逃跑指令。");
            if (petWins && !next.Any(e => e.t == "act" && e.u == pet.Id && e.skill == "bite"))
                throw new InvalidOperationException("主角倒下后，宠物应执行玩家选择的技能。");
            while (engine.Over == null && engine.Round < 20)
                engine.RunRound(new BattleCmd { type = "auto" });
            if (engine.Over != (petWins ? "win" : "lose") || player.Alive || pet.Alive != petWins)
                throw new InvalidOperationException("宠物独力战斗结果不正确。");
        }
    }

    static void VerifyPetManaAndPouch()
    {
        var role = new RoleState { cls = "novice_f", lv = 12, map = "village", bag = InventoryService.NewBag() };
        var inventory = new InventoryService(role);
        var stage = GameData.Stages["boar"];
        var empty = BattleFactory.Create(role, stage, Seed);
        if (empty.Engine.Pet.Mp != 0 || empty.Engine.Pet.MaxMp != 108 ||
            empty.View.petSkills.Single(s => s.id == "foxfire").mp != 20)
            throw new InvalidOperationException("旧存档的法力或宠物技能消耗不正确。");
        var unavailable = empty.Engine.RunRound(new BattleCmd { type = "defend", petSkill = "foxfire", petTarget = 1 });
        if (!unavailable.Any(e => e.t == "act" && e.u == empty.Engine.Pet.Id && e.skill == "bite"))
            throw new InvalidOperationException("无蓝的宠物应使用免费普通招式。");

        inventory.Give("mp_potion", 1);
        int potions = inventory.ItemCount("mp_potion");
        if (inventory.StoreManaPotion("mp_potion") || inventory.ItemCount("mp_potion") != potions)
            throw new InvalidOperationException("未装备药囊时不能消耗回气丹。");
        inventory.Give("herb_pouch", 1);
        if (!inventory.Equip("herb_pouch")) throw new InvalidOperationException("无法装备测试用药囊。");
        int maxMana = RoleStats.Compute(role.cls, role.lv, role.bag).maxMp;
        role.mp = maxMana - 15;
        if (!inventory.StoreManaPotion("mp_potion") || inventory.ItemCount("mp_potion") != potions - 1 ||
            role.mp != maxMana || PetService.CurrentMana(role, "fox") != 108 || role.bag.manaReserve != 177)
            throw new InvalidOperationException("储备未按人物、宠物的顺序补蓝。");

        var battle = BattleFactory.Create(role, stage, Seed);
        var events = battle.Engine.RunRound(new BattleCmd { type = "defend", petSkill = "foxfire", petTarget = 1 });
        if (!events.Any(e => e.t == "mp" && e.u == battle.Engine.Pet.Id && e.mp == 88) ||
            battle.Snapshot().units.Single(u => u.id == battle.Engine.Pet.Id).mp != 88)
            throw new InvalidOperationException("施法扣蓝或重连快照错误。");

        battle.Engine.Over = "win";
        var result = BattleSettlement.Settle(role, battle);
        if (result.petMana != 108 || role.bag.manaReserve != 157)
            throw new InvalidOperationException("战斗胜利后药囊应只补缺少的法力。");
        role.bag.manaReserve = 0;
        var noReserve = BattleFactory.Create(role, stage, Seed);
        noReserve.Engine.Pet.Mp = 0;
        noReserve.Engine.Over = "win";
        BattleSettlement.Settle(role, noReserve);
        if (BattleFactory.Create(role, stage, Seed).Engine.Pet.Mp != 0)
            throw new InvalidOperationException("没有储备时下一场不能恢复法力。");

        role.petOwn.Add("ss_1227");
        PetService.SaveMana(role, "ss_1227", 31);
        role.petAct = "ss_1227";
        if (BattleFactory.Create(role, stage, Seed).Engine.Pet.Mp != 31 ||
            PetService.CurrentMana(role, "fox") != 0)
            throw new InvalidOperationException("宠物法力未按个体分别保存。");
    }

    static string RunBattle()
    {
        var role = new RoleState
        {
            id = "smoke", name = "回放角色", cls = "novice_f", lv = 5,
            map = "village", hp = 500, bag = InventoryService.NewBag()
        };
        PetService.SaveMana(role, "fox", PetService.MaxMana(role.lv));
        var battle = BattleFactory.Create(role, GameData.Stages["boar"], Seed);
        var inventory = new InventoryService(role);
        battle.Engine.ItemUse = id => inventory.UseInBattle(id, out int hp, out int mp) ? new[] { hp, mp } : null;

        var output = new StringBuilder().Append("seed=").Append(Seed).Append(" stage=").Append(battle.StageId).Append('\n');
        while (battle.Engine.Over == null)
        {
            if (battle.Engine.Round >= 100) throw new InvalidOperationException("战斗在 100 回合内未结束。");
            var cmd = battle.Engine.Round == 0
                ? new BattleCmd { type = "item", skill = "hp_potion" }
                : new BattleCmd { type = "skill", skill = "attack", target = 1 };
            var events = battle.Engine.RunRound(cmd);
            output.Append("round ").Append(battle.Engine.Round).Append(" | ")
                .Append(string.Join(" | ", events.Skip(1).Select(Describe))).Append('\n');
        }

        var end = BattleSettlement.Settle(role, battle);
        string drops = string.Join(",", end.items.OrderBy(item => item.Key, StringComparer.Ordinal)
            .Select(item => item.Key + "=" + item.Value));
        output.Append("result=").Append(end.result).Append(" exp=").Append(end.exp).Append(" coin=").Append(end.coin)
            .Append(" drops=").Append(drops.Length == 0 ? "-" : drops).Append('\n');
        return output.ToString();
    }

    static string Describe(BattleEvent e)
    {
        switch (e.t)
        {
            case "act": return $"act {e.u} {e.skill} -> {string.Join(",", e.ts ?? Array.Empty<int>())}";
            case "hit": return $"hit {e.u} -{e.v} hp={e.hp}" + (e.crit ? " crit" : "");
            case "heal": return $"heal {e.u} +{e.v} hp={e.hp}";
            case "mp": return $"mp {e.u}={e.mp}";
            case "status": return $"status {e.u} {e.st} {e.turns}";
            case "unstatus": return $"unstatus {e.u} {e.st}";
            default: return $"{e.t} {e.u}" + (string.IsNullOrEmpty(e.text) ? "" : " " + e.text);
        }
    }
}
