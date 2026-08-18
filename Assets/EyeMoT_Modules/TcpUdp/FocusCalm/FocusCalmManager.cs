using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using EyeMoT;
using EyeMoT.Heatmap;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class FocusCalmManager : MonoBehaviour
{
    #region singleton
    public static FocusCalmManager  Instance{get; private set;}
    void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);

        foreach(var server in _servers)
        {
            if(!_serverLists.ContainsKey(server.Data))
            {    
                _serverLists.Add(server.Data, server);
            }
        }
    }
    #endregion
    public enum EData
    {
        Attention = 0, Meditation, BandPower, 
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
    [SerializeField] RawImage[] _miniGraphs; //0 : Mental, 1 : BandPower
    [SerializeField] TMP_Text[] _miniGraphLogText; //0 : Mental, 1 : BandPower
    [SerializeField] TabManager _tabManager;
    [SerializeField] private bool _test = false;
    [SerializeField] private Image[] _activeStateImages;
    [SerializeField, Min(0.1f)] private float _livenessPingIntervalSeconds = 1f;
    [SerializeField, Min(0.1f)] private float _livenessPingTimeoutSeconds = 1f;

    public string[] Type = new string[] { "Attention", "Meditation", "Alpha", "Beta", "Delta", "Theta", "Gamma" };
    private string[] _bandPowerType = new string[] { "Alpha", "Beta", "Delta", "Theta", "Gamma" };
    string format = "yyyy-MM-dd HH:mm:ss.fff";
    DateTime _startTime;
    private Coroutine _testRoutine;
    private bool _isRecording = false;

    private bool _isVisible = false;
    private Dictionary<EData, FocusCalmServer> _serverLists = new();
    private Dictionary<EData, float> _mentalDataSet = new Dictionary<EData, float>
    {
        {EData.Attention, -1f}, {EData.Meditation, -1f}
    };
    private Action<RecordSample> _onDataReceived;
    private Coroutine _livenessPingRoutine;

    void Start()
    {
        if(Instance != this) return;

        StartUdpListen();

        _miniGraphs[0].texture = _serverLists[EData.Attention].Graph[0].DataTexture;
        _miniGraphs[1].texture = _serverLists[EData.Meditation].Graph[0].DataTexture;

    }

    void Update()
    {
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            _isVisible = !_panelAnimator.GetCurrentAnimatorStateInfo(0).IsName("Panel In");
            _tabManager.OpenPanel(_isVisible ? "Main" : "Mini");
        }

        if(_test)
        {
            if(_testRoutine == null)
            {
                _testRoutine = StartCoroutine(TestRoutine());
            }
        }
        else
        {
            if(_testRoutine != null)
            {
                StopCoroutine(_testRoutine);
                _testRoutine = null;
            }
        }
    }


    IEnumerator TestRoutine()
    {
        float time = 0;
        while(true)
        {
            time += 0.2f;
            foreach(var server in _serverLists)
            {
                var msg = $"{server.Key},Test|{((Mathf.Sin(time + Mathf.PI * (int)server.Key) + 1)/2) * 100}|TimeStamp Test";
                if(server.Key == EData.BandPower)
                {
                    msg = $"{server.Key},Test|{((Mathf.Sin(time) + 1)/2) * 0.2f};{((Mathf.Cos(time) + 1)/2) * 0.2f};{((Mathf.Sin(time + Mathf.PI/2) + 1)/2) * 0.2f};{((Mathf.Cos(time + Mathf.PI/2) + 1)/2) * 0.2f};{((Mathf.Sin(time + Mathf.PI) + 1)/2) * 0.2f}|TimeStamp Test";
                }
                OnMessageReceived(null, msg);
            }
            yield return new WaitForSeconds(0.3f);
        }
    }

    public void StartRecord(Action<RecordSample> onDataReceived)
    {
        _onDataReceived = onDataReceived;
        _isRecording = true;
        _startTime = DateTime.UtcNow.AddHours(9);
        foreach(var server in _serverLists)
        {
            foreach(var graph in server.Value.Graph)
            {
                graph.StartRecord();
            }
        }
    }

    public void StopRecord()
    {
        _isRecording = false;
        foreach(var server in _serverLists)
        {
            foreach(var graph in server.Value.Graph)
            {
                graph.StopRecord();
            }
        }
    }

    public void OnLivenessIPEditEnd(string ip)
    {
        if(_livenessPingRoutine != null)
        {
            StopCoroutine(_livenessPingRoutine);
            _livenessPingRoutine = null;
        }

        ip = ip?.Trim();
        if(string.IsNullOrEmpty(ip) || !IPAddress.TryParse(ip, out _))
        {
            SetActiveStateImages(false);
            return;
        }

        _livenessPingRoutine = StartCoroutine(LivenessPingRoutine(ip));
    }

    private IEnumerator LivenessPingRoutine(string ip)
    {
        while(true)
        {
            var ping = new UnityEngine.Ping(ip);
            float startTime = Time.realtimeSinceStartup;

            while(!ping.isDone && Time.realtimeSinceStartup - startTime < _livenessPingTimeoutSeconds)
            {
                yield return null;
            }

            SetActiveStateImages(ping.isDone && ping.time >= 0);
            ping.DestroyPing();

            yield return new WaitForSeconds(_livenessPingIntervalSeconds);
        }
    }

    private void SetActiveStateImages(bool isActive)
    {
        if(_activeStateImages == null) return;

        foreach(var image in _activeStateImages)
        {
            if(image != null)
            {
                image.color = isActive ? Color.green : Color.red;
            }
        }
    }

    void OnDestroy()
    {
        if(Instance != this) return;

        if(_livenessPingRoutine != null)
        {
            StopCoroutine(_livenessPingRoutine);
            _livenessPingRoutine = null;
        }

        foreach(var server in _serverLists)
        {
            server.Value.Server.Stop();
        }

        Instance = null;
    }

    public void StartUdpListen()
    {
        foreach(var server in _servers)
        {
            var udpServer = new UdpServer(server.Port);
            server.Server.UdpMessageReceived -= OnMessageReceived;
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

        Debug.Log("Message Received : " + msg);
        Debug.Log("Header : " + header);
        Debug.Log("Date : " + timestamp);
        Debug.Log("Data : " + data);

        if(!Enum.TryParse(header.Replace("/", ""), out EData result)) return;

        string[] type = null;
        string[] datas = null;

        switch(result)
        {
            case EData.Attention:
            case EData.Meditation:
                _mentalDataSet[result] = float.Parse(data);
                _miniGraphLogText[(int)result].text = _mentalDataSet[result].ToString("F1");
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
                datas = new string[] { data };
                type = new string[] { header.Replace("/", "") };
                break;
            case EData.BandPower:
                datas = data.Split(";");
                _dataLists[1].AddData(
                    $"[{timestamp.Split(" ")[1].Split(".")[0]}] " +
                    $"Alpha : {float.Parse(datas[0]):F4}, Beta : {float.Parse(datas[1]):F4}, Delta : {float.Parse(datas[2]):F4}, " +
                    $"Theta : {float.Parse(datas[3]):F4}, Gamma : {float.Parse(datas[4]):F4}" 
                );
                for(int i = 0; i < datas.Length; i++)
                {
                    _serverLists[result].Graph[i].AddValue(float.Parse(datas[i]));
                }
                type = _bandPowerType;
                break;
        }

        if(_isRecording)
        {
            var time = DateTime.ParseExact(timestamp, format, CultureInfo.InvariantCulture);
            TimeSpan elapsed = time - _startTime;
            _onDataReceived?.Invoke(new RecordSample
            {
                Type = type,
                Data = datas,
                TimeStamp = (float)elapsed.TotalSeconds
            });
        }
    }
}
