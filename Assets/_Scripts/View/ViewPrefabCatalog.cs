using System;
using UnityEngine;

namespace View
{
    [CreateAssetMenu(menuName = "Simulation/View Prefab Catalog")]
    public sealed class ViewPrefabCatalog : ScriptableObject
    {
        [Serializable]
        private struct Entry
        {
            public int id;
            public GameObject prefab;
        }

        [SerializeField] private Entry[] _worldViews;
        [SerializeField] private Entry[] _uiViews;
        [SerializeField] private Entry[] _effectViews;

        public bool TryGetWorldPrefab(int id, out GameObject prefab)
        {
            return TryGet(_worldViews, id, out prefab);
        }

        public bool TryGetUIPrefab(int id, out GameObject prefab)
        {
            return TryGet(_uiViews, id, out prefab);
        }

        public bool TryGetEffectPrefab(int id, out GameObject prefab)
        {
            return TryGet(_effectViews, id, out prefab);
        }

        private static bool TryGet(Entry[] entries, int id, out GameObject prefab)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].id == id && entries[i].prefab != null)
                    {
                        prefab = entries[i].prefab;
                        return true;
                    }
                }
            }

            prefab = null;
            return false;
        }
    }
}