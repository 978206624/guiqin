using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Net;
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
        return 0;
    }

    static string RunBattle()
    {
        var role = new RoleState
        {
            id = "smoke", name = "回放角色", cls = "novice_f", lv = 5,
            map = "village", hp = 500, bag = InventoryService.NewBag()
        };
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
