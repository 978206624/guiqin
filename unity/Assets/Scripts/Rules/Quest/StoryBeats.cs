using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace GuiQin.Quest
{
    // 主线剧情的“节拍”：story.json 的每个任务拆成若干拍，step 就是节拍序号。客户端（显示）和服务端（判定）用同一份展平结果。
    // 类型：talk 找人对话 / spot 采集点（和 talk 一样走 quest.talk）/ fight 首领战 / kill 清怪计数。
    public class Beat
    {
        public string qid, title, chapter, summary, type, goal, npc, map, stage, spot;
        public float[] at;
        public int n;
        public bool first, last;
        public List<string[]> pages = new List<string[]>(), pre = new List<string[]>(), accept = new List<string[]>(), outro = new List<string[]>();
        public int exp, coin; public Dictionary<string, int> items = new Dictionary<string, int>();
        public bool IsTalk { get { return type == "talk" || type == "spot"; } }
    }

    public static class StoryBeats
    {
        static List<Beat> all;
        static Dictionary<string, int> first;   // 任务 id → 第一拍序号

        static List<string[]> Pages(JToken t)
        {
            var l = new List<string[]>(); if (t == null) return l;
            foreach (var p in t) l.Add(new[] { (string)p[0], (string)p[1] });
            return l;
        }

        internal static void Build(JObject story, Dictionary<string, NpcInfo> npcs)
        {
            var beats = new List<Beat>();
            var starts = new Dictionary<string, int>();
            foreach (var q in story["quests"])
            {
                var bs = (JArray)q["beats"];
                for (int i = 0; i < bs.Count; i++)
                {
                    var b = bs[i];
                    var beat = new Beat
                    {
                        qid = (string)q["id"], title = (string)q["title"], chapter = (string)q["chapter"], summary = (string)q["summary"],
                        type = (string)b["type"], goal = (string)b["goal"], npc = (string)b["npc"], map = (string)b["map"], stage = (string)b["stage"], spot = (string)b["spot"],
                        n = b["n"] != null ? (int)b["n"] : 0, pages = Pages(b["pages"]), pre = Pages(b["pre"])
                    };
                    if (b["at"] != null) beat.at = new[] { (float)b["at"][0], (float)b["at"][1] };
                    if (i == 0) { beat.first = true; beat.accept = Pages(q["accept"]); starts[beat.qid] = beats.Count; }
                    if (i == bs.Count - 1)
                    {
                        beat.last = true; beat.outro = Pages(q["complete"]);
                        var rw = q["reward"];
                        if (rw != null)
                        {
                            beat.exp = rw["exp"] != null ? (int)rw["exp"] : 0; beat.coin = rw["coin"] != null ? (int)rw["coin"] : 0;
                            if (rw["items"] != null) foreach (var p in ((JObject)rw["items"]).Properties()) beat.items[p.Name] = (int)p.Value;
                        }
                    }
                    beats.Add(beat);
                }
            }
            // 剧情里的采集点当成一个只在对应任务时出现的“人物”
            foreach (var b in beats) if (b.type == "spot" && !npcs.ContainsKey(b.npc)) npcs[b.npc] = new NpcInfo { name = b.spot, spot = true, color = "#9ff0ff", idle = "" };
            all = beats;
            first = starts;
        }

        public static Beat At(int step) { return step >= 0 && step < all.Count ? all[step] : null; }
        public static int NeedStep(string questId) { int s; return first.TryGetValue(questId, out s) ? s : 0; }
    }
}
