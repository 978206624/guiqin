using System.Text.RegularExpressions;
using GuiQin.Chat;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Server;

internal sealed partial class GameGateway
{
    static JToken ChatConfig => GameData.Raw["chat"];
    readonly Dictionary<(string Role, string Channel), (long Time, string Text, string Peer)> lastChat = new();
    readonly HashSet<string> onlineGuild = new();

    object Social(LoginSession session, SavedRole saved, Envelope request)
    {
        var role = saved.State;
        if (role.guildMember) onlineGuild.Add(role.id);
        else onlineGuild.Remove(role.id);
        switch (request.cmd)
        {
            case ChatCmd.Send:
                return SendChat(role, Read<ChatSendReq>(request));
            case ChatCmd.Hist:
                return new ChatHistResp
                {
                    msgs = store.ChatHistory(Zone, role.id, role.guildMember, parties.GetValueOrDefault(role.id)?.Id), friends = store.Friends(role.id),
                    party = PartyView(role.id),
                    guild = role.guildMember ? (string)ChatConfig["guildName"] : null,
                    members = role.guildMember ? store.GuildMembers(Zone) : 0
                };
            case ChatCmd.Guild:
                role.guildMember = Read<ChatGuildReq>(request).join;
                Save(session, saved);
                if (role.guildMember) onlineGuild.Add(role.id); else onlineGuild.Remove(role.id);
                SystemChat(role.id, role.guildMember ? "加入了帮派" : "退出了帮派");
                return new ChatResp { ok = true, guild = role.guildMember ? (string)ChatConfig["guildName"] : null,
                    members = role.guildMember ? store.GuildMembers(Zone) : 0 };
            case Cmd.PartyInvite:
            case Cmd.PartyReply:
            case Cmd.PartyLeave:
                return ChangeParty(role, request);
            default:
                return Friends(role.id, request);
        }
    }

    FriendResp Friends(string id, Envelope request)
    {
        var list = store.Friends(id);
        if (request.cmd == Cmd.FriendList) return new() { ok = true, friends = list };
        string name = Read<ChatPeerReq>(request).peer?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 24) return new() { msg = "找不到这个玩家", friends = list };
        var peer = store.FindPerson(Zone, name);
        if (peer.Id == null || peer.Id == id) return new() { msg = "找不到这个玩家", friends = list };
        bool add = request.cmd == Cmd.FriendAdd;
        if (add && !list.Any(p => p.name == name) && list.Count >= (int)ChatConfig["friendLimit"])
            return new() { msg = "好友已满", friends = list };
        store.SetFriend(id, peer.Id, add);
        return new() { ok = true, msg = add ? "已添加好友" : "已删除好友", friends = store.Friends(id) };
    }

    void SystemChat(string roleId, string text, bool notice = false)
    {
        if (string.IsNullOrEmpty(text)) return;
        var message = new ChatMsg { ch = notice ? "notice" : "system", from = "系统", text = text, t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        try { store.SaveChat(Zone, null, notice ? null : roleId, message, ""); }
        catch (MySqlConnector.MySqlException error)
        {
            Console.Error.WriteLine($"系统消息保存失败：{error.Number}");
            return;
        }
        foreach (var target in sessions.Values.Where(s => s.Role != null && (notice || s.Role == roleId)))
        {
            message.roleId = target.Role;
            PushTo(target, ChatCmd.Push, message);
        }
    }

    ChatResp SendChat(RoleState role, ChatSendReq request)
    {
        string channel = request.ch, text = request.text?.Trim();
        int limit = (int)ChatConfig["maxLen"];
        if (string.IsNullOrWhiteSpace(text) || text.Length > limit) return new() { msg = "消息需为 1 到 " + limit + " 个字" };
        if (channel is not ("world" or "guild" or "pm" or "party")) return new() { msg = "这个频道暂时不能发言" };
        if (channel == "guild" && !role.guildMember) return new() { msg = "你还没有加入帮派" };
        var party = parties.GetValueOrDefault(role.id);
        if (channel == "party" && party == null) return new() { msg = "你还没有队伍" };
        string recipient = null, peer = null;
        if (channel == "pm")
        {
            peer = request.peer?.Trim();
            if (string.IsNullOrEmpty(peer) || peer.Length > 24) return new() { msg = "找不到这个玩家" };
            recipient = store.FindPerson(Zone, peer).Id;
            if (recipient == null || recipient == role.id) return new() { msg = "找不到这个玩家" };
        }
        foreach (string term in ChatConfig["ban"]?.Values<string>() ?? Enumerable.Empty<string>())
            if (!string.IsNullOrEmpty(term))
                text = Regex.Replace(text, Regex.Escape(term), new string('*', term.Length), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long cooldown = (long)((channel == "world" ? (double)ChatConfig["cooldown"] : channel == "pm" ? 1 : 1.5) * 1000);
        var key = (role.id, channel);
        if (lastChat.TryGetValue(key, out var previous))
        {
            if (now - previous.Time < cooldown) return new() { msg = "说话太快了" };
            if (now - previous.Time < 20000 && previous.Text == text && previous.Peer == peer) return new() { msg = "别刷屏" };
        }
        var message = new ChatMsg { ch = channel, from = role.name, cls = role.cls, lv = role.lv, text = text, peer = peer, t = now };
        var items = GameData.Raw["items"];
        foreach (Match match in Regex.Matches(text, @"\[([^\[\]]{1,8})\]"))
        {
            var item = role.bag.items.FirstOrDefault(p => p.Value > 0 && (string)items[p.Key]?["name"] == match.Groups[1].Value);
            if (item.Key != null) message.links.Add(new() { start = match.Index, length = match.Length, itemId = item.Key });
        }
        store.SaveChat(Zone, role.id, recipient, message, channel == "guild" ? "zone-guild" : channel == "party" ? party.Id : "");
        lastChat[key] = (now, text, peer);
        message.self = false;
        if (channel == "pm") message.peer = role.name;
        foreach (var target in sessions.Values.Where(s => s.Role != null && s.Role != role.id && s.Connection?.Token != null))
        {
            if (channel == "pm" && target.Role != recipient || channel == "guild" && !onlineGuild.Contains(target.Role) ||
                channel == "party" && !party.Members.ContainsKey(target.Role)) continue;
            message.roleId = target.Role;
            PushTo(target, ChatCmd.Push, message);
        }
        message.roleId = role.id; message.self = true; message.peer = peer;
        return new() { ok = true, sent = message };
    }
}
