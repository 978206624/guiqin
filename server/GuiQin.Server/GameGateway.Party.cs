using GuiQin.Chat;
using GuiQin.Net;

namespace GuiQin.Server;

internal sealed partial class GameGateway
{
    sealed class Party
    {
        public readonly string Id = Guid.NewGuid().ToString("N");
        public readonly Dictionary<string, ChatPerson> Members = new();
    }

    sealed record Invitation(string From, string FromName, string ToName, DateTimeOffset Until);
    readonly Dictionary<string, Party> parties = new();
    readonly Dictionary<string, Invitation> invites = new();
    int partyRevision;

    void PushTo(LoginSession target, string command, object payload)
    {
        var connection = target.Connection;
        if (connection?.Token == null || !sessions.TryGetValue(connection.Token, out var current) || current != target) return;
        connection.Pushes.Writer.TryWrite(new Envelope { cmd = command, data = Data(payload), ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
    }

    PartyState PartyView(string id)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new PartyState { roleId = id, revision = partyRevision };
        if (parties.TryGetValue(id, out var party))
            result.members.AddRange(party.Members.Where(p => p.Key != id).Select(p => p.Value));
        if (invites.TryGetValue(id, out var incoming) && incoming.Until > now) result.incoming.Add(incoming.FromName);
        result.pending.AddRange(invites.Values.Where(i => i.From == id && i.Until > now).Select(i => i.ToName));
        return result;
    }

    void NotifyParty(IEnumerable<string> roles)
    {
        partyRevision++;
        var ids = roles.ToHashSet();
        foreach (var s in sessions.Values.Where(s => s.Role != null && ids.Contains(s.Role)))
            PushTo(s, Cmd.PushParty, PartyView(s.Role));
    }

    PartyResp ChangeParty(RoleState role, Envelope request)
    {
        var now = DateTimeOffset.UtcNow;
        var expired = invites.Where(p => p.Value.Until <= now).ToArray();
        foreach (var invite in expired) invites.Remove(invite.Key);
        if (expired.Length > 0) NotifyParty(expired.SelectMany(p => new[] { p.Key, p.Value.From }));

        PartyResp Result(bool ok, string msg) => new() { ok = ok, msg = msg, party = PartyView(role.id) };
        int limit = (int)ChatConfig["partyLimit"];
        if (request.cmd == Cmd.PartyLeave)
        {
            LeaveParty(role.id);
            return Result(true, "已离开队伍或取消邀请");
        }
        if (request.cmd == Cmd.PartyInvite)
        {
            string name = Read<ChatPeerReq>(request).peer?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > 24) return Result(false, "找不到这个玩家");
            var peer = store.FindPerson(Zone, name);
            if (peer.Id == null || peer.Id == role.id ||
                !sessions.Values.Any(s => s.Role == peer.Id && s.Connection?.Pushes.Reader.Completion.IsCompleted == false))
                return Result(false, "对方不在线");
            if (parties.ContainsKey(peer.Id)) return Result(false, "对方已在队伍");
            if (invites.ContainsKey(peer.Id)) return Result(false, "对方有待处理的邀请");
            int occupied = parties.GetValueOrDefault(role.id)?.Members.Count ?? 1;
            if (occupied + invites.Values.Count(i => i.From == role.id) >= limit) return Result(false, "队伍已满");
            invites[peer.Id] = new(role.id, role.name, peer.Person.name, now.AddMinutes(1));
            NotifyParty(new[] { role.id, peer.Id });
            return Result(true, "邀请已发出");
        }

        var reply = Read<PartyReplyReq>(request);
        if (!invites.TryGetValue(role.id, out var invitation) || invitation.FromName != reply.peer)
            return Result(false, "邀请已失效");
        invites.Remove(role.id);
        if (!reply.accept || parties.ContainsKey(role.id))
        {
            NotifyParty(new[] { role.id, invitation.From });
            return Result(!reply.accept, reply.accept ? "你已在队伍" : "已拒绝邀请");
        }
        var inviter = store.FindPerson(Zone, invitation.FromName);
        if (inviter.Id != invitation.From) return Result(false, "邀请已失效");
        var party = parties.GetValueOrDefault(invitation.From) ?? new Party();
        if (party.Members.Count >= limit)
        {
            NotifyParty(new[] { role.id, invitation.From });
            return Result(false, "队伍已满");
        }
        party.Members[inviter.Id] = inviter.Person;
        party.Members[role.id] = new ChatPerson { name = role.name, cls = role.cls, lv = role.lv };
        parties[inviter.Id] = parties[role.id] = party;
        NotifyParty(party.Members.Keys);
        return Result(true, "已加入队伍");
    }

    void LeaveParty(string id)
    {
        var affected = new HashSet<string> { id };
        foreach (var item in invites.Where(p => p.Key == id || p.Value.From == id).ToArray())
        {
            affected.Add(item.Key);
            affected.Add(item.Value.From);
            invites.Remove(item.Key);
        }
        if (parties.Remove(id, out var party))
        {
            party.Members.Remove(id);
            affected.UnionWith(party.Members.Keys);
            if (party.Members.Count == 1) parties.Remove(party.Members.Keys.Single());
        }
        NotifyParty(affected);
    }
}
