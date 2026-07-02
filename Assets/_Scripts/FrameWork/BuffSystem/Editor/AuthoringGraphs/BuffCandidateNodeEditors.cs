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

    [NodeEditor.CustomNodeEditor(typeof(BuffRootNode))]
    internal sealed class BuffRootNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "旧 Buff 根 / Legacy Root";
        protected override int NodeWidth => 280;

        public override void OnBodyGUI()
        {
            EditorGUILayout.HelpBox("兼容旧图使用。新图请优先使用 Candidate Start + Effect Composition Root；Effects 连接只表示成员关系，不表示顺序。", MessageType.Warning);
            DrawCandidateBody(
                Field("ConfigId", "配置 ID"),
                Field("BuffName", "Buff 名称"),
                Field("Description", "描述"),
                Field("Owner", "负责人"),
                Field("Notes", "备注"),
                Field("Effects", "Effects"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(EffectCompositionRootNode))]
    internal sealed class EffectCompositionRootNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "Effect 组合根 / Composition";
        protected override int NodeWidth => 320;

        public override void OnBodyGUI()
        {
            EditorGUILayout.HelpBox("推荐的新 Effect 组合入口。Effects 连接表示成员关系；显式顺序使用 EffectNode.Next，否则使用 ExecutionOrder。", MessageType.Info);
            DrawCandidateBody(
                Field("Candidate", "候选入口"),
                Section("最终 Effect"),
                Field("FinalEffectId", "最终 Effect ID"),
                Field("FinalEffectName", "最终 Effect 名称"),
                Field("FinalEffectClassName", "最终 Effect 类名"),
                Field("CompositionMode", "组合模式"),
                Field("Owner", "负责人"),
                Field("Description", "描述"),
                Field("Notes", "备注"),
                Field("Effects", "Effects"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(EffectNode))]
    internal sealed class EffectNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "Effect 节点";
        protected override int NodeWidth => 320;

        public override void OnBodyGUI()
        {
            DrawCandidateBody(
                Field("Previous", "Buff / 上一步"),
                Section("Effect 信息"),
                Field("EffectId", "Effect ID"),
                Field("EffectName", "Effect 名称"),
                Field("EffectClassName", "Effect 类名"),
                Field("ExecutionOrder", "执行顺序"),
                Field("Description", "描述"),
                Field("Notes", "备注"),
                Section("生命周期"),
                Field("OnApply", "OnApply"),
                Field("OnTick", "OnTick"),
                Field("OnRemove", "OnRemove"),
                Field("OnRefresh", "OnRefresh"),
                Field("OnStackChanged", "OnStackChanged"),
                Field("Next", "Next"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(EmptyActionPlaceholderNode))]
    internal sealed class EmptyActionPlaceholderNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "已废弃占位 / Deprecated";
        protected override int NodeWidth => 260;

        public override void OnBodyGUI()
        {
            EditorGUILayout.HelpBox("该占位节点已废弃，只保留兼容旧图；不会生成可运行调用。请改用 ScriptActionNode。", MessageType.Warning);
            DrawCandidateBody(
                Field("Previous", "上一步"),
                Field("ActionName", "Action 名称"),
                Field("Description", "描述"),
                Field("Todo", "TODO"),
                Field("Next", "下一步"));
        }
    }

    [NodeEditor.CustomNodeEditor(typeof(ScriptActionNode))]
    internal sealed class ScriptActionNodeEditor : BuffCandidateNodeEditorBase
    {
        protected override string HeaderTitle => "功能节点 / Script Action";
        protected override int NodeWidth => 320;

        public override void OnBodyGUI()
        {
            serializedObject.Update();

            float previousLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 108f;

            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("Previous"), new GUIContent("上一步"), true);
            EditorGUILayout.Space(2f);
            EditorGUILayout.LabelField("脚本", EditorStyles.boldLabel);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("ActionName"), new GUIContent("Action 名称"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("ActionScript"), new GUIContent("Action 脚本"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("ActionTypeName"), new GUIContent("类型名"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("ActionDisplayName"), new GUIContent("显示名"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("ExecutionOrder"), new GUIContent("执行顺序"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("IsValidAction"), new GUIContent("校验有效"), true);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("Description"), new GUIContent("描述"), true);
            GUILayout.Space(4f);
            NodeEditorGUILayout.PropertyField(serializedObject.FindProperty("Next"), new GUIContent("下一步"), true);

            EditorGUIUtility.labelWidth = previousLabelWidth;
            serializedObject.ApplyModifiedProperties();

            ScriptActionNode node = serializedObject.targetObject as ScriptActionNode;
            BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(node);
            string message = node != null ? node.ValidationMessage : string.Empty;
            if (!string.IsNullOrWhiteSpace(message))
            {
                MessageType type = validation.Errors.Count > 0
                    ? MessageType.Error
                    : (validation.Warnings.Count > 0 ? MessageType.Warning : MessageType.Info);
                EditorGUILayout.HelpBox(message, type);
            }
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
        protected override string HeaderTitle => "旧 Effect 绑定 / Legacy";
        protected override int NodeWidth => 280;

        public override void OnBodyGUI()
        {
            EditorGUILayout.HelpBox("旧节点仅为旧图兼容保留。新图请使用 EffectCompositionRootNode + EffectNode；存在新组合结构时，CompositeEffect 会忽略该节点。", MessageType.Warning);
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
