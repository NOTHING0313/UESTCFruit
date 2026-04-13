using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;

using UnityEngine;
namespace BuffSystem
{
    [CreateAssetMenu(menuName = "BuffSystem/BuffTags", fileName = "BuffTagsData")]
    public sealed class BuffTags : ScriptableObject
    {
        private static BuffTags _instance;
        public static BuffTags GetOrFind()
        {
            if (_instance != null) return _instance;

#if UNITY_EDITOR
            // 编辑器：从工程里找任意一个 BuffTags 资产（并缓存）
            var guids = UnityEditor.AssetDatabase.FindAssets("t:BuffSystem.BuffTags");
            if (guids != null && guids.Length > 0)
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<BuffTags>(path);
            }
#else
        // 运行时：建议放进 Resources 固定路径
        _instance = Resources.Load<BuffTags>("BuffTagsData");
#endif
            return _instance;
        }

        [SerializeField, LabelText("预设BuffTag"), InfoBox("前面填使用名字（推荐中文），后面填实际名字（必须英文）", InfoMessageType.Info), OnCollectionChanged(nameof(RebuildDropdown))]
        private List<TagPair<string>> _buffTags = new();

        [NonSerialized] private readonly ValueDropdownList<string> _defaultBuffTags = new();
        public IEnumerable<ValueDropdownItem<string>> DefaultBuffTags => _defaultBuffTags;
        public List<TagPair<string>> BuffTagPairs => _buffTags;

#if UNITY_EDITOR
        private void OnValidate() => RebuildDropdown(); // 编辑器改动时自动刷新更稳
#endif

        private void RebuildDropdown()
        {
            _defaultBuffTags.Clear();
            if (_buffTags == null) return;

            foreach (var t in _buffTags)
            {
                if (string.IsNullOrEmpty(t.Second)) continue; // value 必须有效
                _defaultBuffTags.Add(t.First, t.Second);
            }
        }
    }
    [Serializable]
    public struct TagPair<T>
    {
        [LabelText("使用名字")]
        public T First;
        [LabelText("实际名字")]
        public T Second;
    }
}
