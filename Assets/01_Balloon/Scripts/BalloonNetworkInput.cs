using Fusion;
using UnityEngine;

namespace EyeMoT.Balloon
{
    public struct BalloonNetworkInput : INetworkInput
    {
        public NetworkBool HasMouse;
        public Vector2 MouseUV;
        public float ScreenAspect;
    }
}
