using System;
using System.Collections.Generic;
using System.Threading.Tasks;
#nullable enable

namespace EyeMoT.Ble
{
    public class BleService : IDisposable
    {
        private readonly IBleReceiver? _receiver;
        private readonly Dictionary<string, string> _fullIdMap = new();
        private readonly HashSet<string> _seenDeviceIds = new();
        private readonly HashSet<string> _seenAdvertisementDeviceIds = new();

        private bool _serchingDevice = false;
        private bool _scanningServices = false;
        private bool _scanningCharacteristic = false;

        public BleService(
            IBleReceiver? receiver = null
            )
        {
            _receiver = receiver;
        }

        public Dictionary<string, string> FullIdMap => _fullIdMap;


        public void Start()
        {
            #if UNITY_WEBGL && !UNITY_EDITOR
            return;
            #endif
            BleApi.StartDeviceScan();
            BleApi.StartAdvertisementScan();
            _serchingDevice = true;
        }

        public void Stop()
        {
            BleApi.StopDeviceScan();
            BleApi.StopAdvertisementScan();
            BleApi.Quit();
            _serchingDevice = false;
        }

        // Monobehaviourで動かす際は、Update()内で定期的にTick()を呼び出すこと。
        public void Tick()
        {
            if(_serchingDevice)
            {    
                PollDevices();
                PollAdvertisements();
            }
            if(_scanningServices)
                PollServices();
            if(_scanningCharacteristic)
                PollCharacteristics();
        }

        public async Task<bool> TrySendToDetectedTarget(BleDevice device, int timeoutMs = 3000)
        {
            if (string.IsNullOrEmpty(device.DeviceId))
            {
                return false;
            }

            if (!TryGetFullDeviceID(device.DeviceId, out var fullDeviceId))
            {
                return false;
            }

            if(timeoutMs > 0)
            {
                return await SendAsync(fullDeviceId, device, timeoutMs);
            }
            else
            {
                return Send(fullDeviceId, device);
            }
        }

        private async Task<bool> SendAsync(string fullDeviceId, BleDevice device, int timeoutMs = 3000)
        {
            var sendTask = Task.Run(() =>
            {
                BleApi.BLEData data = new BleApi.BLEData
                {
                    buf = new byte[512],
                    size = (short)device.Payload.Length,
                    deviceId = fullDeviceId,
                    serviceUuid = device.ServiceUuid,
                    characteristicUuid = device.CharacteristicUuid
                };

                Array.Copy(device.Payload, data.buf, device.Payload.Length);

                return BleApi.SendData(in data, true);
            });

            var completedTask = await Task.WhenAny(sendTask, Task.Delay(timeoutMs));

            if (completedTask != sendTask)
            {
                return false;
            }

            return await sendTask;
        }


        private bool Send(string fullDeviceId, BleDevice device)
        {
            BleApi.BLEData data = new BleApi.BLEData
            {
                buf = new byte[512],
                size = (short)device.Payload.Length,
                deviceId = fullDeviceId,
                serviceUuid = device.ServiceUuid,
                characteristicUuid = device.CharacteristicUuid
            };

            Array.Copy(device.Payload, data.buf, device.Payload.Length);

            bool ok = BleApi.SendData(in data, false); //block = true　だとUnityが固まることがあるため、falseで呼び出す。送信に失敗することがあるが、成功することもある。
            return ok;
        }

        public bool ScanServices(string targetMac)
        {
            if (!TryGetFullDeviceID(targetMac, out var fullDeviceId))
            {
                return false;
            }
            BleApi.ScanServices(fullDeviceId);
            _scanningServices = true;
            return true;
        }

        public bool ScanCharacteristics(string targetMac, string serviceUuid)
        {
            if (!TryGetFullDeviceID(targetMac, out var fullDeviceId))
            {
                return false;
            }
            BleApi.ScanCharacteristics(fullDeviceId, serviceUuid);
            _scanningCharacteristic = true;
            return true;
        }

        private void PollDevices()
        {
            BleApi.DeviceUpdate res = new BleApi.DeviceUpdate();
            BleApi.ScanStatus status;

            do
            {
                status = BleApi.PollDevice(ref res, false);

                if (status == BleApi.ScanStatus.AVAILABLE)
                {   
                    if (string.IsNullOrEmpty(res.id) || !res.nameUpdated)
                        continue;

                    string? mac = ExtractMacFromFullDeviceId(res.id);
                    if (string.IsNullOrEmpty(mac))
                        continue;

                    // if (_seenDeviceIds.Contains(mac))
                    //     continue;

                    _seenDeviceIds.Add(mac);
                    _fullIdMap[mac] = res.id;
                    res.id = mac;
                    _receiver?.OnDeviceReceived(res);
                }
                else if(status == BleApi.ScanStatus.FINISHED)
                {
                    _serchingDevice = false;
                    _receiver?.OnAllDeviceAvailable();
                    BleApi.StopAdvertisementScan(); //全てのデバイスが受信できたら、アドバタイズのスキャンは止める。アドバタイズには完了通知のコールバックがないため。
                }
            }
            while (status == BleApi.ScanStatus.AVAILABLE);
        }

        private void PollAdvertisements()
        {
            BleApi.AdvertisementUpdate adv = new BleApi.AdvertisementUpdate();
            BleApi.ScanStatus status;

            do
            {
                status = BleApi.PollAdvertisement(ref adv, false);

                if (status == BleApi.ScanStatus.AVAILABLE)
                {   
                    if (!adv.serviceDataUpdated)
                        continue;

                    if (_seenAdvertisementDeviceIds.Contains(adv.deviceId))
                        continue;

                    _seenAdvertisementDeviceIds.Add(adv.deviceId);
                    _receiver?.OnAdvertisementReceived(adv);
                }
            }
            while (status == BleApi.ScanStatus.AVAILABLE);
        }

        private void PollServices()
        {
            BleApi.Service service = new BleApi.Service();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollService(out service, false);

                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    if(string.IsNullOrEmpty(service.uuid))
                        continue;
                    _receiver?.OnServiceReceived(service);
                }
                else if(status == BleApi.ScanStatus.FINISHED)
                {
                    _scanningServices = false;
                }
            }
            while (status == BleApi.ScanStatus.AVAILABLE);
        }

        private void PollCharacteristics()
        {
            BleApi.Characteristic characteristic = new BleApi.Characteristic();
            BleApi.ScanStatus status;
            do
            {
                status = BleApi.PollCharacteristic(out characteristic, false);

                if (status == BleApi.ScanStatus.AVAILABLE)
                {
                    if(string.IsNullOrEmpty(characteristic.uuid))
                        continue;
                    _receiver?.OnCharacteristicReceived(characteristic);
                }
                else if (status == BleApi.ScanStatus.FINISHED)
                {
                    _scanningCharacteristic = false;
                }
            }
            while (status == BleApi.ScanStatus.AVAILABLE);
        }

        private static string? ExtractMacFromFullDeviceId(string fullId)
        {
            if (string.IsNullOrEmpty(fullId))
                return null;

            int idx = fullId.LastIndexOf('-');
            if (idx < 0 || idx + 1 >= fullId.Length)
                return null;

            return fullId[(idx + 1)..].ToLowerInvariant();
        }

        public bool TryGetFullDeviceID(string targetMac, out string fullID)
        {
            if (!_fullIdMap.TryGetValue(targetMac, out string? fullDeviceId))
            {
                fullID = "";
                return false;
            }
            
            fullID = fullDeviceId;
            return true;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}