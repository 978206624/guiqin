using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GuiQin.Net
{
    // WebSocket 长连接实现（骨架）：请求/应答按 seq 对应，服务端推送走 OnPush，15 秒一次心跳，8 秒请求超时。
    // 注意：System.Net.WebSockets 在 WebGL 构建里不可用（要换浏览器 WebSocket 插件）；iOS / Android / PC 可用。
    // 还没做的（见 docs/BACKEND-DESIGN.md 第 3 节）：断线自动重连 + 会话续传、消息压缩、protobuf。
    public class WsBackend : IGameBackend
    {
        class Pending { public TaskCompletionSource<Envelope> Tcs; public float Deadline; }

        readonly string url;
        ClientWebSocket ws; CancellationTokenSource cts;
        readonly ConcurrentQueue<string> inbox = new ConcurrentQueue<string>();
        readonly ConcurrentQueue<string> events = new ConcurrentQueue<string>();      // 来自接收线程的断线通知
        readonly Dictionary<int, Pending> pending = new Dictionary<int, Pending>();
        int seq; float nextPing;
        const float RequestTimeout = 8f, PingEvery = 15f;

        public bool Connected { get { return ws != null && ws.State == WebSocketState.Open; } }
        public event Action<string, JToken> OnPush;
        public event Action<string> OnDisconnected;
        public long ServerTimeOffsetMs;            // 服务端时间 - 本机时间，用来对齐倒计时 / 天气到期

        public WsBackend(string url) { this.url = url; }

        async Task<bool> Connect()
        {
            if (Connected) return true;
            try
            {
                cts = new CancellationTokenSource(); ws = new ClientWebSocket();
                await ws.ConnectAsync(new Uri(url), cts.Token);
                var _ = Task.Run(ReceiveLoop);
                nextPing = Time.unscaledTime + PingEvery;
                return true;
            }
            catch (Exception e) { Debug.LogWarning("连接失败：" + e.Message); return false; }
        }

        async Task ReceiveLoop()
        {
            var buf = new byte[8192];
            try
            {
                while (ws.State == WebSocketState.Open && !cts.IsCancellationRequested)
                {
                    var ms = new MemoryStream(); WebSocketReceiveResult r;
                    do
                    {
                        r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                        if (r.MessageType == WebSocketMessageType.Close) { events.Enqueue("服务器关闭了连接"); return; }
                        ms.Write(buf, 0, r.Count);
                    } while (!r.EndOfMessage);
                    inbox.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                }
            }
            catch (Exception e) { if (!cts.IsCancellationRequested) events.Enqueue("连接中断：" + e.Message); }
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
            string s;
            while (inbox.TryDequeue(out s))
            {
                Envelope e;
                try { e = JsonConvert.DeserializeObject<Envelope>(s); } catch { continue; }
                if (e == null) continue;
                if (e.ts > 0) ServerTimeOffsetMs = e.ts - DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                Pending p;
                if (e.seq > 0 && pending.TryGetValue(e.seq, out p)) { pending.Remove(e.seq); p.Tcs.TrySetResult(e); }
                else if (e.seq == 0 && OnPush != null) OnPush(e.cmd, e.data);
            }
            float now = Time.unscaledTime;
            List<int> dead = null;
            foreach (var kv in pending) if (now > kv.Value.Deadline) (dead ?? (dead = new List<int>())).Add(kv.Key);
            if (dead != null) foreach (var k in dead) { var p = pending[k]; pending.Remove(k); p.Tcs.TrySetResult(new Envelope { code = Code.Timeout, msg = "请求超时" }); }
            while (events.TryDequeue(out s)) { FailAll(s); if (OnDisconnected != null) OnDisconnected(s); }
            if (Connected && now >= nextPing) { nextPing = now + PingEvery; SendRaw(new Envelope { seq = 0, cmd = Cmd.Ping }); }
        }

        void FailAll(string why)
        {
            foreach (var kv in pending) kv.Value.Tcs.TrySetResult(new Envelope { code = Code.Network, msg = why });
            pending.Clear();
        }

        public void Close() { try { if (cts != null) cts.Cancel(); if (ws != null) ws.Dispose(); } catch { } ws = null; }

        async Task<Result<T>> Call<T>(string cmd, object data)
        {
            if (!await Connect()) return Result<T>.Fail(Code.Network, "连不上服务器");
            var tcs = new TaskCompletionSource<Envelope>();
            int id = ++seq;
            pending[id] = new Pending { Tcs = tcs, Deadline = Time.unscaledTime + RequestTimeout };
            SendRaw(new Envelope { seq = id, cmd = cmd, data = data == null ? null : JToken.FromObject(data) });
            var e = await tcs.Task;
            if (e.code != Code.Ok) return Result<T>.Fail(e.code, e.msg);
            try { return Result<T>.Success(e.data == null ? default(T) : e.data.ToObject<T>()); }
            catch (Exception ex) { return Result<T>.Fail(Code.Protocol, "回包格式错误：" + ex.Message); }
        }

        public Task<Result<LoginResp>> Login(LoginReq req) { return Call<LoginResp>(Cmd.Login, req); }
        public Task<Result<EnterResp>> EnterGame(string roleId) { return Call<EnterResp>(Cmd.EnterGame, new EnterReq { roleId = roleId }); }
        public Task<Result<MoveResp>> Move(MoveReq req) { return Call<MoveResp>(Cmd.Move, req); }
        public Task<Result<MapChangeResp>> Portal(string from, string to) { return Call<MapChangeResp>(Cmd.Portal, new PortalReq { from = from, to = to }); }
        public Task<Result<MapChangeResp>> Teleport(string to) { return Call<MapChangeResp>(Cmd.Teleport, new TeleportReq { to = to }); }
        public Task<Result<Empty>> SetLook(string cls) { return Call<Empty>(Cmd.SetLook, new LookReq { cls = cls }); }
    }
}
