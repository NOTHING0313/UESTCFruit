using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// EffectId 的策划配置目录，只用于编辑器选择和说明展示，运行时模拟仍只读取 EffectId。
    /// </summary>
    [CreateAssetMenu(menuName = "BuffSystem/Buff Effect Catalog", fileName = "BuffEffectCatalogData")]
    public sealed class BuffEffectCatalogData : ScriptableObject
    {
        private static BuffEffectCatalogData _instance;

        [SerializeField, LabelText("Effect 目录"), TableList(AlwaysExpanded = true)]
        private List<BuffEffectCatalogEntry> _entries = new List<BuffEffectCatalogEntry>();

        public IReadOnlyList<BuffEffectCatalogEntry> Entries => _entries;

        /// <summary>
        /// 查找工程中的 Effect 目录；找不到时返回 null，BuffConfigData 会退回手动填写。
        /// </summary>
        public static BuffEffectCatalogData GetOrFind()
        {
            if (_instance != null)
                return _instance;

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets($"t:{nameof(BuffEffectCatalogData)}");

            if (guids != null && guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _instance = UnityEditor.AssetDatabase.LoadAssetAtPath<BuffEffectCatalogData>(path);
            }
#else
            _instance = Resources.Load<BuffEffectCatalogData>("BuffEffectCatalogData");
#endif
            return _instance;
        }

        public bool TryGetEntry(int effectId, out BuffEffectCatalogEntry entry)
        {
            entry = default;

            if (_entries == null)
                return false;

            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].EffectId == effectId)
                {
                    entry = _entries[i];
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// 单个 Buff Effect 的编辑器说明项，帮助策划用显示名选择 EffectId。
    /// </summary>
    [Serializable]
    public struct BuffEffectCatalogEntry
    {
        [VerticalGroup, LabelText("EffectId"), MinValue(1)]
        public int EffectId;

        [VerticalGroup, LabelText("显示名")]
        public string DisplayName;

        [VerticalGroup, LabelText("说明"), Multiline]
        public string Description;

        [VerticalGroup, LabelText("支持的触发类型")]
        public BuffTriggerType[] SupportedTriggerTypes;

        [VerticalGroup, LabelText("开发备注"), Multiline]
        public string DeveloperNote;

        /// <summary>
        /// 判断该 Effect 是否声明支持指定触发类型；未配置支持列表时视为不限制。
        /// </summary>
        public bool Supports(BuffTriggerType triggerType)
        {
            if (SupportedTriggerTypes == null || SupportedTriggerTypes.Length == 0)
                return true;

            for (int i = 0; i < SupportedTriggerTypes.Length; i++)
            {
                if (SupportedTriggerTypes[i] == triggerType)
                    return true;
            }

            return false;
        }
    }
}
