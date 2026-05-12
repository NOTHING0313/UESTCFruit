using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// Buff 事件编号目录，只用于编辑器选择和说明展示，运行时模拟仍只读取 EventId。
    /// </summary>
    [CreateAssetMenu(menuName = "BuffSystem/Buff Event Catalog", fileName = "BuffEventCatalogData")]
    public sealed class BuffEventCatalogData : ScriptableObject
    {
        private static BuffEventCatalogData _instance;

        [SerializeField, LabelText("事件目录"), TableList(AlwaysExpanded = true)]
        private List<BuffEventCatalogEntry> _entries = new List<BuffEventCatalogEntry>();

        public IReadOnlyList<BuffEventCatalogEntry> Entries => _entries;

        /// <summary>
        /// 查找工程中的事件目录；找不到时 BuffConfigData 会退回手动填写 EventId。
        /// </summary>
        public static BuffEventCatalogData GetOrFind()
        {
            if (_instance != null)
                return _instance;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(BuffEventCatalogData)}");

            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<BuffEventCatalogData>(path);
            }
#else
            _instance = Resources.Load<BuffEventCatalogData>("BuffEventCatalogData");
#endif
            return _instance;
        }

        public bool TryGetEntry(int eventId, out BuffEventCatalogEntry entry)
        {
            entry = default;

            if (_entries == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].EventId == eventId)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 单个 Buff 逻辑事件的编辑器说明项，帮助策划用显示名选择 EventId。
    /// </summary>
    [Serializable]
    public struct BuffEventCatalogEntry
    {
        [VerticalGroup,LabelText("EventId"), MinValue(1)]
        public int EventId;

        [VerticalGroup, LabelText("事件 Key")]
        public string EventKey;

        [VerticalGroup, LabelText("显示名")]
        public string DisplayName;

        [VerticalGroup, LabelText("说明"), Multiline]
        public string Description;

        [VerticalGroup, LabelText("事件结构类型名")]
        public string EventTypeName;
    }
}
