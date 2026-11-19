using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuiQin.Chat;
using GuiQin.Inventory;
using GuiQin.Net;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    public partial class ChatUi
    {
        RectTransform picker, nameMenu, pickerView, pickerContent;
        ScrollRect pickerScroll;
        GameObject menuDismiss;
        ModalUi detail;
        string pickerKind;

        void BuildInteractions()
        {
            picker = WebUi.Panel("picker", sheet).rectTransform;
            pickerScroll = MakeVerticalScroll("scroll", picker, out pickerView, out pickerContent);
            var blocker = Small("menuDismiss", root.transform, "", 18, 0, 0, 720, 1280, HideNameMenu, Color.clear);
            menuDismiss = blocker.gameObject;
            nameMenu = WebUi.Panel("nameMenu", root.transform).rectTransform;
            detail = new ModalUi(root.transform);
            HideOverlays();
        }

        void HideNameMenu()
        {
            nameMenu.gameObject.SetActive(false);
            menuDismiss.SetActive(false);
        }

        void HidePicker()
        {
            pickerKind = null;
            picker.gameObject.SetActive(false);
        }

        void HideOverlays()
        {
            HideNameMenu();
            HidePicker();
            detail.Close();
        }

        IEnumerable<string> PeerNames() => friends.Where(p => p != null && !string.IsNullOrEmpty(p.name)).Select(p => p.name)
            .Concat(peers).Distinct(StringComparer.Ordinal);

        bool IsFriend(string name) => friends.Any(p => p != null && p.name == name);

        int DrawStrip()
        {
            Clear(stripRow);
            if (friendsView)
            {
                Label("friendsTitle", stripRow, "好友（" + friends.Count + "）", 22, WebUi.Light, 18, 0, 684, 46);
                return 46;
            }
            if (tab == "guild")
            {
                if (guild == null)
                {
                    Label("guildEmpty", stripRow, "你还没有加入帮派", 20, WebUi.Light, 20, 0, 530, 32);
                    Label("guildHint", stripRow, "加入后可在帮派频道发言", 16, WebUi.Muted, 20, 34, 500, 33);
                    Small("joinGuild", stripRow, "加入帮派", 16, 562, 18, 140, 40, () => GuildOp(true), WebUi.Red);
                }
                else
                {
                    Label("guildName", stripRow, "「" + guild + "」　成员 " + members + " 人", 17, WebUi.Light, 18, 0, 522, 52);
                    Small("leaveGuild", stripRow, "退出帮派", 16, 562, 8, 140, 36, () => GuildOp(false));
                }
                return guild == null ? 78 : 54;
            }

            if (tab == "party")
            {
                if (party.incoming?.Count > 0)
                {
                    string peer = party.incoming[0];
                    Label("partyInvite", stripRow, peer + " 邀请你组队", 20, WebUi.Light, 20, 0, 420, 48);
                    Small("acceptParty", stripRow, "接受", 16, 440, 8, 120, 40,
                        () => { _ = PartyOp(backend => backend.PartyReply(peer, true)); }, WebUi.Red);
                    Small("declineParty", stripRow, "拒绝", 16, 580, 8, 120, 40,
                        () => { _ = PartyOp(backend => backend.PartyReply(peer, false)); });
                    return 58;
                }
                bool joined = party.members.Count > 0;
                if (!joined)
                {
                    Label("partyEmpty", stripRow, "你还没有队伍", 20, WebUi.Light, 20, 0, 530, 32);
                    string note = party.pending.Count == 0 ? "点发言人的名字可以邀请组队" : "等待 " + string.Join("、", party.pending) + " 回应";
                    Label("partyHint", stripRow, note, 16, WebUi.Muted, 20, 34, 535, 42);
                    if (party.pending.Count > 0)
                        Small("cancelParty", stripRow, "取消邀请", 16, 562, 18, 140, 40, LeaveParty, WebUi.Red);
                    else
                        Small("partyShout", stripRow, "喊人组队", 16, 562, 18, 140, 40, PartyShout);
                    return 78;
                }
                var names = new[] { getRole()?.name ?? "你" }.Concat(party.members.Select(m => m.name));
                Label("partyNames", stripRow, "队伍 · " + string.Join("、", names), 17, WebUi.Light, 18, 2, 531, 54);
                Small("leaveParty", stripRow, "离开队伍", 16, 562, 8, 140, 40, LeaveParty, WebUi.Red);
                return 60;
            }

            if (tab != "pm") return 0;
            var namesInTab = PeerNames().ToList();
            if (namesInTab.Count == 0)
            {
                Label("noPeers", stripRow, "点发言人的名字开始私聊", 17, WebUi.Muted, 18, 0, 612, 50);
            }
            else
            {
                var viewport = Ui.Box("peers", stripRow, Color.clear);
                viewport.gameObject.AddComponent<RectMask2D>();
                WebUi.At(viewport.rectTransform, 14, 3, 604, 44);
                var content = Ui.Rect("content", viewport.transform);
                content.pivot = Vector2.up;
                WebUi.At(content, 0, 0, namesInTab.Count * 114, 42);
                var scrollPeers = viewport.gameObject.AddComponent<ScrollRect>();
                scrollPeers.content = content; scrollPeers.viewport = viewport.rectTransform;
                scrollPeers.vertical = false; scrollPeers.movementType = ScrollRect.MovementType.Clamped;
                for (int i = 0; i < namesInTab.Count; i++)
                {
                    string name = namesInTab[i];
                    int count = PeerUnread(name);
                    var b = Small("peer_" + name, content, (IsFriend(name) ? "★ " : "") + name, 16,
                        i * 114, 3, 108, 37, () => SelectPeer(name),
                        name == pmPeer ? WebUi.C("#572440") : WebUi.Purple);
                    bool selected = name == pmPeer;
                    WebUi.Dress(b.image, selected ? WebUi.C("#572440") : WebUi.C("#322744"),
                        selected ? WebUi.C("#45203a") : WebUi.C("#291f3b"),
                        selected ? WebUi.C("#ff8ad4") : WebUi.C("#e2b04a66"), 16);
                    if (count > 0)
                    {
                        Dot(b.transform, 94, 1, 10);
                        Label("count", b.transform, count > 99 ? "99+" : count.ToString(), 11, WebUi.Light, 80, 19, 24, 16, TextAnchor.MiddleRight);
                    }
                }
            }
            Small("friends", stripRow, "好友", 16, 626, 5, 78, 38, () => Open("friends"));
            return 53;
        }

        void DrawFriendRows()
        {
            if (friends.Count == 0)
            {
                emptyRow = Label("empty", listContent, "暂无好友，可以点发言人名字添加", 17, WebUi.Muted, 0, 0, 720, 95, TextAnchor.MiddleCenter).rectTransform;
                emptyRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 95;
                return;
            }
            // 好友列表用相同的可滚动区域，数量较多时仍能增删和私聊。
            var rows = Ui.Rect("friendRows", listContent);
            rows.gameObject.AddComponent<LayoutElement>().preferredHeight = friends.Count * 57;
            int index = 0;
            foreach (var friend in friends.Where(f => f != null && !string.IsNullOrEmpty(f.name)))
            {
                string name = friend.name;
                int y = index++ * 57;
                Label("friend_" + name, rows, "★ " + name, 18, WebUi.Light, 20, y, 325, 49);
                Small("pm_" + name, rows, "私聊", 16, 460, y + 5, 108, 39, () => SelectPeer(name));
                Small("remove_" + name, rows, "删除", 16, 578, y + 5, 108, 39, () => ConfirmRemove(name), WebUi.Red);
            }
        }

        void ConfirmRemove(string name)
        {
            detail.Open("删除好友", "确定删除好友「" + Safe(name) + "」？",
                new ModalUi.Btn("删除", () => FriendOp(name, false), true), new ModalUi.Btn("取消", null));
        }

        void SelectPeer(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!peers.Contains(name)) peers.Add(name);
            pmPeer = name;
            Open("pm");
            input.ActivateInputField();
        }

        async void RefreshFriends()
        {
            var backend = net(); int scope = generation, social = socialVersion;
            var result = await backend.FriendList();
            if (!IsCurrent(scope, backend) || social != socialVersion) return;
            if (!result.Ok || result.data == null || !result.data.ok)
            {
                if (Visible && friendsView) hint.text = result.Ok ? result.data?.msg ?? "好友列表加载失败" : result.msg;
                return;
            }
            friends = result.data.friends ?? new List<ChatPerson>();
            if (Visible) Redraw(false);
        }

        async void FriendOp(string peer, bool add)
        {
            if (busy) return;
            var backend = net(); int scope = generation; busy = true;
            try
            {
                var result = await (add ? backend.FriendAdd(peer) : backend.FriendDel(peer));
                if (!IsCurrent(scope, backend)) return;
                if (!result.Ok || result.data == null || !result.data.ok)
                {
                    hint.text = result.Ok ? result.data?.msg ?? "好友操作失败" : result.msg;
                    return;
                }
                socialVersion++;
                friends = result.data.friends ?? new List<ChatPerson>();
                if (Visible) Redraw(false);
            }
            finally { if (IsCurrent(scope, backend)) busy = false; }
        }

        async void GuildOp(bool join)
        {
            if (busy) return;
            var backend = net(); int scope = generation; busy = true;
            try
            {
                var result = await backend.ChatGuild(join);
                if (!IsCurrent(scope, backend)) return;
                if (!result.Ok || result.data == null || !result.data.ok)
                {
                    hint.text = result.Ok ? result.data?.msg ?? "帮派操作失败" : result.msg;
                    return;
                }
                socialVersion++; guild = result.data.guild; members = result.data.members;
                if (Visible) Redraw(true);
            }
            finally { if (IsCurrent(scope, backend)) busy = false; }
        }

        void Invite(string peer) { _ = PartyOp(backend => backend.PartyInvite(peer)); }
        void LeaveParty() { _ = PartyOp(backend => backend.PartyLeave()); }
        async Task PartyOp(Func<IGameBackend, Task<Result<PartyResp>>> action)
        {
            if (busy) return;
            var backend = net(); int scope = generation; busy = true;
            try
            {
                var result = await action(backend);
                if (!IsCurrent(scope, backend)) return;
                if (!result.Ok || result.data == null || !result.data.ok)
                {
                    hint.text = result.Ok ? result.data?.msg ?? "队伍操作失败" : result.msg;
                    return;
                }
                ApplyParty(result.data.party);
            }
            finally { if (IsCurrent(scope, backend)) busy = false; }
        }

        void PartyShout()
        {
            var role = getRole();
            if (role == null || busy) return;
            Open("world");
            _ = SendText("world", "组队！" + role.lv + " 级 " + GuiQin.Sect.SectService.ClsName(role.cls) + "，来个队友[笑]", false);
        }

        void TogglePicker(string kind)
        {
            if (CurSend() == null) return;
            if (pickerKind == kind) { HidePicker(); return; }
            HideNameMenu(); pickerKind = kind;
            Clear(pickerContent);
            if (kind == "emoji")
            {
                for (int i = 0; i < ChatEmotes.Names.Length; i++)
                {
                    string name = ChatEmotes.Names[i];
                    var b = Small("emoji_" + name, pickerContent, "", 16, 8 + i % 6 * 116, 5 + i / 6 * 58, 107, 52, () => Insert(name, false));
                    WebUi.Dress(b.image, WebUi.C("#ffffff10"), WebUi.C("#ffffff0b"), Color.clear, 10);
                    var icon = Ui.Box("icon", b.transform, Color.white);
                    icon.sprite = ChatEmotes.Get(name); icon.raycastTarget = false;
                    WebUi.At(icon.rectTransform, 35, 7, 37, 37);
                }
                SizePicker(126, 126);
            }
            else
            {
                var bag = getRole()?.bag?.items;
                var ids = bag == null ? new List<string>() : bag.Where(item => item.Value > 0 &&
                    !string.IsNullOrEmpty(ChatLineUi.ItemLabel(item.Key))).Select(item => item.Key).ToList();
                if (ids.Count == 0) Label("empty", pickerContent, "背包里没有可链接的物品", 17, WebUi.Muted, 14, 0, 640, 54);
                int x = 8, y = 8;
                foreach (string id in ids)
                {
                    string name = ChatLineUi.ItemLabel(id);
                    int width = Mathf.Clamp((name.Length + 2) * 18 + 20, 76, 300);
                    if (x + width > 688) { x = 8; y += 47; }
                    var b = Small("pick_" + id, pickerContent, "[" + name + "]", 17, x, y, width, 39, () => Insert(name, true));
                    WebUi.Dress(b.image, WebUi.C("#ffffff10"), WebUi.C("#ffffff10"), WebUi.C("#e2b04a66"), 18);
                    b.GetComponentInChildren<Text>().color = ChatLineUi.ItemColor(id);
                    x += width + 8;
                }
                int h = Mathf.Max(54, y + 47);
                SizePicker(h, Mathf.Min(h, 240));
            }
            picker.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            pickerScroll.verticalNormalizedPosition = 1;
        }

        void SizePicker(int contentHeight, int viewHeight)
        {
            WebUi.At(picker, 12, 726 - viewHeight, 696, viewHeight);
            Ui.Anchor(pickerView, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Ui.Anchor(pickerContent, Vector2.up, Vector2.one, new Vector2(0, -contentHeight), Vector2.zero);
        }

        void Insert(string name, bool close)
        {
            string token = "[" + name + "]";
            int a = Math.Min(input.selectionAnchorPosition, input.selectionFocusPosition);
            int b = Math.Max(input.selectionAnchorPosition, input.selectionFocusPosition);
            a = Mathf.Clamp(a, 0, input.text.Length); b = Mathf.Clamp(b, a, input.text.Length);
            if (input.text.Length - (b - a) + token.Length > input.characterLimit)
            {
                hint.text = "最多 " + input.characterLimit + " 个字";
                return;
            }
            input.text = input.text.Substring(0, a) + token + input.text.Substring(b);
            input.ActivateInputField(); input.caretPosition = a + token.Length;
            hint.text = "";
            if (close) HidePicker();
        }

        void ShowNameMenu(ChatPerson person, RectTransform anchor)
        {
            string name = person.name;
            if (string.IsNullOrEmpty(name) || name == getRole()?.name) return;
            HidePicker(); Clear(nameMenu);
            Vector3 point = stage.InverseTransformPoint(anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMin, 0)));
            float x = Mathf.Clamp(point.x - stage.rect.xMin, 10, 520);
            float y = Mathf.Clamp(stage.rect.yMax - point.y + 4, 120, 1055);
            WebUi.At(nameMenu, x, y, 190, 210);
            Label("playerName", nameMenu, name, 19, WebUi.Light, 8, 4, 174, 38, TextAnchor.MiddleCenter);
            void Choose(Action next) { HideNameMenu(); next(); }
            Small("whisper", nameMenu, "私聊", 18, 8, 44, 174, 35, () => Choose(() => SelectPeer(name)));
            bool friend = IsFriend(name);
            Small("addFriend", nameMenu, friend ? "已是好友" : "加好友", 18, 8, 84, 174, 35,
                () => Choose(() => FriendOp(name, true))).interactable = !friend;
            bool inParty = party.members.Any(p => p.name == name), invited = party.pending.Contains(name);
            Small("invite", nameMenu, inParty ? "已在队伍" : invited ? "已邀请" : "邀请组队", 18, 8, 124, 174, 35,
                () => Choose(() => Invite(name))).interactable = !inParty && !invited;
            Small("profile", nameMenu, "查看", 18, 8, 164, 174, 35, () => Choose(() => ShowProfile(person)));
            foreach (var b in nameMenu.GetComponentsInChildren<Button>(true))
                WebUi.Dress(b.image, WebUi.C("#ffffff12"), WebUi.C("#ffffff0d"), Color.clear, 7);
            menuDismiss.SetActive(true); nameMenu.gameObject.SetActive(true);
        }

        static string Safe(string text) => (text ?? "").Replace("<", "＜").Replace(">", "＞");

        void ShowProfile(ChatPerson person)
        {
            var cls = (string)GameData.Raw["classes"][person.cls ?? ""]?["name"];
            string text = (person.lv > 0 ? person.lv + " 级" : "等级未公开") + (cls == null ? "" : " · " + cls);
            detail.OpenWithPortrait(Safe(person.name), "cls_" + person.cls, text, new ModalUi.Btn("关闭", null));
        }

        void ShowItem(string id)
        {
            HideOverlays();
            var it = GameData.Raw["items"][id];
            if (it == null) return;
            var q = GameData.Raw["quality"][((int?)it["q"] ?? 1).ToString()];
            string type = (string)it["type"];
            string text = Safe((string)q?["name"] ?? (type == "potion" ? "药品" : "物品"));
            var keys = new[] { "hp", "mp", "atk", "matk", "def", "mdef", "spd", "crit", "dodge" };
            var names = new[] { "气血", "法力", "物攻", "法攻", "物防", "法防", "速度", "暴击", "闪避" };
            for (int i = 0; i < keys.Length; i++)
                if (it[keys[i]] != null) text += "\n" + names[i] + " +" + Safe(it[keys[i]].ToString()) + (RoleStats.IsPct(keys[i]) ? "%" : "");
            if (it["desc"] != null) text += "\n\n" + Safe((string)it["desc"]);
            detail.OpenWithPortrait(Safe((string)it["name"]), "item_" + (string)it["icon"], text, new ModalUi.Btn("关闭", null));
        }
    }
}
