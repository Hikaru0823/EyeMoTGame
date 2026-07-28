using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class UdpServer : IServer
{
    private readonly int _port;
    private UdpClient _udp;
    private UdpClient _discoveryUdp; // ホスト（UDPサーバー）があることをクライアントに知らせるためのUDPクライアント
    private CancellationTokenSource _cts;
    private Task _receiveTask;
    public event Action<IPEndPoint, string> MessageReceived;
    public event Action<Exception> Error;

    public int Port { get { return _port; } }

    public UdpServer(int port)
    {
        _port = port;
    }

    public void StartServer()
    {
        if (_udp != null) return;

        _udp = new UdpClient(_port);
        _cts = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        try { _udp?.Close(); } catch { }
        try { _discoveryUdp?.Close(); } catch { }

        _udp = null;
        _discoveryUdp = null;
        _cts?.Dispose();
        _cts = null;
        _receiveTask = null;
    }

    public void Tick()
    {
        // UDPはコネクションレスなので特にやることなし
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
                    result = await _udp.ReceiveAsync().ConfigureAwait(false);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                
                var ep = result.RemoteEndPoint;
                var msg = DecodeMessage(result.Buffer);

                MessageReceived?.Invoke(ep, msg);
            }
        }
        catch (Exception ex)
        {
            if (!token.IsCancellationRequested)
                Error?.Invoke(ex);
        }
    }

    private static string DecodeMessage(byte[] data)
    {
        string oscMessage;
        return TryDecodeOscMessage(data, out oscMessage)
            ? oscMessage
            : Encoding.UTF8.GetString(data);
    }

    // OSC文字列はNULL終端で、次の4バイト境界までパディングされる。
    // address + type tag | argument | argument の形に戻して既存のstringイベントへ渡す。
    private static bool TryDecodeOscMessage(byte[] data, out string message)
    {
        message = null;
        if (data == null || data.Length < 8 || data[0] != (byte)'/') return false;

        int offset = 0;
        string address;
        string typeTags;
        if (!TryReadOscString(data, ref offset, out address) ||
            !TryReadOscString(data, ref offset, out typeTags) ||
            string.IsNullOrEmpty(typeTags) || typeTags[0] != ',')
        {
            return false;
        }

        var result = new StringBuilder(address.Length + typeTags.Length + data.Length);
        result.Append(address).Append(typeTags);

        for (int i = 1; i < typeTags.Length; i++)
        {
            result.Append('|');

            switch (typeTags[i])
            {
                case 's':
                case 'S':
                {
                    string text;
                    if (!TryReadOscString(data, ref offset, out text)) return false;
                    result.Append(text);
                    break;
                }

                case 'i':
                {
                    int integer;
                    if (!TryReadInt32(data, ref offset, out integer)) return false;
                    result.Append(integer.ToString(CultureInfo.InvariantCulture));
                    break;
                }

                case 'f':
                {
                    int floatBits;
                    if (!TryReadInt32(data, ref offset, out floatBits)) return false;
                    result.Append(BitConverter.ToSingle(BitConverter.GetBytes(floatBits), 0)
                        .ToString("R", CultureInfo.InvariantCulture));
                    break;
                }

                // 値を表すデータを持たないOSC型。
                case 'T': result.Append("true"); break;
                case 'F': result.Append("false"); break;
                case 'N': result.Append("null"); break;
                case 'I': result.Append("Infinity"); break;

                default:
                    // 未対応型は読み取り位置を決定できないため通常のUTF-8処理へ戻す。
                    return false;
            }
        }

        message = result.ToString();
        return true;
    }

    private static bool TryReadOscString(byte[] data, ref int offset, out string value)
    {
        value = null;
        if (offset < 0 || offset >= data.Length) return false;

        int end = Array.IndexOf(data, (byte)0, offset);
        if (end < 0) return false;

        value = Encoding.UTF8.GetString(data, offset, end - offset);
        offset = AlignToFourBytes(end + 1);
        return offset <= data.Length;
    }

    private static bool TryReadInt32(byte[] data, ref int offset, out int value)
    {
        value = 0;
        if (offset < 0 || offset + 4 > data.Length) return false;

        value = (data[offset] << 24) |
                (data[offset + 1] << 16) |
                (data[offset + 2] << 8) |
                data[offset + 3];
        offset += 4;
        return true;
    }

    private static int AlignToFourBytes(int value)
    {
        return (value + 3) & ~3;
    }

    public async Task BroadcastAsync(List<ClientSession> targetClients, string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        foreach (var client in targetClients)
        {
            try
            {
                await _udp.SendAsync(data, data.Length, client.Udp);
            }
            catch (Exception ex)
            {
                Error?.Invoke(ex);
            }
        }
    
    }

    public async Task SendToClientAsync(ClientSession targetClient, string message)
    {
        var data = Encoding.UTF8.GetBytes(message);
        try
        {
            await _udp.SendAsync(data, data.Length, targetClient.Udp);
        }
        catch (Exception ex)
        {
            Error?.Invoke(ex);
        }
    }

    private string GetLocalIPAddress()
    {
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
