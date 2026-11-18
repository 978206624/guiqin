using System;
using System.Collections.Generic;
using System.Linq;

namespace GuiQin.Battle
{
    public class BStatus { public int turns, v, n; }

    public class BUnit
    {
        public int Id; public string Side, Row, Name, Sprite, Ai = "attack";
        public float H; public bool Flip, Me, MagicAtk;
        public int Hp, MaxHp, Mp, MaxMp, Atk, Matk, Def, Mdef, Spd;
        public float Crit = 10f, Dodge, Aggro = 1f;
        public List<KeyValuePair<string, float>> Kit;       // 神兽技能表 [(技能, 概率)]
        public PasDef Pas;
        public bool Alive = true, Defending, Boss;
        public Dictionary<string, int> Cd = new Dictionary<string, int>();
        public Dictionary<string, BStatus> St = new Dictionary<string, BStatus>();
        // 战斗中的临时数值（对应网页版 u._xxx）
        public int Invul, Absorb, Guard, Bt, Hs, Ha, Cu, Mf, Ca;
        public int Rg0, Dd0, Md0, Df0, Spd0; public float Dg0, DmgBuff;
        public bool Dw, Rv, Ls, Take2;
    }

    // 回合制战斗引擎（服务端参考实现）。纯逻辑：不碰 Unity API，种子固定就能重放，方便服务端移植 / 校验。
    // 公式、技能效果、被动、AI 都对应网页版 game.js 的 perform / unitAct / autoAct。
    public class BattleEngine
    {
        public readonly List<BUnit> Units; public BUnit Player;
        public int Round; public string Over;
        public readonly List<string> PlayerSkills = new List<string>();       // 主角当前能用的技能
        public Dictionary<string, int> SkillLv;                               // 主角各技能的修炼等级（只有门派职业有）；每升 1 级威力 / 疗效 +12%
        public readonly List<KeyValuePair<string, string>> PlayerAuto = new List<KeyValuePair<string, string>>();   // 职业自动规则 [(技能, 条件)]
        readonly IReadOnlyDictionary<string, SkillDef> skills;
        readonly IReadOnlyDictionary<string, string> itemNames;
        readonly Random rng;
        List<BattleEvent> ev;

        public BattleEngine(List<BUnit> units, IReadOnlyDictionary<string, SkillDef> skills,
            IReadOnlyDictionary<string, string> itemNames, int seed)
        {
            Units = units; this.skills = skills; this.itemNames = itemNames; rng = new Random(seed);
            foreach (var u in units)
            {
                if (u.Me) Player = u;
                if (u.Pas != null && u.Pas.baotiStart > 0) u.Bt = u.Pas.baotiStart;
                if (u.Pas != null && u.Pas.dodgeUp > 0) u.Dodge += u.Pas.dodgeUp;
                if (u.Pas != null && u.Pas.atkMul > 0) { u.Atk = (int)Math.Round(u.Atk * u.Pas.atkMul); u.Matk = (int)Math.Round(u.Matk * u.Pas.atkMul); }
            }
        }

        // ---------------- 小工具 ----------------
        float Rnd(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        bool Chance(float pct) { return rng.NextDouble() * 100.0 < pct; }
        static int R(float v) { return (int)Math.Round(v, MidpointRounding.AwayFromZero); }
        List<BUnit> Living(string side) { return Units.Where(u => u.Alive && u.Side == side).ToList(); }
        static string Other(string side) { return side == "ally" ? "enemy" : "ally"; }
        BUnit Weakest(string side) { return Living(side).OrderBy(u => u.Hp).FirstOrDefault(); }
        SkillDef Sk(string id) { SkillDef s; return skills.TryGetValue(id, out s) ? s : null; }

        void Add(BattleEvent e) { ev.Add(e); }
        void Float(BUnit u, string text, string kind) { Add(new BattleEvent { t = "float", u = u.Id, text = text, kind = kind }); }
        void SetSt(BUnit u, string k, int turns, int v = 0) { u.St[k] = new BStatus { turns = turns, v = v }; Add(new BattleEvent { t = "status", u = u.Id, st = k, turns = turns }); }

        void ClearSt(BUnit u, string k)
        {
            if (!u.St.ContainsKey(k)) return;
            switch (k)
            {
                case "stun": u.Take2 = false; break;
                case "rage": if (u.Rg0 != 0) { u.Def = u.Rg0; u.Rg0 = 0; } break;
                case "curse": if (u.Cu > 0) { u.Atk = R(u.Atk / (float)Math.Pow(0.9, u.Cu)); u.Matk = R(u.Matk / (float)Math.Pow(0.9, u.Cu)); u.Cu = 0; } break;
                case "dd": if (u.Dd0 != 0) { u.Def = u.Dd0; u.Dd0 = 0; } break;
                case "mfear": if (u.Md0 != 0) { u.Mdef = u.Md0; u.Md0 = 0; } break;
                case "fear": if (u.Df0 != 0) { u.Def = u.Df0; u.Df0 = 0; } break;
                case "slow": u.Spd = u.Spd0; u.Dodge = u.Dg0; break;
            }
            u.St.Remove(k);
            Add(new BattleEvent { t = "unstatus", u = u.Id, st = k });
        }

        // ---------------- 一个回合 ----------------
        // 主角的指令 + 本回合所有人的行动。返回这一回合的全部事件，Over 非空表示战斗结束（win / lose / flee）。
        public List<BattleEvent> RunRound(BattleCmd cmd)
        {
            ev = new List<BattleEvent>();
            Round++;
            Add(new BattleEvent { t = "round", v = Round });
            foreach (var u in Units) u.Defending = false;
            var act = cmd == null || cmd.type == "auto" ? AutoAct() : cmd;
            if (act.type == "flee")
            {
                bool hasBoss = Units.Any(u => u.Ai == "boss");
                if (!hasBoss && rng.NextDouble() < 0.65) { Over = "flee"; Add(new BattleEvent { t = "flee", text = "成功逃脱" }); return ev; }
                Add(new BattleEvent { t = "flee", text = hasBoss ? "头目堵住了去路，逃不掉！" : "逃跑失败！" });
                act = new BattleCmd { type = "none" };
            }
            var order = Units.Where(u => u.Alive).Select(u => new { u, k = u.Spd * Rnd(0.9f, 1.1f) }).OrderByDescending(o => o.k).Select(o => o.u).ToList();
            foreach (var u in order)
            {
                if (!u.Alive || Over != null) continue;
                StatusStart(u);
                if (u.St.ContainsKey("stun")) { Add(new BattleEvent { t = "stun", u = u.Id }); }
                else Perform(u, u == Player ? act : UnitAct(u));
                StatusEnd(u);
                if (Living("enemy").Count == 0) Over = "win";
                else if (!Player.Alive) Over = "lose";
            }
            return ev;
        }

        void StatusStart(BUnit u)
        {
            if (u.Invul > 0) u.Invul--;
            if (u.Alive && u.Pas != null && u.Pas.baoti > 0 && u.Bt < 5) { u.Bt++; Float(u, "霸体 ×" + u.Bt, "info"); }
            foreach (var k in u.Cd.Keys.ToList()) { if (--u.Cd[k] <= 0) u.Cd.Remove(k); }
            BStatus s;
            if (u.Alive && u.St.TryGetValue("bleed", out s)) { u.Hp = Math.Max(1, u.Hp - s.v); Add(new BattleEvent { t = "dot", u = u.Id, v = s.v, text = "流血", hp = u.Hp }); }
            if (u.Alive && u.St.TryGetValue("hot", out s)) { u.Hp = Math.Min(u.MaxHp, u.Hp + s.v); Add(new BattleEvent { t = "heal", u = u.Id, v = s.v, hp = u.Hp }); }
        }

        void StatusEnd(BUnit u)
        {
            foreach (var k in u.St.Keys.ToList()) { if (--u.St[k].turns <= 0) ClearSt(u, k); }
        }

        // ---------------- 出手 ----------------
        public Func<string, int[]> ItemUse;      // 用掉一件药品：返回 [气血, 法力] 恢复量，没有 / 不能用返回 null（背包在服务端，由外面注入）

        void Perform(BUnit u, BattleCmd act)
        {
            if (act == null || act.type == "none" || !u.Alive) return;
            if (act.type == "item")
            {
                var r = u.Me && ItemUse != null ? ItemUse(act.skill) : null;
                if (r == null) { Float(u, "用不了", "miss"); return; }
                string name = act.skill != null && itemNames.TryGetValue(act.skill, out var label) ? label : act.skill;
                Float(u, name, "heal");
                if (r[0] > 0) { int d = Math.Min(r[0], u.MaxHp - u.Hp); u.Hp += d; Add(new BattleEvent { t = "heal", u = u.Id, v = d, hp = u.Hp }); }
                if (r[1] > 0) { u.Mp = Math.Min(u.MaxMp, u.Mp + r[1]); Add(new BattleEvent { t = "mp", u = u.Id, mp = u.Mp }); }
                return;
            }
            if (act.type == "defend") { u.Defending = true; Add(new BattleEvent { t = "defend", u = u.Id }); return; }
            var s = Sk(act.skill); if (s == null) return;
            if (u.Me && SkillLv != null) { int lv; SkillLv.TryGetValue(act.skill, out lv); float km = GuiQin.Train.TrainService.Mul(lv); if (km != 1f) s = s.Scaled(km); }
            BUnit chosen = act.target >= 0 ? Units.FirstOrDefault(x => x.Id == act.target) : null;
            if (s.mp > 0)
            {
                if (u.Mp < s.mp) { Perform(u, new BattleCmd { type = "skill", skill = "attack", target = Weakest(Other(u.Side)) != null ? Weakest(Other(u.Side)).Id : -1 }); return; }
                u.Mp -= (int)s.mp; Add(new BattleEvent { t = "mp", u = u.Id, mp = u.Mp });
            }

            if (s.teamBuff != null || s.rage > 0)                      // 战魂歌（全队增益）/ 狂龙吼（自身舍防换伤）
            {
                var rs = s.teamBuff != null ? Units.Where(x => x.Side == u.Side && x.Alive).ToList() : new List<BUnit> { u };
                Add(new BattleEvent { t = "act", u = u.Id, skill = act.skill, ts = rs.Select(x => x.Id).ToArray() });
                if (s.cd > 0) u.Cd[act.skill] = s.cd + 1;
                foreach (var t in rs)
                {
                    if (s.teamBuff != null) { int n = s.teamBuff.turns + (t == u ? 1 : 0); SetSt(t, "bless", n); SetSt(t, "shield", n); Float(t, "战魂", "info"); }
                    else { ClearSt(t, "rage"); t.Rg0 = t.Def; t.Def = R(t.Def * 0.2f); SetSt(t, "rage", s.rage + 1); Float(t, "狂龙", "info"); }
                }
                return;
            }

            if (s.heal > 0 || s.healPow > 0 || s.revive > 0)
            {
                var mates = Units.Where(x => x.Side == u.Side).ToList();
                Func<BUnit> lowest = () => mates.Where(x => x.Alive).OrderBy(x => x.Hp / (float)x.MaxHp).FirstOrDefault() ?? u;
                if (s.revive > 0)
                {
                    var dead = mates.FirstOrDefault(x => !x.Alive);
                    if (dead != null)
                    {
                        Add(new BattleEvent { t = "act", u = u.Id, skill = act.skill, ts = new[] { dead.Id } });
                        dead.Alive = true; dead.Hp = R(dead.MaxHp * s.revive);
                        Add(new BattleEvent { t = "revive", u = dead.Id, hp = dead.Hp, skill = act.skill }); return;
                    }
                }
                List<BUnit> ts;
                if (s.target == "allies") ts = mates.Where(x => x.Alive).ToList();
                else if (s.target == "self") ts = new List<BUnit> { u };
                else ts = new List<BUnit> { chosen != null && chosen.Alive && chosen.Side == u.Side ? chosen : lowest() };
                Add(new BattleEvent { t = "act", u = u.Id, skill = act.skill, ts = ts.Select(x => x.Id).ToArray() });
                foreach (var t in ts)
                {
                    int v = s.healPow > 0 ? R(u.Matk * s.healPow * Rnd(0.9f, 1.1f)) : R(t.MaxHp * (s.heal > 0 ? s.heal : 0.3f));
                    t.Hp = Math.Min(t.MaxHp, t.Hp + v);
                    Add(new BattleEvent { t = "heal", u = t.Id, v = v, hp = t.Hp, skill = act.skill });
                    if (s.cleanse) { ClearSt(t, "slow"); ClearSt(t, "mark"); }
                    if (s.shield > 0) { SetSt(t, "shield", s.shield); Float(t, "护体", "info"); }
                    if (s.hot != null) SetSt(t, "hot", s.hot.turns, R(v * s.hot.pct));
                }
                return;
            }

            var foes = Living(Other(u.Side));
            if (foes.Count == 0) return;
            var primary = chosen != null && chosen.Alive && chosen.Side != u.Side ? chosen : foes[0];
            var targets = s.target == "all" ? foes : new List<BUnit> { primary };
            if (s.hits > 1) targets = new List<BUnit> { primary }.Concat(foes.Where(x => x != primary).OrderBy(x => rng.Next()).Take(s.hits - 1)).ToList();
            bool magic = s.magic || ((act.skill == "attack" || act.skill == "ss_hit") && u.MagicAtk);
            if (s.cd > 0) u.Cd[act.skill] = s.cd + 1;
            if (s.absorb > 0 || s.guard > 0)                         // 自身防护：伤害转治疗 N 次 / N 次免伤
            {
                Add(new BattleEvent { t = "act", u = u.Id, skill = act.skill, ts = new[] { u.Id } });
                if (s.absorb > 0) { u.Absorb = s.absorb; SetSt(u, "absorb", 99); }
                if (s.guard > 0) u.Guard = s.guard;
                Float(u, s.absorb > 0 ? "血盾 ×" + s.absorb : "抵挡 ×" + s.guard, "info");
                if (s.power <= 0) return;
            }
            if (s.selfVuln > 0) { SetSt(u, "vuln", s.selfVuln + 1); Float(u, "易伤", "info"); }
            if (s.selfCost > 0) { int c = R(u.MaxHp * s.selfCost); u.Hp = Math.Max(1, u.Hp - c); Add(new BattleEvent { t = "dot", u = u.Id, v = c, text = "", hp = u.Hp }); }
            Add(new BattleEvent { t = "act", u = u.Id, skill = act.skill, ts = targets.Select(x => x.Id).ToArray() });

            foreach (var t in targets)
            {
                if (Chance(t.Dodge)) { Add(new BattleEvent { t = "miss", u = t.Id, text = "闪避" }); continue; }
                bool crit = Chance((u.Crit) + s.critUp);
                float dmg = (magic ? u.Matk : u.Atk) * s.power * Rnd(0.9f, 1.1f) * (crit ? 1.5f : 1f) - (magic ? t.Mdef : t.Def) * 0.6f;
                if (s.vsMark > 0 && t.St.ContainsKey("mark")) dmg *= s.vsMark;
                if (s.vsBleed > 0 && t.St.ContainsKey("bleed")) dmg *= s.vsBleed;
                if (t.St.ContainsKey("vuln")) dmg *= 1.3f;
                if (u.St.ContainsKey("bless")) dmg *= 1.2f;
                if (u.St.ContainsKey("rage")) dmg *= 1.6f;
                if (t.St.ContainsKey("shield")) dmg *= 0.7f;
                if (t.Defending) dmg *= 0.5f;
                var up = u.Pas ?? new PasDef(); var tp = t.Pas ?? new PasDef();
                if (up.vsFear > 0 && t.St.ContainsKey("fear")) dmg *= up.vsFear;
                if (up.lowAtk > 0 && u.Hp < u.MaxHp * 0.3f) dmg *= 1 + up.lowAtk;
                if (up.dbl > 0 && rng.NextDouble() < up.dbl) { dmg *= 2; Float(u, "双倍", "info"); }
                if (tp.take > 0) dmg *= tp.take;
                if (up.dmgUp > 0) dmg *= 1 + up.dmgUp;
                if (u.DmgBuff > 0) dmg *= 1 + u.DmgBuff;
                if (s.baoti && u.Bt > 0) { dmg *= 1 + 0.1f * u.Bt; Float(u, "霸体 ×" + u.Bt + " 燎原", "info"); u.Bt = 0; }
                if (t.Take2) dmg *= 2;
                if (t.St.ContainsKey("curse")) dmg *= 1 + 0.1f * t.Cu;
                if (tp.baoti > 0 && t.Bt > 0) dmg *= 1 - 0.1f * t.Bt;
                if (tp.hurtStack != null && t.Hs > 0) dmg *= 1 - Math.Min(tp.hurtStack.max, t.Hs) * tp.hurtStack.step;
                int d = Math.Max(1, R(dmg));
                if (t.Invul > 0) { Add(new BattleEvent { t = "miss", u = t.Id, text = "无敌" }); continue; }
                if (t.Guard > 0) { t.Guard--; Add(new BattleEvent { t = "miss", u = t.Id, text = "抵挡" }); continue; }
                if (t.Absorb > 0)
                {
                    t.Absorb--; if (t.Absorb == 0) ClearSt(t, "absorb");
                    t.Hp = Math.Min(t.MaxHp, t.Hp + d); Add(new BattleEvent { t = "heal", u = t.Id, v = d, hp = t.Hp }); continue;
                }
                t.Hp = Math.Max(0, t.Hp - d);
                if (t.Hp <= 0 && tp.deathWard > 0 && !t.Dw) { t.Dw = true; t.Hp = 1; t.Invul = 2; t.DmgBuff = 0.5f; Float(t, "绝杀咒印", "heal"); }
                if (t.Hp <= 0 && tp.revive > 0 && !t.Rv)
                {
                    t.Rv = true; t.Hp = R(t.MaxHp * tp.revive); Float(t, string.IsNullOrEmpty(tp.reviveText) ? "神龙祝福" : tp.reviveText, "heal");
                    if (tp.reviveAtk > 0) { t.Atk = R(t.Atk * (1 + tp.reviveAtk)); t.Matk = R(t.Matk * (1 + tp.reviveAtk)); }
                }
                if (t.Hp <= 0 && tp.lethalStun > 0 && !t.Ls) { t.Ls = true; SetSt(u, "stun", 1); u.Take2 = true; Float(u, "定身", "info"); }
                Add(new BattleEvent { t = "hit", u = t.Id, v = d, crit = crit, hp = t.Hp, skill = act.skill });
                if (s.lifesteal > 0 && u.Alive)
                {
                    int hv = Math.Max(1, R(d * s.lifesteal)); u.Hp = Math.Min(u.MaxHp, u.Hp + hv);
                    Add(new BattleEvent { t = "heal", u = u.Id, v = hv, hp = u.Hp });
                }
                if (t.Hp <= 0 && tp.ownerAtk > 0 && Player != null && Player.Alive && t.Side == Player.Side)
                { Player.Atk = R(Player.Atk * (1 + tp.ownerAtk)); Player.Matk = R(Player.Matk * (1 + tp.ownerAtk)); Float(Player, "攻击 +" + R(tp.ownerAtk * 100) + "%", "info"); }
                if (t.Hp > 0 && tp.hurtStack != null) t.Hs++;
                if (t.Hp > 0 && tp.hurtAtk != null && t.Ha < tp.hurtAtk.max) { t.Ha++; t.Atk = R(t.Atk * (1 + tp.hurtAtk.step)); t.Matk = R(t.Matk * (1 + tp.hurtAtk.step)); }
                if (crit && s.critAtk && u.Ca < 3) { u.Ca++; u.Atk = R(u.Atk * 1.15f); u.Matk = R(u.Matk * 1.15f); Float(u, "攻击 +15%", "info"); }
                if (t.Hp <= 0) { t.Alive = false; t.St.Clear(); Add(new BattleEvent { t = "die", u = t.Id }); continue; }
                if (s.fear > 0)
                {
                    BStatus f; if (!t.St.TryGetValue("fear", out f)) f = new BStatus { turns = 3 };
                    f.n = Math.Min(3, f.n + s.fear); f.turns = 3; if (t.Df0 == 0) t.Df0 = t.Def; t.Def = R(t.Df0 * (1 - 0.12f * f.n));
                    t.St["fear"] = f; Add(new BattleEvent { t = "status", u = t.Id, st = "fear", turns = 3 }); Float(t, "恐惧", "info");
                }
                if (s.hpCut > 0) { int c = R(t.MaxHp * s.hpCut); t.MaxHp -= c; t.Hp = Math.Min(t.Hp, t.MaxHp); Float(t, "上限 -" + c, "info"); Add(new BattleEvent { t = "maxhp", u = t.Id, maxHp = t.MaxHp, hp = t.Hp }); }
                if (s.defDown > 0) { if (t.Dd0 == 0) { t.Dd0 = t.Def; t.Def = R(t.Def * (1 - s.defDown)); } SetSt(t, "dd", 3); Float(t, "破防", "info"); }
                if (s.mfear > 0) { if (t.Md0 == 0) t.Md0 = t.Mdef; int n = Math.Min(3, t.Mf + 1); t.Mf = n; t.Mdef = R(t.Md0 * (1 - 0.1f * n)); SetSt(t, "mfear", 3); Float(t, "离魂", "info"); }
                if (s.curse) { t.Cu = Math.Min(2, t.Cu + 1); if (t.Cu <= 2) { t.Atk = R(t.Atk * 0.9f); t.Matk = R(t.Matk * 0.9f); } SetSt(t, "curse", 2); Float(t, "天罚", "info"); }
                if (s.bleed != null) { SetSt(t, "bleed", s.bleed.turns, Math.Max(1, R(t.MaxHp * s.bleed.pct))); Float(t, "流血", "info"); }
                if (s.vuln > 0) { SetSt(t, "vuln", s.vuln); Float(t, "易伤", "info"); }
                if (s.mark > 0) { SetSt(t, "mark", s.mark); Float(t, "魔化", "info"); }
                if (s.slow > 0)
                {
                    if (!t.St.ContainsKey("slow")) { t.Spd0 = t.Spd; t.Dg0 = t.Dodge; t.Spd = R(t.Spd * 0.8f); t.Dodge = Math.Max(0, t.Dodge - 10); }
                    SetSt(t, "slow", s.slow); Float(t, "减速", "info");
                }
            }
        }

        // ---------------- 自动出手 ----------------
        public BattleCmd AutoAct()
        {
            var p = Player;
            if (p == null || !p.Alive) return new BattleCmd { type = "none" };
            Func<string, bool> ok = id => PlayerSkills.Contains(id) && Sk(id) != null && p.Mp >= Sk(id).mp;
            var foes = Living("enemy"); var mates = Units.Where(x => x.Side == "ally").ToList();
            Func<BUnit, int> idOf = x => x != null ? x.Id : -1;
            if (PlayerAuto.Count > 0)
            {
                var cond = new Dictionary<string, Func<bool>>
                {
                    { "selfHurt", () => p.Hp < p.MaxHp * 0.5f }, { "allyHurt", () => mates.Any(x => x.Alive && x.Hp < x.MaxHp * 0.55f) },
                    { "allyHurtMany", () => mates.Count(x => x.Alive && x.Hp < x.MaxHp * 0.7f) >= 2 }, { "dead", () => mates.Any(x => !x.Alive) },
                    { "many", () => foes.Count >= 3 }, { "marked", () => foes.Any(f => f.St.ContainsKey("mark")) },
                    { "fast", () => foes.Any(f => !f.St.ContainsKey("slow")) && Round % 3 == 2 }, { "always", () => true },
                    { "bleeding", () => foes.Any(f => f.St.ContainsKey("bleed")) }, { "noVuln", () => foes.Any(f => !f.St.ContainsKey("vuln")) },
                    { "hpHigh", () => p.Hp > p.MaxHp * 0.5f }, { "rageOK", () => !p.St.ContainsKey("rage") && p.Hp > p.MaxHp * 0.4f && Round % 3 == 1 },
                    { "teamStart", () => !mates.Any(x => x.Alive && x.St.ContainsKey("bless")) } };
                foreach (var kv in PlayerAuto)
                {
                    Func<bool> c; if (!ok(kv.Key) || !cond.TryGetValue(kv.Value, out c) || !c()) continue;
                    var sk = Sk(kv.Key);
                    if (sk.target == "one")
                    {
                        BUnit tg = null;
                        if (kv.Value == "marked") tg = foes.FirstOrDefault(f => f.St.ContainsKey("mark"));
                        else if (kv.Value == "bleeding") tg = foes.FirstOrDefault(f => f.St.ContainsKey("bleed"));
                        else if (kv.Value == "noVuln") tg = foes.FirstOrDefault(f => !f.St.ContainsKey("vuln"));
                        return new BattleCmd { type = "skill", skill = kv.Key, target = idOf(tg ?? Weakest("enemy")) };
                    }
                    return new BattleCmd { type = "skill", skill = kv.Key };
                }
                return new BattleCmd { type = "skill", skill = "attack", target = idOf(Weakest("enemy")) };
            }
            if (p.Hp < p.MaxHp * 0.35f && ok("ningshen")) return new BattleCmd { type = "skill", skill = "ningshen" };
            if (foes.Count >= 3 && ok("zongheng")) return new BattleCmd { type = "skill", skill = "zongheng" };
            if (Sk("pokong") != null && PlayerSkills.Contains("pokong") && p.Mp >= Sk("pokong").mp + 80) return new BattleCmd { type = "skill", skill = "pokong", target = idOf(Weakest("enemy")) };
            return new BattleCmd { type = "skill", skill = "attack", target = idOf(Weakest("enemy")) };
        }

        // 其余单位的出手，按 ai 类型决定
        BattleCmd UnitAct(BUnit u)
        {
            var foeSide = Other(u.Side); var foes = Living(foeSide); var mates = Living(u.Side);
            var hurt = mates.OrderBy(x => x.Hp / (float)x.MaxHp).FirstOrDefault(); var low = Weakest(foeSide);
            if (foes.Count == 0) return new BattleCmd { type = "none" };
            Func<BUnit> pickFoe = () =>
            {
                float sum = foes.Sum(f => f.Aggro), r = (float)rng.NextDouble() * sum;
                foreach (var f in foes) { r -= f.Aggro; if (r <= 0) return f; }
                return foes[0];
            };
            Func<string, BUnit, BattleCmd> sk = (id, t) => new BattleCmd { type = "skill", skill = id, target = t != null ? t.Id : -1 };
            switch (u.Ai)
            {
                case "heal": return hurt != null && hurt.Hp < hurt.MaxHp * 0.6f ? sk("heal_ally", hurt) : sk("attack", pickFoe());
                case "mage": return Round % 3 == 0 ? sk("rain", null) : sk("bolt", pickFoe());
                case "striker": return sk("snipe", low);
                case "tank": return sk("smash", pickFoe());
                case "boss": return Round % 3 == 0 ? sk("sweep", null) : sk("attack", pickFoe());
                case "ss":
                    {
                        string pick = null;
                        if (u.Kit != null)
                            foreach (var a in u.Kit)
                            {
                                var k = Sk(a.Key) ?? new SkillDef();
                                if (u.Cd.ContainsKey(a.Key) || (k.absorb > 0 && u.Absorb > 0) || (k.guard > 0 && u.Guard > 0)) continue;
                                if (rng.NextDouble() < a.Value) { pick = a.Key; break; }
                            }
                        return sk(pick ?? "ss_hit", pickFoe());
                    }
                case "fox": return sk(rng.NextDouble() < 0.35 ? "foxfire" : "bite", low);
                default: return sk(u.Row == "pet" ? "bite" : "attack", pickFoe());
            }
        }
    }
}
