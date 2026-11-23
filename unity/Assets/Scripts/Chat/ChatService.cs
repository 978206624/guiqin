using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Chat
{
    // 模拟服务端按角色维护聊天和社交状态；正式服可替换路人和定时器。
    public class ChatService
    {
        public event Action<ChatMsg> OnPush;
        public event Action<PartyState> OnParty;
        RoleState role;
        readonly List<ChatMsg> hist = new List<ChatMsg>();
        readonly List<string> friends = new List<string>(), party = new List<string>();
        readonly HashSet<string> pending = new HashSet<string>();
        readonly Dictionary<string, long> lastAt = new Dictionary<string, long>();
        readonly Dictionary<string, (string text, long time)> lastText = new Dictionary<string, (string, long)>();
        readonly List<(long time, Action run)> queue = new List<(long, Action)>();
        readonly Random rng = new Random();
        int uid, revision, outing, guildVersion, sessionAfter;
        long nextWorld, nextGuild, nextParty, nextNotice, nextGreet;
        string guild;
        int Members => guild == null ? 0 : D["guildMembers"].Count() + 1;
        static JToken D => GameData.Raw["chat"];
        static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        public ChatService(RoleState role)
        {
            this.role = role;
            long now = Now();
            nextWorld = now + 4000; nextGuild = now + 9000; nextNotice = now + 60000; nextGreet = now + 90000;
        }

        public void Bind(RoleState current) => role = current;

        public void BeginSession()
        {
            sessionAfter = uid;
            nextWorld = Now() + 4000;
        }

        string Pick(JToken values) => (string)values[rng.Next(values.Count())];
        string PickBot() => (string)D["bots"][rng.Next(D["bots"].Count())]["name"];
        string Fill(string text)
        {
            // 模拟服务端当前没同步城镇天气，闲聊使用晴天文案。
            return text.Replace("{name}", role.name).Replace("{bot}", PickBot())
                .Replace("{weather}天气", "晴天").Replace("{weather}", "晴");
        }
        static JToken Bot(string name) => D["bots"].FirstOrDefault(b => (string)b["name"] == name);
        static bool Known(string name) => !string.IsNullOrEmpty(name) && (Bot(name) != null || D["guildMembers"].Any(b => (string)b == name));
        static ChatPerson Person(string name)
        {
            var b = Bot(name);
            return new ChatPerson { name = name, cls = (string)b?["cls"], lv = (int?)b?["lv"] ?? 0 };
        }

        ChatMsg Make(string ch, string text) => new ChatMsg { roleId = role.id, ch = ch, text = text };
        ChatMsg BotMsg(string ch, string name, string text)
        {
            var m = Make(ch, text);
            var p = Person(name);
            m.from = p.name; m.cls = p.cls; m.lv = p.lv;
            if (ch == "pm") m.peer = name;
            return m;
        }

        void Save(ChatMsg m, bool push)
        {
            m.id = ++uid;
            m.t = Now();
            hist.Add(m);
            int limit = Math.Min(200, (int)D["history"]);
            if (hist.Count > limit) hist.RemoveAt(0);
            if (push) OnPush?.Invoke(Clone(m));
        }

        static ChatMsg Clone(ChatMsg m) => JObject.FromObject(m).ToObject<ChatMsg>();

        PartyState Party() => new PartyState { roleId = role.id, revision = revision,
            members = party.Select(Person).ToList(), pending = pending.ToList() };
        void PushParty() { revision++; OnParty?.Invoke(Party()); }
        public ChatHistResp History() => new ChatHistResp { msgs = hist.Where(m => m.id > sessionAfter || (m.ch != "world" && m.ch != "system" && m.ch != "notice")).Select(Clone).ToList(), guild = guild,
            members = Members, friends = friends.Select(Person).ToList(), party = Party() };

        public ChatResp SetGuild(bool join)
        {
            if (join && guild == null)
            {
                guild = (string)D["guildName"]; guildVersion++; nextGuild = Now() + 9000;
                SystemMessage("你加入了帮派「" + guild + "」");
            }
            else if (!join && guild != null)
            {
                string name = guild; guild = null; guildVersion++;
                SystemMessage("你退出了帮派「" + name + "」");
            }
            return new ChatResp { ok = true, guild = guild, members = Members };
        }

        public FriendResp Friends() => new FriendResp { ok = true, friends = friends.Select(Person).ToList() };
        public FriendResp SetFriend(string peer, bool add)
        {
            if (!add)
            {
                if (friends.Remove(peer)) SystemMessage("已删除好友 " + peer);
                return Friends();
            }
            if (!Known(peer) || peer == role.name) return new FriendResp { msg = "找不到这个玩家" };
            if (friends.Contains(peer)) return Friends();
            int limit = (int)D["friendLimit"];
            if (friends.Count >= limit) return new FriendResp { msg = "好友已满（最多 " + limit + " 人）" };
            friends.Add(peer);
            SystemMessage("已添加 " + peer + " 为好友");
            return Friends();
        }

        public PartyResp Invite(string peer)
        {
            if (!Known(peer) || peer == role.name) return new PartyResp { msg = "找不到这个玩家" };
            if (party.Contains(peer) || pending.Contains(peer)) return new PartyResp { msg = "对方已在队伍或已收到邀请" };
            int limit = (int)D["partyLimit"];
            if (1 + party.Count + pending.Count >= limit) return new PartyResp { msg = "队伍已满（最多 " + limit + " 人）" };
            pending.Add(peer);
            PushParty();
            SystemMessage("已向 " + peer + " 发出组队邀请");
            int generation = outing;
            Later(rng.Next(1500, 4000), () =>
            {
                if (generation != outing || !pending.Remove(peer)) return;
                if (rng.NextDouble() < 0.75)
                {
                    party.Add(peer);
                    if (party.Count == 1) nextParty = Now() + 7000;
                    PushParty();
                    SystemMessage(peer + " 加入了队伍");
                    Save(BotMsg("party", peer, "来啦～[笑] 往哪儿走？"), true);
                }
                else
                {
                    PushParty();
                    SystemMessage(Pick(D["partyDecline"]).Replace("{bot}", peer));
                }
            });
            return new PartyResp { ok = true, party = Party() };
        }

        public PartyResp Leave()
        {
            bool hadParty = party.Count > 0;
            outing++; party.Clear(); pending.Clear(); PushParty();
            if (hadParty) SystemMessage("你离开了队伍");
            return new PartyResp { ok = true, party = Party() };
        }

        public void Notice(string text) => Save(Make("notice", text), true);
        public void SystemMessage(string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) Save(Make("system", text), true);
        }

        public ChatResp Send(ChatSendReq q)
        {
            ChatResp Fail(string m) => new ChatResp { msg = m };
            string text = (q?.text ?? "").Trim();
            if (text.Length == 0) return Fail("说点什么吧");
            if (text.Length > (int)D["maxLen"]) return Fail("最多 " + (int)D["maxLen"] + " 个字");
            string ch = q.ch;
            if (ch != "world" && ch != "guild" && ch != "party" && ch != "pm") return Fail("这个频道不能发言");
            if (ch == "guild" && guild == null) return Fail("你还没有加入帮派");
            if (ch == "party" && party.Count == 0) return Fail("你还没有队伍");
            if (ch == "pm" && (!Known(q.peer) || q.peer == role.name)) return Fail("找不到这个玩家");
            foreach (var term in D["ban"] ?? new JArray())
            {
                string word = (string)term;
                if (!string.IsNullOrEmpty(word)) text = Regex.Replace(text, Regex.Escape(word), new string('*', word.Length), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            }
            long now = Now();
            long cool = (long)((ch == "world" ? (double)D["cooldown"] : ch == "pm" ? 1 : 1.5) * 1000);
            if (lastAt.TryGetValue(ch, out long last) && now - last < cool)
                return Fail("说话太快了，请 " + Math.Ceiling((cool - now + last) / 1000.0) + " 秒后再发");
            string channel = ch == "pm" ? ch + ":" + q.peer : ch;
            if (lastText.TryGetValue(channel, out var previous) && text == previous.text && now - previous.time < 20000)
                return Fail("别刷屏");
            lastAt[ch] = now; lastText[channel] = (text, now);
            var m = Make(ch, text);
            m.from = role.name; m.cls = role.cls; m.lv = role.lv; m.self = true; m.peer = ch == "pm" ? q.peer : null;
            m.links = Links(text);
            Save(m, false);
            ScheduleReply(ch, q.peer, text);
            return new ChatResp { ok = true, sent = Clone(m) };
        }

        List<ChatItemLink> Links(string text)
        {
            var result = new List<ChatItemLink>();
            if (role.bag?.items == null) return result;
            var items = GameData.Raw["items"];
            foreach (Match match in Regex.Matches(text, @"\[([^\[\]]{1,8})\]"))
            {
                string name = match.Groups[1].Value;
                foreach (var entry in role.bag.items)
                {
                    if (entry.Value <= 0 || (string)items[entry.Key]?["name"] != name) continue;
                    result.Add(new ChatItemLink { start = match.Index, length = match.Length, itemId = entry.Key });
                    break;
                }
            }
            return result;
        }

        void ScheduleReply(string ch, string peer, string text)
        {
            if (ch == "pm")
            {
                Later(rng.Next(1500, 4000), () => Save(BotMsg("pm", peer, Pick(D["pmReplies"])), true));
                return;
            }
            double chance = ch == "world" ? 0.60 : ch == "guild" ? 0.75 : 0.80;
            if (rng.NextDouble() >= chance) return;
            int previousGuild = guildVersion, previousParty = outing;
            var pool = D["replies"].OfType<JProperty>().FirstOrDefault(p => Regex.IsMatch(text, p.Name))?.Value ?? D["fallback"];
            Later(rng.Next(1500, 4500), () =>
            {
                if (ch == "guild" && (guild == null || previousGuild != guildVersion)) return;
                if (ch == "party" && (party.Count == 0 || previousParty != outing)) return;
                string name = ch == "guild" ? Pick(D["guildMembers"]) : ch == "party" ? party[rng.Next(party.Count)] : PickBot();
                Save(BotMsg(ch, name, Fill(Pick(pool))), true);
            });
        }

        void Later(long ms, Action run) => queue.Add((Now() + ms, run));

        public void Tick()
        {
            long now = Now();
            for (int i = 0; i < queue.Count;)
            {
                if (queue[i].time > now) { i++; continue; }
                var run = queue[i].run; queue.RemoveAt(i); run();
            }
            if (now >= nextWorld)
            {
                nextWorld = now + rng.Next(6000, 15000);
                Save(BotMsg("world", PickBot(), Fill(Pick(D["lines"]))), true);
            }
            if (guild != null && now >= nextGuild)
            {
                nextGuild = now + rng.Next(10000, 24000);
                Save(BotMsg("guild", Pick(D["guildMembers"]), Fill(Pick(D["guildLines"]))), true);
            }
            if (party.Count > 0 && now >= nextParty)
            {
                nextParty = now + rng.Next(9000, 20000);
                Save(BotMsg("party", party[rng.Next(party.Count)], Fill(Pick(D["partyLines"]))), true);
            }
            if (now >= nextNotice)
            {
                nextNotice = now + rng.Next(70000, 130000);
                Notice(Fill(Pick(D["notices"])));
            }
            if (now >= nextGreet)
            {
                nextGreet = now + rng.Next(120000, 210000);
                string name = PickBot();
                Save(BotMsg("pm", name, Pick(D["pmGreets"]).Replace("{bot}", name)), true);
            }
        }
    }
}
