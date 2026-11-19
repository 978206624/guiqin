using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GuiQin.Battle;
using GuiQin.Chat;
using GuiQin.Inventory;
using GuiQin.Quest;
using GuiQin.Sect;
using GuiQin.Tower;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GuiQin.Net
{
    // 本地假后端：状态只在内存里（退出就没了——和“不做本地存档”一致）。用来在没有服务器时把客户端跑通，
    // 同时是服务端行为的“参考实现”：真服务器要做的校验（等级、移动合法性等）在这里先写成最宽松的版本。
    public class MockBackend : IGameBackend
    {
        public bool Connected { get { return true; } }
        public event Action<string, JToken> OnPush;
        public event Action<string> OnDisconnected { add { } remove { } }
        public event Action<int> OnReconnecting { add { } remove { } }
        public event Action OnReconnected { add { } remove { } }
        public bool Reconnecting { get { return false; } }
        readonly Dictionary<string, ChatService> chats = new Dictionary<string, ChatService>();
        ChatService Chat => ChatFor(role);

        ChatService ChatFor(RoleState owner)
        {
            if (chats.TryGetValue(owner.id, out var chat)) return chat;
            chat = new ChatService(owner);
            chat.OnPush += m => { if (role?.id == owner.id) OnPush?.Invoke(ChatCmd.Push, JToken.FromObject(m)); };
            chat.OnParty += p => { if (role?.id == owner.id) OnPush?.Invoke(Cmd.PushParty, JToken.FromObject(p)); };
            chats.Add(owner.id, chat);
            return chat;
        }

        public void Tick() { if (role != null) Chat.Tick(); }

        Task<Result<T>> ChatCall<T>(Func<ChatService, T> call)
        {
            if (role == null) return Task.FromResult(Result<T>.Fail(Code.Unauthorized, "还没进游戏"));
            return Reply(call(Chat));
        }

        public Task<Result<ChatResp>> ChatSend(ChatSendReq req) => ChatCall(c => c.Send(req));
        public Task<Result<ChatHistResp>> ChatHistory() => ChatCall(c => c.History());
        public Task<Result<ChatResp>> ChatGuild(bool join) => ChatCall(c => c.SetGuild(join));
        public Task<Result<FriendResp>> FriendList() => ChatCall(c => c.Friends());
        public Task<Result<FriendResp>> FriendAdd(string peer) => ChatCall(c => c.SetFriend(peer, true));
        public Task<Result<FriendResp>> FriendDel(string peer) => ChatCall(c => c.SetFriend(peer, false));
        public Task<Result<PartyResp>> PartyInvite(string peer) => ChatCall(c => c.Invite(peer));
        public Task<Result<PartyResp>> PartyReply(string peer, bool accept) => Task.FromResult(Result<PartyResp>.Success(new PartyResp { msg = "没有待处理的邀请" }));
        public Task<Result<PartyResp>> PartyLeave() => ChatCall(c => c.Leave());

        void ZoneNotice(string text)
        {
            foreach (var owner in Zone(enteredServer)) ChatFor(owner).Notice(text);
        }

        static string ItemName(string id) => (string)GameData.Raw["items"][id]?["name"] ?? id;

        void ReportRewards(int oldLevel, int exp, int coin, Dictionary<string, int> items)
        {
            if (role.lv > oldLevel)
            {
                Chat.SystemMessage("升到 " + role.lv + " 级！");
                for (int lv = oldLevel + 1; lv <= role.lv; lv++)
                    if (lv % 5 == 0) ZoneNotice("恭喜玩家「" + role.name + "」等级突破 " + lv + " 级！");
            }
            var parts = new List<string>();
            if (exp > 0) parts.Add("经验 +" + exp);
            if (coin > 0) parts.Add("银两 +" + coin);
            if (items != null)
                foreach (var item in items) parts.Add(ItemName(item.Key) + " ×" + item.Value);
            if (parts.Count > 0) Chat.SystemMessage("获得 " + string.Join("、", parts));
        }

        public void Close() { }

        const int LatencyMs = 40;                  // 模拟一点网络延迟，免得客户端把“瞬间返回”当成理所当然
        RoleState role;                            // 当前进了游戏的角色（EnterGame 之后才有）

        // ---- 账号 / 区服 / 角色（内存里；真服务器在数据库里）----
        static readonly ServerInfo[] Servers =
        {
            new ServerInfo { id = "s12", no = 12, name = "函谷关", status = "new" }, new ServerInfo { id = "s11", no = 11, name = "骊山", status = "ok" },
            new ServerInfo { id = "s10", no = 10, name = "阿房", status = "busy" }, new ServerInfo { id = "s9", no = 9, name = "云梦泽", status = "ok" },
            new ServerInfo { id = "s8", no = 8, name = "巴蜀", status = "busy" }, new ServerInfo { id = "s7", no = 7, name = "陇西", status = "ok" },
            new ServerInfo { id = "s6", no = 6, name = "邯郸", status = "down" }, new ServerInfo { id = "s5", no = 5, name = "琅琊", status = "ok" },
            new ServerInfo { id = "s4", no = 4, name = "大梁", status = "busy" }, new ServerInfo { id = "s3", no = 3, name = "郢都", status = "hot" },
            new ServerInfo { id = "s2", no = 2, name = "临淄", status = "hot" }, new ServerInfo { id = "s1", no = 1, name = "咸阳", status = "hot" }
        };
        const int MaxRoles = 3;
        readonly Dictionary<string, List<RoleState>> zones = new Dictionary<string, List<RoleState>>();
        string lastServer, enteredServer;

        List<RoleState> Zone(string sid) { List<RoleState> l; if (!zones.TryGetValue(sid, out l)) zones[sid] = l = new List<RoleState>(); return l; }

        static RoleState NewRole(string id, string name, string cls)
        {
            var st = GameData.Raw["start"];
            return new RoleState { id = id, name = name, cls = cls, lv = (int)st["lv"], map = (string)st["map"], x = -1f, y = -1f, coin = (int)st["coin"], bag = InventoryService.NewBag() };
        }
        static RoleBrief Brief(RoleState r) { return new RoleBrief { id = r.id, name = r.name, cls = r.cls, lv = r.lv, map = r.map }; }

        // 重新开始：同一个角色回到出生状态（职业回到新手）。只给本地演示后端用；真服务器没有这个命令
        public void ResetRole()
        {
            if (role == null) return;
            var z = Zone(enteredServer); int i = z.FindIndex(r => r.id == role.id);
            string baseCls = (string)GameData.Raw["classes"][role.cls]["gender"] == "m" ? "novice_m" : "novice_f";
            role = NewRole(role.id, role.name, baseCls); if (i >= 0) z[i] = role;
            if (chats.TryGetValue(role.id, out var chat)) chat.Bind(role);
        }

        // 返回给客户端的数据一律深拷贝：Mock 和真服务器一样，客户端拿到的是“副本”，不会和服务端状态共享引用
        static T Clone<T>(T o) { return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(Newtonsoft.Json.JsonConvert.SerializeObject(o)); }

        static async Task<Result<T>> Reply<T>(T data) { await Task.Delay(LatencyMs); return Result<T>.Success(data); }

        public Task<Result<LoginResp>> Login(LoginReq req)
        {
            // 离线预览需要填写账号和密码，不创建或验证正式账号。
            if (string.IsNullOrWhiteSpace(req?.account) || string.IsNullOrEmpty(req.password))
                return Task.FromResult(Result<LoginResp>.Fail(Code.Unauthorized, "请输入账号和密码"));
            var list = new List<ServerInfo>();
            foreach (var s in Servers) list.Add(new ServerInfo { id = s.id, no = s.no, name = s.name, status = s.status, roles = Zone(s.id).Count });
            return Reply(new LoginResp { session = "mock-session", last = lastServer, servers = list, serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });
        }

        public Task<Result<RoleListResp>> RoleList(string server)
        {
            var r = new RoleListResp(); foreach (var x in Zone(server)) r.roles.Add(Brief(x));
            lastServer = server; return Reply(r);
        }

        public Task<Result<RoleCreateResp>> RoleCreate(string server, string name, string cls)
        {
            var z = Zone(server); name = (name ?? "").Trim();
            Func<string, Task<Result<RoleCreateResp>>> no = m => Reply(new RoleCreateResp { ok = false, msg = m });
            if (z.Count >= MaxRoles) return no("每个区最多创建 " + MaxRoles + " 个角色");
            if (name.Length < 2 || name.Length > 6) return no("名字要 2 到 6 个字");
            if (z.Exists(r => r.name == name)) return no("这个名字已经用过了");
            var c = GameData.Raw["classes"][cls];
            if (c == null || c["hidden"] != null && (bool)c["hidden"]) return no("这个职业还没开放");
            var role0 = NewRole(server + "-" + Guid.NewGuid().ToString("N").Substring(0, 8), name, cls); z.Add(role0);
            return Reply(new RoleCreateResp { ok = true, role = Brief(role0) });
        }

        public Task<Result<Empty>> RoleDelete(string roleId)
        {
            foreach (var kv in zones) { int n = kv.Value.RemoveAll(r => r.id == roleId); if (n > 0) { chats.Remove(roleId); if (role != null && role.id == roleId) role = null; return Reply(new Empty()); } }
            return Task.FromResult(Result<Empty>.Fail(Code.Protocol, "没有这个角色"));
        }

        public Task<Result<EnterResp>> EnterGame(string roleId)
        {
            foreach (var kv in zones) { var r = kv.Value.Find(x => x.id == roleId); if (r != null) { if (role != r) battle = null; role = r; enteredServer = kv.Key; GuiQin.Train.TrainService.EnsureSkl(role); QuestService.EnsureSectLevel(role); return Reply(new EnterResp { role = Clone(role) }); } }
            return Task.FromResult(Result<EnterResp>.Fail(Code.Protocol, "没有这个角色"));
        }

        public Task<Result<MoveResp>> Move(MoveReq req)
        {
            if (req.to != null && req.to.Length >= 2 && req.map == role.map) { role.x = req.to[0]; role.y = req.to[1]; }
            return Reply(new MoveResp { ok = true, x = role.x, y = role.y });
        }

        Task<Result<MapChangeResp>> Change(string to, float[] at)
        {
            MapDef d;
            if (!GameData.Maps.TryGetValue(to, out d)) return Task.FromResult(Result<MapChangeResp>.Success(new MapChangeResp { ok = false, msg = "目的地不存在" }));
            role.map = to; role.x = at != null ? at[0] : -1f; role.y = at != null ? at[1] : -1f;
            return Reply(new MapChangeResp { ok = true, map = to, x = role.x, y = role.y });
        }

        public Task<Result<MapChangeResp>> Portal(string from, string to)
        {
            if (battle != null) return Task.FromResult(Result<MapChangeResp>.Fail(Code.InBattle, "战斗中"));
            var portal = from == role.map ? GameData.Maps[from].portals?.Find(q => q.to == to) : null;
            if (portal == null) return Reply(new MapChangeResp { msg = "当前地图没有这个传送点", map = role.map });
            if (portal.needStages?.Exists(id => !role.cleared.Contains(id)) == true)
                return Reply(new MapChangeResp { msg = portal.lockMsg ?? "需先击败守卫", map = role.map });
            return Change(to, portal.arrive);
        }

        public Task<Result<MapChangeResp>> Teleport(string to, string npc = null)
        {
            if (battle != null) return Task.FromResult(Result<MapChangeResp>.Fail(Code.InBattle, "战斗中"));
            if (string.IsNullOrEmpty(to) || !GameData.Maps.TryGetValue(to, out var destination))
                return Reply(new MapChangeResp { msg = "目的地不存在", map = role.map });
            if (to == ZiyinService.Entrance && npc == ZiyinService.Guard && role.map == "qingyang")
            {
                var error = ZiyinService.Enter(role, DateTime.UtcNow);
                return error != null ? Reply(new MapChangeResp { map = role.map, msg = error }) : Change(to, null);
            }
            if (!string.IsNullOrEmpty(npc) && !SectService.CanEscort(role, npc, to))
                return Reply(new MapChangeResp { msg = "请靠近对应的门派接引师" });
            if (string.IsNullOrEmpty(npc) && !GameData.CanWorldTeleport(destination))
                return Reply(new MapChangeResp { msg = "请从副本入口进入", map = role.map });
            return Change(to, null);
        }


        // ---------------- 门派 / 玄兵塔 ----------------
        public Task<Result<SectResp>> SectJoin(string sect) => SectChat(s => s.Join(sect));
        public Task<Result<SectResp>> SectHall(string hall) => SectChat(s => s.Hall(hall));

        Task<Result<SectResp>> SectChat(Func<SectService, SectResp> change)
        {
            if (battle != null) return Task.FromResult(Result<SectResp>.Fail(Code.InBattle, "战斗中"));
            var result = change(new SectService(role));
            if (result.ok) Chat.SystemMessage(result.msg);
            return Reply(result);
        }

        public Task<Result<MapChangeResp>> Tower(string op)
        {
            if (battle != null) return Task.FromResult(Result<MapChangeResp>.Fail(Code.InBattle, "战斗中"));
            var ts = new TowerService(role); string msg, dest = null;
            switch (op)
            {
                case "enter": dest = ts.Enter(out msg); break;
                case "next": dest = ts.Next(out msg); break;
                case "leave": if (ts.Leave(out msg)) return Change("town", new[] { 850f, 610f }); break;
                default: return Task.FromResult(Result<MapChangeResp>.Fail(Code.Protocol, "未知命令 tower." + op));
            }
            if (dest == null) return Reply(new MapChangeResp { ok = false, msg = msg, map = role.map });
            return Change(dest, null);
        }

        // ---------------- 任务 ----------------
        public Task<Result<QuestTalkResp>> QuestTalk(string npc)
        {
            if (battle != null) return Task.FromResult(Result<QuestTalkResp>.Fail(Code.InBattle, "战斗中"));
            int oldLevel = role.lv;
            var result = new QuestService(role).Talk(npc);
            if (result.ok && result.done)
            {
                Chat.SystemMessage("完成了主线任务");
                ReportRewards(oldLevel, result.exp, result.coin, result.items);
            }
            return Reply(result);
        }

        // ---------------- 背包 ----------------
        public async Task<Result<BagResp>> BagOp(string cmd, BagOpReq q)
        {
            await Task.Delay(LatencyMs);
            if (battle != null) return Result<BagResp>.Fail(Code.InBattle, "战斗中不能这样做");
            q = q ?? new BagOpReq();
            var svc = new InventoryService(role); bool ok;
            int oldStar = -1;
            if (cmd == BagCmd.Forge && q.slot != null && role.bag.gear.TryGetValue(q.slot, out var gear)) oldStar = gear.star;
            switch (cmd)
            {
                case BagCmd.Expand: ok = svc.Expand(q.capacity); break;
                case BagCmd.Get: ok = true; svc.Msg = null; break;
                case BagCmd.Rest: { var st = RoleStats.Compute(role.cls, role.lv, role.bag); bool need = role.hp >= 0 && role.hp < st.maxHp || role.mp >= 0 && role.mp < st.maxMp; role.hp = -1; role.mp = -1; ok = true; svc.Msg = need ? "歇了一会儿，气血法力回满" : null; break; }
                case BagCmd.Gift: ok = svc.DailyGift(DateTime.UtcNow); break;
                case BagCmd.Use: ok = svc.Use(q.id); break;
                case BagCmd.Equip: ok = svc.Equip(q.id); break;
                case BagCmd.Unequip: ok = svc.Unequip(q.slot); break;
                case BagCmd.Sell: ok = svc.Sell(q.id); break;
                case BagCmd.Sort: ok = svc.Sort(); break;
                case BagCmd.ShopBuy: ok = svc.Buy(q.shop, q.id); break;
                case BagCmd.MallBuy: ok = svc.BuyMall(q.id, q.quantity, out _); break;
                case BagCmd.ShopSell: ok = svc.ShopSell(q.shop, q.id); break;
                case BagCmd.Forge: ok = svc.Forge(q.slot, q.gem); break;
                case BagCmd.Inlay: ok = svc.Inlay(q.slot, q.i, q.id); break;
                case BagCmd.Uninlay: ok = svc.Uninlay(q.slot, q.i); break;
                case BagCmd.Merge: ok = svc.Merge(q.id); break;
                default: return Result<BagResp>.Fail(Code.Protocol, "未知命令 " + cmd);
            }
            if (ok && (cmd == BagCmd.Forge || cmd == BagCmd.ShopBuy || cmd == BagCmd.MallBuy || cmd == BagCmd.Gift || cmd == BagCmd.Merge))
                Chat.SystemMessage(svc.Msg);
            if (cmd == BagCmd.Forge && ok && svc.Star >= 10 && svc.Star > oldStar && q.slot != null && role.bag.equip.TryGetValue(q.slot, out var forged))
                ZoneNotice("恭喜玩家「" + role.name + "」将" + ItemName(forged) + "锻造至 " + svc.Star + " 星！");
            return Result<BagResp>.Success(new BagResp { ok = ok, msg = svc.Msg, bag = Clone(role.bag), coin = role.coin, yuanbao = role.yuanbao, silverNote = role.silverNote, hp = role.hp, mp = role.mp, star = svc.Star });
        }

        // ---------------- 战斗（参考实现：服务端该做的事都在这里）----------------
        BattleSession battle;

        public Task<Result<BattleStartResp>> BattleStart(string stageId)
        {
            StageDef st;
            if (battle != null) return Task.FromResult(Result<BattleStartResp>.Fail(Code.InBattle, "已经在战斗中"));
            if (!GameData.Stages.TryGetValue(stageId, out st)) return Task.FromResult(Result<BattleStartResp>.Fail(Code.NoBattle, "这场战斗不存在"));
            // 真服务器在这里还要校验：敌人是否在角色所在地图、塔层数、体力……
            var lock_ = new QuestService(role).CheckStage(st);
            if (lock_ != null) return Reply(new BattleStartResp { ok = false, msg = lock_ });
            battle = BattleFactory.Create(role, st, Environment.TickCount);
            var inv = new InventoryService(role);
            battle.View.items = inv.Potions();
            battle.Engine.ItemUse = id => { int h, m; return inv.UseInBattle(id, out h, out m) ? new[] { h, m } : null; };
            return Reply(new BattleStartResp { ok = true, battle = battle.View });
        }

        public Task<Result<GuiQin.Train.TrainResp>> Train(string who, string id)
        {
            if (battle != null) return Task.FromResult(Result<GuiQin.Train.TrainResp>.Fail(Code.InBattle, "战斗中"));
            return Reply(new GuiQin.Train.TrainService(role).Do(who, id));
        }

        public Task<Result<GuiQin.Pets.PetResp>> PetOp(string cmd, string id)
        {
            if (battle != null) return Task.FromResult(Result<GuiQin.Pets.PetResp>.Fail(Code.InBattle, "战斗中"));
            return Reply(Clone(new GuiQin.Pets.PetService(role).Run(cmd, id)));        // 深拷贝：客户端别和服务端共用同一个背包对象
        }

        public Task<Result<BattleStartResp>> BattleResume()
        {
            if (battle == null) return Task.FromResult(Result<BattleStartResp>.Fail(Code.NoBattle, "没有进行中的战斗"));
            var snap = battle.Snapshot(); snap.items = new InventoryService(role).Potions();
            return Reply(new BattleStartResp { ok = true, battle = snap });
        }

        public async Task<Result<BattleRoundResp>> BattleAct(string battleId, BattleCmd cmd)
        {
            await Task.Delay(LatencyMs);
            if (battle == null || battle.Id != battleId) return Result<BattleRoundResp>.Fail(Code.NoBattle, "战斗已经结束");
            if (!battle.Engine.AcceptsPetCommand(cmd?.petSkill)) return Result<BattleRoundResp>.Fail(Code.Protocol, "宠物无法使用该技能");
            var resp = new BattleRoundResp();
            resp.events = battle.Engine.RunRound(cmd);
            resp.round = battle.Engine.Round;
            resp.petCooldowns = battle.Engine.PetCooldowns();
            if (battle.Engine.Over != null)
            {
                resp.over = true; resp.end = Settle(battle);
                battle = null;
            }
            return Result<BattleRoundResp>.Success(resp);
        }

        // 结算：发经验 / 银两 / 掉落，升级，写回气血法力，战败送回城
        BattleEnd Settle(BattleSession b)
        {
            int oldLevel = role.lv;
            var end = BattleSettlement.Settle(role, b);
            if (end.result == "win") ReportRewards(oldLevel, end.exp, end.coin, end.items);
            return end;
        }
    }
}
