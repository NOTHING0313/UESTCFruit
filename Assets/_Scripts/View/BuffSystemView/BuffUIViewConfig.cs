using System;
using UnityEngine;

namespace View
{
    [CreateAssetMenu(menuName = "Simulation/Buff UI Config")]
    public sealed class BuffUIViewConfig : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public int configId;
            public Sprite icon;
            public string displayName;
            public Color color;
            public int sortOrder;
        }

        [SerializeField] private Entry[] _entries;

        public bool TryGet(int configId, out Entry entry)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; i++)
                {
                    if (_entries[i].configId == configId)
                    {
                        entry = _entries[i];
                        return true;
                    }
                }
            }

            entry = default;
            return false;
        }
    }
}