using System;
using System.Threading.Tasks;
using GuiQin.Battle;
using Newtonsoft.Json.Linq;

namespace GuiQin.Net
{
    // 客户端只通过这个接口和“后端”说话。两个实现：MockBackend（本地内存，没服务器也能玩）和 WsBackend（WebSocket 长连接）。
    // 所有 Task 的后续代码都回到 Unity 主线程执行（Tick 里完成），所以回调里可以直接操作场景。
    public interface IGameBackend
    {
        bool Connected { get; }
        event Action<string, JToken> OnPush;         // (命令, 数据)：服务端主动推送
        event Action<string> OnDisconnected;         // 连接断开（原因）
        void Tick();                                 // 每帧调用：派发收到的消息、处理超时、发心跳
        void Close();

        Task<Result<LoginResp>> Login(LoginReq req);
        Task<Result<EnterResp>> EnterGame(string roleId);
        Task<Result<MoveResp>> Move(MoveReq req);
        Task<Result<MapChangeResp>> Portal(string from, string to);     // 走传送阵：服务端判任务锁 / 等级
        Task<Result<MapChangeResp>> Teleport(string to);                // 世界地图传送：服务端判等级 / 扣费
        Task<Result<Empty>> SetLook(string cls);

        // 战斗：服务端结算。start 返回战场；cmd 提交主角指令，返回本回合全部事件（客户端按序播放）；打完时 over=true 并带结算
        Task<Result<BattleStartResp>> BattleStart(string stageId);
        Task<Result<BattleRoundResp>> BattleAct(string battleId, BattleCmd cmd);
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
