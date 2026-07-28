using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;
public class ServerManager : MonoBehaviour
{
    public Dictionary<int, ClientSession> Clients = new Dictionary<int, ClientSession>();

    public event Action<TcpServer.ClientConnection> ClientConnected;
    public event Action<TcpServer.ClientConnection> ClientDisconnected;
    public event Action<IPEndPoint, string> TcpMessageReceived;
    public event Action<Exception> TcpError;
    public event Action<IPEndPoint, string> UdpMessageReceived;
    public event Action<Exception> UdpError;
    public IServer TcpServer { get { return _tcpServer; } }
    public IServer UdpServer { get { return _udpServer; } }

    private IServer _tcpServer;
    private IServer _udpServer;
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    public void EnqueueMainThread(Action action)
    {
        if (action == null) return;
        _mainThreadQueue.Enqueue(action);
    }

    public void RemoveAllListeners()
    {
        ClientConnected = null;
        ClientDisconnected = null;
        TcpMessageReceived = null;
        TcpError = null;
        UdpMessageReceived = null;
        UdpError = null;
    }

    public void AddListener(IServerCallbacks server)
    {
        RemoveListener(server); // Ensure no duplicate subscriptions
        
        ClientConnected += server.OnClientConnected;
        ClientDisconnected += server.OnClientDisconnected;
        TcpMessageReceived += server.OnTcpReceived;
        TcpError += server.OnTcpError;
        UdpMessageReceived += server.OnUdpReceived;
        UdpError += server.OnUdpError;
    }

    public void RemoveListener(IServerCallbacks server)
    {
        ClientConnected -= server.OnClientConnected;
        ClientDisconnected -= server.OnClientDisconnected;
        TcpMessageReceived -= server.OnTcpReceived;
        TcpError -= server.OnTcpError;
        UdpMessageReceived -= server.OnUdpReceived;
        UdpError -= server.OnUdpError;
    }

    public void InitializeTcp(IServer server)
    {
        if (_tcpServer != null)
        {
            (_tcpServer as TcpServer).ClientConnected -= OnTcpClientConnected;
            (_tcpServer as TcpServer).ClientDisconnected -= OnTcpClientDisconnected;
            _tcpServer.MessageReceived -= OnTcpMessageReceived;
            _tcpServer.Error -= OnTcpError;
        }

        _tcpServer = server;

        if (_tcpServer  != null)
        {
            (_tcpServer as TcpServer).ClientConnected += OnTcpClientConnected;
            (_tcpServer as TcpServer).ClientDisconnected += OnTcpClientDisconnected;
            _tcpServer.MessageReceived += OnTcpMessageReceived;
            _tcpServer.Error += OnTcpError;
        }
    }

    public void InitializeUdp(IServer server)
    {
        if (_udpServer != null)
        {
            _udpServer.MessageReceived -= OnUdpMessageReceived;
            _udpServer.Error -= OnUdpError;
        }

        _udpServer = server;

        if (_udpServer != null)
        {
            _udpServer.MessageReceived += OnUdpMessageReceived;
            _udpServer.Error += OnUdpError;
        }
    }

    public void StartTcp()
    {
        if (_tcpServer == null)
        {
            Debug.LogError("TCP Server is not initialized.");
            return;
        }
        _tcpServer.StartServer();
    }

    public void StartUdp()
    {
        if (_udpServer == null)
        {
            Debug.LogError("UDP Server is not initialized.");
            return;
        }
        _udpServer.StartServer();
    }

    public void Stop()
    {
        _tcpServer?.Stop();
        _udpServer?.Stop();
    }

    public void OnTcpClientConnected(TcpServer.ClientConnection client)
    {
        if(!Clients.ContainsKey(client.Id))
        {
            Clients.Add(client.Id, new ClientSession
            {
                Id = client.Id,
                Tcp = client,
                Udp = null
            });
        }
        EnqueueMainThread(() => ClientConnected?.Invoke(client));
    }

    public void OnTcpClientDisconnected(TcpServer.ClientConnection client)
    {
        EnqueueMainThread(() => ClientDisconnected?.Invoke(client));
        if(Clients.ContainsKey(client.Id))
        {
            Clients.Remove(client.Id);
        }
    }

    public async void OnTcpMessageReceived(IPEndPoint sender, string message)
    {
        EnqueueMainThread(() => TcpMessageReceived?.Invoke(sender, message));
        await SendMessage(_tcpServer, message, sender);
    }

    public async void OnUdpMessageReceived(IPEndPoint sender, string message)
    {
        EnqueueMainThread(() => UdpMessageReceived?.Invoke(sender, message));

        // OSCにはNetMessageのTargetIdがないため、JSON中継処理には渡さない。
        if (message.StartsWith("/", StringComparison.Ordinal))
            return;

        await SendMessage(_udpServer, message, sender);
    }

    public async void SendTcp(string message)
    {
        await SendMessage(_tcpServer, message);
    }

    public async void SendUdp(string message)
    {
        await SendMessage(_udpServer, message);
    }

    public async Task SendMessage(IServer server, string message, IPEndPoint sender = null)
    {
        var header = NetJson.FromJson<NetMessage<object>>(message);
        switch (header.TargetId)
        {
            case -1:
                // サーバー宛のメッセージは無視する
                break;
            case -2:
                List<ClientSession> snapshot;
                lock (Clients)
                {
                    snapshot = new List<ClientSession>(Clients.Values);
                }
                await server.BroadcastAsync(snapshot, message);
                break;
            default:
                ClientSession session;
                lock(Clients)
                {
                    if(!Clients.TryGetValue(header.TargetId, out session))
                    {
                        Debug.LogWarning($"[Server] Target client {header.TargetId} not found or has TCP connection.");
                        return;
                    }
                }
                await server.SendToClientAsync(session, message);
                break;
        }
    }

    public void OnTcpError(Exception ex)
    {
        EnqueueMainThread(() => TcpError?.Invoke(ex));
    }

    public void OnUdpError(Exception ex)
    {
        EnqueueMainThread(() => UdpError?.Invoke(ex));
    }

    void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    void OnDestroy()
    {
        Stop();
    }
}

public class ClientSession
{
    public int Id;
    public TcpServer.ClientConnection Tcp;   // null あり
    public System.Net.IPEndPoint Udp; // null あり
}
