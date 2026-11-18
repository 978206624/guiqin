using System.Collections.Generic;

namespace GuiQin.Quest
{
    public static class QuestCmd { public const string Talk = "quest.talk"; }

    public class QuestTalkReq { public string npc; }
    // ok=false：当前任务和这个 NPC 无关，客户端走普通对话。ok=true：这一拍完成了；任务最后一拍带奖励
    public class QuestTalkResp
    {
        public bool ok; public string msg;
        public int step, cnt, lv, exp, coin;
        public bool levelUp, done;                                   // done：这一拍是任务的最后一拍
        public Dictionary<string, int> items = new Dictionary<string, int>();
    }
}
