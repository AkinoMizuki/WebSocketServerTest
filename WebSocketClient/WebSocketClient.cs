using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace WebSocketClient
{
    public class WebSocketClient
    {
        private static string clientId = string.Empty;
        private static bool endCommandReceived = false;

        public static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var serverSettings = config.GetSection("ServerSettings");
            var ipAddress = serverSettings["IPAddress"];
            var port = serverSettings["Port"];

            var serverUri = new Uri($"ws://{ipAddress}:{port}/ws");

            try
            {
                await ConnectWithRetryAsync(serverUri, 3);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"接続ができませんでした: {ex.Message}");
                await Task.Delay(3000); // 3秒間待機
            }
        }

        private static async Task ConnectWithRetryAsync(Uri serverUri, int maxRetries)
        {
            int attempt = 0;

            while (attempt < maxRetries && !endCommandReceived)
            {
                using (ClientWebSocket webSocket = new ClientWebSocket())
                {
                    try
                    {
                        await webSocket.ConnectAsync(serverUri, CancellationToken.None);
                        Console.WriteLine("Connected to WebSocket server");

                        // クライアントIDを受信
                        await ReceiveClientId(webSocket);

                        // メッセージ受信タスクを開始
                        Task receiving = ReceiveMessages(webSocket);

                        // メインスレッドでコマンド入力を処理
                        await ProcessInputCommands(webSocket);

                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None);
                        await receiving;
                        return;
                    }
                    catch (WebSocketException)
                    {
                        attempt++;
                        if (!endCommandReceived)
                        {
                            Console.WriteLine($"接続に失敗しました。再試行しています... ({attempt}/{maxRetries})");
                            await Task.Delay(2000);
                        }
                    }
                }
            }

            if (!endCommandReceived)
            {
                throw new WebSocketException("接続に3回失敗しました。");
            }
        }

        private static async Task ReceiveClientId(ClientWebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var receivedMessage = JsonSerializer.Deserialize<Message>(message);

            if (receivedMessage != null)
            {
                clientId = receivedMessage.ClientId;
                Console.WriteLine("Assigned Client ID: " + clientId);
            }
        }

        private static async Task ReceiveMessages(ClientWebSocket webSocket)
        {
            var buffer = new byte[1024 * 4];
            while (webSocket.State == WebSocketState.Open)
            {
                try
                {
                    WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine("Received: " + message);

                    if (message.Trim().Equals("end", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Received 'end' message. Closing connection.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
                        endCommandReceived = true;
                        break;
                    }
                }
                catch (WebSocketException ex)
                {
                    Console.WriteLine($"WebSocketException: {ex.Message}");
                    break;
                }
            }
            Console.WriteLine("Serverが切断されました。");
        }

        private static async Task ProcessInputCommands(ClientWebSocket webSocket)
        {
            while (true)
            {
                Console.WriteLine("Enter message in the format 'ClientID,Message' or 'ClientID,key1:value1,key2:value2,...', or 'cmd list' to list clients (or 'end' to quit):");
                string input = Console.ReadLine();
                if (input == "end")
                {
                    Console.WriteLine("Closing connection.");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client shutting down", CancellationToken.None);
                    endCommandReceived = true;
                    break;
                }

                if (input == "cmd list")
                {
                    await ListClients();
                    continue;
                }

                var parts = input.Split(new[] { ',' }, 2);
                if (parts.Length < 2)
                {
                    Console.WriteLine("Invalid input format. Please use 'ClientID,Message' or 'ClientID,key1:value1,key2:value2,...'.");
                    continue;
                }

                string targetClientId = parts[0];
                var messageContent = parts[1];

                Dictionary<string, string>? messages = null;

                if (messageContent.Contains(":"))
                {
                    var keyValuePairs = messageContent.Split(',');
                    messages = new Dictionary<string, string>();

                    foreach (var pair in keyValuePairs)
                    {
                        var keyValue = pair.Split(new[] { ':' }, 2);
                        if (keyValue.Length == 2)
                        {
                            messages.Add(keyValue[0], keyValue[1]);
                        }
                    }
                }
                else
                {
                    messages = new Dictionary<string, string> { { "message", messageContent } };
                }

                var structuredMessage = new Message
                {
                    TargetClientId = targetClientId,
                    Content = messages
                };

                var messageBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(structuredMessage, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                await webSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
            }
        }

        private static async Task ListClients()
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            var serverSettings = config.GetSection("ServerSettings");
            var ipAddress = serverSettings["IPAddress"];
            var port = serverSettings["Port"];

            using (HttpClient client = new HttpClient())
            {
                string clientIds = await client.GetStringAsync($"http://{ipAddress}:{port}/clients");
                Console.WriteLine("Connected Clients: " + clientIds);
            }
        }

        private class Message
        {
            public string ClientId { get; set; } = string.Empty;
            public string TargetClientId { get; set; } = string.Empty;
            public Dictionary<string, string>? Content { get; set; }
        }
    }
}
