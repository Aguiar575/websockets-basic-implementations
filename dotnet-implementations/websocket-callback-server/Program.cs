using System.Net;
using System.Net.WebSockets;

public class WebSocketServer
{
    private const int BUFFER_SIZE = 4096;
    private byte[] receiveBuffer;
    private HttpListener httpListener;
    private CancellationTokenSource cancellationTokenSource;

    public WebSocketServer()
    {
        receiveBuffer = new byte[BUFFER_SIZE];
        cancellationTokenSource = new CancellationTokenSource();
        httpListener = new HttpListener();
    }
    public async Task StartServer(string url)
    {
        httpListener.Prefixes.Add(url);
        httpListener.Start();

        Console.WriteLine("WebSocket server started.");

        while (!cancellationTokenSource.Token.IsCancellationRequested)
        {
            HttpListenerContext context = await httpListener.GetContextAsync();
            
            if (context.Request.IsWebSocketRequest)
                await HandleRequest(context);
            else
            {
                context.Response.StatusCode = 400;
                context.Response.Close();
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        WebSocketContext webSocketContext;

        try
        {
            webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
            await HandleConnection(webSocketContext.WebSocket);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.Close();
            Console.WriteLine($"WebSocket request error: {ex.Message}");
        }
    }

    private async Task HandleConnection(WebSocket webSocket)
    {

        WebSocketReceiveResult receiveResult = 
            await webSocket.ReceiveAsync(
                new ArraySegment<byte>(receiveBuffer),
                cancellationTokenSource.Token);

        while (!receiveResult.CloseStatus.HasValue)
        {
            byte[] data;
            string message;
            ParseMessage(receiveResult, out data, out message);

            Console.WriteLine($"Received message: {message}");

            await SendMessageBack(webSocket, receiveResult, data);

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(receiveBuffer),
                cancellationTokenSource.Token);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            cancellationTokenSource.Token);
    }

    private void ParseMessage(
        WebSocketReceiveResult receiveResult,
        out byte[] data,
        out string message)
    {
        data = new byte[receiveResult.Count];
        Array.Copy(receiveBuffer, data, receiveResult.Count);
        message = System.Text.Encoding.UTF8.GetString(data);
    }

    private async Task SendMessageBack(
        WebSocket webSocket,
        WebSocketReceiveResult receiveResult,
        byte[] data)
    {
        await webSocket.SendAsync(
            new ArraySegment<byte>(data, 0, receiveResult.Count),
            WebSocketMessageType.Text,
            true,
            cancellationTokenSource.Token);
    }
}

public class Program
{
    public static async Task Main(string[] args)
    {
        string WebSocketUrl = "http://localhost:8080/";

        WebSocketServer server = new WebSocketServer();
        await server.StartServer(WebSocketUrl);
    }
}
