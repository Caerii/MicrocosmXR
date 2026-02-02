using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// Connects to the Fabric mod WebSocket server and feeds received messages into ChunkStreamClient.
    /// Uses System.Net.WebSockets.ClientWebSocket (works in Editor and Standalone; on Quest/Android
    /// you may need a different transport, e.g. NativeWebSocket, and feed the same ChunkStreamClient).
    /// </summary>
    public class MinecraftStreamConnection
    {
        public ChunkStreamClient Client { get; } = new ChunkStreamClient();
        public bool IsConnected => _ws?.State == WebSocketState.Open;
        public string ServerUri { get; private set; }

        ClientWebSocket _ws;
        CancellationTokenSource _cts;
        Task _receiveTask;

        public event Action Connected;
        public event Action Disconnected;
        public event Action<string> Error;

        public void Connect(string host, int port = Protocol.DefaultPort, bool useTls = false)
        {
            string scheme = useTls ? "wss" : "ws";
            ServerUri = $"{scheme}://{host}:{port}";
            Connect(new Uri(ServerUri));
        }

        public async void Connect(Uri uri)
        {
            if (_ws != null)
            {
                Disconnect();
            }

            ServerUri = uri?.ToString();
            _ws = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            try
            {
                await _ws.ConnectAsync(uri, _cts.Token);
                Connected?.Invoke();
                _receiveTask = ReceiveLoop(_cts.Token);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex.Message);
                Disconnected?.Invoke();
            }
        }

        public void Disconnect()
        {
            _cts?.Cancel();
            try
            {
                _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None).GetAwaiter().GetResult();
            }
            catch { }
            _ws?.Dispose();
            _ws = null;
            _cts?.Dispose();
            _cts = null;
            Disconnected?.Invoke();
        }

        async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[64 * 1024];
            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var segment = new ArraySegment<byte>(buffer);
                    var result = await _ws.ReceiveAsync(segment, ct);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Disconnect();
                        break;
                    }
                    if (result.Count == 0) continue;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        ChunkStreamParser.ParseText(text, Client);
                    }
                    else if (result.MessageType == WebSocketMessageType.Binary)
                    {
                        var payload = new byte[result.Count];
                        Array.Copy(buffer, 0, payload, 0, result.Count);
                        ChunkStreamParser.ParseBinary(payload, Client);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Error?.Invoke(ex.Message);
            }
            finally
            {
                if (_ws?.State == WebSocketState.Open)
                    Disconnect();
            }
        }
    }
}
