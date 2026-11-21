using System.Net;
using System.Net.WebSockets;
using System.Text;
using GuiQin;
using GuiQin.Net;
using GuiQin.Server;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("Game")
    ?? throw new InvalidOperationException("请设置 ConnectionStrings__Game。");
var port = builder.Configuration.GetValue("GuiQin:Port", 5090);
IPAddress lan = null;
var lanAddress = builder.Configuration["GuiQin:LanAddress"];
if (!string.IsNullOrWhiteSpace(lanAddress) &&
    (!IPAddress.TryParse(lanAddress, out lan) || lan.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork ||
     lan.Equals(IPAddress.Any) || lan.Equals(IPAddress.Broadcast) || IPAddress.IsLoopback(lan)))
    throw new InvalidOperationException("GuiQin:LanAddress 必须是电脑网卡上的局域网 IPv4 地址。");
builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(IPAddress.Loopback, port);
    if (lan != null) options.Listen(lan, port);
});
var directory = Path.Combine(AppContext.BaseDirectory, "Data");
GameData.Load(new FileDataSource(directory));
var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(directory, "data.json"))));
builder.Services.AddSingleton(new GameGateway(new GameStore(connection), hash));
var app = builder.Build();
app.UseWebSockets();
app.MapGet("/health", () => Results.Ok(new { ok = true }));
app.Map("/ws", async (HttpContext context, GameGateway gateway) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = 400;
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var state = new ConnectionState();
    var buffer = new byte[8192];
    using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
    using var sends = new SemaphoreSlim(1);
    var cancellation = lifetime.Token;

    async Task Send(Envelope message)
    {
        var output = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message));
        await sends.WaitAsync(cancellation);
        try { await socket.SendAsync(new ArraySegment<byte>(output), WebSocketMessageType.Text, true, cancellation); }
        finally { sends.Release(); }
    }

    async Task Close(WebSocketCloseStatus status, string message)
    {
        state.Pushes.Writer.TryComplete();
        await sends.WaitAsync(cancellation);
        try { await socket.CloseOutputAsync(status, message, cancellation); }
        finally { sends.Release(); }
    }

    async Task Push()
    {
        try
        {
            await foreach (var message in state.Pushes.Reader.ReadAllAsync(cancellation))
                if (socket.State == WebSocketState.Open) await Send(message);
        }
        catch (Exception error) when (error is WebSocketException or OperationCanceledException)
        {
            lifetime.Cancel();
        }
    }
    var pushes = Push();
    try
    {
        while (socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult frame;
            do
            {
                frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellation);
                if (frame.MessageType == WebSocketMessageType.Close)
                {
                    await Close(WebSocketCloseStatus.NormalClosure, "");
                    return;
                }
                if (frame.MessageType != WebSocketMessageType.Text || message.Length + frame.Count > 65536)
                {
                    await Close(WebSocketCloseStatus.InvalidPayloadData, "无效消息");
                    return;
                }
                message.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);

            var request = JsonConvert.DeserializeObject<Envelope>(Encoding.UTF8.GetString(message.ToArray()));
            if (request == null || request.seq <= 0 || string.IsNullOrEmpty(request.cmd))
            {
                await Close(WebSocketCloseStatus.InvalidPayloadData, "无效命令");
                return;
            }
            var response = gateway.Handle(state, request);
            await Send(response);
        }
    }
    catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException)
    {
        // 断线或无效帧由连接层终止，角色和战斗保留在会话中。
    }
    finally
    {
        state.Pushes.Writer.TryComplete();
        lifetime.Cancel();
        await pushes;
    }
});
app.Run();
