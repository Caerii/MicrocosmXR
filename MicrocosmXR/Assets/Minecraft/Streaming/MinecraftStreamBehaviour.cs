using UnityEngine;

namespace MicrocosmXR.MinecraftStreaming
{
    /// <summary>
    /// MonoBehaviour that connects to the Minecraft stream server and exposes the ChunkStreamClient.
    /// Add to a GameObject and set Host/Port in the Inspector (or in code). Connect on Start or via UI.
    /// </summary>
    public class MinecraftStreamBehaviour : MonoBehaviour
    {
        [Tooltip("Server host (e.g. localhost or your PC's LAN IP when running on Quest).")]
        public string host = "localhost";
        [Tooltip("WebSocket port (default 25566).")]
        public int port = Protocol.DefaultPort;
        [Tooltip("Connect automatically on Start.")]
        public bool connectOnStart = true;

        readonly MinecraftStreamConnection _connection = new MinecraftStreamConnection();

        public ChunkStreamClient Client => _connection.Client;
        public bool IsConnected => _connection.IsConnected;

        public event System.Action Connected { add => _connection.Connected += value; remove => _connection.Connected -= value; }
        public event System.Action Disconnected { add => _connection.Disconnected += value; remove => _connection.Disconnected -= value; }
        public event System.Action<string> Error { add => _connection.Error += value; remove => _connection.Error -= value; }

        void Start()
        {
            _connection.Connected += OnConnected;
            _connection.Disconnected += OnDisconnected;
            _connection.Error += OnError;

            if (connectOnStart && !string.IsNullOrEmpty(host))
                Connect();
        }

        void OnDestroy()
        {
            _connection.Connected -= OnConnected;
            _connection.Disconnected -= OnDisconnected;
            _connection.Error -= OnError;
            _connection.Disconnect();
        }

        public void Connect()
        {
            _connection.Connect(host, port);
        }

        public void Connect(string host, int port)
        {
            this.host = host;
            this.port = port;
            _connection.Connect(host, port);
        }

        public void Disconnect()
        {
            _connection.Disconnect();
        }

        void OnConnected()
        {
            Debug.Log("[MinecraftStream] Connected to " + _connection.ServerUri);
        }

        void OnDisconnected()
        {
            Debug.Log("[MinecraftStream] Disconnected.");
        }

        void OnError(string message)
        {
            Debug.LogWarning("[MinecraftStream] Error: " + message);
        }
    }
}
