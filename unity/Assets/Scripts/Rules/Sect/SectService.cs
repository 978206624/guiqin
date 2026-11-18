using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Sect
{
    public static class SectCmd { public const string Join = "sect.join", Hall = "sect.hall"; }
    public class SectReq { public string sect, hall; }
    public class SectResp { public bool ok; public string msg, cls, sect; public int hp = -1, mp = -1; }

    // 门派两步：到了入门等级找门主入门（role.sect = dao / mo，职业变成“xx弟子”），再去对应堂主处选分堂（职业形象此时才定下，选了不能改）。
    // 原作入门 15 级、分堂 30 级，网页演示只做到 10 级，这里沿用 10 级。
    public class SectService
    {
        public const int SectLv = 10;
        readonly RoleState role;
        public SectService(RoleState role) { this.role = role; }

        static JToken Cls(string id) { return GameData.Raw["classes"][id]; }
        public static string ClsName(string id) { var c = Cls(id); if (c == null) return id; string n = (string)c["name"]; return n == "道家弟子" ? n : (c["sect"] != null ? (string)c["sect"] + "·" : "") + n; }
        static bool Male(string id) { var c = Cls(id); return c != null && (string)c["gender"] == "m"; }
        static string SectName(string s) { return s == "mo" ? "墨家" : "道家"; }
        static string Prefix(string hall) { return hall == "qm" || hall == "ty" ? "dao" : "mo"; }

        // 这个职业在某个堂能变成哪个职业（客户端也用它来预览）：joinSect 里以 “dao_qm” 开头的那个（带 _m 的男版）
        public static string HallClass(string clsId, string hall)
        {
            var c = Cls(clsId); if (c == null || c["joinSect"] == null) return null;
            foreach (var x in c["joinSect"]) if (((string)x).StartsWith(Prefix(hall) + "_" + hall)) return (string)x;
            return null;
        }

        SectResp Fail(string m) { return new SectResp { ok = false, msg = m, cls = role.cls, sect = role.sect }; }
        SectResp Done(string cls, string msg)
        {
            role.cls = cls; role.hp = -1; role.mp = -1;                      // 换职业气血法力回满
            GuiQin.Train.TrainService.GrantInit(role, cls);                  // 入门送前两个技能 1 级
            return new SectResp { ok = true, msg = msg, cls = cls, sect = role.sect, hp = -1, mp = -1 };
        }

        public SectResp Join(string sect)
        {
            if (sect != "dao" && sect != "mo") return Fail("没有这个门派");
            var c = Cls(role.cls);
            if (!string.IsNullOrEmpty(role.sect) && role.sect != sect) return Fail("你已拜入" + SectName(role.sect) + "，不可再入" + SectName(sect));
            if (c == null || c["joinSect"] == null) return Fail("你已是门派弟子");
            if (role.lv < SectLv) return Fail("需达到 " + SectLv + " 级方可入门（现在 " + role.lv + " 级）");
            role.sect = sect;
            string id = sect + "_dz" + (Male(role.cls) ? "_m" : "");
            return Done(id, "拜入" + SectName(sect));
        }

        public SectResp Hall(string hall)
        {
            string pre = Prefix(hall);
            if (role.sect != pre) return Fail(!string.IsNullOrEmpty(role.sect) ? "你已拜入" + SectName(role.sect) : "先去找" + (pre == "mo" ? "墨巨子" : "道家门主") + "入门（" + SectLv + " 级）");
            var c = Cls(role.cls);
            if (c == null || c["joinSect"] == null) return Fail("你已入另一堂，分堂不可改选");
            string id = HallClass(role.cls, hall); if (id == null) return Fail("这个堂不收你");
            return Done(id, "拜入" + SectName(pre) + "·" + (string)Cls(id)["name"]);
        }
    }
}
