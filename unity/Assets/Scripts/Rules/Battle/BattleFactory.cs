using System;
using GuiQin.Net;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GuiQin.Battle
{
    // 一场进行中的战斗（服务端持有；客户端只拿到 BattleView 和每回合的事件）
    public class BattleSession
    {
        public string Id, StageId; public BattleEngine Engine; public BattleView View; public StageDef Stage;
        public int Seed { get; internal set; }

        // 当前战场快照：开战时的静态信息 + 引擎里每个单位此刻的血 / 蓝 / 存活 / 状态。断线重连、重进游戏时发给客户端重建战场。
        public BattleView Snapshot()
        {
            var v = new BattleView { id = View.id, title = View.title, mode = View.mode, bg = View.bg, skills = View.skills, turnSeconds = View.turnSeconds, items = View.items, round = Engine.Round };
            v.units = View.units.Select(u =>
            {
                var b = Engine.Units.Find(x => x.Id == u.id);
                var c = new UnitView { id = u.id, side = u.side, row = u.row, name = u.name, sprite = u.sprite, h = u.h, flip = u.flip, me = u.me, boss = u.boss, hp = u.hp, maxHp = u.maxHp, mp = u.mp, maxMp = u.maxMp };
                if (b != null)
                {
                    c.hp = b.Hp; c.maxHp = b.MaxHp; c.mp = b.Mp; c.maxMp = b.MaxMp; c.dead = !b.Alive;
                    c.sts = b.St.ToDictionary(kv => kv.Key, kv => kv.Value.turns);
                }
                return c;
            }).ToList();
            return v;
        }
    }

    // 根据角色状态 + 关卡配置搭出战场：属性公式对应网页版 stats() / petStats() / levelStats()，敌人取 data.enemies。
    // 真服务器在开战时做同样的事，并把装备 / 宝石 / 亲密度加成算进属性（装备 / 宝石已计入；亲密度未做）。
    public static class BattleFactory
    {
        public static IReadOnlyDictionary<string, SkillDef> Skills => GameData.Skills;

        static float F(JToken t, float def = 0f) { return t == null || t.Type == JTokenType.Null ? def : (float)t; }
        static int Base(JToken b, JToken g, string k, int L, float mul) { return (int)Math.Round((F(b[k]) + F(g[k]) * L) * mul, MidpointRounding.AwayFromZero); }

        public static BattleSession Create(RoleState role, StageDef st, int seed)
        {
            var D = GameData.Raw; var b = D["base"]; var g = D["grow"]; int L = Math.Max(1, role.lv);
            var units = new List<BUnit>(); int nextId = 1;

            // 敌方
            foreach (var id in st.enemies)
            {
                var e = D["enemies"][id]; bool isPet = e["pet"] != null && (bool)e["pet"], boss = e["boss"] != null && (bool)e["boss"];
                int hp = (int)F(e["hp"]);
                units.Add(new BUnit
                {
                    Id = nextId++, Side = "enemy", Row = isPet ? "pet" : "person", Name = (string)e["name"], Sprite = (string)e["sprite"], H = F(e["h"]) * 1.12f,
                    Flip = id != "xinmo",  // 心魔原图已经朝向玩家一侧
                    Hp = hp, MaxHp = hp, Atk = (int)F(e["atk"]), Matk = (int)F(e["atk"]), Def = (int)F(e["def"]), Mdef = (int)F(e["def"]), Spd = (int)F(e["spd"]),
                    Ai = boss ? "boss" : "attack", Boss = boss
                });
            }

            // 我方：主角
            var cls = D["classes"][role.cls] ?? D["classes"]["novice_f"];
            var ps = GuiQin.Inventory.RoleStats.Compute(role.cls, L, role.bag);      // 职业 + 等级 + 心法 + 装备 + 宝石
            int maxHp = ps.maxHp, maxMp = ps.maxMp;
            var me = new BUnit
            {
                Id = nextId++, Side = "ally", Row = "person", Me = true, Name = role.name, H = 126f, MagicAtk = cls["magicAtk"] != null && (bool)cls["magicAtk"],
                MaxHp = maxHp, MaxMp = maxMp, Hp = role.hp < 0 ? maxHp : Math.Max(1, Math.Min(maxHp, role.hp)), Mp = role.mp < 0 ? maxMp : Math.Min(maxMp, role.mp),
                Atk = ps.atk, Matk = ps.matk, Def = ps.def, Mdef = ps.mdef, Spd = ps.spd, Crit = ps.crit, Dodge = ps.dodge
            };
            string sp = "b_" + role.cls; me.Sprite = GameData.Assets.img.ContainsKey(sp) ? sp : "b_player";
            units.Add(me);

            // 我方：出战宠物（灵狐或已拥有的神兽）；属性和亲密度加成走 PetService，和宠物面板显示的一致
            string pid = GuiQin.Pets.PetService.ActiveId(role); var dx = D["petDex"][pid]; var pst = GuiQin.Pets.PetService.Stats(role, pid);
            int pHp = pst.maxHp;
            var pet = new BUnit
            {
                Id = nextId++, Side = "ally", Row = "pet", Name = (string)dx["name"], Sprite = (string)dx["sprite"], H = F(dx["h"], 62f),
                Ai = dx["ai"] != null ? (string)dx["ai"] : "fox", MagicAtk = dx["mag"] != null && (bool)dx["mag"],
                MaxHp = pHp, Hp = pHp, Atk = pst.atk, Matk = pst.matk, Def = pst.def, Mdef = pst.mdef, Spd = pst.spd, Crit = 10f, Dodge = 3f
            };
            if (dx["act"] != null) pet.Kit = dx["act"].Select(a => new KeyValuePair<string, float>((string)a[0], (float)a[1])).ToList();
            if (dx["pas"] != null) pet.Pas = dx["pas"].ToObject<PasDef>();
            units.Add(pet);

            var eng = new BattleEngine(units, Skills, GameData.ItemNames, seed);
            // 主角可用技能：门派职业用自己的技能表，新手用通用表；等级不够的不能用
            var list = cls["skills"] != null ? cls["skills"].Select(x => (string)x).ToList() : D["playerSkills"].Select(x => (string)x).ToList();
            // 门派职业的技能要“学过”才能用（技能修炼）；新手的通用技能仍按人物等级解锁
            bool table = cls["skills"] != null; GuiQin.Train.TrainService.EnsureSkl(role);
            Func<string, bool> usable = id => Skills.ContainsKey(id) && (table ? GuiQin.Train.TrainService.Level(role, id) > 0 : Skills[id].lv <= L);
            foreach (var id in list) if (usable(id)) eng.PlayerSkills.Add(id);
            if (table) eng.SkillLv = new Dictionary<string, int>(role.skl);
            if (cls["auto"] != null) foreach (var a in cls["auto"]) eng.PlayerAuto.Add(new KeyValuePair<string, string>((string)a[0], (string)a[1]));

            var view = new BattleView
            {
                id = Guid.NewGuid().ToString("N").Substring(0, 8), title = st.name, mode = "pve", bg = null,
                units = units.Select(u => new UnitView { id = u.Id, side = u.Side, row = u.Row, name = u.Name, sprite = u.Sprite, h = u.H, flip = u.Flip, me = u.Me, boss = u.Boss, hp = u.Hp, maxHp = u.MaxHp, mp = u.Mp, maxMp = u.MaxMp }).ToList(),
                skills = list.Where(id => Skills.ContainsKey(id)).Select(id => new SkillView { id = id, name = Skills[id].name + (table && usable(id) ? " Lv." + GuiQin.Train.TrainService.Level(role, id) : ""), target = (Skills[id].heal > 0 || Skills[id].healPow > 0 || Skills[id].revive > 0) && (Skills[id].target ?? "one") == "one" ? "ally" : (Skills[id].target ?? "one"), mp = (int)Skills[id].mp, usable = usable(id), note = usable(id) ? null : (table ? "未学习" : Skills[id].lv + " 级解锁") }).ToList()
            };
            return new BattleSession { Id = view.id, StageId = st.id, Engine = eng, View = view, Stage = st, Seed = seed };
        }
    }
}
