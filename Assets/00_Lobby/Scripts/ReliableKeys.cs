
using Fusion.Sockets;

namespace EyeMoT.Fusion
{
    
    public class ReliableKeys
    {
        public static int HeatmapIndex = 1;
        public static int ImageIndex = 2;
        public static ReliableKey GetHeatMapKey(int index, bool isBroadcast) => ReliableKey.FromInts(HeatmapIndex, index, 0, isBroadcast ? 1 : 0);
        public static ReliableKey GetImageKey(int index, bool isBroadcast) => ReliableKey.FromInts(ImageIndex, index, 0, isBroadcast ? 1 : 0);

    }
}