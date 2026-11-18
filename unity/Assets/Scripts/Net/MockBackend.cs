using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        static RoleState NewRole() { return new RoleState { id = "r1", name = "云中客", cls = "dao_ty", lv = 1, map = "village", x = -1f, y = -1f }; }
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
    }
}
