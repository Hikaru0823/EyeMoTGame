using System;
using System.Threading.Tasks;

public interface INetworkClient
{
    string Host { get; }
    int Port { get; }
    Task ConnectAsync();
    void Disconnect();
    void Send(string message);
    void Tick();

    event Action Connected;
    event Action Disconnected;
    event Action<string> MessageReceived;
    event Action<Exception> Error;
}