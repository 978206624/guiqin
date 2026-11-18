using System.Collections.Generic;

namespace GuiQin.Battle
{
    // ---- 技能 / 被动数据（data.json 的 skills / pas）----
    public class BleedDef { public int turns; public float pct; }
    public class HotDef { public int turns; public float pct; }
    public class TeamBuffDef { public int turns; }
    public class StepMax { public float step, max; }

    public class SkillDef
    {
        public string name, target, desc;
        public float mp, power, heal, healPow, revive, lifesteal, vsMark, vsBleed, selfCost, defDown, hpCut;
        public int lv, cd, hits, absorb, guard, shield, fear, mark, slow, vuln, selfVuln, rage, critUp, mfear;
        public bool magic, fire, melee, cleanse, critAtk, baoti, curse, ss;
        public BleedDef bleed; public HotDef hot; public TeamBuffDef teamBuff;
    }

    public class PasDef
    {
        public float atkMul, vsFear, dbl, revive, take, lowAtk, ownerAtk, dmgUp, reviveAtk;
        public int deathWard, lethalStun, dodgeUp, baoti, baotiStart;
        public string reviveText;
        public StepMax hurtStack, hurtAtk;
    }

    // ---- 协议里的战斗数据（服务端 → 客户端）----
    public class UnitView
    {
        public int id; public string side, row, name, sprite;
        public float h; public bool flip, me, boss;
        public int hp, maxHp, mp, maxMp;
    }

    public class SkillView { public string id, name, target; public int mp; public bool usable; public string note; }

    public class BattleView
    {
        public string id, title, mode, bg;
        public List<UnitView> units;
        public List<SkillView> skills;       // 主角可用的技能（给指令面板）
        public int turnSeconds = 30;
        public Dictionary<string, int> items;   // 背包里的药品（id → 数量），指令面板的“物品”用
    }

    public class BattleStartReq { public string stage; }
    public class BattleStartResp { public bool ok; public string msg; public BattleView battle; }

    public class BattleCmd { public string type; public string skill; public int target = -1; }   // type: skill / item(skill 填物品 id) / defend / flee / auto
    public class BattleCmdReq { public string battleId; public BattleCmd cmd; }

    // 一条战斗事件。客户端按顺序播放；所有数值都已由服务端算好。
    // t: round(v=回合数) act(u 出手者, skill, ts 目标) defend(u) hit(u 受击者, v 伤害, crit, hp) dot(u, v, text, hp) heal(u, v, hp)
    //    miss(u, text) float(u, text, kind) mp(u, mp) status(u, st, turns) unstatus(u, st) die(u) revive(u, hp) maxhp(u, maxHp, hp)
    //    stun(u) flee(text)
    public class BattleEvent
    {
        public string t; public int u = -1; public int[] ts;
        public string skill, text, kind, st;
        public int v, hp, mp, maxHp, turns; public bool crit;
    }

    public class BattleEnd
    {
        public string result;                 // win / lose / flee
        public int round, exp, coin, lv;
        public bool levelUp;
        public Dictionary<string, int> items = new Dictionary<string, int>();
        public string respawnMap;             // 战败回到的地图
        public string note;                   // 附加提示（任务 / 塔进度等）
    }

    public class BattleRoundResp
    {
        public List<BattleEvent> events = new List<BattleEvent>();
        public bool over;
        public BattleEnd end;
        public int round;
    }
}
