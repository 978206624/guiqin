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
builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, port));
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
    var cancellation = context.RequestAborted;
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
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "", cancellation);
                    return;
                }
                if (frame.MessageType != WebSocketMessageType.Text || message.Length + frame.Count > 65536)
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.InvalidPayloadData, "无效消息", cancellation);
                    return;
                }
                message.Write(buffer, 0, frame.Count);
            } while (!frame.EndOfMessage);

            var request = JsonConvert.DeserializeObject<Envelope>(Encoding.UTF8.GetString(message.ToArray()));
            if (request == null || request.seq <= 0 || string.IsNullOrEmpty(request.cmd))
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.InvalidPayloadData, "无效命令", cancellation);
                return;
            }
            var response = gateway.Handle(state, request);
            var output = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(response));
            await socket.SendAsync(new ArraySegment<byte>(output), WebSocketMessageType.Text, true, cancellation);
        }
    }
    catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException)
    {
        // 断线或无效帧由连接层终止，角色和战斗保留在会话中。
    }
});
app.Run();
