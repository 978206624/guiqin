using System;
using System.Collections.Generic;
using System.Linq;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Train
{
    public static class TrainCmd { public const string Do = "train.do"; }
    public class TrainReq { public string who, id; }                         // who: menzhu（弟子找门主学基础技能）/ hall（分堂找堂主）
    public class TrainResp { public bool ok; public string msg; public Dictionary<string, int> skl = new Dictionary<string, int>(); public int coin; }

    // 技能修炼（参考原作：门派心法不在此升级；分堂技能只能在本堂堂主处学）。演示简化：只耗银两，等级上限 5，升级要求人物等级随技能等级升高。
    // 有门派技能表的职业必须“学过”才能用技能；新手用通用技能，仍按人物等级解锁。技能每升 1 级，威力 / 疗效 +12%。
    public class TrainService
    {
        readonly RoleState role;
        static JToken D { get { return GameData.Raw; } }
        public TrainService(RoleState role) { this.role = role; }

        public static JToken Cls(string id) { return D["classes"][id]; }
        public static bool HasSkillTable(string clsId) { var c = Cls(clsId); return c != null && c["skills"] != null; }
        public static int Level(RoleState r, string id) { int n; return r.skl != null && r.skl.TryGetValue(id, out n) ? n : 0; }
        public static float Mul(int level) { return 1f + 0.12f * (Math.Max(1, level) - 1); }
        public static int MaxLv { get { return (int)D["train"]["maxLv"]; } }

        // 旧存档 / 新职业：按等级把已解锁的技能算作已学
        public static void EnsureSkl(RoleState r)
        {
            if (r.skl == null) r.skl = new Dictionary<string, int>();
            if (r.skl.Count > 0 || !HasSkillTable(r.cls)) return;
            foreach (var x in Cls(r.cls)["skills"]) { string id = (string)x; var s = D["skills"][id]; if (s != null && r.lv >= (s["lv"] != null ? (int)s["lv"] : 1)) r.skl[id] = 1; }
        }

        // 入门 / 选分堂换了职业：送前两个技能 1 级
        public static void GrantInit(RoleState r, string clsId)
        {
            if (r.skl == null) r.skl = new Dictionary<string, int>();
            var c = Cls(clsId); if (c == null || c["skills"] == null) return;
            foreach (var x in c["skills"].Take(2)) { string id = (string)x; if (Level(r, id) == 0) r.skl[id] = 1; }
        }

        // 这位师父教哪些技能
        public static List<string> List(RoleState r, string who)
        {
            var c = Cls(r.cls); if (c == null) return new List<string>();
            if (who == "menzhu") { if (c["joinSect"] == null) return new List<string>(); var T = D["train"][r.sect == "mo" ? "baseMo" : "base"]; return T.Select(x => (string)x).ToList(); }
            if (c["joinSect"] != null || c["skills"] == null) return new List<string>();      // 还没分堂的不能找堂主学
            return c["skills"].Select(x => (string)x).ToList();
        }

        public struct CostInfo { public int lvReq, coin; }
        public static CostInfo Cost(RoleState r, string id)
        {
            int n = Level(r, id) + 1; var x = D["skills"][id]; var T = D["train"]; int sl = x["lv"] != null ? (int)x["lv"] : 1;
            return n == 1 ? new CostInfo { lvReq = sl, coin = (int)T["learnCoin"] * sl }
                          : new CostInfo { lvReq = sl + (int)T["upLv"] * (n - 1), coin = (int)T["upCoin"] * n * n };
        }

        TrainResp Res(bool ok, string msg) { return new TrainResp { ok = ok, msg = msg, skl = new Dictionary<string, int>(role.skl), coin = role.coin }; }

        public TrainResp Do(string who, string id)
        {
            EnsureSkl(role);
            if (!List(role, who).Contains(id)) return Res(false, "这位师父不教这个");
            if (Level(role, id) >= MaxLv) return Res(false, "已经满级了");
            var ct = Cost(role, id);
            if (role.lv < ct.lvReq) return Res(false, "需要 " + ct.lvReq + " 级");
            if (role.coin < ct.coin) return Res(false, "银两不够");
            role.coin -= ct.coin; role.skl[id] = Level(role, id) + 1;
            return Res(true, (string)D["skills"][id]["name"] + " 升到 " + role.skl[id] + " 级");
        }
    }
}
