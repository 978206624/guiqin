using System.Collections.Generic;
using GuiQin.Net;
using GuiQin.Tower;

namespace GuiQin.Quest
{
    public enum QuestNpcMark { None, Available, Pending, TurnIn }

    // 任务进度的服务端参考逻辑。进度（step / cnt / cleared）只在服务端改，客户端发“我和谁说话了 / 打赢了”之类的事件，服务端自己判。
    public class QuestService
    {
        readonly RoleState role;
        public QuestService(RoleState role) { this.role = role; }

        public Beat Current { get { return StoryBeats.Current(role); } }

        public Dictionary<string, QuestNpcMark> NpcMarkers(string map)
        {
            var marks = new Dictionary<string, QuestNpcMark>();
            var current = Current;
            if (current == null) return marks;
            if (current.IsSectGate)
            {
                var mark = role.lv >= GuiQin.Sect.SectService.SectLv ? QuestNpcMark.TurnIn : QuestNpcMark.Pending;
                if (map == "taixuforest") marks["dao_menzhu"] = mark;
                if (map == "taixupeak") marks["taixu_mo"] = mark;
                return marks;
            }

            // 单段对话任务在同一轮对话接取并完成，显示接取标记。
            if (current.first && current.type == "talk")
            {
                if (current.map == map && current.npc != null) marks[current.npc] = QuestNpcMark.Available;
                return marks;
            }

            var last = current;
            for (int step = role.step + 1; !last.last; step++)
            {
                var next = StoryBeats.At(step);
                if (next == null || next.qid != current.qid) return marks;
                last = next;
            }
            if (last.type == "talk" && last.map == map && last.npc != null)
                marks[last.npc] = last == current ? QuestNpcMark.TurnIn : QuestNpcMark.Pending;
            return marks;
        }

        // 旧角色可能按原奖励走完了引见；只补到入门所需等级，不重发任务奖励。
        public static int EnsureSectLevel(RoleState role)
        {
            if (StoryBeats.Current(role)?.IsSectGate != true || role.lv >= GuiQin.Sect.SectService.SectLv) return 0;
            int gain = -role.exp;
            for (int lv = role.lv; lv < GuiQin.Sect.SectService.SectLv; lv++) gain += Rewards.ExpNeed(lv);
            Rewards.Grant(role, gain, 0, null, out _);
            return gain;
        }

        // 找人对话 / 采集：只有当前这一拍的 npc 对得上才算
        public QuestTalkResp Talk(string npc)
        {
            var b = Current;
            if (b == null || !b.IsTalk || b.npc != npc) return new QuestTalkResp { ok = false, step = role.step, cnt = role.cnt };
            var r = new QuestTalkResp { ok = true };
            Advance(b, r);
            return r;
        }

        // 一拍完成：step++；任务最后一拍发奖励
        public void Advance(Beat b, QuestTalkResp r, Dictionary<string, int> extraItems = null)
        {
            role.step++; role.cnt = 0; r.done = b.last;
            if (b.last)
            {
                List<string> lost;
                r.levelUp = Rewards.Grant(role, b.exp, b.coin, b.items, out lost);
                r.exp = b.exp; r.coin = b.coin; r.items = new Dictionary<string, int>(b.items);
                if (lost.Count > 0) r.msg = "背包已满，部分奖励丢失";
            }
            int sectExp = EnsureSectLevel(role);
            if (sectExp > 0) { r.exp += sectExp; r.levelUp = true; }
            r.step = role.step; r.cnt = role.cnt; r.lv = role.lv;
        }

        // 战斗是否允许：剧情关卡只在对应那一拍开放；有前置关卡要先通关
        public string CheckStage(StageDef st)
        {
            if (st.story)
            {
                var b = Current;
                if (b?.IsSectGate == true) return "请先完成「投师学艺」，拜入道家或墨家。";
                if (b == null || (b.type != "fight" && b.type != "kill") || b.stage != st.id) return "现在还不是和他动手的时候";
            }
            if (st.tower)
            {
                if (role.lv < TowerService.MinLv) return "达到 " + TowerService.MinLv + " 级才能挑战玄兵塔守卫。";
                if (st.n <= role.tower) return "这名守卫你已经打败了，不能重复挑战。";
                if (st.n > role.tower + 1) return "要按顺序挑战守卫。";
            }
            if (st.needAll != null && st.needAll.Exists(id => !role.cleared.Contains(id))) return st.needMsg ?? "需先击败守卫。";
            if (!string.IsNullOrEmpty(st.need) && !role.cleared.Contains(st.need))
            {
                StageDef need; string nm = GameData.Stages.TryGetValue(st.need, out need) ? need.name : st.need;
                return "他们人多势众，贸然上前太危险。先打通「" + nm + "」再来。";
            }
            return null;
        }

        // 打赢一场：记通关；如果正好是当前任务的战斗拍，推进（kill 拍累计数量）。返回完成的那一拍（没有推进返回 null）
        public Beat OnWin(StageDef st, out string killLine, out QuestTalkResp done)
        {
            killLine = null; done = null;
            if (!role.cleared.Contains(st.id)) role.cleared.Add(st.id);
            var b = Current;
            if (b == null || (b.type != "fight" && b.type != "kill") || b.stage != st.id) return null;
            bool finished = true;
            if (b.type == "kill") { role.cnt += st.enemies.Count; finished = role.cnt >= b.n; if (!finished) killLine = b.goal + " " + role.cnt + "/" + b.n; }
            if (!finished) return null;
            done = new QuestTalkResp { ok = true }; Advance(b, done);
            return b;
        }
    }
}
