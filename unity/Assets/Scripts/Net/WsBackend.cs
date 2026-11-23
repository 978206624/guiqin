using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Quest;
using GuiQin.Chat;
using GuiQin.Sect;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GuiQin.Net
{
    // WebSocket 长连接实现：请求/应答按 seq 对应，服务端推送走 OnPush，15 秒一次心跳，8 秒请求超时。
    // 断线重连：掉线（或 35 秒没收到任何包）后按 1/2/4/8/15 秒退避重连，最多 8 次；连上先发 auth.resume{session}，
    // 成功后把没收到回包的请求用“原 seq”补发——服务端对 seq 幂等（缓存最近的回包），所以扣费 / 发奖不会重复。
    // 会话失效（401）、被顶号（push.kick）、重连用尽 → OnDisconnected，客户端回登录界面。
    // 注意：System.Net.WebSockets 在 WebGL 构建里不可用（要换浏览器 WebSocket 插件）；iOS / Android / PC 可用。
    // 还没做的（见 docs/BACKEND-DESIGN.md 第 3 节）：消息压缩、protobuf。
    public class WsBackend : IGameBackend
    {
        class Pending { public Envelope Req; public TaskCompletionSource<Envelope> Tcs; public float Deadline; }

        readonly string url;
        internal string Endpoint => url;
        ClientWebSocket ws; CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();      // 来自接收线程的断线通知
        readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        int seq; float nextPing, lastRecv;
        const float RequestTimeout = 8f, PingEvery = 15f, SilentLimit = 35f;
        static readonly float[] Backoff = { 1f, 2f, 4f, 8f, 15f, 15f, 15f, 15f };

        string session;                            // 登录成功后服务端发的会话 id，重连时用来恢复
        bool reconnecting, retrying, closed, kicked;
        int attempt; float nextRetry;
        int resumeSeq; TaskCompletionSource<Envelope> resumeTcs; float resumeDeadline;

        public bool Connected { get { return ws != null && ws.State == WebSocketState.Open; } }
        public bool Reconnecting { get { return reconnecting; } }
        public event Action<string, JToken> OnPush;
        public event Action<string> OnDisconnected;
        public event Action<int> OnReconnecting;
        public event Action OnReconnected;
        public long ServerTimeOffsetMs;            // 服务端时间 - 本机时间，用来对齐倒计时 / 天气到期

        public WsBackend(string url) { this.url = url; }

        async Task<bool> Connect()
        {
            if (Connected) return true;
            try
            {
                if (cts != null) cts.Cancel();
                cts = new CancellationTokenSource(); ws = new ClientWebSocket();
                var token = cts.Token; var sock = ws;
                await sock.ConnectAsync(new Uri(url), token);
                var _ = Task.Run(() => ReceiveLoop(sock, token));
                nextPing = Time.unscaledTime + PingEvery; lastRecv = Time.unscaledTime;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("连接失败：" + e.Message); return false; }
        }

        async Task ReceiveLoop(ClientWebSocket sock, CancellationToken token)
        {
            var buf = new byte[8192];
            try
            {
                while (sock.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    var ms = new MemoryStream(); WebSocketReceiveResult r;
                    do
                    {
                        r = await sock.ReceiveAsync(new ArraySegment<byte>(buf), token);
                        if (r.MessageType == WebSocketMessageType.Close) { events.Enqueue("服务器关闭了连接"); return; }
                        ms.Write(buf, 0, r.Count);
                    } while (!r.EndOfMessage);
                    inbox.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (Exception e) { if (!token.IsCancellationRequested) events.Enqueue("连接中断：" + e.Message); }
        }

        void SendRaw(Envelope e)
        {
            if (!Connected) return;
            var bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(e));
            var _ = ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);   // 单线程调用，SendAsync 不会并发
        }

        // 主线程每帧调用：这里才把回包交给 await 的代码，所以后续逻辑都在主线程
        public void Tick()
        {
            if (closed) return;
            float now = Time.unscaledTime;
            string s;
            while (inbox.TryDequeue(out s))
            {
                Envelope e;
                try { e = JsonConvert.DeserializeObject<Envelope>(s); } catch { continue; }
                if (e == null) continue;
                lastRecv = now;
                if (e.ts > 0) ServerTimeOffsetMs = e.ts - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Pending p;
                if (e.seq > 0 && e.seq == resumeSeq && resumeTcs != null) { var t = resumeTcs; resumeTcs = null; t.TrySetResult(e); }
                else if (e.seq > 0 && pending.TryGetValue(e.seq, out p)) { pending.Remove(e.seq); p.Tcs.TrySetResult(e); }
                else if (e.seq == 0)
                {
                    if (e.cmd == Cmd.PushKick) { kicked = true; session = null; reconnecting = false; FailAll("账号已下线"); }       // 被顶号 / 封禁：不重连
                    if (OnPush != null) OnPush(e.cmd, e.data);
                }
            }
            while (events.TryDequeue(out s)) OnDrop(s, now);

            if (reconnecting)
            {
                if (resumeTcs != null && now > resumeDeadline) { var t = resumeTcs; resumeTcs = null; t.TrySetResult(new Envelope { code = Code.Timeout, msg = "重连超时" }); }
                if (!retrying && now >= nextRetry) { retrying = true; var _ = TryResume(); }
                return;                                                  // 重连期间请求不超时，连上后统一补发
            }

            List<int> dead = null;
            foreach (var kv in pending) if (now > kv.Value.Deadline) (dead ?? (dead = new List<int>())).Add(kv.Key);
            if (dead != null) foreach (var k in dead) { var p = pending[k]; pending.Remove(k); p.Tcs.TrySetResult(new Envelope { code = Code.Timeout, msg = "请求超时" }); }

            if (Connected)
            {
                if (session != null && now - lastRecv > SilentLimit) { OnDrop("网络没有响应", now); return; }          // 半开连接：对面早没了，TCP 还不知道
                if (now >= nextPing) { nextPing = now + PingEvery; SendRaw(new Envelope { seq = ++seq, cmd = Cmd.Ping }); }   // 带 seq，服务端回 pong，用来判断连接还活着
            }
        }

        // 连接掉了：已登录就进重连，没登录（还在登录界面）就直接报失败
        void OnDrop(string why, float now)
        {
            if (kicked || reconnecting || closed) return;
            DropSocket();
            if (session == null) { FailAll(why); if (OnDisconnected != null) OnDisconnected(why); return; }
            reconnecting = true; retrying = false; attempt = 0; nextRetry = now + Backoff[0];
            if (OnReconnecting != null) OnReconnecting(0);
        }

        void DropSocket() { try { if (cts != null) cts.Cancel(); if (ws != null) ws.Dispose(); } catch { } ws = null; }

        async Task TryResume()
        {
            attempt++;
            if (OnReconnecting != null) OnReconnecting(attempt);
            DropSocket();
            Envelope e = null;
            if (await Connect())
            {
                resumeSeq = ++seq; resumeTcs = new TaskCompletionSource<Envelope>(); resumeDeadline = Time.unscaledTime + RequestTimeout;
                SendRaw(new Envelope { seq = resumeSeq, cmd = Cmd.Resume, data = JToken.FromObject(new ResumeReq { session = session, lastSeq = seq }) });
                e = await resumeTcs.Task;
            }
            retrying = false;
            if (closed || kicked || !reconnecting) return;
            if (e != null && e.code == Code.Ok)
            {
                reconnecting = false; attempt = 0; lastRecv = Time.unscaledTime;
                foreach (var kv in pending) { kv.Value.Deadline = Time.unscaledTime + RequestTimeout; SendRaw(kv.Value.Req); }     // 原 seq 补发，服务端幂等
                if (OnReconnected != null) OnReconnected();
                return;
            }
            if (e != null && (e.code == Code.Unauthorized || e.code == Code.Banned || e.code == Code.VersionTooOld)) { GiveUp(e.msg ?? "登录已失效，请重新登录"); return; }
            if (attempt >= Backoff.Length) { GiveUp("网络不通，已断开连接"); return; }
            nextRetry = Time.unscaledTime + Backoff[Math.Min(attempt, Backoff.Length - 1)];
        }

        void GiveUp(string why)
        {
            reconnecting = false; session = null; DropSocket(); FailAll(why);
            if (OnDisconnected != null) OnDisconnected(why);
        }

        void FailAll(string why)
        {
            var all = new List<Pending>(pending.Values); pending.Clear();
            foreach (var p in all) p.Tcs.TrySetResult(new Envelope { code = Code.Network, msg = why });
        }

        public void Close() { closed = true; DropSocket(); FailAll("已关闭"); }

        async Task<Result<T>> Call<T>(string cmd, object data)
        {
            if (!reconnecting && !await Connect()) return Result<T>.Fail(Code.Network, "连不上服务器");
            var tcs = new TaskCompletionSource<Envelope>();
            int id = ++seq;
            var req = new Envelope { seq = id, cmd = cmd, data = data == null ? null : JToken.FromObject(data) };
            pending[id] = new Pending { Req = req, Tcs = tcs, Deadline = Time.unscaledTime + RequestTimeout };
            SendRaw(req);                                    // 重连中 Connected=false，不发；连上后统一补发
            var e = await tcs.Task;
            if (e.code != Code.Ok) return Result<T>.Fail(e.code, e.msg);
            try { return Result<T>.Success(e.data == null ? default(T) : e.data.ToObject<T>()); }
            catch (Exception ex) { return Result<T>.Fail(Code.Protocol, "回包格式错误：" + ex.Message); }
        }

        public async Task<Result<LoginResp>> Login(LoginReq req)
        {
            kicked = false;
            var r = await Call<LoginResp>(req.register ? Cmd.Register : Cmd.Login, req);
            if (r.Ok && r.data != null) session = r.data.session;       // 之后断线就靠它恢复
            return r;
        }
        public Task<Result<RoleListResp>> RoleList(string server) { return Call<RoleListResp>(Cmd.RoleList, new RoleListReq { server = server }); }
        public Task<Result<RoleCreateResp>> RoleCreate(string server, string name, string cls) { return Call<RoleCreateResp>(Cmd.RoleCreate, new RoleCreateReq { server = server, name = name, cls = cls }); }
        public Task<Result<Empty>> RoleDelete(string roleId) { return Call<Empty>(Cmd.RoleDelete, new RoleDeleteReq { roleId = roleId }); }
        public Task<Result<EnterResp>> EnterGame(string roleId, bool resume = false) { return Call<EnterResp>(Cmd.EnterGame, new EnterReq { roleId = roleId, resume = resume }); }
        public Task<Result<MoveResp>> Move(MoveReq req) { return Call<MoveResp>(Cmd.Move, req); }
        public Task<Result<MapChangeResp>> Portal(string from, string to) { return Call<MapChangeResp>(Cmd.Portal, new PortalReq { from = from, to = to }); }
        public Task<Result<MapChangeResp>> Teleport(string to, string npc = null) { return Call<MapChangeResp>(Cmd.Teleport, new TeleportReq { to = to, npc = npc }); }
        public Task<Result<SectResp>> SectJoin(string sect) { return Call<SectResp>(SectCmd.Join, new SectReq { sect = sect }); }
        public Task<Result<SectResp>> SectHall(string hall) { return Call<SectResp>(SectCmd.Hall, new SectReq { hall = hall }); }
        public Task<Result<MapChangeResp>> Tower(string op) { return Call<MapChangeResp>("tower." + op, null); }
        public Task<Result<QuestTalkResp>> QuestTalk(string npc) { return Call<QuestTalkResp>(QuestCmd.Talk, new QuestTalkReq { npc = npc }); }
        public Task<Result<BagResp>> BagOp(string cmd, BagOpReq req) { return Call<BagResp>(cmd, req ?? new BagOpReq()); }
        public Task<Result<BattleStartResp>> BattleStart(string stageId) { return Call<BattleStartResp>(Cmd.BattleStart, new BattleStartReq { stage = stageId }); }
        public Task<Result<GuiQin.Chat.ChatResp>> ChatSend(GuiQin.Chat.ChatSendReq req) { return Call<GuiQin.Chat.ChatResp>(GuiQin.Chat.ChatCmd.Send, req); }
        public Task<Result<GuiQin.Chat.ChatHistResp>> ChatHistory() { return Call<GuiQin.Chat.ChatHistResp>(GuiQin.Chat.ChatCmd.Hist, null); }
        public Task<Result<ChatResp>> ChatGuild(bool join) => Call<ChatResp>(ChatCmd.Guild, new ChatGuildReq { join = join });
        public Task<Result<FriendResp>> FriendList() => Call<FriendResp>(Cmd.FriendList, null);
        public Task<Result<FriendResp>> FriendAdd(string peer) => Call<FriendResp>(Cmd.FriendAdd, new ChatPeerReq { peer = peer });
        public Task<Result<FriendResp>> FriendDel(string peer) => Call<FriendResp>(Cmd.FriendDel, new ChatPeerReq { peer = peer });
        public Task<Result<PartyResp>> PartyInvite(string peer) => Call<PartyResp>(Cmd.PartyInvite, new ChatPeerReq { peer = peer });
        public Task<Result<PartyResp>> PartyReply(string peer, bool accept) => Call<PartyResp>(Cmd.PartyReply, new PartyReplyReq { peer = peer, accept = accept });
        public Task<Result<PartyResp>> PartyLeave() => Call<PartyResp>(Cmd.PartyLeave, null);
        public Task<Result<GuiQin.Train.TrainResp>> Train(string who, string id) { return Call<GuiQin.Train.TrainResp>(GuiQin.Train.TrainCmd.Do, new GuiQin.Train.TrainReq { who = who, id = id }); }
        public Task<Result<GuiQin.Pets.PetResp>> PetOp(string cmd, string id) { return Call<GuiQin.Pets.PetResp>(cmd, new GuiQin.Pets.PetReq { id = id }); }
        public Task<Result<BattleStartResp>> BattleResume() { return Call<BattleStartResp>(Cmd.BattleResume, null); }
        public Task<Result<BattleRoundResp>> BattleAct(string battleId, BattleCmd cmd) { return Call<BattleRoundResp>(Cmd.BattleCmd, new BattleCmdReq { battleId = battleId, cmd = cmd }); }
    }
}
