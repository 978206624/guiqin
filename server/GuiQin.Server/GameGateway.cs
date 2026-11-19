using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Net;
using GuiQin.Quest;
using GuiQin.Tower;
using GuiQin.Train;
using GuiQin.Chat;
using Newtonsoft.Json.Linq;
using MySqlConnector;

namespace GuiQin.Server;

internal sealed class ConnectionState { public string Token; }

internal sealed class GameGateway(GameStore store, string rulesVersion)
{
    const string Zone = "s12";
    readonly object gate = new();
    readonly PasswordHasher<SavedAccount> passwords = new();
    readonly Dictionary<string, LoginSession> sessions = new();
    readonly Dictionary<string, ActiveBattle> battles = new();

    sealed class LoginSession(string account)
    {
        public string Account { get; } = account;
        public string Role;
        public DateTimeOffset Seen = DateTimeOffset.UtcNow;
        public readonly Dictionary<int, Envelope> Replies = new();
        public readonly Queue<int> ReplyOrder = new();
    }

    sealed record ActiveBattle(SavedRole Role, RoleState Initial, BattleSession Session, List<BattleCmd> Commands);
    sealed class GameError(int code, string message) : Exception(message) { public int Code { get; } = code; }

    static T Read<T>(Envelope request) where T : class, new() => request.data?.ToObject<T>() ?? new();
    static JToken Data(object value) => JToken.FromObject(value);
    static bool ValidId(string id) => id is { Length: 32 } && id.All(Uri.IsHexDigit);
    static RoleBrief Brief(SavedRole saved) => new() { id = saved.State.id, name = saved.State.name, cls = saved.State.cls, lv = saved.State.lv, map = saved.State.map };

    public Envelope Handle(ConnectionState connection, Envelope request)
    {
        var reply = new Envelope { seq = request.seq, cmd = request.cmd, ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        lock (gate)
        {
            LoginSession session = null;
            try
            {
                if (request.cmd == Cmd.Ping) return reply;
                if (request.cmd is Cmd.Login or Cmd.Register)
                {
                    reply.data = Data(Login(connection, Read<LoginReq>(request), request.cmd == Cmd.Register));
                    return reply;
                }

                if (request.cmd == Cmd.Resume)
                {
                    var token = Read<ResumeReq>(request).session;
                    RequireSession(token);
                    connection.Token = token;
                    return reply;
                }
                session = RequireSession(connection.Token);
                if (session.Replies.TryGetValue(request.seq, out var previous))
                {
                    if (previous.cmd == request.cmd) return previous;
                    throw new GameError(Code.Protocol, "请求序号已被使用");
                }
                reply.data = Data(Dispatch(session, request));
            }
            catch (GameError error) { reply.code = error.Code; reply.msg = error.Message; }
            catch (Newtonsoft.Json.JsonException) { reply.code = Code.Protocol; reply.msg = "命令参数无效"; }
            catch (MySqlException error)
            {
                Console.Error.WriteLine($"MySQL 命令失败：{request.cmd}，错误 {error.Number}");
                reply.code = Code.Server; reply.msg = "服务器暂时无法保存数据";
            }
            if (session != null && !session.Replies.ContainsKey(request.seq))
            {
                session.Replies.Add(request.seq, reply);
                session.ReplyOrder.Enqueue(request.seq);
                if (session.Replies.Count > 128) session.Replies.Remove(session.ReplyOrder.Dequeue());
            }
        }
        return reply;
    }

    LoginSession RequireSession(string token)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var expired in sessions.Where(p => now - p.Value.Seen > TimeSpan.FromHours(8)).Select(p => p.Key).ToArray())
        {
            var old = sessions[expired];
            sessions.Remove(expired);
            if (old.Role != null && !sessions.Values.Any(s => s.Role == old.Role)) battles.Remove(old.Role);
        }
        if (token == null || !sessions.TryGetValue(token, out var session)) throw new GameError(Code.Unauthorized, "请重新登录");
        session.Seen = now;
        return session;
    }

    LoginResp Login(ConnectionState connection, LoginReq request, bool register)
    {
        string name = request.account?.Trim().ToLowerInvariant();
        if (name == null || name.Length is < 3 or > 24 ||
            !name.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.'))
            throw new GameError(Code.Protocol, "账号需为 3 到 24 位字母、数字、点、下划线或连字符");
        if (request.password == null || request.password.Length is < 8 or > 32)
            throw new GameError(Code.Protocol, "密码需为 8 到 32 个字符");

        SavedAccount saved;
        if (register)
        {
            saved = new SavedAccount(Guid.NewGuid().ToString("N"), name, null, 0);
            if (!store.Register(saved, passwords.HashPassword(saved, request.password)))
                throw new GameError(Code.Protocol, "账号已存在，请直接登录");
        }
        else
        {
            saved = store.Account(name);
            if (saved?.Hash == null ||
                passwords.VerifyHashedPassword(saved, saved.Hash, request.password) == PasswordVerificationResult.Failed)
                throw new GameError(Code.Unauthorized, "账号或密码错误");
            if (saved.Status != 0) throw new GameError(Code.Banned, "该账号已停用");
        }
        string account = saved.Id;
        var roles = store.Roles(account, Zone);
        string token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        sessions[token] = new LoginSession(account);
        connection.Token = token;
        return new LoginResp
        {
            session = token, last = Zone, serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            servers = new() { new ServerInfo { id = Zone, no = 12, name = "函谷关", status = "new", roles = roles.Count } }
        };
    }

    object Dispatch(LoginSession session, Envelope request)
    {
        switch (request.cmd)
        {
            case Cmd.RoleList:
                CheckZone(Read<RoleListReq>(request).server);
                return new RoleListResp { roles = store.Roles(session.Account, Zone).Select(Brief).ToList() };
            case Cmd.RoleCreate:
                return Create(session, Read<RoleCreateReq>(request));
            case Cmd.RoleDelete:
                var id = Read<RoleDeleteReq>(request).roleId;
                if (!ValidId(id) || battles.ContainsKey(id) || !store.Delete(session.Account, id))
                    throw new GameError(Code.Protocol, "无法删除该角色");
                if (session.Role == id) session.Role = null;
                return new Empty();
            case Cmd.EnterGame:
                var entered = OwnRole(session, Read<EnterReq>(request).roleId);
                session.Role = entered.State.id;
                return new EnterResp { role = entered.State };
        }

        var role = OwnRole(session, session.Role);
        battles.TryGetValue(role.State.id, out var battle);
        if (request.cmd == Cmd.BattleResume)
            return battle == null ? throw new GameError(Code.NoBattle, "没有进行中的战斗") : new BattleStartResp { ok = true, battle = battle.Session.Snapshot() };
        if (request.cmd == Cmd.BattleCmd)
            return Act(session, battle, Read<BattleCmdReq>(request));
        if (request.cmd == ChatCmd.Hist) return new ChatHistResp();
        if (battle != null) throw new GameError(Code.InBattle, "战斗中");

        return request.cmd switch
        {
            Cmd.Move => Move(session, role, Read<MoveReq>(request)),
            Cmd.Portal => Portal(session, role, Read<PortalReq>(request)),
            Cmd.Teleport => Escort(session, role, Read<TeleportReq>(request)),
            TowerCmd.Enter or TowerCmd.Next or TowerCmd.Leave => Tower(session, role, request.cmd),
            Cmd.BattleStart => Start(role, Read<BattleStartReq>(request).stage),
            QuestCmd.Talk => Talk(session, role, Read<QuestTalkReq>(request).npc),
            BagCmd.Get or BagCmd.Expand or BagCmd.Rest or BagCmd.Use or BagCmd.Equip or BagCmd.Unequip or BagCmd.MallBuy => Bag(session, role, request),
            _ => throw new GameError(Code.Protocol, "该功能尚未接入真实后端")
        };
    }

    static void CheckZone(string zone)
    {
        if (zone != Zone) throw new GameError(Code.Protocol, "区服不存在");
    }

    RoleCreateResp Create(LoginSession session, RoleCreateReq request)
    {
        CheckZone(request.server);
        var name = request.name?.Trim();
        var cls = request.cls == null ? null : GameData.Raw["classes"][request.cls];
        if (name == null || name.Length < 2 || name.Length > 6 || name.Any(char.IsControl))
            return new() { msg = "名字要 2 到 6 个字" };
        if (cls == null || (bool?)cls["hidden"] == true) return new() { msg = "这个职业还没开放" };
        if (store.Roles(session.Account, Zone).Count >= 3) return new() { msg = "每个区最多创建 3 个角色" };
        var start = GameData.Raw["start"];
        var role = new RoleState
        {
            id = Guid.NewGuid().ToString("N"), name = name, cls = request.cls, map = (string)start["map"],
            lv = (int)start["lv"], coin = (int)start["coin"], bag = InventoryService.NewBag()
        };
        TrainService.EnsureSkl(role);
        return store.Create(session.Account, Zone, role)
            ? new() { ok = true, role = Brief(new SavedRole(Zone, 0, role)) }
            : new() { msg = "这个名字已经用过了" };
    }

    SavedRole OwnRole(LoginSession session, string id)
    {
        if (!ValidId(id)) throw new GameError(Code.Unauthorized, "请先进入角色");
        var role = store.Roles(session.Account, id: id).SingleOrDefault()
            ?? throw new GameError(Code.Unauthorized, "角色不存在");
        InventoryService.EnsureBaseCapacity(role.State.bag);
        return role;
    }

    void Save(LoginSession session, SavedRole role, BattleRecord battle = null, MallPurchase purchase = null,
        (int Capacity, int Yuanbao)? expansion = null)
    {
        if (!store.Save(session.Account, role, battle, purchase, expansion)) throw new GameError(Code.Busy, "角色状态已更新，请重试");
    }

    MoveResp Move(LoginSession session, SavedRole role, MoveReq request)
    {
        var state = role.State;
        var map = GameData.Maps[state.map];
        var at = request.to;
        if (request.map != state.map || at?.Length != 2 || !at.All(float.IsFinite) ||
            at[0] < 0 || at[1] < 0 || at[0] > map.iw / map.k || at[1] > map.ih / map.k)
            return new() { x = state.x, y = state.y };
        state.x = at[0]; state.y = at[1];
        Save(session, role);
        return new() { ok = true, x = state.x, y = state.y };
    }

    MapChangeResp Portal(LoginSession session, SavedRole role, PortalReq request)
    {
        var state = role.State;
        var portal = request.from == state.map ? GameData.Maps[state.map].portals?.Find(p => p.to == request.to) : null;
        if (portal == null) return new() { msg = "当前地图没有这个传送点", map = state.map };
        var locked = new QuestService(state).CheckPortal(portal);
        return locked != null ? new() { msg = locked, map = state.map } : Change(session, role, portal.to, portal.arrive);
    }

    MapChangeResp Escort(LoginSession session, SavedRole role, TeleportReq request)
    {
        if (!GuiQin.Sect.SectService.CanEscort(role.State, request.npc, request.to))
            return new() { map = role.State.map, msg = "请靠近对应的门派接引师" };
        return Change(session, role, request.to, null);
    }

    MapChangeResp Change(LoginSession session, SavedRole role, string map, float[] at)
    {
        role.State.map = map; role.State.x = at?[0] ?? -1; role.State.y = at?[1] ?? -1;
        Save(session, role);
        return new() { ok = true, map = map, x = role.State.x, y = role.State.y };
    }

    MapChangeResp Tower(LoginSession session, SavedRole role, string cmd)
    {
        var tower = new TowerService(role.State);
        string msg = null;
        string map = cmd switch
        {
            TowerCmd.Enter when role.State.map == "town" => tower.Enter(out msg),
            TowerCmd.Next => tower.Next(out msg),
            TowerCmd.Leave when tower.Leave(out msg) => "town",
            _ => null
        };
        return map == null
            ? new() { map = role.State.map, msg = msg ?? "请到汉中郡找守塔人" }
            : Change(session, role, map, cmd == TowerCmd.Leave ? new[] { 850f, 610f } : null);
    }

    QuestTalkResp Talk(LoginSession session, SavedRole role, string npc)
    {
        var map = role.State.map;
        var quest = new QuestService(role.State);
        var beat = quest.Current;
        // 主线采集点由当前任务生成，不一定属于地图的固定人物。
        bool currentSpot = beat is { type: "spot" } && beat.map == map && beat.npc == npc;
        if (npc == null || (!currentSpot && GameData.Maps[map].npcs?.Exists(p => p.id == npc) != true))
            throw new GameError(Code.BadMap, "当前地图没有该人物或采集点");
        var response = quest.Talk(npc);
        if (response.ok) Save(session, role);
        return response;
    }

    BagResp Bag(LoginSession session, SavedRole role, Envelope request)
    {
        var state = role.State;
        var req = Read<BagOpReq>(request);
        var inventory = new InventoryService(state);
        MallPurchase purchase = null;
        (int Capacity, int Yuanbao)? expansion = request.cmd == BagCmd.Expand ? (state.bag.cap, state.yuanbao) : null;
        bool Rest() { state.hp = -1; state.mp = -1; return true; }
        bool ok = request.cmd switch
        {
            BagCmd.Get => true,
            BagCmd.Expand => inventory.Expand(req.capacity),
            BagCmd.MallBuy => inventory.BuyMall(req.id, req.quantity, out purchase),
            BagCmd.Rest => Rest(),
            BagCmd.Use => req.id != null && inventory.Use(req.id),
            BagCmd.Equip => req.id != null && inventory.Equip(req.id),
            BagCmd.Unequip => req.slot != null && inventory.Unequip(req.slot),
            _ => false
        };
        if (ok && request.cmd != BagCmd.Get) Save(session, role, purchase: purchase, expansion: expansion);
        return new() { ok = ok, msg = inventory.Msg, bag = state.bag, coin = state.coin, yuanbao = state.yuanbao, silverNote = state.silverNote, hp = state.hp, mp = state.mp };
    }

    BattleStartResp Start(SavedRole role, string stageId)
    {
        var state = role.State;
        if (stageId == null || !GameData.Stages.TryGetValue(stageId, out var stage)) throw new GameError(Code.NoBattle, "这场战斗不存在");
        var map = GameData.Maps[state.map];
        var quest = new QuestService(state);
        var beat = quest.Current;
        // 剧情敌人由当前任务生成，不一定出现在地图固定敌人列表。
        bool questFoe = beat != null && beat.map == state.map && beat.stage == stageId &&
            beat.type is ("fight" or "kill");
        bool present = stage.tower ? map.tower == (stage.n - 1) / 10 + 1 && state.lv >= TowerService.MinLv
            : questFoe || map.foes?.Exists(f => f.stage == stageId) == true;
        if (!present) throw new GameError(Code.BadMap, "当前地图没有该守卫或敌人");
        var locked = quest.CheckStage(stage);
        if (locked != null) return new() { msg = locked };
        var initial = Data(state).ToObject<RoleState>();
        var battle = BattleFactory.Create(state, stage, RandomNumberGenerator.GetInt32(int.MaxValue));
        battle.View.id = battle.Id = Guid.NewGuid().ToString("N");
        var inventory = new InventoryService(state);
        battle.View.items = inventory.Potions();
        battle.Engine.ItemUse = id => id != null && inventory.UseInBattle(id, out int hp, out int mp) ? new[] { hp, mp } : null;
        battles.Add(state.id, new ActiveBattle(role, initial, battle, new()));
        return new() { ok = true, battle = battle.View };
    }

    BattleRoundResp Act(LoginSession session, ActiveBattle active, BattleCmdReq request)
    {
        if (active == null || active.Session.Id != request.battleId) throw new GameError(Code.NoBattle, "战斗已经结束");
        var battle = active.Session;
        var cmd = request.cmd;
        if (cmd == null || cmd.type is not ("skill" or "item" or "defend" or "flee" or "auto") ||
            cmd.type == "skill" && cmd.skill != "attack" && !battle.Engine.PlayerSkills.Contains(cmd.skill) ||
            cmd.type == "item" && string.IsNullOrEmpty(cmd.skill))
            throw new GameError(Code.Protocol, "无法使用该战斗指令");

        active.Commands.Add(cmd);
        var events = battle.Engine.RunRound(cmd);
        var reply = new BattleRoundResp { events = events, round = battle.Engine.Round, over = battle.Engine.Over != null };
        if (!reply.over) return reply;
        battles.Remove(active.Role.State.id);
        reply.end = BattleSettlement.Settle(active.Role.State, battle);
        Save(session, active.Role, new BattleRecord(battle.Id, battle.StageId, battle.Seed, rulesVersion, active.Initial, active.Commands, reply.end));
        return reply;
    }
}
