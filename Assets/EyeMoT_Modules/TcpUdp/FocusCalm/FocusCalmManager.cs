using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class FocusCalmManager : MonoBehaviour
{
    public enum EData
    {
        Attention, Meditation, BandPower, 
    }
    [Serializable]
    public class FocusCalmServer
    {
        public EData Data;
        public ServerManager Server;
        public int Port;
        public DynamicLineGraph[] Graph;
    }
    [Header("Resources")]
    [SerializeField] Animator _panelAnimator;
    [SerializeField] FocusCalmServer[] _servers;
    [SerializeField] TMP_Text _ipText;
    [SerializeField] DataLogList[] _dataLists; //0 : Mental, 1 : BandPower

    private bool _isVisible = false;
    private Dictionary<EData, FocusCalmServer> _serverLists = new();
    private Dictionary<EData, float> _mentalDataSet = new Dictionary<EData, float>
    {
        {EData.Attention, -1f}, {EData.Meditation, -1f}
    };

    void Awake()
    {
        foreach(var server in _servers)
        {
            if(!_serverLists.ContainsKey(server.Data))
                _serverLists.Add(server.Data, server);
        }
    }

    void Start()
    {
        StartUdpListen();
    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            _isVisible = !_isVisible;
            _panelAnimator.Play(_isVisible ? "Panel In" : "Panel Out");
        }
    }

    void OnDestroy()
    {
        foreach(var server in _serverLists)
        {
            server.Value.Server.Stop();
        }
    }

    public void StartUdpListen()
    {
        foreach(var server in _servers)
        {
            var udpServer = new UdpServer(server.Port);
            server.Server.UdpMessageReceived += OnMessageReceived;
            server.Server.InitializeUdp(udpServer);
            server.Server.StartUdp();
        }

        _ipText.text = "Local IP Address : " +  GetLocalIPAddress();
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
    public void OnMessageReceived(IPEndPoint ep, string msg)
    {
        string[] parts = msg.Split('|');

        string header = parts[0].Split(',')[0];
        string data = parts[1];
        string timestamp = parts[2];

        Debug.Log("Header : " + header);
        Debug.Log("Date : " + timestamp);
        Debug.Log("Data : " + data);

        if(!Enum.TryParse(header.Replace("/", ""), out EData result)) return;

        switch(result)
        {
            case EData.Attention:
            case EData.Meditation:
                _mentalDataSet[result] = float.Parse(data);
                bool isSet = true;
                foreach(var set in _mentalDataSet)
                    if(set.Value == -1)
                        isSet = false;
                if(isSet)
                {    
                    _dataLists[0].AddData(
                        $"[{timestamp.Split(" ")[1].Split(".")[0]}] " +
                        $"Attention : {_mentalDataSet[EData.Attention]:F1}, " +
                        $"Meditation : {_mentalDataSet[EData.Meditation]:F1}"
                    );
                    _mentalDataSet = new Dictionary<EData, float>
                    {
                        {EData.Attention, -1f}, {EData.Meditation, -1f}
                    };
                }
                _serverLists[result].Graph[0].AddValue(float.Parse(data));
                break;
            case EData.BandPower:
                var datas = data.Split(";");
                _dataLists[1].AddData(
                    $"[{timestamp.Split(" ")[1].Split(".")[0]}] " +
                    $"Alpha : {float.Parse(datas[0]):F4}, Beta : {float.Parse(datas[1]):F4}, Delta : {float.Parse(datas[2]):F4}, " +
                    $"Theta : {float.Parse(datas[3]):F4}, Gamma : {float.Parse(datas[4]):F4}" 
                );
                for(int i = 0; i < datas.Length; i++)
                {
                    _serverLists[result].Graph[i].AddValue(float.Parse(datas[i]));
                }
                break;
        }
    }
}
