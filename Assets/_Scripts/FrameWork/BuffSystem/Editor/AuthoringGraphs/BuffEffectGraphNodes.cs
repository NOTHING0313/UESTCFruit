using UnityEngine;
using UnityEditor;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 旧版 Buff 图根节点。保留用于兼容已有图，新图优先使用 CandidateStart + EffectCompositionRoot。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Deprecated/Legacy Buff Root")]
    public sealed class BuffRootNode : Node
    {
        public int ConfigId;
        public string BuffName;

        [TextArea(3, 6)]
        public string Description;

        public string Owner;

        [TextArea(3, 8)]
        public string Notes;

        [Output]
        public string Effects;
    }

    /// <summary>
    /// Effect 组合根节点。Root.Effects 表示组合成员关系，不表示执行顺序；顺序由 EffectNode.Next 或 ExecutionOrder 决定。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Effect Composition Root")]
    public sealed class EffectCompositionRootNode : Node
    {
        [Input]
        public string Candidate;

        public int FinalEffectId;
        public string FinalEffectName;
        public string FinalEffectClassName;
        public string CompositionMode = "SingleMainEffect";
        public string Owner;

        [TextArea(3, 6)]
        public string Description;

        [TextArea(3, 8)]
        public string Notes;

        [Output]
        public string Effects;
    }

    /// <summary>
    /// Effect 设计节点。用于表达 EffectId、类名、执行顺序和生命周期端口；只有用户在 Effect Template 中手动生成时才会输出草稿代码。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Effect")]
    public sealed class EffectNode : Node
    {
        [Input]
        public string Previous;

        public int EffectId;
        public string EffectName;
        public string EffectClassName;
        public int ExecutionOrder;

        [TextArea(3, 6)]
        public string Description;

        [TextArea(3, 8)]
        public string Notes;

        [Output]
        public string OnApply;

        [Output]
        public string OnTick;

        [Output]
        public string OnRemove;

        [Output]
        public string OnRefresh;

        [Output]
        public string OnStackChanged;

        [Output]
        public string Next;
    }

    /// <summary>
    /// 已废弃的生命周期功能占位节点。保留兼容旧图；新图应使用 ScriptActionNode。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Deprecated/Action Placeholder")]
    public sealed class EmptyActionPlaceholderNode : Node
    {
        [Input]
        public string Previous;

        public string ActionName;

        [TextArea(3, 6)]
        public string Description;

        [TextArea(3, 8)]
        public string Todo;

        [Output]
        public string Next;
    }

    /// <summary>
    /// 脚本功能节点。仅用于 Editor 图形化 authoring、审查和 Effect 草稿调用链生成，不进入 runtime。
    /// </summary>
    [Node.CreateNodeMenu("BuffSystem/Script Action")]
    public sealed class ScriptActionNode : Node
    {
        [Input]
        public string Previous;

        public string ActionName;
        public MonoScript ActionScript;
        public string ActionTypeName;
        public string ActionDisplayName;
        public bool IsValidAction;
        public string ValidationMessage;

        [TextArea(3, 6)]
        public string Description;

        public int ExecutionOrder;

        [Output]
        public string Next;
    }
}
