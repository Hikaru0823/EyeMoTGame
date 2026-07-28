using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;

public class ClientManager : MonoBehaviour
{
    public static ClientManager Instance;
    public int Idx;
    private INetworkClient _tcpClient;
    private INetworkClient _udpClient;

    // Reliable用イベント
    public event Action TcpConnected;
    public event Action TcpDisconnected;
    public event Action<string> TcpMessageReceived;
    public event Action<Exception> TcpError;

    // Unreliable用イベント
    public event Action UdpConnected;
    public event Action UdpDisconnected;
    public event Action<string> UdpMessageReceived;
    public event Action<Exception> UdpError;

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
    }

    public void EnqueueMainThread(Action action)
    {
        if (action == null) return;
        _mainThreadQueue.Enqueue(action);
    }

    public void RemoveAllCallbacks()
    {
        TcpConnected = null;
        TcpDisconnected = null;
        TcpMessageReceived = null;
        TcpError = null;

        UdpConnected = null;
        UdpDisconnected = null;
        UdpMessageReceived = null;
        UdpError = null;
    }

    public void AddCallbacks(IClientCallbacks callbacks)
    {
        RemoveCallbacks(callbacks); // Ensure no duplicate subscriptions

        TcpConnected += callbacks.OnTcpConnected;
        TcpDisconnected += callbacks.OnTcpDisconnected;
        TcpMessageReceived += callbacks.OnTcpMessageReceived;
        TcpError += callbacks.OnTcpError;

        UdpConnected += callbacks.OnUdpConnected;
        UdpDisconnected += callbacks.OnUdpDisconnected;
        UdpMessageReceived += callbacks.OnUdpReceived;
        UdpError += callbacks.OnUdpError;
    }

    public void RemoveCallbacks(IClientCallbacks callbacks)
    {
        TcpConnected -= callbacks.OnTcpConnected;
        TcpDisconnected -= callbacks.OnTcpDisconnected;
        TcpMessageReceived -= callbacks.OnTcpMessageReceived;
        TcpError -= callbacks.OnTcpError;

        UdpConnected -= callbacks.OnUdpConnected;
        UdpDisconnected -= callbacks.OnUdpDisconnected;
        UdpMessageReceived -= callbacks.OnUdpReceived;
        UdpError -= callbacks.OnUdpError;
    }

    public void InitializeTcp(INetworkClient client)
    {
        if (_tcpClient != null)
        {
            _tcpClient.Connected -= OnTcpConnected;
            _tcpClient.Disconnected -= OnTcpDisconnected;
            _tcpClient.MessageReceived -= OnTcpMessageReceived;
            _tcpClient.Error -= OnTcpError;
        }

        _tcpClient = client;

        if (_tcpClient != null)
        {
            _tcpClient.Connected += OnTcpConnected;
            _tcpClient.Disconnected += OnTcpDisconnected;
            _tcpClient.MessageReceived += OnTcpMessageReceived;
            _tcpClient.Error += OnTcpError;
        }
    }

    public void InitializeUdp(INetworkClient client)
    {
        if (_udpClient != null)
        {
            _udpClient.Connected -= OnUdpConnected;
            _udpClient.Disconnected -= OnUdpDisconnected;
            _udpClient.MessageReceived -= OnUdpMessageReceived;
            _udpClient.Error -= OnUdpError;
        }

        _udpClient = client;

        if (_udpClient != null)
        {
            _udpClient.Connected += OnUdpConnected;
            _udpClient.Disconnected += OnUdpDisconnected;
            _udpClient.MessageReceived += OnUdpMessageReceived;
            _udpClient.Error += OnUdpError;
        }
    }

    public async Task ConnectTcpAsync()
    {
        if (_tcpClient == null)
            throw new InvalidOperationException("Tcp client not initialized.");

        await _tcpClient.ConnectAsync();
    }

    public async Task ConnectUdpAsync()
    {
        if (_udpClient == null)
            throw new InvalidOperationException("Udp client not initialized.");

        await _udpClient.ConnectAsync();
    }

    public void Disconnect() 
    {
        _tcpClient?.Disconnect();
        _udpClient?.Disconnect();
        Instance = null;
    }

    public void SendTcp(string message)
    {
        _tcpClient?.Send(message);
    }

    public void SendUdp(string message)
    {
        _udpClient?.Send(message);
    }

    public void Send(Channel ch, string message)
    {
        switch (ch)
        {
            case Channel.Reliable:
                SendTcp(message);
                break;
            case Channel.Unreliable:
                SendUdp(message);
                break;
        }
    }

    private void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            try { action.Invoke(); }
            catch (Exception ex) { Debug.LogException(ex); }
        }
    }

    public string TCPHost => _tcpClient?.Host ?? "0.0.0.0";
    public int TCPPort => _tcpClient?.Port ?? 0;
    public string UDPHost => _udpClient?.Host ?? "0.0.0.0";
    public int UDPPort => _udpClient?.Port ?? 0;

    private void OnTcpConnected() => EnqueueMainThread(() => TcpConnected?.Invoke());
    private void OnTcpDisconnected() => EnqueueMainThread(() => TcpDisconnected?.Invoke());
    private void OnTcpMessageReceived(string msg) => EnqueueMainThread(() => TcpMessageReceived?.Invoke(msg));
    private void OnTcpError(Exception ex) => EnqueueMainThread(() => TcpError?.Invoke(ex));

    private void OnUdpConnected() => EnqueueMainThread(() => UdpConnected?.Invoke());
    private void OnUdpDisconnected() => EnqueueMainThread(() => UdpDisconnected?.Invoke());
    private void OnUdpMessageReceived(string msg) => EnqueueMainThread(() => UdpMessageReceived?.Invoke(msg));
    private void OnUdpError(Exception ex) => EnqueueMainThread(() => UdpError?.Invoke(ex));

    public enum Channel
    {
        Reliable,
        Unreliable
    }

    public enum NetworkRole
    {
        None,
        Host,
        Client
    }
}

    public class HostInfo
    {
        public string Name;
        public string Address;
        public int TcpPort;
        public int UdpPort;
        public int Players; 
    }