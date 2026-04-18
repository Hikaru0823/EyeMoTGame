using Fusion;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public struct EyeMoTNetworkInput : INetworkInput
    {
        public NetworkBool HasMouse;
        public Vector2 MouseUV;
    }
}
