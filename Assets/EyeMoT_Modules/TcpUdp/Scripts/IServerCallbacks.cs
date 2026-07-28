using System.Collections;
using System.Collections.Generic;
using System.Net;
using UnityEngine;

public interface IServerCallbacks
{
    public void OnClientConnected(TcpServer.ClientConnection client);
    public void OnClientDisconnected(TcpServer.ClientConnection client);
    public void OnTcpReceived(IPEndPoint ep, string msg);
    public void OnUdpReceived(IPEndPoint ep, string msg);
    public void OnTcpError(System.Exception ex);
    public void OnUdpError(System.Exception ex);
}