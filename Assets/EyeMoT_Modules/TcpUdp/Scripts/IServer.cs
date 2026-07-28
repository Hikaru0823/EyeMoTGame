using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public interface IServer
{
    int Port { get; }
    void StartServer();
    void Stop();
    void Tick();
    Task BroadcastAsync(List<ClientSession> targetClients, string message);
    Task SendToClientAsync(ClientSession targetClient, string message);

    event Action<IPEndPoint, string> MessageReceived;
    event Action<Exception> Error;
}