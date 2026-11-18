using System.Collections.Generic;
using GuiQin.Net;

namespace GuiQin.Quest
{
    // 任务进度的服务端参考逻辑。进度（step / cnt / cleared）只在服务端改，客户端发“我和谁说话了 / 打赢了”之类的事件，服务端自己判。
    public class QuestService
    {
        readonly RoleState role;
        public QuestService(RoleState role) { this.role = role; }

        public Beat Current { get { return StoryBeats.At(role.step); } }

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
            r.step = role.step; r.cnt = role.cnt; r.lv = role.lv;
        }

        // 战斗是否允许：剧情关卡只在对应那一拍开放；有前置关卡要先通关
        public string CheckStage(StageDef st)
        {
            if (st.story)
            {
                var b = Current;
                if (b == null || (b.type != "fight" && b.type != "kill") || b.stage != st.id) return "现在还不是和他动手的时候";
            }
            if (!string.IsNullOrEmpty(st.need) && !role.cleared.Contains(st.need))
            {
                StageDef need; string nm = GameData.Stages.TryGetValue(st.need, out need) ? need.name : st.need;
                return "他们人多势众，贸然上前太危险。先打通「" + nm + "」再来。";
            }
            return null;
        }

        public string CheckPortal(PortalDef p)
        {
            if (!string.IsNullOrEmpty(p.needQuest) && role.step < StoryBeats.NeedStep(p.needQuest)) return string.IsNullOrEmpty(p.lockMsg) ? "现在还不能过去" : p.lockMsg;
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
