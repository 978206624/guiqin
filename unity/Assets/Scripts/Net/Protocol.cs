using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GuiQin.Net
{
    // 协议：一条消息 = 一个 Envelope（JSON 文本帧）。设计说明见 docs/BACKEND-DESIGN.md。
    // 命令名 "模块.动作"；请求带 seq（>0），服务端用同一个 seq 回；服务端主动推送 seq = 0。
    public static class Cmd
    {
        public const string Ping = "sys.ping";
        public const string Login = "auth.login";
        public const string EnterGame = "role.enter";
        public const string SetLook = "role.look";
        public const string Move = "map.move";
        public const string Portal = "map.portal";
        public const string Teleport = "map.teleport";
        public const string BattleStart = "battle.start";
        public const string BattleCmd = "battle.cmd";
        // 服务端推送
        public const string PushKick = "push.kick";            // 被顶号 / 封禁 / 维护
        public const string PushMapState = "push.map";         // 地图内其它玩家、NPC 状态的增量（AOI）
        public const string PushWeather = "push.weather";
        public const string PushRole = "push.role";            // 角色属性变化
    }

    public static class Code
    {
        public const int Ok = 0;
        public const int Network = -1, Timeout = -2, Protocol = -3;
        public const int Unauthorized = 401, Banned = 403, VersionTooOld = 426, Busy = 429, Server = 500;
        public const int QuestLocked = 2001, LevelTooLow = 2002, BadMap = 2003;
        public const int BadMove = 3001;
        public const int NoBattle = 4001, InBattle = 4002;
    }

    public class Envelope
    {
        public int seq;
        public string cmd;
        public int code;
        public string msg;
        public long ts;                 // 服务端时间（毫秒），用来校准客户端时钟
        public JToken data;
    }

    public class Result<T>
    {
        public int code; public string msg; public T data;
        public bool Ok { get { return code == Code.Ok; } }
        public static Result<T> Fail(int code, string msg) { return new Result<T> { code = code, msg = msg }; }
        public static Result<T> Success(T data) { return new Result<T> { code = Code.Ok, data = data }; }
    }

    // ------------------------------ 数据 ------------------------------
    // 位置一律是“地图数据坐标”（舞台像素 / scale*k），服务端不用知道客户端的缩放；x、y 为 -1 表示“出生点”。

    public class LoginReq { public string account, token, device, platform, clientVer, dataHash; }
    public class RoleBrief { public string id, name, cls; public int lv; public string map; }
    public class LoginResp { public string session; public List<RoleBrief> roles; public long serverTime; }

    public class EnterReq { public string roleId; }
    public class RoleState
    {
        public string id, name, cls, map;
        public int lv;
        public float x = -1f, y = -1f;
        public int exp, coin, hp = -1, mp = -1;       // hp / mp 为 -1 表示满
    }
    public class EnterResp { public RoleState role; }

    public class MoveReq { public string map; public float[] from, to; }
    public class MoveResp { public bool ok; public float x, y; }            // ok=false：服务端拒绝，(x,y) 是应回到的位置

    public class PortalReq { public string from, to; }
    public class TeleportReq { public string to; }
    public class MapChangeResp { public bool ok; public string msg, map; public float x = -1f, y = -1f; }

    public class LookReq { public string cls; }
    public class Empty { }
}
