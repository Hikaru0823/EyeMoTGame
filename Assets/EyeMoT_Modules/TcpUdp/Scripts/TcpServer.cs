using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using UnityEngine;

public class TcpServer : IServer
{
    private readonly int _port;
    private TcpListener _listener;
    private CancellationTokenSource _cts;
    private int _nextClientId = 0;

    public class ClientConnection
    {
        public int Id;
        public TcpClient Client;
        public StreamReader Reader;
        public StreamWriter Writer;
        public Task ReceiveTask;
    }

    public event Action<ClientConnection> ClientConnected;
    public event Action<ClientConnection> ClientDisconnected;
    public event Action<IPEndPoint, string> MessageReceived;
    public event Action<Exception> Error;

    public int Port { get { return _port; } }

    public TcpServer(int port)
    {
        _port = port;
    }

    public void StartServer()
    {
        if (_listener != null) return;

        _cts = new CancellationTokenSource();
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();

        // 接続待ちループ
        _ = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();

        try { _listener?.Stop(); } catch { }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
    }

    public void Tick()
    {
        // while (_clientConnectQueue.TryDequeue(out var conn))
        // {
        //     ClientConnected?.Invoke(conn);
        // }    
        // while (_clientDisconnectQueue.TryDequeue(out var conn))
        // {
        //     ClientDisconnected?.Invoke(conn);
        // }
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                var stream = client.GetStream();
                var reader = new StreamReader(stream, Encoding.UTF8);
                var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
                var id = _nextClientId;
                var conn = new ClientConnection
                {
                    Id = id,
                    Client = client,
                    Reader = reader,
                    Writer = writer
                };

                ClientConnected?.Invoke(conn);

                conn.ReceiveTask = Task.Run(() => ReceiveLoopAsync(conn, token));
                _nextClientId++;
            }
        }
        catch (ObjectDisposedException)
        {
            // Stop() された場合
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Error?.Invoke(ex);
        }
    }

    private async Task ReceiveLoopAsync(ClientConnection conn, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await conn.Reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null) break;

                var ep = conn.Client.Client.RemoteEndPoint as IPEndPoint;
                MessageReceived?.Invoke(ep, line);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Error?.Invoke(ex);
        }
        finally
        {
            DisconnectClient(conn);
        }
    }

    private void DisconnectClient(ClientConnection conn)
    {
        try { conn.Reader?.Dispose(); } catch { }
        try { conn.Writer?.Dispose(); } catch { }
        try { conn.Client?.Close(); } catch { }

        ClientDisconnected?.Invoke(conn);
    }

    public async Task BroadcastAsync(List<ClientSession> targetClients, string message)
    {
        foreach (var c in targetClients)
        {
            try
            {
                await c.Tcp.Writer.WriteLineAsync(message);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
        }
    }

    public async Task SendToClientAsync(ClientSession session, string message)
    {
        try
        {
            await session.Tcp.Writer.WriteLineAsync(message);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }
}
