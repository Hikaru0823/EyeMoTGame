namespace EyeMoT.Ble
{
    public interface IBleReceiver
    {
        void OnAdvertisementReceived(BleApi.AdvertisementUpdate adv);
        void OnDeviceReceived(BleApi.DeviceUpdate device);
        void OnServiceReceived(BleApi.Service service);
        void OnCharacteristicReceived(BleApi.Characteristic characteristic);

        void OnAllDeviceAvailable();
    }
}