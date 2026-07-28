using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IClientCallbacks
{
    public void OnTcpConnected();
    public void OnTcpDisconnected();
    public void OnTcpMessageReceived(string msg);
    public void OnTcpError(System.Exception ex);

    public void OnUdpConnected();

    public void OnUdpDisconnected();

    public void OnUdpReceived(string msg);

    public void OnUdpError(System.Exception ex);
}
