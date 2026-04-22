using UnityEngine;

namespace EyeMoT.Fusion
{
    [System.Serializable]
    public class SessionData
    {
        [SerializeField]
        SessionDef.Name _Name;
        public SessionDef.Name Name => _Name;

        [SerializeField]
        Sprite _Sprite;
        public Sprite Sprite => _Sprite;
        [SerializeField]
        string _UIName;
        public string UIName => _UIName;
    }
}