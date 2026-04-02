using UnityEngine;

namespace EyeMoT
{
    [System.Serializable]
    public class VFXData
    {
        [SerializeField]
        VFXDef.TYPE _Type;
        public VFXDef.TYPE Type => _Type;

        [SerializeField]
        GameObject _Object;
        public GameObject Object => _Object;
        [SerializeField]
        string _SEPath;
        public string CurrentSEPath => _SEPath;
    }
}