using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuiQin.Chat;
using GuiQin.Net;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 聊天：综合 / 世界 / 帮派 / 队伍 / 私聊 / 系统 六个页签 + 顶部滚动公告条。对应网页版 chat.js。
    // 发言规则（频道权限、字数、冷却、防刷屏）全在服务端判，客户端只负责显示和提交；别人的发言走 push.chat。
    // 暂未做：表情图、物品链接、点名字弹菜单（现在点一条消息就私聊发言人）、组队 / 好友（要多人在线服务）。
    public class ChatUi
    {
        static readonly string[][] Tabs = { new[] { "all", "综合" }, new[] { "world", "世界" }, new[] { "guild", "帮派" }, new[] { "party", "队伍" }, new[] { "pm", "私聊" }, new[] { "system", "系统" } };
        static readonly Dictionary<string, string[]> Ch = new Dictionary<string, string[]>   // 频道 → {标签, 颜色, 页签}
        {
            { "world", new[] { "世界", "#7ad7ff", "world" } }, { "guild", new[] { "帮派", "#7fe07f", "guild" } }, { "party", new[] { "队伍", "#ffb36b", "party" } },
            { "pm", new[] { "私聊", "#ff8ad4", "pm" } }, { "system", new[] { "系统", "#ff7a5a", "system" } }, { "notice", new[] { "公告", "#ffb020", "system" } },
        };

        readonly GameObject root; readonly RectTransform box, listContent; readonly ScrollRect scroll;
        readonly Func<IGameBackend> net; readonly Func<RoleState> getRole; readonly Action<string> mini, toast;
        readonly GameObject noticeBar; readonly Text noticeText; float noticeT;
        readonly List<ChatMsg> hist = new List<ChatMsg>(); readonly Dictionary<string, int> unread = new Dictionary<string, int>();
        readonly List<string> peers = new List<string>();
        string tab = "all", sendCh = "world", pmPeer, guild; int members; bool busy, stick;
        Transform tabsRow, stripRow; InputField input; Text hint, chPill; Button sendBtn;

        public bool Visible { get { return root.activeSelf; } }

        public ChatUi(Transform parent, Func<IGameBackend> net, Func<RoleState> role, Action<string> mini, Action<string> toast)
        {
            this.net = net; getRole = role; this.mini = mini; this.toast = toast;
            var dim = Ui.Box("chatUi", parent, WebUi.Dim); root = dim.gameObject;
            Ui.Anchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = WebUi.Panel("box", dim.transform); box = panel.rectTransform; WebUi.At(box, 24, 150, 672, 1000);
            var title = Ui.Label("title", box, "聊天", 32, WebUi.Amber, TextAnchor.MiddleCenter); WebUi.Title(title); WebUi.At(title.rectTransform, 0, 6, 672, 52);
            var close = WebUi.Close(box, Close); WebUi.At((RectTransform)close.transform, 618, 10, 40, 40);
            tabsRow = Ui.Rect("tabs", box); WebUi.At((RectTransform)tabsRow, 4, 64, 664, 46);
            stripRow = Ui.Rect("strip", box); WebUi.At((RectTransform)stripRow, 12, 114, 648, 76);
            var vp = Ui.Box("vp", box, WebUi.C("#0a061466")); vp.gameObject.AddComponent<RectMask2D>(); WebUi.At(vp.rectTransform, 12, 196, 648, 690);
            listContent = Ui.Rect("content", vp.transform);
            listContent.anchorMin = new Vector2(0, 1); listContent.anchorMax = new Vector2(1, 1); listContent.pivot = new Vector2(0.5f, 1); listContent.offsetMin = new Vector2(8, 0); listContent.offsetMax = new Vector2(-8, 0);
            var lg = listContent.gameObject.AddComponent<VerticalLayoutGroup>(); lg.spacing = 8; lg.childControlWidth = true; lg.childControlHeight = true; lg.childForceExpandHeight = false; lg.padding = new RectOffset(0, 0, 8, 8);
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll = vp.gameObject.AddComponent<ScrollRect>(); scroll.content = listContent; scroll.viewport = vp.rectTransform; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            var pill = Ui.Btn("ch", box, "世界", 20, Color.clear, () => { if (tab == "all" && guild != null) { sendCh = sendCh == "world" ? "guild" : "world"; Redraw(); } });
            WebUi.Dress(pill.image, WebUi.C("#22183a"), WebUi.C("#120c20"), WebUi.Gold, 8); chPill = pill.GetComponentInChildren<Text>(); WebUi.At((RectTransform)pill.transform, 12, 896, 90, 56);
            input = Ui.Input("msg", box, "说点什么（最多 40 个字）", 22, false, 40); WebUi.At((RectTransform)input.transform, 110, 896, 434, 56);
            sendBtn = Ui.Btn("send", box, "发送", 24, Color.clear, Send); WebUi.Dress(sendBtn.image, WebUi.Red, WebUi.C("#5a1418"), WebUi.Gold, 8); sendBtn.GetComponentInChildren<Text>().color = WebUi.Light; WebUi.At((RectTransform)sendBtn.transform, 552, 896, 108, 56);
            hint = Ui.Label("hint", box, "", 15, WebUi.C("#ff9a8a"), TextAnchor.MiddleCenter); WebUi.At(hint.rectTransform, 12, 958, 648, 32);
            // 顶部滚动公告条（世界地图上才显示）
            var nb = WebUi.Gradient("notice", parent, WebUi.C("#241a3ce6"), WebUi.C("#140d24e6"), 14); noticeBar = nb.gameObject; WebUi.At(nb.rectTransform, 140, 150, 440, 38);
            noticeText = Ui.Label("t", nb.transform, "", 16, WebUi.C("#ffb020"), TextAnchor.MiddleCenter); noticeText.horizontalOverflow = HorizontalWrapMode.Overflow; Ui.Anchor(noticeText.rectTransform, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-10, 0));
            noticeBar.SetActive(false); root.SetActive(false);
        }

        // ---------------- 数据 ----------------
        public async Task LoadHistory()
        {
            var r = await net().ChatHistory(); hist.Clear(); unread.Clear(); peers.Clear();
            if (!r.Ok) return;
            guild = r.data.guild; members = r.data.members;
            foreach (var m in r.data.msgs) { hist.Add(m); if (m.ch == "pm" && !string.IsNullOrEmpty(m.peer) && !peers.Contains(m.peer)) peers.Add(m.peer); }
        }

        public void Add(ChatMsg m)
        {
            hist.Add(m); while (hist.Count > 200) hist.RemoveAt(0);
            if (m.ch == "pm" && !string.IsNullOrEmpty(m.peer) && !peers.Contains(m.peer)) peers.Add(m.peer);
            string home = Ch[m.ch][2]; bool shown = Visible && (tab == "all" || InTab(m, tab));
            if (!shown && !m.self) { Inc(home); if (m.ch == "pm") Inc("pm:" + m.peer); }
            if (m.ch == "notice") { noticeText.text = m.text; noticeT = 9f; }
            mini(Line(m, true));
            if (Visible) { stick = m.self || NearBottom(); Redraw(); }
        }

        void Inc(string k) { int n; unread.TryGetValue(k, out n); unread[k] = n + 1; }
        bool NearBottom() { return scroll.verticalNormalizedPosition < 0.08f; }

        public void Tick(float dt, bool worldVisible)
        {
            if (noticeT > 0f) noticeT -= dt;
            noticeBar.SetActive(worldVisible && noticeT > 0f && !Visible);
        }

        static bool InTab(ChatMsg m, string t, string peer = null)
        {
            if (t == "all") return true;
            if (t == "pm") return m.ch == "pm" && m.peer == peer;
            return Ch[m.ch][2] == t;
        }

        static string Esc(string s) { return (s ?? "").Replace("<", "＜").Replace(">", "＞"); }
        static string Line(ChatMsg m, bool brief = false)
        {
            string[] c = Ch[m.ch]; string h = "<color=" + c[1] + ">[" + c[0] + "]</color> ";
            if (m.ch == "notice" || m.ch == "system") return h + Esc(m.text);
            if (m.ch == "pm") h += m.self ? "你对 " + Esc(m.peer) + " 说：" : Esc(m.from) + " 对你说：";
            else
            {
                h += (m.self ? "<color=#8ff08a>" + Esc(m.from) + "</color>" : Esc(m.from));
                if (!brief) { var cl = GameData.Raw["classes"][m.cls ?? ""]; h += "<size=13><color=#a99db4> " + (cl != null ? (string)cl["name"] : "") + " " + m.lv + "级</color></size>"; }
                h += "：";
            }
            return h + Esc(m.text);
        }

        // ---------------- 面板 ----------------
        public void Open(string t)
        {
            if (!string.IsNullOrEmpty(t)) tab = t;
            if (tab == "pm" && pmPeer == null) pmPeer = peers.Count > 0 ? peers[0] : null;
            root.SetActive(true); hint.text = ""; ClearUnread(); stick = true; Redraw();
        }
        public void Close() { root.SetActive(false); }

        void ClearUnread()
        {
            if (tab == "all") unread.Clear(); else { unread.Remove(tab); if (tab == "pm" && pmPeer != null) unread.Remove("pm:" + pmPeer); }
        }

        string CurSend()
        {
            if (tab == "pm") return pmPeer != null ? "pm" : null;
            if (tab == "system" || tab == "party") return null;
            if (tab == "guild") return guild != null ? "guild" : null;
            if (tab == "world") return "world";
            return sendCh == "guild" && guild != null ? "guild" : "world";
        }

        void Clear(Transform t) { for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject); }

        Button Small(Transform parent, string label, int size, float x, float y, float w, float h, bool on, Action click, bool red = false)
        {
            var b = Ui.Btn("b_" + label, parent, label, size, Color.clear, () => click());
            Color top = red ? WebUi.Red : on ? WebUi.C("#8a5a14") : WebUi.C("#3b2a5c"), bot = red ? WebUi.C("#5a1418") : on ? WebUi.C("#5a3a0c") : WebUi.C("#1d1433");
            WebUi.Dress(b.image, top, bot, on ? WebUi.Amber : WebUi.Gold, 8); b.GetComponentInChildren<Text>().color = on ? WebUi.Light : WebUi.Text;
            WebUi.At((RectTransform)b.transform, x, y, w, h); return b;
        }

        void Redraw()
        {
            // 页签
            Clear(tabsRow);
            for (int i = 0; i < Tabs.Length; i++)
            {
                string t = Tabs[i][0]; var b = Small(tabsRow, Tabs[i][1], 18, i * 111, 0, 104, 44, t == tab, () => { tab = t; if (t == "pm" && pmPeer == null && peers.Count > 0) pmPeer = peers[0]; ClearUnread(); stick = true; Redraw(); });
                int n; if ((unread.TryGetValue(t, out n) && n > 0)) { var dot = Ui.Box("dot", b.transform, WebUi.C("#ff3b30")); dot.sprite = WebUi.Circle(); dot.raycastTarget = false; WebUi.At(dot.rectTransform, 88, 4, 12, 12); }
            }
            // 条：帮派 / 私聊对象
            Clear(stripRow);
            if (tab == "guild")
            {
                if (guild != null) { Lbl(stripRow, "「" + guild + "」　成员 " + members + " 人", 18, 0, 0, 480, 76, WebUi.Light); Small(stripRow, "退出帮派", 18, 500, 14, 140, 48, false, () => GuildOp(false)); }
                else { Lbl(stripRow, "你还没有加入帮派。帮派频道只有帮派成员能看到。（演示：直接加入一个现成的帮派）", 15, 0, 0, 490, 76, WebUi.Muted); Small(stripRow, "加入帮派", 18, 500, 14, 140, 48, false, () => GuildOp(true), true); }
            }
            else if (tab == "party") Lbl(stripRow, "你还没有队伍。组队需要多人在线服务，暂未开放。", 16, 0, 0, 648, 76, WebUi.Muted);
            else if (tab == "pm")
            {
                if (peers.Count == 0) Lbl(stripRow, "点聊天里的任意一条别人的发言，就能私聊发言的人。", 16, 0, 0, 648, 76, WebUi.Muted);
                for (int i = 0; i < peers.Count && i < 5; i++)
                {
                    string p = peers[i]; int n; unread.TryGetValue("pm:" + p, out n);
                    Small(stripRow, p + (n > 0 ? " •" : ""), 16, i * 130, 14, 124, 48, p == pmPeer, () => { pmPeer = p; unread.Remove("pm:" + p); stick = true; Redraw(); });
                }
            }
            // 输入区
            string ch = CurSend(); bool ro = ch == null;
            input.interactable = !ro; sendBtn.interactable = !ro;
            ((Text)input.placeholder).text = tab == "system" ? "系统频道只能看，不能发言" : ro ? (tab == "pm" ? "先选一个私聊对象" : tab == "guild" ? "加入帮派后才能说话" : "组队后才能说话") : "说点什么（最多 40 个字）";
            chPill.text = ch == null ? "—" : ch == "pm" ? "私聊" : Ch[ch][0];
            // 列表
            Clear(listContent);
            var ms = hist.Where(m => InTab(m, tab, pmPeer)).ToList();
            if (ms.Count == 0) { var e = Ui.Label("empty", listContent, "还没有消息。", 18, WebUi.Muted, TextAnchor.MiddleCenter); var le = e.gameObject.AddComponent<LayoutElement>(); le.minHeight = 80; }
            foreach (var m in ms)
            {
                var t = Ui.Label("m", listContent, Line(m) + "  <size=12><color=#6f6580>" + DateTimeOffset.FromUnixTimeMilliseconds(m.t).ToLocalTime().ToString("HH:mm") + "</color></size>", 18, WebUi.Text, TextAnchor.UpperLeft);
                t.supportRichText = true; t.lineSpacing = 1.1f;
                if (!m.self && !string.IsNullOrEmpty(m.from) && m.ch != "pm" || (m.ch == "pm" && !m.self))
                {
                    string who = m.ch == "pm" ? m.peer : m.from; t.raycastTarget = true; var b = t.gameObject.AddComponent<Button>(); b.targetGraphic = t; b.transition = Selectable.Transition.None;
                    b.onClick.AddListener(() => { if (string.IsNullOrEmpty(who)) return; pmPeer = who; if (!peers.Contains(who)) peers.Add(who); tab = "pm"; unread.Remove("pm:" + who); stick = true; Redraw(); });
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (stick) { Canvas.ForceUpdateCanvases(); scroll.verticalNormalizedPosition = 0f; stick = false; }
        }

        Text Lbl(Transform parent, string s, int size, float x, float y, float w, float h, Color c)
        {
            var t = Ui.Label("t", parent, s, size, c, TextAnchor.MiddleLeft); t.supportRichText = true; WebUi.At(t.rectTransform, x, y, w, h); return t;
        }

        async void GuildOp(bool join)
        {
            if (busy) return; busy = true;
            try { var r = await net().ChatGuild(join); if (!r.Ok) { toast(r.msg); return; } guild = r.data.guild; members = r.data.members; if (!string.IsNullOrEmpty(r.data.msg)) toast(r.data.msg); }
            finally { busy = false; }
            if (Visible) Redraw();
        }

        async void Send()
        {
            string ch = CurSend(); if (ch == null || busy) return;
            string text = input.text.Trim(); if (text.Length == 0) { hint.text = "说点什么吧"; return; }
            busy = true; hint.text = "";
            try
            {
                var r = await net().ChatSend(new ChatSendReq { ch = ch, text = text, peer = ch == "pm" ? pmPeer : null });
                if (!r.Ok) { hint.text = r.msg; return; }
                if (!r.data.ok) { hint.text = r.data.msg; return; }
                input.text = ""; Add(r.data.sent);
            }
            finally { busy = false; }
        }
    }
}
