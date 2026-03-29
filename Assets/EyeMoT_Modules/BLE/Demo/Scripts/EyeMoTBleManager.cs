using UnityEngine;
using System.Xml.Serialization;
using System.IO;

using EyeMoT.Ble;
using System;
using System.Collections.Generic;
using System.Text;

public class EyeMoTBleManager : MonoBehaviour, IBleReceiver
{
    #region singleton
    public static EyeMoTBleManager Instance{get; private set;}
    void Awake()
    {
        if(Instance != null)
        {
            Destroy(this.gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(this.gameObject);
    }
    #endregion
    [SerializeField]private string xmlPath = "BLE/SwitchBotData.xml";
    private BleService _service = null!;
    private BleConfig _config = null!;
    private Action<BleDevice> _onRegistDevice;
    private bool _isSerching = false;

    private List<BleDevice> bleDevices = new();

    private void Start()
    {
        LoadSwitchBotData();

        if(_config == null)
        {
            Debug.LogError("<color=orange>[BLE]</color> Failed to load SwitchBot configuration.");
            return;
        }
    }

    private void Update()
    {
        if(_isSerching)
            _service.Tick();
    }

    private void OnApplicationQuit()
    {
        Stop();
    }
    
    public void Init(Action<BleDevice> onRegistDevice)
    {
        #if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log($"<color=orange>[BLE]</color> WebGL platform does not support BLE. Initialization skipped.");
        return;
        #endif
        Stop();

        _service = new BleService(receiver: this);
        _onRegistDevice = onRegistDevice;

        _service.Start();
        _isSerching = true;
        Debug.Log($"<color=orange>[BLE]</color> Start Scaninig");
    }

    public void Stop()
    {
        _onRegistDevice = null;
        _isSerching = false;
        if(_service != null)
        {    
            _service.Dispose();
            _service = null;
        }
    }

    public async void SendCommand(BleDevice device)
    {
        if (_service == null)
        {
            Debug.LogError("<color=orange>[BLE]</color> BleService is not initialized.");
            return;
        }

        try
        {
            bool ok = await _service.TrySendToDetectedTarget(device, 10000);
            if (ok)
                Debug.Log($"<color=orange>[BLE]</color> Command sent: ID ={device.DeviceId}, Payload={BitConverter.ToString(device.Payload)}");
            else
                Debug.LogError($"<color=orange>[BLE]</color> Failed to send command: ID ={device.DeviceId}, Payload={BitConverter.ToString(device.Payload)}");
        }
        catch
        {
            Debug.LogError($"<color=orange>[BLE]</color> Failed to send command: IID ={device.DeviceId}, Payload={BitConverter.ToString(device.Payload)}");
        }
    }

    private void LoadSwitchBotData()
    {
        string path = Path.Combine(Application.streamingAssetsPath, xmlPath);
        _config = XmlLoader.LoadXml<BleConfig>(path);
    }

    private void RegistArmOneda(BleApi.DeviceUpdate dev)
    {
        if (dev.name == null || !dev.name.Contains(_config.ArmOneDa.Indentifier))
            return;

        byte[] payload = Encoding.ASCII.GetBytes(_config.ArmOneDa.CommandDic[ArmOneDaCommand.Swing_Toggle]);
        var armOneDa = new BleDevice
        {
            Name = dev.name,
            DeviceId = dev.id,
            ServiceUuid = _config.ArmOneDa.ServiceUuid,
            CharacteristicUuid = _config.ArmOneDa.CharacteristicUuid,
            Payload = payload,
        };
        Debug.Log($"<color=orange>[BLE]</color><color=orange>[ArmOneDa]</color> Advertisement received: DeviceId={dev.id}, Name={dev.name}");
        bleDevices.Add(armOneDa);
        _onRegistDevice.Invoke(armOneDa);
    }

    private void RegistSwitchBot(BleApi.AdvertisementUpdate adv)
    {
        if (adv.serviceData == null || adv.serviceData.Length == 0)
            return;

        //SwitchBotのアドバタイズは、serviceDataの最初のバイトが0x1Aで、下位7ビットがコマンドタイプになっている。
        byte type = (byte)(adv.serviceData[0] & 0x7F);
        var switchbotDef = _config.SwitchBot.GetSwitchBotDefByType(type);
        
        if(switchbotDef == null) 
            return;

        var switchbot = new BleDevice
        {
            Name = switchbotDef.Name,
            DeviceId = adv.deviceId,
            ServiceUuid = _config.SwitchBot.ServiceUuid,
            CharacteristicUuid = _config.SwitchBot.CharacteristicUuid,
            Payload = switchbotDef.PayloadBytes,
        };
        Debug.Log($"<color=orange>[BLE]</color><color=orange>[SwitchBot]</color> Advertisement received: DeviceId={adv.deviceId}, Type={type:X2}, Name={switchbot.Name}");
        bleDevices.Add(switchbot);
        _onRegistDevice.Invoke(switchbot);
    }

    #region IBleReceiver implementation
    public void OnAdvertisementReceived(BleApi.AdvertisementUpdate adv)
    {
        RegistSwitchBot(adv);
    }

    public void OnDeviceReceived(BleApi.DeviceUpdate device)
    {
        RegistArmOneda(device);
    }
    public void OnServiceReceived(BleApi.Service service)
    {
        Debug.Log($"<color=orange>[BLE]</color> Service received: UUID={service.uuid}");
    }

    public void OnCharacteristicReceived(BleApi.Characteristic characteristic)
    {
        Debug.Log($"<color=orange>[BLE]</color> Characteristic received: UUID={characteristic.uuid}, Description={characteristic.userDescription}");
    }

    public void OnAllDeviceAvailable()
    {
        _isSerching = false;
        Debug.Log("<color=orange>[BLE]</color>  All device available");
    }


    #endregion
}