using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace BuffSystem
{
    public enum BuffInstanceType { normal, parallel }
    [CreateAssetMenu(menuName = "BuffSystem/BuffConfigData", fileName = "BuffConfigData")]
    public class BuffConfigData : ScriptableObject
    {
        [BoxGroup("基础信息"), LabelText("ID")]
        public int ID;
        [BoxGroup("基础信息"), LabelText("名称")]
        public string Name;
        [BoxGroup("基础信息"), LabelText("描述"), Multiline]
        public string Description;
        [BoxGroup("基础信息"), LabelText("图标")]
        public Sprite Icon;
        [BoxGroup("基础信息"), LabelText("优先级"), Tooltip("数值越低，优先级越高")]
        public int Priority;
        [BoxGroup("基础信息"), LabelText("Tags"), ValueDropdown(nameof(GetDefaultTags), NumberOfItemsBeforeEnablingSearch = 8)]
        public List<string> Tags;

        private static IEnumerable<ValueDropdownItem<string>> GetDefaultTags()
        {
            var data = BuffTags.GetOrFind();
            return data != null ? data.DefaultBuffTags : Array.Empty<ValueDropdownItem<string>>();
        }
        [BoxGroup("生命周期配置"), LabelText("无层数上限")]
        public bool Unlimited = false;
        [BoxGroup("生命周期配置"), LabelText("最大层数"), MinValue(0), HideIf("@Unlimited")]
        public int MaxStack = 1;
        [BoxGroup("生命周期配置"), LabelText("永久Buff")]
        public bool IsForever = false;
        [BoxGroup("生命周期配置"), LabelText("持续时间"), MinValue(0), HideIf("@IsForever")]
        public float Duration = 1;
        [BoxGroup("生命周期配置"), LabelText("Buff触发类型")]
        public BuffTriggerType BuffTriggerType;
        private bool IsTick => BuffTriggerType == BuffTriggerType.Tick;
        [BoxGroup("生命周期配置"), LabelText("周期型Buff的触发周期"), ShowIf("@IsTick"), InfoBox("当前触发周期大于buff持续时间", infoMessageType: InfoMessageType.Warning, VisibleIf = "@TickTime>Duration")]
        public float TickTime = 0;
        [BoxGroup("生命周期配置")]
        public BuffInstanceType BuffType = BuffInstanceType.normal;
        [FoldoutGroup("生命周期配置/Buff层数改变策略", VisibleIf = "@BuffType==BuffInstanceType.normal"), LabelText("Buff层数增加方案")]
        public string BuffStackUpStrategyID;
        [FoldoutGroup("生命周期配置/Buff层数改变策略"), LabelText("预设方案")]
        [ValueDropdown(nameof(BuffStackUpStrategyOptions), NumberOfItemsBeforeEnablingSearch = 8)]
        [OnValueChanged(nameof(ApplyPresetToBuffStackUpStrategyId))]
        [SerializeField]
        private string _presetPick1;
        private static IEnumerable<ValueDropdownItem<string>> BuffStackUpStrategyOptions =>
            new ValueDropdownList<string>
            {
            { "重置计时", "ResetRuntimeBuffStackUpStrategy" },
            { "叠加时长", "AddDurationBuffStackUpStrategy" },
            { "仅叠层", "AddStackOnlyBuffStackUpStrategy" },
            { "环形仅叠层(与最大层数取余)","CyclicallyAddStackOnlyBuffStackUpStrategy" },
            { "叠层并重置计时", "AddStackAndResetRuntimeBuffStackUpStrategy" },
            };

        private void ApplyPresetToBuffStackUpStrategyId()
        {
            if (!string.IsNullOrEmpty(_presetPick1))
                BuffStackUpStrategyID = _presetPick1;
        }
        [FoldoutGroup("生命周期配置/Buff层数改变策略"), LabelText("Buff层数减少方案")]
        public string BuffStackDownStrategyID;
        [FoldoutGroup("生命周期配置/Buff层数改变策略"), LabelText("预设方案")]
        [ValueDropdown(nameof(BuffStackDownStrategyOptions), NumberOfItemsBeforeEnablingSearch = 8)]
        [OnValueChanged(nameof(ApplyPresetToBuffStackDownStrategyId))]
        [SerializeField]
        private string _presetPick2;
        private IEnumerable<ValueDropdownItem<string>> BuffStackDownStrategyOptions =>
            new ValueDropdownList<string>
            {
                {"减少层数" ,"ReduceBuffStackDownStrategy"},
                {"清空层数" ,"ClearBuffStackDownStrategy"},
            };
        private void ApplyPresetToBuffStackDownStrategyId()
        {
            if (!string.IsNullOrEmpty(_presetPick2))
                BuffStackDownStrategyID = _presetPick2;
        }
        [FoldoutGroup("生命周期配置/ParallelBuff层数改变策略", VisibleIf = "@BuffType!=BuffInstanceType.normal"), LabelText("ParallelBuff层数增加方案")]
        public string ParallelBuffStackUpStrategyID;
        [FoldoutGroup("生命周期配置/ParallelBuff层数改变策略"), LabelText("预设方案")]
        [ValueDropdown(nameof(ParallelBuffStackUpStrategyOptions), NumberOfItemsBeforeEnablingSearch = 8)]
        [OnValueChanged(nameof(ApplyPresetToParallelBuffStackUpStrategyId))]
        [SerializeField]
        private string _presetPick3;
        private IEnumerable<ValueDropdownItem<string>> ParallelBuffStackUpStrategyOptions =>
            new ValueDropdownList<string>
            {
                {"新增层，每层独立持续时间" ,"ParallelAppendStackUpStrategy"},
                {"不新增总层数，优先刷新最早到期的层" ,"ParallelRefreshEarliestUpStrategy"},
                {"所有现有层统一续满，再按需要补新增层","ParallelRefreshAllUpStrategy" },
                {"满层时，不丢这次叠加，而是替换最早到期层","ParallelReplaceEarliestWhenFullUpStrategy" }
            };
        private void ApplyPresetToParallelBuffStackUpStrategyId()
        {
            if (!string.IsNullOrEmpty(_presetPick3))
                ParallelBuffStackUpStrategyID = _presetPick3;
        }
        [FoldoutGroup("生命周期配置/ParallelBuff层数改变策略", VisibleIf = "@BuffType!=BuffInstanceType.normal"), LabelText("ParallelBuff层数减少方案")]
        public string ParallelBuffStackDownStrategyID;
        [FoldoutGroup("生命周期配置/ParallelBuff层数改变策略"), LabelText("预设方案")]
        [ValueDropdown(nameof(ParallelBuffStackDownStrategyOptions), NumberOfItemsBeforeEnablingSearch = 8)]
        [OnValueChanged(nameof(ApplyPresetToParallelBuffStackDownStrategyId))]
        [SerializeField]
        private string _presetPick4;
        private IEnumerable<ValueDropdownItem<string>> ParallelBuffStackDownStrategyOptions =>
            new ValueDropdownList<string>
            {
                {"移除最早到期层" ,"ParallelRemoveEarliestDownStrategy"},
                {"移除最新加入层" ,"ParallelRemoveLatestDownStrategy"},
                {"清空","ParallelClearAllDownStrategy" },
            };
        private void ApplyPresetToParallelBuffStackDownStrategyId()
        {
            if (!string.IsNullOrEmpty(_presetPick4))
                ParallelBuffStackDownStrategyID = _presetPick4;
        }






        [BoxGroup("生命周期配置"), LabelText("每层增加时间等于Buff持续时间")]
        public bool IsEqualDuration = true;
        [BoxGroup("生命周期配置"), LabelText("每层增加时间"), Tooltip("当层数影响Buff持续时间时,每一层增加多少时间"), ShowIf("@!IsEqualDuration")]
        public float DurationExtendPerStack = -1;

        [BoxGroup("Buff效果配置"), LabelText("Buff效果")]
        public BuffEffect BuffEffect;
        /// <summary>
        /// 拷贝数据，防止污染配置文件
        /// </summary>
        /// <param name="emptyData"></param>
        public virtual void CopyTo(BuffConfigData emptyData)
        {
            if (emptyData == null)
            {
                Debug.LogError("BuffConfigData CopyTo Error:EmptyData Is Null");
                return;
            }
            emptyData.ID = ID;
            emptyData.Name = Name;
            emptyData.Description = Description;
            emptyData.Icon = Icon;
            emptyData.Priority = Priority;
            //深拷贝，避免污染数据
            emptyData.Tags = Tags.ToList();
            emptyData.MaxStack = MaxStack;
            emptyData.IsForever = IsForever;
            emptyData.Duration = Duration;
            emptyData.Unlimited = Unlimited;
            emptyData.TickTime = TickTime;
            emptyData.BuffStackUpStrategyID = BuffStackUpStrategyID;
            emptyData.BuffStackDownStrategyID = BuffStackDownStrategyID;
            emptyData.DurationExtendPerStack = IsEqualDuration ? Duration : DurationExtendPerStack;
            emptyData.BuffTriggerType = BuffTriggerType;
            emptyData.BuffEffect = BuffEffect;
            emptyData.BuffType = BuffType;
            emptyData.ParallelBuffStackDownStrategyID = ParallelBuffStackDownStrategyID;
            emptyData.ParallelBuffStackUpStrategyID = ParallelBuffStackUpStrategyID;
        }
    }
}
