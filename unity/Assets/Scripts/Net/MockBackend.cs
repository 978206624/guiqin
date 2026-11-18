using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GuiQin.Battle;
using GuiQin.Inventory;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GuiQin.Net
{
    // 本地假后端：状态只在内存里（退出就没了——和“不做本地存档”一致）。用来在没有服务器时把客户端跑通，
    // 同时是服务端行为的“参考实现”：真服务器要做的校验（传送阵任务锁、等级、移动合法性）在这里先写成最宽松的版本。
    public class MockBackend : IGameBackend
    {
        public bool Connected { get { return true; } }
        public event Action<string, JToken> OnPush { add { } remove { } }
        public event Action<string> OnDisconnected { add { } remove { } }
        public void Tick() { }
        public void Close() { }

        const int LatencyMs = 40;                  // 模拟一点网络延迟，免得客户端把“瞬间返回”当成理所当然
        RoleState role = NewRole();

        static RoleState NewRole() { return new RoleState { id = "r1", name = "云中客", cls = "dao_ty", lv = 1, map = "village", x = -1f, y = -1f, coin = (int)GameData.Raw["start"]["coin"], bag = InventoryService.NewBag() }; }
        public void ResetRole() { role = NewRole(); }

        static async Task<Result<T>> Reply<T>(T data) { await Task.Delay(LatencyMs); return Result<T>.Success(data); }

        public Task<Result<LoginResp>> Login(LoginReq req)
        {
            return Reply(new LoginResp
            {
                session = "mock-session",
                serverTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                roles = new List<RoleBrief> { new RoleBrief { id = role.id, name = role.name, cls = role.cls, lv = role.lv, map = role.map } }
            });
        }

        public Task<Result<EnterResp>> EnterGame(string roleId) { return Reply(new EnterResp { role = role }); }

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
            // 真服务器在这里查 PortalDef 的 needQuest / needStep / 等级，不满足就 ok=false + lockMsg。Mock 一律放行。
            PortalDef p = null;
            MapDef fd;
            if (GameData.Maps.TryGetValue(from, out fd) && fd.portals != null) p = fd.portals.Find(q => q.to == to);
            return Change(to, p != null ? p.arrive : null);
        }

        public Task<Result<MapChangeResp>> Teleport(string to) { return Change(to, null); }

        public Task<Result<Empty>> SetLook(string cls) { role.cls = cls; return Reply(new Empty()); }

        // ---------------- 背包 ----------------
        public async Task<Result<BagResp>> BagOp(string cmd, BagOpReq q)
        {
            await Task.Delay(LatencyMs);
            if (battle != null) return Result<BagResp>.Fail(Code.InBattle, "战斗中不能这样做");
            q = q ?? new BagOpReq();
            var svc = new InventoryService(role); bool ok;
            switch (cmd)
            {
                case BagCmd.Get: ok = true; svc.Msg = null; break;
                case BagCmd.Use: ok = svc.Use(q.id); break;
                case BagCmd.Equip: ok = svc.Equip(q.id); break;
                case BagCmd.Unequip: ok = svc.Unequip(q.slot); break;
                case BagCmd.Sell: ok = svc.Sell(q.id); break;
                case BagCmd.Sort: ok = svc.Sort(); break;
                case BagCmd.ShopBuy: ok = svc.Buy(q.shop, q.id); break;
                case BagCmd.ShopSell: ok = svc.ShopSell(q.shop, q.id); break;
                case BagCmd.Forge: ok = svc.Forge(q.slot, q.gem); break;
                case BagCmd.Inlay: ok = svc.Inlay(q.slot, q.i, q.id); break;
                case BagCmd.Uninlay: ok = svc.Uninlay(q.slot, q.i); break;
                case BagCmd.Merge: ok = svc.Merge(q.id); break;
                default: return Result<BagResp>.Fail(Code.Protocol, "未知命令 " + cmd);
            }
            return Result<BagResp>.Success(new BagResp { ok = ok, msg = svc.Msg, bag = role.bag, coin = role.coin, hp = role.hp, mp = role.mp, star = svc.Star });
        }

        // ---------------- 战斗（参考实现：服务端该做的事都在这里）----------------
        BattleSession battle;

        public Task<Result<BattleStartResp>> BattleStart(string stageId)
        {
            StageDef st;
            if (battle != null) return Task.FromResult(Result<BattleStartResp>.Fail(Code.InBattle, "已经在战斗中"));
            if (!GameData.Stages.TryGetValue(stageId, out st)) return Task.FromResult(Result<BattleStartResp>.Fail(Code.NoBattle, "这场战斗不存在"));
            // 真服务器在这里还要校验：敌人是否在角色所在地图、前置关卡（st.need）、塔层数、体力……
            battle = BattleFactory.Create(role, st, Environment.TickCount);
            var inv = new InventoryService(role);
            battle.View.items = inv.Potions();
            battle.Engine.ItemUse = id => { int h, m; return inv.UseInBattle(id, out h, out m) ? new[] { h, m } : null; };
            return Reply(new BattleStartResp { ok = true, battle = battle.View });
        }

        public async Task<Result<BattleRoundResp>> BattleAct(string battleId, BattleCmd cmd)
        {
            await Task.Delay(LatencyMs);
            if (battle == null || battle.Id != battleId) return Result<BattleRoundResp>.Fail(Code.NoBattle, "战斗已经结束");
            var resp = new BattleRoundResp();
            resp.events = battle.Engine.RunRound(cmd);
            resp.round = battle.Engine.Round;
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
            var eng = b.Engine; var p = eng.Player;
            var end = new BattleEnd { result = eng.Over, round = eng.Round, lv = role.lv };
            role.hp = Math.Max(1, p.Hp); role.mp = p.Mp;
            var rng = new Random();
            if (eng.Over == "win")
            {
                int exp = 0, coin = 0;
                foreach (var id in b.Stage.enemies)
                {
                    var e = GameData.Raw["enemies"][id];
                    exp += (int)e["exp"]; coin += (int)e["coin"];
                    foreach (var d in e["drops"]) if (rng.NextDouble() < (double)d[1]) { string k = (string)d[0]; if (!new InventoryService(role).Give(k, 1)) { end.note = "背包已满，部分战利品丢失"; continue; } int n; end.items.TryGetValue(k, out n); end.items[k] = n + 1; }
                }
                end.exp = exp; end.coin = coin;
                role.coin += coin; role.exp += exp;
                int need = (int)GameData.Raw["expNeed"]["base"] + (int)GameData.Raw["expNeed"]["perLevel"] * role.lv;
                while (role.exp >= need) { role.exp -= need; role.lv++; end.levelUp = true; need = (int)GameData.Raw["expNeed"]["base"] + (int)GameData.Raw["expNeed"]["perLevel"] * role.lv; }
                if (end.levelUp) { role.hp = -1; role.mp = -1; }
                end.lv = role.lv;
            }
            else if (eng.Over == "lose")
            {
                role.hp = Math.Max(1, (int)(p.MaxHp * 0.3f));
                MapDef md; end.respawnMap = GameData.Maps.TryGetValue(role.map, out md) && !string.IsNullOrEmpty(md.home) ? md.home : role.map;
                role.map = end.respawnMap; role.x = -1f; role.y = -1f;
            }
            return end;
        }
    }
}
