using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class TcpNetworkClient : INetworkClient
{
    private readonly string _host;
    public string Host { get { return _host; } }
    private readonly int _port;
    public int Port { get { return _port; } }

    private TcpClient _client;
    private StreamReader _reader;
    private StreamWriter _writer;

    private CancellationTokenSource _cts;
    private Task _receiveTask;


    public event Action Connected;
    public event Action Disconnected;
    public event Action<string> MessageReceived;
    public event Action<Exception> Error;

    public TcpNetworkClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task ConnectAsync()
    {
        if (_client != null)
        {
            return;
        }

        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port);

            var stream = _client.GetStream();

            _reader = new StreamReader(stream, Encoding.UTF8, false, 1024, leaveOpen: true);
            _writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true)
            {
                AutoFlush = true
            };

            _cts = new CancellationTokenSource();

            // 受信ループ開始
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));

            Connected?.Invoke();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            // 失敗したらクリーンアップ
            Cleanup();
            throw;
        }
    }

    public void Disconnect()
    {
        try
        {
            _cts?.Cancel();
        }
        catch { /* ignore */ }

        Cleanup();

        Disconnected?.Invoke();
    }

    public void Send(string message)
    {
        // 非同期にしでもええけど、ここでは同期送信
        try
        {
            if (_writer == null) return;
            _writer.WriteLine(message);  // 行単位ね
            _writer.Flush();
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
            // エラーが痛すぎるなら切断
            Disconnect();
        }
    }

    public void Tick()
    {
        // 受信キューからメッセージを取り出してイベント発火
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
                // 接続が切れた場合などは null が返る or IOException
                var line = await _reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                {
                    // サーバ側が切断
                    break;
                }

                MessageReceived?.Invoke(line);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
            {
                Error?.Invoke(ex);
            }
        }
        finally
        {
            // 受信ループ終了 → 切断扱い
            Cleanup();

            // 注意：ここは別スレッドなので、Disconnect() は呼ばずDisconnected イベントだけ飛ばしておくよー
            Disconnected?.Invoke();
        }
    }

    private void Cleanup()
    {
        try
        {
            _reader?.Dispose();
        }
        catch { }

        try
        {
            _writer?.Dispose();
        }
        catch { }

        try
        {
            _client?.Close();
        }
        catch { }

        _reader = null;
        _writer = null;
        _client = null;

        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }
}
