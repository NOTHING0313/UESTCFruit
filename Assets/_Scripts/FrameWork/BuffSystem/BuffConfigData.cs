using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace BuffSystem
{
    /// <summary>
    /// Buff 策划配置资产；仅作为 Authoring 输入，运行时会转换为纯数据 BuffDefinition。
    /// </summary>
    [CreateAssetMenu(menuName = "BuffSystem/BuffConfigData", fileName = "BuffConfigData")]
    public class BuffConfigData : ScriptableObject
    {
        private const float PreviewTickLength = 0.02f;
        private const int ParallelStackWarningThreshold = 32;

        [BoxGroup("基础信息"), LabelText("ID"), Tooltip("Buff 的唯一配置编号，必须大于 0。")]
        [ValidateInput(nameof(IsValidId), "ID 必须大于 0。")]
        public int ID;

        [BoxGroup("基础信息"), LabelText("名称"), Tooltip("给策划、调试和表现层阅读的 Buff 名称。")]
        [ValidateInput(nameof(IsValidName), "名称不能为空。")]
        public string Name;

        [BoxGroup("基础信息"), LabelText("描述"), Multiline, Tooltip("面向策划或 UI 文案的说明，不参与运行时逻辑。")]
        public string Description;

        [BoxGroup("基础信息"), LabelText("图标"), PreviewField(56), Tooltip("表现层可使用的图标引用，不参与 ECS 模拟。")]
        public Sprite Icon;

        [BoxGroup("基础信息"), LabelText("优先级"), Tooltip("同一事件命中多个 Buff 时，优先级越小越早执行。")]
        public int Priority;

        [BoxGroup("基础信息"), LabelText("标签"), Tooltip("用于配置筛选和批量查询的标签。")]
        [ValueDropdown(nameof(GetDefaultTags), NumberOfItemsBeforeEnablingSearch = 8)]
        public List<string> Tags = new List<string>();

        [BoxGroup("生命周期"), LabelText("是否永久"), Tooltip("永久 Buff 不会因剩余帧数归零而自动移除。")]
        public bool IsForever = false;

        [BoxGroup("生命周期"), LabelText("持续时间（秒）"), MinValue(0), HideIf(nameof(IsForever))]
        [Tooltip("非永久 Buff 的持续时间，会按固定帧长度转换为 DurationFrames。")]
        [ValidateInput(nameof(IsValidDuration), "非永久 Buff 的持续时间必须大于 0。")]
        public float Duration = 1f;

        [BoxGroup("生命周期"), LabelText("触发类型"), Tooltip("Tick 表示按固定帧间隔触发；EventTrigger 表示响应 IGameEvent。")]
        public BuffTriggerType BuffTriggerType;

        [BoxGroup("生命周期"), LabelText("Tick 间隔（秒）"), MinValue(0), ShowIf(nameof(IsTick))]
        [Tooltip("Tick 类型 Buff 的触发间隔，会按固定帧长度转换为 TickIntervalFrames。")]
        [ValidateInput(nameof(IsValidTickTime), "Tick 类型 Buff 的 Tick 间隔必须大于 0。")]
        public float TickTime = 0f;

        [BoxGroup("堆叠规则"), LabelText("Buff 类型"), Tooltip("普通 Buff 使用一个 Runtime Entity 保存层数；并行 Buff 每层一个 Runtime Entity。")]
        public BuffInstanceType BuffType = BuffInstanceType.normal;

        [BoxGroup("堆叠规则"), LabelText("是否无限层数"), Tooltip("启用后 MaxStack 不限制运行时层数。")]
        public bool Unlimited = false;

        [BoxGroup("堆叠规则"), LabelText("最大层数"), MinValue(1), HideIf(nameof(Unlimited))]
        [Tooltip("普通 Buff 的最大叠层，或并行 Buff 的最大并行层数。")]
        [ValidateInput(nameof(IsValidMaxStack), "最大层数必须大于 0，除非开启无限层数。")]
        [ValidateInput(nameof(IsParallelStackCountSafe), "并行 Buff 最大层数较大，可能带来 Runtime Entity、排序和回滚快照成本。", InfoMessageType.Warning)]
        public int MaxStack = 1;

        [BoxGroup("堆叠规则"), LabelText("普通 Buff 叠层策略"), ShowIf(nameof(IsNormalBuff))]
        [Tooltip("普通 Buff 重复添加时如何处理层数和持续时间。")]
        public NormalBuffStackPolicy NormalStackPolicy = NormalBuffStackPolicy.RefreshDuration;

        [BoxGroup("堆叠规则"), LabelText("并行 Buff 叠层策略"), HideIf(nameof(IsNormalBuff))]
        [Tooltip("并行 Buff 添加新层时如何处理已有层。")]
        public ParallelBuffStackUpPolicy ParallelStackUpPolicy = ParallelBuffStackUpPolicy.Append;

        [BoxGroup("堆叠规则"), LabelText("并行 Buff 移除策略"), HideIf(nameof(IsNormalBuff))]
        [Tooltip("并行 Buff 移除层数时优先移除哪一层。")]
        public ParallelBuffStackDownPolicy ParallelStackDownPolicy = ParallelBuffStackDownPolicy.RemoveEarliest;

        [BoxGroup("堆叠规则"), LabelText("并行 Buff 存储模式"), HideIf(nameof(IsNormalBuff))]
        [Tooltip("Phase 3B 预留配置入口：当前运行时尚未启用压缩模式，所有并行 Buff 仍然走 EntityPerStack 主流程。")]
        public ParallelBuffStorageMode ParallelStorageMode = ParallelBuffStorageMode.EntityPerStack;

        [BoxGroup("堆叠规则"), LabelText("每层延长时间（秒）"), MinValue(0), ShowIf(nameof(UsesDurationExtension))]
        [Tooltip("仅普通 Buff 的 AddDuration 策略使用。")]
        public float DurationExtendPerStack = 0f;

        [BoxGroup("效果配置"), LabelText("EffectId"), Tooltip("运行时只保存这个整数 ID；推荐从 Effect 目录下拉选择。")]
        [ValueDropdown(nameof(GetEffectIdDropdown), NumberOfItemsBeforeEnablingSearch = 8)]
        [ValidateInput(nameof(IsValidEffectId), "EffectId 必须大于 0。")]
        [ValidateInput(nameof(IsEffectSupportedForTrigger), "选择的 Effect 未声明支持当前触发类型。", InfoMessageType.Warning)]
        public int EffectId = 0;

        [BoxGroup("效果配置"), ShowInInspector, ReadOnly, LabelText("Effect 显示名")]
        [InfoBox("未找到 BuffEffectCatalogData，当前允许手动填写 EffectId。", InfoMessageType.Warning, nameof(IsEffectCatalogMissing))]
        private string EffectDisplayName => GetEffectDisplayName();

        [BoxGroup("效果配置"), LabelText("事件触发列表"), ShowIf(nameof(IsEventTrigger))]
        [Tooltip("事件 Buff 只有在收到匹配 EventId 的 IGameEvent 时才会触发。")]
        [InfoBox("事件 Buff 只有在收到匹配 EventId 的 IGameEvent 时才会触发。", InfoMessageType.Info, nameof(IsEventTrigger))]
        [ValueDropdown(nameof(GetEventIdDropdown), NumberOfItemsBeforeEnablingSearch = 8)]
        [ValidateInput(nameof(IsValidEventIds), "EventTrigger 类型 Buff 必须至少选择一个 EventId。")]
        public List<int> EventIds = new List<int>();

        [BoxGroup("调试信息"), ShowInInspector, ReadOnly, LabelText("持续帧数预览")]
        private int DurationFramesPreview => IsForever ? 0 : SecondsToFrameCount(Duration, PreviewTickLength);

        [BoxGroup("调试信息"), ShowInInspector, ReadOnly, LabelText("Tick 间隔帧数预览")]
        private int TickIntervalFramesPreview => IsTick ? SecondsToFrameCount(TickTime, PreviewTickLength) : 0;

        [BoxGroup("调试信息"), ShowInInspector, ReadOnly, LabelText("当前触发类型说明")]
        private string TriggerTypeDescription => GetTriggerTypeDescription();

        [BoxGroup("调试信息"), ShowInInspector, ReadOnly, LabelText("当前堆叠策略说明")]
        private string StackPolicyDescription => GetStackPolicyDescription();

        [BoxGroup("调试信息"), ShowInInspector, ReadOnly, LabelText("当前配置校验结果"), MultiLineProperty(5)]
        private string ValidationSummary => BuildValidationSummary();

        private bool IsTick => BuffTriggerType == BuffTriggerType.Tick;
        private bool IsEventTrigger => BuffTriggerType == BuffTriggerType.EventTrigger;
        private bool IsNormalBuff => BuffType == BuffInstanceType.normal;
        private bool UsesDurationExtension => IsNormalBuff && NormalStackPolicy == NormalBuffStackPolicy.AddDuration;

        private static IEnumerable<ValueDropdownItem<string>> GetDefaultTags()
        {
            BuffTags data = BuffTags.GetOrFind();
            return data != null ? data.DefaultBuffTags : Array.Empty<ValueDropdownItem<string>>();
        }

        private IEnumerable<ValueDropdownItem<int>> GetEffectIdDropdown()
        {
            BuffEffectCatalogData catalog = BuffEffectCatalogData.GetOrFind();

            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
            {
                yield return new ValueDropdownItem<int>($"手动填写：{EffectId}", EffectId);
                yield break;
            }

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                BuffEffectCatalogEntry entry = catalog.Entries[i];

                if (entry.EffectId <= 0)
                    continue;

                string displayName = string.IsNullOrEmpty(entry.DisplayName) ? "未命名 Effect" : entry.DisplayName;
                yield return new ValueDropdownItem<int>($"{entry.EffectId} - {displayName}", entry.EffectId);
            }
        }

        private IEnumerable<ValueDropdownItem<int>> GetEventIdDropdown()
        {
            BuffEventCatalogData catalog = BuffEventCatalogData.GetOrFind();

            if (catalog == null || catalog.Entries == null || catalog.Entries.Count == 0)
            {
                if (EventIds != null)
                {
                    for (int i = 0; i < EventIds.Count; i++)
                        yield return new ValueDropdownItem<int>($"手动填写：{EventIds[i]}", EventIds[i]);
                }

                yield break;
            }

            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                BuffEventCatalogEntry entry = catalog.Entries[i];

                if (entry.EventId <= 0)
                    continue;

                string displayName = string.IsNullOrEmpty(entry.DisplayName) ? entry.EventKey : entry.DisplayName;

                if (string.IsNullOrEmpty(displayName))
                    displayName = "未命名事件";

                yield return new ValueDropdownItem<int>($"{entry.EventId} - {displayName}", entry.EventId);
            }
        }

        public BuffDefinition ToDefinition(float tickLength)
        {
            int durationFrames = IsForever ? 0 : SecondsToFrameCount(Duration, tickLength);
            int tickIntervalFrames = BuffTriggerType == BuffTriggerType.Tick
                ? SecondsToFrameCount(TickTime, tickLength)
                : 0;
            int durationExtendFrames = SecondsToFrameCount(DurationExtendPerStack, tickLength);

            return new BuffDefinition(
                ID,
                Name,
                Priority,
                MaxStack,
                Unlimited,
                IsForever,
                durationFrames,
                tickIntervalFrames,
                durationExtendFrames,
                BuffTriggerType,
                BuffType,
                NormalStackPolicy,
                ParallelStackUpPolicy,
                ParallelStackDownPolicy,
                EffectId,
                ToEventIdArray(),
                ParallelStorageMode);
        }

        public virtual void CopyTo(BuffConfigData target)
        {
            if (target == null)
                return;

            target.ID = ID;
            target.Name = Name;
            target.Description = Description;
            target.Icon = Icon;
            target.Priority = Priority;
            target.Tags = Tags == null ? new List<string>() : new List<string>(Tags);
            target.IsForever = IsForever;
            target.Duration = Duration;
            target.BuffTriggerType = BuffTriggerType;
            target.TickTime = TickTime;
            target.BuffType = BuffType;
            target.Unlimited = Unlimited;
            target.MaxStack = MaxStack;
            target.NormalStackPolicy = NormalStackPolicy;
            target.ParallelStackUpPolicy = ParallelStackUpPolicy;
            target.ParallelStackDownPolicy = ParallelStackDownPolicy;
            target.ParallelStorageMode = ParallelStorageMode;
            target.DurationExtendPerStack = DurationExtendPerStack;
            target.EffectId = EffectId;
            target.EventIds = EventIds == null ? new List<int>() : new List<int>(EventIds);
        }

        private static int SecondsToFrameCount(float seconds, float tickLength)
        {
            if (seconds <= 0f)
                return 0;

            float safeTickLength = tickLength > 0f ? tickLength : 0.02f;
            return Math.Max(1, (int)Math.Ceiling(seconds / safeTickLength));
        }

        private int[] ToEventIdArray()
        {
            if (EventIds == null || EventIds.Count == 0)
                return Array.Empty<int>();

            int[] eventIds = new int[EventIds.Count];

            for (int i = 0; i < EventIds.Count; i++)
                eventIds[i] = EventIds[i];

            return eventIds;
        }

        private bool IsValidId() => ID > 0;
        private bool IsValidName() => !string.IsNullOrWhiteSpace(Name);
        private bool IsValidMaxStack() => Unlimited || MaxStack > 0;
        private bool IsValidDuration() => IsForever || Duration > 0f;
        private bool IsValidTickTime() => !IsTick || TickTime > 0f;
        private bool IsValidEventIds() => !IsEventTrigger || (EventIds != null && EventIds.Count > 0);
        private bool IsValidEffectId() => EffectId > 0;
        private bool IsParallelStackCountSafe() => BuffType != BuffInstanceType.parallel || Unlimited || MaxStack <= ParallelStackWarningThreshold;

        private bool IsEffectSupportedForTrigger()
        {
            BuffEffectCatalogData catalog = BuffEffectCatalogData.GetOrFind();

            if (catalog == null || !catalog.TryGetEntry(EffectId, out BuffEffectCatalogEntry entry))
                return true;

            return entry.Supports(BuffTriggerType);
        }

        private bool IsEffectCatalogMissing() => BuffEffectCatalogData.GetOrFind() == null;

        private string GetEffectDisplayName()
        {
            BuffEffectCatalogData catalog = BuffEffectCatalogData.GetOrFind();

            if (catalog == null || !catalog.TryGetEntry(EffectId, out BuffEffectCatalogEntry entry))
                return EffectId > 0 ? $"手动填写 EffectId：{EffectId}" : "未选择 Effect";

            return string.IsNullOrEmpty(entry.DisplayName) ? $"EffectId：{EffectId}" : entry.DisplayName;
        }

        private string GetTriggerTypeDescription()
        {
            switch (BuffTriggerType)
            {
                case BuffTriggerType.Tick:
                    return "按固定帧间隔触发 OnTick。";
                case BuffTriggerType.EventTrigger:
                    return "收到匹配 EventId 的 IGameEvent 后触发事件 Effect。";
                default:
                    return "未知触发类型。";
            }
        }

        private string GetStackPolicyDescription()
        {
            if (BuffType == BuffInstanceType.parallel)
                return $"并行 Buff：新增策略 {ParallelStackUpPolicy}，移除策略 {ParallelStackDownPolicy}。";

            return $"普通 Buff：叠层策略 {NormalStackPolicy}。";
        }

        private string BuildValidationSummary()
        {
            List<string> messages = new List<string>();

            if (!IsValidId())
                messages.Add("错误：ID 必须大于 0。");

            if (!IsValidName())
                messages.Add("错误：名称不能为空。");

            if (!IsValidMaxStack())
                messages.Add("错误：最大层数必须大于 0，除非开启无限层数。");

            if (!IsValidDuration())
                messages.Add("错误：非永久 Buff 的持续时间必须大于 0。");

            if (!IsValidTickTime())
                messages.Add("错误：Tick 类型 Buff 的 Tick 间隔必须大于 0。");

            if (!IsValidEventIds())
                messages.Add("错误：EventTrigger 类型 Buff 必须至少选择一个 EventId。");

            if (!IsValidEffectId())
                messages.Add("错误：EffectId 必须大于 0。");

            if (!IsEffectSupportedForTrigger())
                messages.Add("警告：选择的 Effect 未声明支持当前触发类型。");

            if (!IsParallelStackCountSafe())
                messages.Add("警告：并行 Buff 最大层数较大，可能增加 Entity、排序和回滚快照成本。");

            if (messages.Count == 0)
                return "当前配置未发现明显问题。";

            return string.Join("\n", messages);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (BuffTriggerType == BuffTriggerType.EventTrigger && (EventIds == null || EventIds.Count == 0))
                Debug.LogWarning($"BuffConfigData 警告：事件触发 Buff {ID} 未配置 EventIds，运行时不会响应事件。", this);

            if (!IsEffectSupportedForTrigger())
                Debug.LogWarning($"BuffConfigData 警告：Buff {ID} 选择的 EffectId {EffectId} 未声明支持当前触发类型 {BuffTriggerType}。", this);

            if (!IsParallelStackCountSafe())
                Debug.LogWarning($"BuffConfigData 警告：并行 Buff {ID} 的最大层数较大，可能带来性能风险。", this);
        }
#endif
    }
}
