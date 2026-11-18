using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace GuiQin.Inventory
{
    public class StatBlock { public int maxHp, maxMp, atk, matk, def, mdef, spd; public float crit, dodge; }

    // 角色属性 = 职业倍率 × 等级成长 + 心法 + 装备（自带属性 × 星级加成）+ 宝石。对应网页版 stats() / slotBonus() / gearSum()。
    public static class RoleStats
    {
        public static readonly string[] Keys = { "atk", "matk", "hp", "def", "mdef", "mp", "dodge", "spd", "crit" };
        public static bool IsPct(string k) { return k == "dodge" || k == "crit"; }
        static float F(JToken t, float d = 0f) { return t == null || t.Type == JTokenType.Null ? d : (float)t; }

        // 某个部位的加成（属性键 → 数值），键用 data.json 的写法（hp / mp 而不是 maxHp / maxMp）
        public static Dictionary<string, float> SlotBonus(BagState bag, string slot)
        {
            var o = new Dictionary<string, float>();
            string id; if (!bag.equip.TryGetValue(slot, out id) || id == null) return o;
            var it = GameData.Raw["items"][id]; if (it == null) return o;
            GearState gr; if (!bag.gear.TryGetValue(slot, out gr)) gr = new GearState();
            float mul = 1f + gr.star * F(GameData.Raw["forge"]["perStar"]);
            foreach (var k in Keys)
                if (it[k] != null) o[k] = (o.ContainsKey(k) ? o[k] : 0f) + (IsPct(k) ? F(it[k]) : (float)Math.Round(F(it[k]) * mul, MidpointRounding.AwayFromZero));
            foreach (var gid in gr.gems)
            {
                var g = gid != null ? GameData.Raw["items"][gid] : null; if (g == null) continue;
                string gk = (string)g["gem"]; int lv = (int)g["lv"];
                o[gk] = (o.ContainsKey(gk) ? o[gk] : 0f) + (float)GameData.Raw["gems"][gk]["vals"][lv - 1];
            }
            return o;
        }

        public static Dictionary<string, float> GearSum(BagState bag)
        {
            var t = new Dictionary<string, float>();
            foreach (var slot in bag.equip.Keys)
                foreach (var kv in SlotBonus(bag, slot)) t[kv.Key] = (t.ContainsKey(kv.Key) ? t[kv.Key] : 0f) + kv.Value;
            return t;
        }

        public static StatBlock Compute(string cls, int lv, BagState bag)
        {
            var D = GameData.Raw; var b = D["base"]; var g = D["grow"]; int L = Math.Max(1, lv);
            var c = D["classes"][cls] ?? D["classes"]["novice_f"];
            Func<string, float> mod = k => c["mod"] != null && c["mod"][k] != null ? (float)c["mod"][k] : 1f;
            Func<string, int> heart = k => c["heart"] != null && c["heart"]["perLv"] != null && c["heart"]["perLv"][k] != null ? (int)((float)c["heart"]["perLv"][k] * L) : 0;
            Func<string, float, int> basev = (k, m) => (int)Math.Round((F(b[k]) + F(g[k]) * L) * m, MidpointRounding.AwayFromZero);
            var s = new StatBlock
            {
                maxHp = basev("hp", mod("hp")), maxMp = basev("mp", mod("mp")),
                atk = basev("atk", mod("atk")) + heart("atk"), matk = basev("matk", mod("matk")) + heart("matk"),
                def = basev("def", mod("def")) + heart("def"), mdef = basev("mdef", mod("mdef")) + heart("mdef"),
                spd = (int)(F(b["spd"]) + F(g["spd"]) * L), crit = F(b["crit"], 10f), dodge = F(b["dodge"])
            };
            if (bag != null)
                foreach (var kv in GearSum(bag))
                    switch (kv.Key)
                    {
                        case "atk": s.atk += (int)kv.Value; break; case "matk": s.matk += (int)kv.Value; break;
                        case "hp": s.maxHp += (int)kv.Value; break; case "mp": s.maxMp += (int)kv.Value; break;
                        case "def": s.def += (int)kv.Value; break; case "mdef": s.mdef += (int)kv.Value; break;
                        case "spd": s.spd += (int)kv.Value; break; case "crit": s.crit += kv.Value; break; case "dodge": s.dodge += kv.Value; break;
                    }
            return s;
        }
    }
}
