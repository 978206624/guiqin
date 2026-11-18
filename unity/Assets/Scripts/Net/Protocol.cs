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
        public const string Resume = "auth.resume";                 // 断线重连：带 session 恢复登录态，服务端回放 / 幂等处理未确认的请求
        public const string BattleResume = "battle.resume";         // 重进游戏时取进行中的战斗快照
        public const string Login = "auth.login";
        public const string EnterGame = "role.enter";
        public const string ServerList = "server.list", RoleList = "role.list", RoleCreate = "role.create", RoleDelete = "role.delete";
        public const string Move = "map.move";
        public const string Portal = "map.portal";
        public const string Teleport = "map.teleport";
        public const string BattleStart = "battle.start";
        public const string BattleCmd = "battle.cmd";
        public const string FriendAdd = "friend.add", FriendDel = "friend.del", FriendList = "friend.list";
        public const string PartyInvite = "party.invite", PartyLeave = "party.leave", PushParty = "push.party";
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
        public const int QuestLocked = 2001, LevelTooLow = 2002, BadMap = 2003, StageLocked = 2004;
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

    // 账号登录（走 HTTPS 网关，WebSocket 连上后再带 token 恢复）；密码只在 TLS 里传，服务端存哈希。游客试玩：account 为空，服务端发一个临时账号。
    public class LoginReq { public string account, password, token, device, platform, clientVer, dataHash; public bool guest; }
    public class RoleBrief { public string id, name, cls; public int lv; public string map; }
    public class ServerInfo { public string id, name, status; public int no, roles; }        // status: new / ok / busy / hot / down；roles：这个账号在该区的角色数
    public class LoginResp { public string session, last; public List<ServerInfo> servers; public long serverTime; }   // last：上次进的区

    public class RoleListReq { public string server; }
    public class RoleListResp { public List<RoleBrief> roles = new List<RoleBrief>(); }
    public class RoleCreateReq { public string server, name, cls; }
    public class RoleCreateResp { public bool ok; public string msg; public RoleBrief role; }
    public class RoleDeleteReq { public string roleId; }

    public class EnterReq { public string roleId; }
    public class RoleState
    {
        public string id, name, cls, map;
        public int lv;
        public float x = -1f, y = -1f;
        public int exp, coin, hp = -1, mp = -1;       // hp / mp 为 -1 表示满
        public string sect = "";                      // 门派：""（未入门）/ dao / mo
        public int step, cnt, tower;                  // 主线进度（节拍序号 / 清怪计数）、玄兵塔进度
        public List<string> cleared = new List<string>();   // 已通关的关卡
        public GuiQin.Inventory.BagState bag;         // 背包 / 装备 / 星级 / 宝石
        public List<string> petOwn = new List<string>();   // 已拥有的神兽 id（灵狐出生自带，不在里面）
        public string petAct = "fox";                 // 出战宠物
        public int bond;                              // 亲密度：打赢一场 +1，升档后宠物气血 / 攻击提高
        public bool ssGift;                           // 神兽体验福利是否领过
        public string giftDay = "";                   // 每日福利上次领取的日期（服务端日期 yyyy-MM-dd）
        public Dictionary<string, int> skl = new Dictionary<string, int>();   // 技能修炼：技能 id → 等级
    }
    public class EnterResp { public RoleState role; }

    public class MoveReq { public string map; public float[] from, to; }
    public class MoveResp { public bool ok; public float x, y; }            // ok=false：服务端拒绝，(x,y) 是应回到的位置

    public class PortalReq { public string from, to; }
    public class TeleportReq { public string to; }
    public class MapChangeResp { public bool ok; public string msg, map; public float x = -1f, y = -1f; }

    public class ResumeReq { public string session; public int lastSeq; }
    public class LookReq { public string cls; }
    public class Empty { }
}
