using UnityEditor;
using UnityEngine;
using XNodeEditor;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// BuffCandidateGraph 节点的 Editor-only 绘制基类。
    /// 这里只调整 xNode 编辑器中的显示标签和节点宽度，不修改序列化字段、端口契约或 runtime。
    /// </summary>
    internal abstract class BuffCandidateNodeEditorBase : NodeEditor
    {
        private const float LabelWidth = 108f;

        protected abstract string HeaderTitle { get; }
        protected abstract int NodeWidth { get; }

        public override int GetWidth()
        {
            return NodeWidth;
        }

        public override void OnHeaderGUI()
        {
            GUILayout.Label(HeaderTitle, NodeEditorResources.styles.nodeHeader, GUILayout.Height(30));
        }

        protected void DrawCandidateBody(params FieldLabel[] fields)
        {
            serializedObject.Update();

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = LabelWidth;

            for (int i = 0; i < fields.Length; i++)
            {
                FieldLabel field = fields[i];
                if (field.IsSection)
                {
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField(field.Label.text, EditorStyles.boldLabel);
                    continue;
                }

                SerializedProperty property = serializedObject.FindProperty(field.FieldName);
                if (property == null)
                    continue;

                NodeEditorGUILayout.PropertyField(property, field.Label, true);
            }

            EditorGUIUtility.labelWidth = previousLabelWidth;
            serializedObject.ApplyModifiedProperties();
        }

        protected static FieldLabel Section(string label)
        {
            return new FieldLabel(null, new GUIContent(label), true);
        }

        protected static FieldLabel Field(string fieldName, string label, string tooltip = null)
        {
            return new FieldLabel(fieldName, new GUIContent(label, tooltip), false);
        }

        protected struct FieldLabel
        {
            public readonly string FieldName;
            public readonly GUIContent Label;
            public readonly bool IsSection;

            public FieldLabel(string fieldName, GUIContent label, bool isSection)
            {
                FieldName = fieldName;
                Label = label;
                IsSection = isSection;
            }
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(BuffCandidateStartNode))]
    internal sealed class BuffCandidateStartNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "候选起点 / Start";
        protected override int NodeWidth => 260;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("ConfigId", "配置 ID"),
                Field("BuffName", "Buff 名称"),
                Field("DesignPurpose", "设计目的"),
                Field("Owner", "负责人"),
                Field("IsGameplayBuffCandidate", "玩法候选"),
                Field("Notes", "备注"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(BuffShapeNode))]
    internal sealed class BuffShapeNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "Buff 形态 / Shape";
        protected override int NodeWidth => 280;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Section("基础形态"),
                Field("BuffType", "Buff 类型"),
                Field("TriggerType", "触发类型"),
                Field("ParallelStorageMode", "存储模式"),
                Field("Unlimited", "无限时长"),
                Field("MaxStack", "最大层数"),
                Field("DurationFrames", "持续帧"),
                Field("TickIntervalFrames", "Tick 间隔"),
                Field("StackUpPolicy", "叠加策略"),
                Field("StackDownPolicy", "移除策略"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(EffectBindingNode))]
    internal sealed class EffectBindingNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "Effect 绑定";
        protected override int NodeWidth => 280;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Field("EffectId", "Effect ID"),
                Field("EffectClassName", "Effect 类名"),
                Field("EffectRegistered", "已注册"),
                Field("RegistrySnippetPreview", "注册预览"),
                Field("EffectRiskNotes", "风险备注"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(CompressedEligibilityNode))]
    internal sealed class CompressedEligibilityNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "压缩资格 / Eligibility";
        protected override int NodeWidth => 300;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Field("Eligible", "候选资格"),
                Field("RejectReasons", "拒绝原因"),
                Field("Warnings", "警告"),
                Field("CapacityHint", "容量提示"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(RuntimeDependencyRiskNode))]
    internal sealed class RuntimeDependencyRiskNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "Runtime 风险";
        protected override int NodeWidth => 320;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Section("依赖风险"),
                Field("DependsOnLayerRuntimeEntity", "逐层实体"),
                Field("DependsOnEventTrigger", "EventTrigger"),
                Field("DependsOnViewOrUnityObject", "View/对象"),
                Field("DependsOnUnityFrameTimeApi", "Unity 帧时"),
                Field("DependsOnNonDeterministicRandom", "非确定随机"),
                Field("NeedsRollbackProof", "需回滚证明"),
                Field("RiskLevel", "风险等级"),
                Field("RiskNotes", "风险备注"),
                Field("Warnings", "警告"),
                Field("RejectReasons", "拒绝原因"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(CandidateDecisionNode))]
    internal sealed class CandidateDecisionNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "候选结论 / Decision";
        protected override int NodeWidth => 300;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Field("CanSubmitForReview", "可提交审查"),
                Field("ShouldReject", "应拒绝"),
                Field("RejectReasons", "拒绝原因"),
                Field("Warnings", "警告"),
                Field("NextActions", "下一步"));
        }
    }
}
