using System;
using System.Collections.Generic;
using System.Linq;
using GuiQin.Inventory;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Pets
{
    public struct PetStats { public int maxHp, maxMp, atk, matk, def, mdef, spd; }
    public struct BondInfo { public int n, i, from, to; public string name; public float bonus; }      // to = -1 表示已满级

    // 宠物 / 神兽：拥有、出战、亲密度、属性、兑换。服务端权威；客户端只用这里的静态公式显示面板（和战斗里算的一样）。
    public class PetService
    {
        readonly RoleState role;
        static JToken D { get { return GameData.Raw; } }
        public PetService(RoleState role) { this.role = role; }

        // ---------------- 静态：谁都能查 ----------------
        public static JToken Dex(string id) { return id == null ? null : D["petDex"][id]; }
        public static List<string> AllIds() { return ((JObject)D["petDex"]).Properties().Select(p => p.Name).ToList(); }
        public static bool IsSs(string id) { var dx = Dex(id); return dx != null && dx["ss"] != null && (bool)dx["ss"]; }

        public static bool Owned(RoleState r, string id)
        {
            var dx = Dex(id); if (dx == null) return false;
            if (id == "fox" || (dx["own"] != null && (bool)dx["own"])) return true;
            return r.petOwn != null && r.petOwn.Contains(id);
        }

        public static string ActiveId(RoleState r)
        {
            return !string.IsNullOrEmpty(r.petAct) && Dex(r.petAct) != null && Owned(r, r.petAct) ? r.petAct : "fox";
        }

        public static BondInfo BondOf(RoleState r)
        {
            var T = (JArray)D["bondTiers"]; int n = r.bond, i = 0;
            while (i + 1 < T.Count && n >= (int)T[i + 1]["at"]) i++;
            return new BondInfo { n = n, i = i, name = (string)T[i]["name"], from = (int)T[i]["at"], to = i + 1 < T.Count ? (int)T[i + 1]["at"] : -1, bonus = i * 0.03f };
        }

        static float F(JToken t, float def) { return t == null || t.Type == JTokenType.Null ? def : (float)t; }
        public static JToken Mult(string id) { var dx = Dex(id); return dx != null && dx["mult"] != null ? dx["mult"] : D["pet"]; }

        // 宠物属性：取主人“裸属性公式”的一部分，随等级成长；亲密度档位加成气血和攻击
        public static PetStats Stats(RoleState r, string id)
        {
            var b = D["base"]; var g = D["grow"]; var p = Mult(id); int L = r.lv; float k = 1f + BondOf(r).bonus;
            Func<string, float, float, int> v = (key, mul, kk) => (int)Math.Round((F(b[key], 0) + F(g[key], 0) * L) * mul * kk, MidpointRounding.AwayFromZero);
            return new PetStats
            {
                maxMp = MaxMana(L), maxHp = v("hp", F(p["hp"], 1f), k), atk = v("atk", F(p["atk"], 1f), k), matk = v("matk", F(p["atk"], 1f), k),
                def = v("def", F(p["def"], 1f), 1f), mdef = v("mdef", F(p["def"], 1f), 1f), spd = v("spd", F(p["spd"], 1f), 1f)
            };
        }

        public static int MaxMana(int level) => 60 + 4 * Math.Max(1, level);

        public static int CurrentMana(RoleState role, string id)
        {
            return role.petMana != null && role.petMana.TryGetValue(id, out int saved)
                ? Math.Max(0, Math.Min(MaxMana(role.lv), saved)) : 0;
        }

        public static void SaveMana(RoleState role, string id, int mana)
        {
            role.petMana ??= new Dictionary<string, int>();
            role.petMana[id] = Math.Max(0, Math.Min(MaxMana(role.lv), mana));
        }

        public static int ManaCost(string skill, int configuredCost)
        {
            if (skill == null || skill == "bite" || skill == "ss_hit") return 0;
            if (configuredCost > 0) return configuredCost;
            if (skill == "foxfire") return 20;
            return skill.StartsWith("ss_", StringComparison.Ordinal) && skill != "ss_hit" ? 30 : 0;
        }

        public static int Power(PetStats s) { return (int)Math.Round(s.maxHp / 10.0 + s.atk * 2 + s.def * 1.5 + s.spd); }

        // ---------------- 服务端动作 ----------------
        public PetResp Run(string cmd, string id)
        {
            switch (cmd)
            {
                case PetCmd.Use: return Use(id);
                case PetCmd.Exchange: return Exchange(id);
                case PetCmd.Rite: return Rite();
                case PetCmd.Gift: return Gift();
            }
            return Fail("未知命令");
        }

        PetResp Fail(string msg) { var r = Snap(); r.ok = false; r.msg = msg; return r; }
        PetResp Ok(string msg) { var r = Snap(); r.ok = true; r.msg = msg; return r; }
        PetResp Snap()
        {
            return new PetResp { petAct = ActiveId(role), petOwn = role.petOwn.ToList(), bond = role.bond, ssGift = role.ssGift, coin = role.coin, bag = role.bag };
        }

        PetResp Use(string id)
        {
            if (!Owned(role, id)) return Fail("还没有这只宠物");
            role.petAct = id; return Ok(((string)Dex(id)["name"]) + " 出战");
        }

        PetResp Exchange(string id)
        {
            if (!IsSs(id)) return Fail("这只不能兑换");
            if (Owned(role, id)) return Fail("已经拥有了");
            var inv = new InventoryService(role); int cost = (int)D["ssCost"];
            if (inv.ItemCount("ss_soul") < cost) return Fail("神兽之魂不够");
            inv.Take("ss_soul", cost);
            role.petOwn.Add(id); role.petAct = id;
            return Ok("神兽「" + (string)Dex(id)["name"] + "」跟随了你");
        }

        PetResp Rite()
        {
            int cost = (int)D["ssRite"]["coin"], min = (int)D["ssRite"]["min"], max = (int)D["ssRite"]["max"];
            if (role.coin < cost) return Fail("银两不够");
            var inv = new InventoryService(role); int n = (int)Math.Round(min + new Random().NextDouble() * (max - min));
            if (!inv.Give("ss_soul", n)) return Fail("背包满了，装不下神兽之魂");
            role.coin -= cost;
            return Ok("神兽祭：天神赐下神兽之魂 ×" + n);
        }

        PetResp Gift()
        {
            if (role.ssGift) return Fail("福利已经领过了");
            role.ssGift = true;
            foreach (var id in AllIds()) if (IsSs(id) && !role.petOwn.Contains(id)) role.petOwn.Add(id);
            if (ActiveId(role) == "fox") role.petAct = "ss_1227";
            return Ok("神兽体验福利：七只神兽已全部入队，可以在宠物面板切换出战");
        }
    }
}
