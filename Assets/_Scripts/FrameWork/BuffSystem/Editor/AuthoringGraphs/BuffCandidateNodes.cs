using UnityEngine;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 候选图起点。每个 BuffCandidateGraph 必须有且只能有一个。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Candidate Start")]
    public sealed class BuffCandidateStartNode : Node
    {
        public int ConfigId;
        public string BuffName;

        [TextArea(3, 6)]
        public string DesignPurpose;

        public string Owner;
        public bool IsGameplayBuffCandidate;

        [TextArea(3, 8)]
        public string Notes;

        [Output]
        public string Next;
    }

    /// <summary>
    /// 描述候选 Buff 的基础形态。
    /// 第一版使用 string 字段，避免把 authoring graph 与 runtime enum 强绑定。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Buff Shape")]
    public sealed class BuffShapeNode : Node
    {
        [Input]
        public string Previous;

        public string BuffType = "parallel";
        public string TriggerType = "Tick";
        public string ParallelStorageMode = "EntityPerStack";
        public bool Unlimited;
        public int MaxStack = 1;
        public int DurationFrames = 1;
        public int TickIntervalFrames = 1;
        public string StackUpPolicy = "Append";
        public string StackDownPolicy = "RemoveEarliest";

        [Output]
        public string Next;
    }

    /// <summary>
    /// 描述候选 Buff 的 Effect 绑定状态。
    /// EffectRegistered 第一版由人工填写，不自动反射 registry。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Effect Binding")]
    public sealed class EffectBindingNode : Node
    {
        [Input]
        public string Previous;

        public int EffectId;
        public string EffectClassName;
        public bool EffectRegistered;
        public string RegistrySnippetPreview;

        [TextArea(3, 8)]
        public string EffectRiskNotes;

        [Output]
        public string Next;
    }

    /// <summary>
    /// 压缩并行候选资格节点。
    /// 第一版使用占位 Capacity，后续应与 runtime capacity 文档或只读工具结果对齐。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Compressed Eligibility")]
    public sealed class CompressedEligibilityNode : Node
    {
        private const int DefaultCompressedLayerCapacity = 8;

        [Input]
        public string Previous;

        public bool Eligible;

        [TextArea(3, 8)]
        public string RejectReasons;

        [TextArea(3, 8)]
        public string Warnings;

        [Tooltip("第一版仅作为 authoring 提示，不能修改 runtime capacity。")]
        public int CapacityHint = DefaultCompressedLayerCapacity;

        [Output]
        public string Next;
    }

    /// <summary>
    /// runtime 依赖风险节点。用于记录候选 Buff 是否依赖不适合 compressed path 的行为。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Runtime Dependency Risk")]
    public sealed class RuntimeDependencyRiskNode : Node
    {
        [Input]
        public string Previous;

        public bool DependsOnLayerRuntimeEntity;
        public bool DependsOnEventTrigger;
        public bool DependsOnViewOrUnityObject;
        public bool DependsOnUnityFrameTimeApi;
        public bool DependsOnNonDeterministicRandom;
        public bool NeedsRollbackProof;
        public string RiskLevel = "Low";

        [TextArea(3, 8)]
        public string RiskNotes;

        [TextArea(3, 8)]
        public string Warnings;

        [TextArea(3, 8)]
        public string RejectReasons;

        [Output]
        public string Next;
    }

    /// <summary>
    /// 候选结论节点。只显示审查结论，不修改 whitelist，不创建 asset，不注册 Effect。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Candidate Decision")]
    public sealed class CandidateDecisionNode : Node
    {
        [Input]
        public string Previous;

        public bool CanSubmitForReview;
        public bool ShouldReject;

        [TextArea(3, 8)]
        public string RejectReasons;

        [TextArea(3, 8)]
        public string Warnings;

        [TextArea(3, 8)]
        public string NextActions;
    }
}
