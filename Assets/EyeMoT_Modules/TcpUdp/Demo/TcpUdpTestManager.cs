using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using TMPro;
using UnityEngine;

public class TcpUdpTestManager : MonoBehaviour, IServerCallbacks
{
    [SerializeField] ServerManager _serverManagerPrefab;
    [SerializeField] TMP_Text _ipText;
    [SerializeField] int _udpPort;
    [SerializeField] TMP_Text _headerText;
    [SerializeField] TMP_Text _dateText;
    [SerializeField] TMP_Text _dataText;

    private ServerManager _serverManager;

    public void StartUdpListen()
    {
        if(_serverManager != null)
        {
            _serverManager.RemoveAllListeners();
            _serverManager.Stop();
            Destroy(_serverManager);
            _serverManager = null;
        }

        _serverManager = Instantiate(_serverManagerPrefab);
        var _usdServer = new UdpServer(_udpPort);
        _serverManager.AddListener((IServerCallbacks)this);
        _serverManager.InitializeUdp(_usdServer);
        _serverManager.StartUdp();

        _ipText.text = GetLocalIPAddress() + " : " + _udpPort;
    }

    public string GetLocalIPAddress()
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

    public void OnClientConnected(TcpServer.ClientConnection client)
    {
        Debug.Log("Connected");
    }

    public void OnClientDisconnected(TcpServer.ClientConnection client)
    {
        Debug.Log("Disconnected");
    }

    public void OnTcpError(Exception ex)
    {

    }

    public void OnTcpReceived(IPEndPoint ep, string msg)
    {
    }

    public void OnUdpError(Exception ex)
    {
        Debug.Log("Udp Error");
    }

    public void OnUdpReceived(IPEndPoint ep, string msg)
    {
        string[] parts = msg.Split('|');

        string header = parts[0].Split(',')[0]; // /EEG
        string eegData = parts[1];   // EEGデータ
        string timestamp = parts[2]; // 日時
        _dateText.text = "Date : " + timestamp;
        _dataText.text = "Header : " + header + "\nData : " + eegData;
        Debug.Log("Header : " + header);
        Debug.Log("Date : " + timestamp);
        Debug.Log("Data : " + eegData);
    }
}
