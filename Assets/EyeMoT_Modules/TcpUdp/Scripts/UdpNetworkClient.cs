using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class UdpNetworkClient : INetworkClient
{
    private readonly string _host;
    public string Host { get { return _host; } }
    private readonly int _port;
    public int Port { get { return _port; } }

    private UdpClient _client;
    private CancellationTokenSource _cts;
    private Task _receiveTask;

    //private readonly ConcurrentQueue<string> _receiveQueue = new ConcurrentQueue<string>();

    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> MessageReceived;
    public event Action<Exception> Error;

    public UdpNetworkClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public Task ConnectAsync()
    {
        try
        {
            _client = new UdpClient();
            _client.Connect(_host, _port);

            _cts = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            Cleanup();
            throw;
        }

        return Task.CompletedTask;
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { }

        Cleanup();

        Disconnected?.Invoke();
    }

    public async void Send(string message)
    {
        if (_client == null) return;

        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(bytes, bytes.Length);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }

    public void Tick()
    {
        // while (_receiveQueue.TryDequeue(out var msg))
        // {
        //     MessageReceived?.Invoke(msg);
        // }
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _client.ReceiveAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                var msg = Encoding.UTF8.GetString(result.Buffer);
                MessageReceived?.Invoke(msg);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Error?.Invoke(ex);
        }
        finally
        {
            Cleanup();
            Disconnected?.Invoke();
        }
    }

    private void Cleanup()
    {
        try { _client?.Close(); } catch { }
        _client = null;

        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }
}
