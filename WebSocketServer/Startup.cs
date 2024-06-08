#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace WebSocketServer
{
    public class Startup
    {
        private static ConcurrentDictionary<string, WebSocket> _clients = new ConcurrentDictionary<string, WebSocket>();
        private static IHostApplicationLifetime _applicationLifetime = default!;
        private readonly IConfiguration _configuration;

        public Startup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            // 他のサービスの登録があればここに追加
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime appLifetime)
        {
            _applicationLifetime = appLifetime;

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120)
            };

            app.UseWebSockets(webSocketOptions);

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        string clientId = Guid.NewGuid().ToString();
                        _clients.TryAdd(clientId, webSocket);

                        // クライアントにIDを送信
                        var clientIdMessage = new Message { ClientId = clientId };
                        var clientIdBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(clientIdMessage, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                        await webSocket.SendAsync(new ArraySegment<byte>(clientIdBuffer), WebSocketMessageType.Text, true, CancellationToken.None);

                        await HandleWebSocketAsync(webSocket, clientId);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else if (context.Request.Path == "/clients")
                {
                    var clientIds = JsonSerializer.Serialize(_clients.Keys);
                    await context.Response.WriteAsync(clientIds);
                }
                else
                {
                    await next();
                }
            });

            app.UseRouting();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/", async context =>
                {
                    await context.Response.WriteAsync("WebSocket server running...");
                });
            });

            // メインスレッドでendコマンドを処理
            Task.Run(() => MonitorEndCommand());
        }

        private static async Task MonitorEndCommand()
        {
            while (true)
            {
                var input = Console.ReadLine();
                if (input == "end")
                {
                    Console.WriteLine("Shutting down server.");
                    _applicationLifetime.StopApplication();
                    break;
                }
            }
        }

        private async Task HandleWebSocketAsync(WebSocket webSocket, string clientId)
        {
            var buffer = new byte[1024 * 4];
            try
            {
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                while (!result.CloseStatus.HasValue)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    Console.WriteLine($"Received from {clientId}: {message}");

                    if (message.Trim().Equals("end", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Received 'end' message. Shutting down server.");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None);
                        _clients.TryRemove(clientId, out _);
                        Environment.Exit(0); // サーバーを終了
                    }
                    else
                    {
                        var parsedMessage = JsonSerializer.Deserialize<Message>(message);
                        if (parsedMessage != null && parsedMessage.Content != null && !string.IsNullOrEmpty(parsedMessage.TargetClientId))
                        {
                            await SendMessageToClient(parsedMessage.TargetClientId, parsedMessage.Content, clientId);
                        }
                    }

                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                }
            }
            catch (WebSocketException ex)
            {
                Console.WriteLine($"WebSocketException: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(clientId, out _);
                if (webSocket.State != WebSocketState.Closed)
                {
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by the server", CancellationToken.None);
                }
            }
        }

        private async Task SendMessageToClient(string targetClientId, Dictionary<string, string>? messages, string senderClientId)
        {
            if (messages == null)
            {
                return;
            }

            if (_clients.TryGetValue(targetClientId, out WebSocket? targetWebSocket) && targetWebSocket.State == WebSocketState.Open)
            {
                var messageBuffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Message { Content = messages }, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
                await targetWebSocket.SendAsync(new ArraySegment<byte>(messageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
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
