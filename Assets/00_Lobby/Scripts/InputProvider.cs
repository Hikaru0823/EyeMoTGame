using Fusion;
using UnityEngine;

namespace EyeMoT.Fusion
{
    public abstract class InputProvider : MonoBehaviour
    {
        public abstract void ApplyInput(NetworkInput input);

        public abstract void ApplyMissingInput(NetworkInput input);
    }
}
