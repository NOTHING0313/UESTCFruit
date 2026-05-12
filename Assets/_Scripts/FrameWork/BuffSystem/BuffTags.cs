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
            // 编辑器下从工程资产中查找任意一个 BuffTags 配置并缓存。
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(BuffTags)}");
            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<BuffTags>(path);
            }
#else
            // 运行时从 Resources 固定路径加载，保持现有资源引用规则不变。
            _instance = Resources.Load<BuffTags>("BuffTagsData");
#endif
            return _instance;
        }

        [SerializeField, LabelText("预设 Buff 标签"), InfoBox("左侧填写显示名称（推荐中文），右侧填写运行时标签名（必须稳定，建议英文）。", InfoMessageType.Info), OnCollectionChanged(nameof(RebuildDropdown))]
        private List<TagPair<string>> _buffTags = new();

        [NonSerialized] private readonly ValueDropdownList<string> _defaultBuffTags = new();
        public IEnumerable<ValueDropdownItem<string>> DefaultBuffTags => _defaultBuffTags;
        public List<TagPair<string>> BuffTagPairs => _buffTags;

#if UNITY_EDITOR
        private void OnValidate() => RebuildDropdown();
#endif

        private void RebuildDropdown()
        {
            _defaultBuffTags.Clear();
            if (_buffTags == null) return;

            foreach (TagPair<string> tag in _buffTags)
            {
                if (string.IsNullOrEmpty(tag.Second)) continue;
                _defaultBuffTags.Add(tag.First, tag.Second);
            }
        }
    }

    [Serializable]
    public struct TagPair<T>
    {
        [LabelText("显示名称")]
        public T First;

        [LabelText("运行时标签名")]
        public T Second;
    }
}
