using System.Collections;
using System.Collections.Generic;
using EyeMoT.Ble;
using UnityEngine;

public class BleDeviceVisualizer : MonoBehaviour
{
    [SerializeField] private BleDeviceButton _buttonPrefab;
    [SerializeField] private Transform _content;

    public void StartSerchBleDevice()
    {
        foreach(Transform child in _content.transform)
        {
            Destroy(child.gameObject);
        }
        EyeMoTBleManager.Instance.Init(OnRegistDevice);
    }

    public void StopSerchBleDevice()
    {
        EyeMoTBleManager.Instance.Stop();
        foreach(Transform child in _content.transform)
        {
            Destroy(child.gameObject);
        }
    }

    public void OnRegistDevice(BleDevice device)
    {
        var button = Instantiate(_buttonPrefab, _content);
        button.Init(device.Name, () => EyeMoTBleManager.Instance.SendCommand(device));
    }
}
