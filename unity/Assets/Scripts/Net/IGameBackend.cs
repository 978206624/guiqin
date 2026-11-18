using System;
using System.Threading.Tasks;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Quest;
using GuiQin.Sect;
using Newtonsoft.Json.Linq;

namespace GuiQin.Net
{
    // 客户端只通过这个接口和“后端”说话。两个实现：MockBackend（本地内存，没服务器也能玩）和 WsBackend（WebSocket 长连接）。
    // 所有 Task 的后续代码都回到 Unity 主线程执行（Tick 里完成），所以回调里可以直接操作场景。
    public interface IGameBackend
    {
        bool Connected { get; }
        event Action<string, JToken> OnPush;         // (命令, 数据)：服务端主动推送
        event Action<string> OnDisconnected;         // 连接彻底断开（重连放弃 / 会话失效 / 被顶号）：客户端要回登录界面
        event Action<int> OnReconnecting;            // 网络掉线，开始第 n 次重连（0 = 刚掉线）：客户端盖一层“正在重连”
        event Action OnReconnected;                  // 重连成功，请求已补发：客户端重新对一次状态（Resync）
        bool Reconnecting { get; }
        void Tick();                                 // 每帧调用：派发收到的消息、处理超时、发心跳
        void Close();

        Task<Result<LoginResp>> Login(LoginReq req);
        Task<Result<RoleListResp>> RoleList(string server);
        Task<Result<RoleCreateResp>> RoleCreate(string server, string name, string cls);   // 名字长度 / 重名 / 每区三个角色 / 职业是否开放：都由服务端判
        Task<Result<Empty>> RoleDelete(string roleId);
        Task<Result<EnterResp>> EnterGame(string roleId);
        Task<Result<MoveResp>> Move(MoveReq req);
        Task<Result<MapChangeResp>> Portal(string from, string to);     // 走传送阵：服务端判任务锁 / 等级
        Task<Result<MapChangeResp>> Teleport(string to);                // 世界地图传送：服务端判等级 / 扣费

        // 背包 / 装备 / 商店 / 锻造 / 宝石：命令名见 BagCmd，参数用 BagOpReq 的哪几个字段见 InventoryService。返回最新背包（整包覆盖客户端）
        Task<Result<BagResp>> BagOp(string cmd, BagOpReq req);

        // 任务：和 NPC / 采集点说话。当前任务对得上就推进一拍（最后一拍发奖励）；对不上 ok=false，客户端走普通对话
        Task<Result<QuestTalkResp>> QuestTalk(string npc);

        // 门派：入门 / 选分堂，服务端判等级与“不可改选”，返回新职业
        Task<Result<SectResp>> SectJoin(string sect);
        Task<Result<SectResp>> SectHall(string hall);
        // 玄兵塔：op = enter（按进度进对应层）/ next（上一层）/ leave（回城）
        Task<Result<MapChangeResp>> Tower(string op);

        // 战斗：服务端结算。start 返回战场；cmd 提交主角指令，返回本回合全部事件（客户端按序播放）；打完时 over=true 并带结算
        Task<Result<BattleStartResp>> BattleStart(string stageId);
        Task<Result<BattleRoundResp>> BattleAct(string battleId, BattleCmd cmd);
        // 重进游戏 / 被踢后重登时：取这个角色进行中的战斗快照（没有就 ok=false, code=NoBattle）
        Task<Result<BattleStartResp>> BattleResume();
    }

    public static class BackendFactory
    {
        // Resources/GuiQin/Config/backend.json：{"mode":"ws","url":"wss://game.example.com/ws"}；没有这个文件就用本地假后端
        class Cfg { public string mode, url; }

        public static IGameBackend Create()
        {
            var ta = UnityEngine.Resources.Load<UnityEngine.TextAsset>("GuiQin/Config/backend");
            if (ta != null)
            {
                var c = Newtonsoft.Json.JsonConvert.DeserializeObject<Cfg>(ta.text);
                if (c != null && c.mode == "ws" && !string.IsNullOrEmpty(c.url)) return new WsBackend(c.url);
            }
            return new MockBackend();
        }
    }
}
