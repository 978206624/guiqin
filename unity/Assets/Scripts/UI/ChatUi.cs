using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuiQin.Chat;
using GuiQin.Net;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GuiQin
{
    // 使用网页版底部聊天面板的坐标。客户端只保存当前会话的显示状态。
    public partial class ChatUi
    {
        static readonly string[][] Tabs = { new[] { "all", "综合" }, new[] { "world", "世界" }, new[] { "guild", "帮派" },
            new[] { "party", "队伍" }, new[] { "pm", "私聊" }, new[] { "system", "系统" } };
        static readonly Dictionary<string, (string label, string color, string tab)> Channels = new()
        {
            { "world", ("世界", "#7ad7ff", "world") }, { "guild", ("帮派", "#7fe07f", "guild") },
            { "party", ("队伍", "#ffb36b", "party") }, { "pm", ("私聊", "#ff8ad4", "pm") },
            { "system", ("系统", "#ff7a5a", "system") }, { "notice", ("公告", "#ffb020", "system") },
        };

        readonly GameObject root, noticeBar;
        readonly RectTransform stage, sheet, tabsRow, stripRow, listView, listContent;
        readonly ScrollRect scroll;
        readonly Text noticeText, hint, chLabel;
        readonly Button channelButton, sendButton, emojiButton, itemButton;
        readonly InputField input;
        readonly Func<IGameBackend> net;
        readonly Func<RoleState> getRole;
        readonly Action<IReadOnlyList<ChatMsg>, bool> mini;
        readonly SortedDictionary<int, ChatMsg> history = new();
        readonly HashSet<int> unread = new();
        readonly Dictionary<int, RectTransform> rendered = new();
        readonly Queue<string> notices = new();
        readonly List<string> peers = new();
        List<ChatPerson> friends = new();
        PartyState party = new PartyState();
        RectTransform emptyRow;
        string roleId, tab = "all", sendCh = "world", pmPeer, guild;
        int members, generation, lastRetired, socialVersion, historyRequest;
        bool busy, loaded, friendsView;
        const int NoticeTrackWidth = 515;
        float noticeX, noticeWidth, noticeGap;

        public bool Visible => root.activeSelf;
        public PartyState CurrentParty => party;

        public ChatUi(RectTransform parent, Func<IGameBackend> net, Func<RoleState> role,
            Action<IReadOnlyList<ChatMsg>, bool> mini)
        {
            this.net = net; getRole = role; this.mini = mini;
            stage = parent;
            var dim = Ui.Box("chatUi", parent, WebUi.C("#08041273")); root = dim.gameObject;
            Ui.Anchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var closeDim = dim.gameObject.AddComponent<Button>(); closeDim.targetGraphic = dim;
            closeDim.transition = Selectable.Transition.None; closeDim.onClick.AddListener(Close);
            sheet = WebUi.Panel("sheet", dim.transform).rectTransform;
            WebUi.At(sheet, 0, 470, 720, 810);
            tabsRow = Ui.Rect("tabs", sheet); WebUi.At(tabsRow, 16, 12, 584, 44);
            var divider = Ui.Box("headerLine", sheet, WebUi.C("#e2b04a59"));
            divider.raycastTarget = false; WebUi.At(divider.rectTransform, 0, 64, 720, 1);
            var close = WebUi.Close(sheet, Close);
            WebUi.Dress(close.image, WebUi.C("#2b1b42"), WebUi.C("#241a3c"), WebUi.Gold, 20);
            WebUi.At((RectTransform)close.transform, 660, 12, 44, 44);
            stripRow = Ui.Rect("strip", sheet);
            scroll = MakeVerticalScroll("messages", sheet, out listView, out listContent);
            var layout = listContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childControlWidth = true; layout.childControlHeight = true; layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 6, 6);
            listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            hint = Label("hint", sheet, "", 15, WebUi.C("#ff9a8a"), 18, 716, 684, 25);
            channelButton = Small("channel", sheet, "世界", 17, 12, 750, 60, 36, CycleChannel);
            chLabel = channelButton.GetComponentInChildren<Text>();
            input = Ui.Input("input", sheet, "说点什么", 20, false, (int)GameData.Raw["chat"]["maxLen"]);
            WebUi.At((RectTransform)input.transform, 180, 746, 454, 44);
            WebUi.Dress(input.GetComponent<Image>(), WebUi.C("#ffffff19"), WebUi.C("#ffffff19"), WebUi.C("#e2b04a59"), 20);
            emojiButton = Small("emoji", sheet, "", 17, 80, 747, 42, 42, () => TogglePicker("emoji"));
            var smile = Ui.Box("pickerIcon", emojiButton.transform, Color.white);
            smile.sprite = ChatEmotes.Get("笑"); smile.raycastTarget = false;
            WebUi.At(smile.rectTransform, 9, 10, 24, 24);
            itemButton = Small("item", sheet, "物品", 13, 130, 747, 42, 42, () => TogglePicker("item"));
            WebUi.Dress(emojiButton.image, WebUi.C("#322744"), WebUi.C("#201830"), WebUi.C("#e2b04a66"), 20);
            WebUi.Dress(itemButton.image, WebUi.C("#322744"), WebUi.C("#201830"), WebUi.C("#e2b04a66"), 20);
            sendButton = Small("send", sheet, "发送", 19, 642, 746, 66, 44, Send, WebUi.Purple);
            input.onEndEdit.AddListener(_ =>
            {
                // 失焦只收起光标，回车才发送。移动端保留显式发送按钮。
                if (ReturnPressed()) Send();
            });

            var bar = WebUi.Panel("notice", parent); noticeBar = bar.gameObject;
            bar.raycastTarget = false;
            bar.transform.SetSiblingIndex(root.transform.GetSiblingIndex());
            WebUi.Dress(bar, WebUi.C("#782814db"), WebUi.C("#1e1028d1"), WebUi.C("#ffb020b3"), 18);
            WebUi.At(bar.rectTransform, 20, 318, 600, 38);
            var tag = WebUi.Gradient("tag", bar.transform, WebUi.Amber, WebUi.C("#ffb020"), 10);
            WebUi.At(tag.rectTransform, 12, 8, 48, 22);
            Label("label", tag.transform, "公告", 15, WebUi.C("#2a1405"), 0, 0, 48, 22, TextAnchor.MiddleCenter);
            var clip = Ui.Rect("track", bar.transform);
            WebUi.At(clip, 70, 0, NoticeTrackWidth, 38); clip.gameObject.AddComponent<RectMask2D>();
            noticeText = Label("text", clip, "", 18, WebUi.Light, 0, 0, NoticeTrackWidth, 38);
            noticeText.horizontalOverflow = HorizontalWrapMode.Overflow;
            BuildInteractions();
            noticeBar.SetActive(false); root.SetActive(false);
        }

        static bool ReturnPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        public void BindRole(string id)
        {
            generation++;
            roleId = id; lastRetired = 0; loaded = false; busy = false;
            history.Clear(); unread.Clear(); peers.Clear(); notices.Clear();
            friends.Clear(); party = new PartyState(); guild = null; members = 0;
            noticeText.text = ""; noticeGap = 0; noticeBar.SetActive(false);
            pmPeer = null; tab = "all"; sendCh = "world"; input.text = "";
            Close(); UpdateMini(); ClearMessages();
        }

        public async Task LoadHistory()
        {
            var backend = net();
            int scope = generation, social = socialVersion, request = ++historyRequest;
            var response = await backend.ChatHistory();
            if (!IsCurrent(scope, backend) || request != historyRequest || !response.Ok || response.data == null) return;
            var data = response.data;
            if (social == socialVersion)
            {
                guild = data.guild; members = data.members;
                friends = data.friends ?? new List<ChatPerson>();
            }
            ApplyParty(data.party, false);
            bool wasLoaded = loaded;
            foreach (var m in (data.msgs ?? new List<ChatMsg>()).Where(m => m != null).OrderBy(m => m.id))
                Remember(m, wasLoaded, wasLoaded);
            loaded = true;
            if (!wasLoaded && notices.Count == 0)
            {
                var latest = history.Values.LastOrDefault(m => m.ch == "notice");
                if (latest != null) QueueNotice(latest.text);
            }
            UpdateMini();
            if (Visible) Redraw(true);
        }

        bool IsCurrent(int scope, IGameBackend backend) => roleId != null && scope == generation && net() == backend;

        bool Remember(ChatMsg m, bool markUnread, bool announce)
        {
            if (roleId == null || m == null || m.ch == null || !Channels.ContainsKey(m.ch) || m.id <= lastRetired
                || (!string.IsNullOrEmpty(m.roleId) && m.roleId != roleId) || history.ContainsKey(m.id)) return false;
            history.Add(m.id, m);
            if (m.ch == "pm" && !string.IsNullOrEmpty(m.peer) && !peers.Contains(m.peer)) peers.Add(m.peer);
            if (markUnread && !m.self && !(Visible && !friendsView && Matches(m, tab)))
                unread.Add(m.id);
            if (announce && m.ch == "notice") QueueNotice(m.text);
            while (history.Count > 200)
            {
                var oldest = history.Keys.First();
                history.Remove(oldest); unread.Remove(oldest); lastRetired = oldest;
            }
            return true;
        }

        public void Add(ChatMsg m)
        {
            if (!Remember(m, true, true)) return;
            UpdateMini();
            if (Visible) Redraw(m.self || scroll.verticalNormalizedPosition < 0.10f);
        }

        public void ApplyParty(PartyState state) => ApplyParty(state, true);
        void ApplyParty(PartyState state, bool redraw)
        {
            if (roleId == null || state == null || state.revision < party.revision
                || (!string.IsNullOrEmpty(state.roleId) && state.roleId != roleId)) return;
            state.members ??= new List<ChatPerson>();
            state.pending ??= new List<string>();
            bool joined = party.members.Count == 0 && state.members.Count > 0;
            party = state;
            if (joined) sendCh = "party";
            if (sendCh == "party" && party.members.Count == 0) sendCh = "world";
            if (redraw && Visible) Redraw(false);
        }

        void QueueNotice(string value)
        {
            if (!string.IsNullOrEmpty(value) && notices.Count < 4) notices.Enqueue(value);
        }

        public void Tick(float dt, bool worldVisible)
        {
            bool canShow = worldVisible && roleId != null;
            if (canShow)
            {
                noticeGap -= dt;
                if (noticeText.text.Length == 0 && notices.Count > 0 && noticeGap <= 0)
                {
                    noticeText.text = notices.Dequeue();
                    noticeWidth = Mathf.Max(1, noticeText.preferredWidth + 10);
                    noticeX = NoticeTrackWidth;
                }
                if (noticeText.text.Length > 0)
                {
                    WebUi.At(noticeText.rectTransform, noticeX, 0, noticeWidth, 38);
                    noticeX -= 70 * dt;
                    if (noticeX < -noticeWidth) { noticeText.text = ""; noticeGap = 0.6f; }
                }
            }
            bool active = canShow && noticeText.text.Length > 0;
            if (noticeBar.activeSelf != active) noticeBar.SetActive(active);
        }

        bool Matches(ChatMsg m, string category) => category == "all" ||
            (category == "pm" ? m.ch == "pm" && m.peer == pmPeer : Channels[m.ch].tab == category);

        IEnumerable<ChatMsg> UnreadMessages => history.Values.Where(m => unread.Contains(m.id));
        int PeerUnread(string name) => UnreadMessages.Count(m => m.ch == "pm" && m.peer == name);
        bool TabUnread(string key) => key == "all" ? unread.Count > 0 : UnreadMessages.Any(m => Channels[m.ch].tab == key);

        void MarkRead()
        {
            if (friendsView) return;
            foreach (var m in history.Values)
                if (Matches(m, tab)) unread.Remove(m.id);
            UpdateMini();
        }

        internal static void DrawMini(RectTransform parent, IReadOnlyList<ChatMsg> messages)
        {
            Clear(parent);
            for (int i = 0; i < messages.Count; i++)
            {
                var m = messages[i];
                var style = Channels[m.ch];
                ChatLineUi.DrawMini(parent, m, style.label, WebUi.C(style.color), i);
            }
        }

        void UpdateMini()
        {
            mini(history.Values.Skip(Math.Max(0, history.Count - 3)).ToList(), unread.Count > 0);
        }

        public void Open(string key)
        {
            if (roleId == null) return;
            friendsView = key == "friends";
            tab = friendsView ? "pm" : key ?? "all";
            if (tab == "pm" && pmPeer == null) pmPeer = PeerNames().FirstOrDefault();
            root.SetActive(true); hint.text = ""; HideOverlays();
            MarkRead(); Redraw(true);
            if (friendsView) RefreshFriends();
        }

        public void Close()
        {
            if (picker != null) HideOverlays();
            input.DeactivateInputField();
            root.SetActive(false);
        }

        string CurSend()
        {
            if (friendsView || tab == "system") return null;
            if (tab == "pm") return pmPeer == null ? null : tab;
            if (tab == "guild") return guild == null ? null : tab;
            if (tab == "party") return party.members.Count > 0 ? tab : null;
            if (tab == "world") return tab;
            if (sendCh == "guild" && guild == null || sendCh == "party" && party.members.Count == 0) return "world";
            return sendCh;
        }

        void CycleChannel()
        {
            if (tab != "all") return;
            var allowed = new List<string> { "world" };
            if (guild != null) allowed.Add("guild");
            if (party.members.Count > 0) allowed.Add("party");
            sendCh = allowed[(allowed.IndexOf(CurSend()) + 1) % allowed.Count];
            DrawInput();
        }

        void DrawInput()
        {
            string ch = CurSend();
            bool writable = ch != null;
            input.interactable = sendButton.interactable = emojiButton.interactable = itemButton.interactable = writable;
            channelButton.interactable = writable && tab == "all";
            chLabel.text = ch == null ? "—" : Channels[ch].label;
            chLabel.color = ch == null ? WebUi.Muted : WebUi.C(Channels[ch].color);
            WebUi.Dress(channelButton.image, WebUi.C("#0d0a18"), WebUi.C("#0d0a18"), chLabel.color, 17);
            ((Text)input.placeholder).text = tab == "system" ? "系统频道只能看，不能发言" :
                friendsView ? "选择好友开始私聊" : writable ? "说点什么（最多 " + input.characterLimit + " 个字）" :
                tab == "pm" ? "先选一个私聊对象" : tab == "guild" ? "加入帮派后才能说话" : "组队后才能说话";
        }

        void Redraw(bool bottom)
        {
            if (!Visible) return;
            Clear(tabsRow);
            for (int i = 0; i < Tabs.Length; i++)
            {
                string key = Tabs[i][0];
                bool selected = !friendsView && tab == key;
                var button = Small("tab_" + key, tabsRow, Tabs[i][1], 18, i * 72, 3, 64, 37, () => Open(key));
                WebUi.Dress(button.image, selected ? WebUi.C("#3b2a5c") : WebUi.C("#ffffff0c"),
                    selected ? WebUi.C("#1d1433") : WebUi.C("#ffffff0c"),
                    selected ? WebUi.Gold : WebUi.C("#e2b04a59"), 18);
                button.GetComponentInChildren<Text>().color = selected ? WebUi.Light : WebUi.Muted;
                if (TabUnread(key)) Dot(button.transform, 54, 0, 10);
            }
            int stripHeight = DrawStrip();
            if (stripHeight > 0)
            {
                var border = Ui.Box("stripLine", stripRow, WebUi.C("#e2b04a42"));
                border.raycastTarget = false; WebUi.At(border.rectTransform, 0, stripHeight - 1, 720, 1);
            }
            WebUi.At(stripRow, 0, 64, 720, stripHeight);
            WebUi.At(listView, 0, 64 + stripHeight, 720, 650 - stripHeight);
            DrawInput();
            DrawList(bottom);
        }

        void DrawList(bool bottom)
        {
            if (friendsView) { ClearMessages(); DrawFriendRows(); bottom = false; }
            else
            {
                for (int i = listContent.childCount - 1; i >= 0; i--)
                {
                    var child = listContent.GetChild(i);
                    if (child.name != "friendRows" || !child.gameObject.activeSelf) continue;
                    child.gameObject.SetActive(false); Object.Destroy(child.gameObject);
                }
                var selected = history.Values.Where(m => Matches(m, tab)).ToList();
                var ids = new HashSet<int>(selected.Select(m => m.id));
                foreach (var id in rendered.Keys.Where(id => !ids.Contains(id)).ToArray())
                {
                    var old = rendered[id].gameObject; old.SetActive(false); Object.Destroy(old); rendered.Remove(id);
                }
                if (emptyRow != null) { emptyRow.gameObject.SetActive(false); Object.Destroy(emptyRow.gameObject); emptyRow = null; }
                if (selected.Count == 0)
                {
                    emptyRow = Label("empty", listContent, "还没有消息", 18, WebUi.Muted, 0, 0, 720, 95, TextAnchor.MiddleCenter).rectTransform;
                    emptyRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 95;
                }
                int index = 0;
                foreach (var m in selected)
                {
                    if (!rendered.TryGetValue(m.id, out var row))
                    {
                        var channel = Channels[m.ch];
                        row = ChatLineUi.Draw(listContent, m, channel.label, WebUi.C(channel.color), ShowNameMenu, ShowItem);
                        rendered.Add(m.id, row);
                    }
                    row.SetSiblingIndex(index++);
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(listContent);
            if (bottom || friendsView)
            {
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = friendsView ? 1f : 0f;
            }
        }

        void ClearMessages() { Clear(listContent); rendered.Clear(); emptyRow = null; }

        async void Send()
        {
            string ch = CurSend();
            if (busy || ch == null) return;
            string text = input.text.Trim();
            if (text.Length == 0) { hint.text = "说点什么吧"; return; }
            await SendText(ch, text, true);
        }

        async Task SendText(string ch, string text, bool clear)
        {
            if (busy || roleId == null) return;
            int scope = generation;
            var backend = net();
            busy = true; hint.text = "";
            try
            {
                var result = await backend.ChatSend(new ChatSendReq { ch = ch, text = text, peer = ch == "pm" ? pmPeer : null });
                if (!IsCurrent(scope, backend)) return;
                if (!result.Ok || result.data == null || !result.data.ok)
                {
                    hint.text = result.Ok ? result.data?.msg ?? "发送失败" : result.msg;
                    return;
                }
                if (clear && input.text.Trim() == text) input.text = "";
                Add(result.data.sent);
            }
            finally { if (IsCurrent(scope, backend)) busy = false; }
        }

        static void Clear(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var go = parent.GetChild(i).gameObject;
                go.SetActive(false); Object.Destroy(go);
            }
        }

        static Text Label(string id, Transform parent, string value, int size, Color color, float x, float y, float w, float h,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(id, parent, value, size, color, alignment);
            label.supportRichText = false;
            WebUi.At(label.rectTransform, x, y, w, h);
            return label;
        }

        static Button Small(string id, Transform parent, string label, int size, float x, float y, float w, float h, Action click, Color? tone = null)
        {
            var button = Ui.Btn(id, parent, label, size, tone ?? WebUi.Purple, () => click());
            button.GetComponentInChildren<Text>().supportRichText = false;
            WebUi.At((RectTransform)button.transform, x, y, w, h);
            return button;
        }

        static void Dot(Transform parent, float x, float y, float size)
        {
            var border = Ui.Box("unread", parent, Color.white);
            border.sprite = WebUi.Circle(); border.raycastTarget = false;
            WebUi.At(border.rectTransform, x, y, size, size);
            var dot = Ui.Box("red", border.transform, WebUi.C("#e8362e"));
            dot.sprite = WebUi.Circle(); dot.raycastTarget = false;
            WebUi.At(dot.rectTransform, 1.5f, 1.5f, size - 3, size - 3);
        }

        static ScrollRect MakeVerticalScroll(string id, Transform parent, out RectTransform view, out RectTransform content)
        {
            var area = Ui.Box(id, parent, Color.clear);
            view = area.rectTransform; area.gameObject.AddComponent<RectMask2D>();
            content = Ui.Rect("content", view);
            content.pivot = Vector2.up;
            Ui.Anchor(content, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            var sc = area.gameObject.AddComponent<ScrollRect>();
            sc.content = content; sc.viewport = view; sc.horizontal = false;
            sc.movementType = ScrollRect.MovementType.Clamped;
            return sc;
        }
    }
}
