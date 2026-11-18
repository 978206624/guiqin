using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Chat
{
    // 聊天（服务端参考实现）：频道权限、字数、冷却、防刷屏都在这里判；路人的发言、帮派闲聊、私聊回话、公告是模拟出来的。
    // 真服务器：发言广播给同频道在线玩家；这里的 simulate 部分整个换成真人即可。
    public class ChatService
    {
        public string Guild; public int Members;
        public event Action<ChatMsg> OnPush;           // 推给这个客户端的消息（别人的发言 / 公告）
        readonly List<ChatMsg> hist = new List<ChatMsg>();
        readonly Dictionary<string, long> lastAt = new Dictionary<string, long>();
        string lastText = ""; long lastTextAt;
        readonly List<KeyValuePair<long, ChatMsg>> queue = new List<KeyValuePair<long, ChatMsg>>();     // 延时发出的（路人回话）
        readonly Random rng = new Random(); int uid;
        long nextWorld, nextGuild, nextNotice, nextGreet; bool started;
        static JToken D { get { return GameData.Raw["chat"]; } }
        static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }

        string Pick(JToken arr) { return (string)arr[rng.Next(arr.Count())]; }
        JToken Bot() { return D["bots"][rng.Next(D["bots"].Count())]; }

        ChatMsg Make(string ch, string from, string cls, int lv, string text, string peer = null)
        {
            return new ChatMsg { id = ++uid, ch = ch, from = from, cls = cls, lv = lv, text = text, peer = peer, t = Now() };
        }
        void Emit(ChatMsg m) { hist.Add(m); while (hist.Count > (int)D["history"]) hist.RemoveAt(0); if (OnPush != null) OnPush(Clone(m)); }
        static ChatMsg Clone(ChatMsg m) { return new ChatMsg { id = m.id, ch = m.ch, from = m.from, cls = m.cls, lv = m.lv, text = m.text, peer = m.peer, t = m.t, self = m.self }; }

        public ChatHistResp History() { return new ChatHistResp { msgs = hist.Select(Clone).ToList(), guild = Guild, members = Members }; }

        public ChatResp SetGuild(bool join)
        {
            if (join) { Guild = (string)D["guildName"]; Members = D["guildMembers"].Count() + 1; return new ChatResp { ok = true, msg = "加入了「" + Guild + "」", guild = Guild, members = Members }; }
            Guild = null; Members = 0; return new ChatResp { ok = true, msg = "退出了帮派" };
        }

        public void Notice(string text) { Emit(Make("notice", "", null, 0, text)); }

        public ChatResp Send(RoleState me, ChatSendReq q)
        {
            Func<string, ChatResp> fail = m => new ChatResp { ok = false, msg = m };
            string text = (q.text ?? "").Trim();
            if (text.Length == 0) return fail("说点什么吧");
            if (text.Length > (int)D["maxLen"]) return fail("最多 " + (int)D["maxLen"] + " 个字");
            string ch = q.ch;
            if (ch != "world" && ch != "guild" && ch != "party" && ch != "pm") return fail("这个频道不能发言");
            if (ch == "guild" && Guild == null) return fail("你还没有加入帮派");
            if (ch == "party") return fail("你还没有队伍");
            if (ch == "pm" && !D["bots"].Any(b => (string)b["name"] == q.peer)) return fail("找不到这个玩家");
            long now = Now(); long cool = (long)((ch == "world" ? (double)D["cooldown"] : ch == "pm" ? 1.0 : 1.5) * 1000);
            long last; if (lastAt.TryGetValue(ch, out last) && now - last < cool) return fail("说话太快了，请 " + Math.Ceiling((cool - (now - last)) / 1000.0) + " 秒后再发");
            if (text == lastText && now - lastTextAt < 20000) return fail("别刷屏");
            lastAt[ch] = now; lastText = text; lastTextAt = now;
            var m = Make(ch, me.name, me.cls, me.lv, text, ch == "pm" ? q.peer : null); m.self = true;
            hist.Add(m); while (hist.Count > (int)D["history"]) hist.RemoveAt(0);
            // 路人回话：命中关键词就隔几秒回一句；私聊对方一定回
            if (ch == "pm") Later(1500 + rng.Next(2500), Make("pm", q.peer, null, 0, Pick(D["pmReplies"]), q.peer));
            else if (ch == "world")
                foreach (JProperty p in D["replies"])
                    if (Regex.IsMatch(text, p.Name)) { var b = Bot(); Later(1800 + rng.Next(3500), Make("world", (string)b["name"], (string)b["cls"], (int)b["lv"], Pick(p.Value))); break; }
            return new ChatResp { ok = true, sent = Clone(m) };
        }

        void Later(long ms, ChatMsg m) { queue.Add(new KeyValuePair<long, ChatMsg>(Now() + ms, m)); }

        // 每帧调用：到点的回话发出去，隔一阵子冒一句世界 / 帮派闲聊、公告、私聊问候
        public void Tick()
        {
            long now = Now();
            if (!started) { started = true; nextWorld = now + 4000; nextGuild = now + 9000; nextNotice = now + 60000; nextGreet = now + 90000; for (int i = 0; i < 4; i++) { var b = Bot(); hist.Add(Make("world", (string)b["name"], (string)b["cls"], (int)b["lv"], Pick(D["lines"]))); } hist.Add(Make("notice", "", null, 0, (string)D["notices"][0])); }
            for (int i = queue.Count - 1; i >= 0; i--) if (queue[i].Key <= now) { var m = queue[i].Value; queue.RemoveAt(i); m.t = now; Emit(m); }
            if (now >= nextWorld) { nextWorld = now + 8000 + rng.Next(14000); var b = Bot(); Emit(Make("world", (string)b["name"], (string)b["cls"], (int)b["lv"], Pick(D["lines"]))); }
            if (Guild != null && now >= nextGuild) { nextGuild = now + 12000 + rng.Next(20000); var n = Pick(D["guildMembers"]); var b = D["bots"].FirstOrDefault(x => (string)x["name"] == n) ?? Bot(); Emit(Make("guild", n, (string)b["cls"], (int)b["lv"], Pick(D["guildLines"]))); }
            if (now >= nextNotice) { nextNotice = now + 90000 + rng.Next(60000); Emit(Make("notice", "", null, 0, Pick(D["notices"]))); }
            if (now >= nextGreet) { nextGreet = now + 120000 + rng.Next(90000); var b = Bot(); string nm = (string)b["name"]; Emit(Make("pm", nm, (string)b["cls"], (int)b["lv"], Pick(D["pmGreets"]).Replace("{bot}", nm), nm)); }
        }
    }
}
