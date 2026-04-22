using System.Collections.Generic;
using EyeMoT.Fusion;
using UnityEngine;

namespace EyeMoT.Fusion
{
    [CreateAssetMenu(menuName = "Session/SessionHolder")]
    public class SessionHolder : ScriptableObject
    {
        Dictionary<int, SessionData> _DataDictionary = new Dictionary<int, SessionData>();

        [SerializeField]
        SessionData[] _DataList = null;

        public void init()
        {
            _DataDictionary.Clear();
            foreach (var data in _DataList)
            {
                _DataDictionary.Add((int)data.Name, data);
            }
        }

        public bool TryGet(SessionDef.Name name, out SessionData data)
        {
            var result = _DataDictionary.TryGetValue((int)name, out data);
            return result;
        }

        public int Count => _DataList.Length;
    }
}