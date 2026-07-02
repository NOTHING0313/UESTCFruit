using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 从 BuffCandidateGraph 构建 Effect 草稿调用链计划。
    /// 构建过程只读取图节点和脚本元信息，不生成文件，不注册 Effect，不修改 runtime。
    /// </summary>
    internal static class BuffGraphEffectCodegenBuilder
    {
        private static readonly Regex IdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

        internal static bool TryBuild(BuffCandidateGraph graph, BuffGraphEffectCodegenRequest request, out BuffGraphEffectCodegenPlan plan)
        {
            plan = new BuffGraphEffectCodegenPlan();
            ApplyRequestDefaults(plan, request);

            if (graph == null)
            {
                plan.Errors.Add("未选择候选图，无法导入 Effect 调用链。");
                return false;
            }

            BuffGraphEffectOrderUtility.OrderResult effectOrder = BuffGraphEffectOrderUtility.Build(graph);
            List<EffectNode> effectNodes = effectOrder.OrderedEffects;
            EffectBindingNode legacyEffect = graph.FindSingleNode<EffectBindingNode>();
            for (int i = 0; i < effectOrder.Errors.Count; i++)
                plan.Errors.Add(effectOrder.Errors[i]);

            for (int i = 0; i < effectOrder.Warnings.Count; i++)
                plan.Warnings.Add(effectOrder.Warnings[i]);

            if (effectNodes.Count == 0)
            {
                if (legacyEffect == null)
                {
                    plan.Errors.Add("候选图缺少 EffectNode / EffectBindingNode，无法构建 Effect 调用链。");
                    return false;
                }

                plan.UsesLegacyEffectBindingNode = true;
                if (legacyEffect.EffectId > 0)
                    plan.EffectId = legacyEffect.EffectId;

                if (!string.IsNullOrWhiteSpace(legacyEffect.EffectClassName))
                    plan.EffectClassName = legacyEffect.EffectClassName.Trim();

                AddRequestedEmptyLifecyclePlans(plan, request);
                plan.Warnings.Add("候选图使用旧 EffectBindingNode；只能导入 Effect 字段，不生成生命周期调用链。");
                return true;
            }

            EffectNode primary = effectNodes[0];
            plan.HasMultipleEffectNodes = effectNodes.Count > 1;
            plan.EffectId = request != null && request.EffectId > 0 ? request.EffectId : primary.EffectId;
            plan.EffectName = primary.EffectName ?? string.Empty;
            if (request != null && !string.IsNullOrWhiteSpace(request.EffectClassName))
                plan.EffectClassName = request.EffectClassName.Trim();
            else if (!string.IsNullOrWhiteSpace(primary.EffectClassName))
                plan.EffectClassName = primary.EffectClassName.Trim();

            plan.SelectedEffectNodeSummary = BuildEffectSummary(primary);
            if (plan.HasMultipleEffectNodes)
                plan.Warnings.Add("当前图包含多个 EffectNode；主 Effect 草稿使用组合根提供的最终 EffectId / Class，生命周期调用链暂取顺序中的第一个 EffectNode。");

            AddLifecyclePlan(plan, primary, "OnApply", request.OnApply, false);
            AddLifecyclePlan(plan, primary, "OnTick", request.OnTick, false);
            AddLifecyclePlan(plan, primary, "OnRemove", request.OnRemove, false);
            AddLifecyclePlan(plan, primary, "OnRefresh", request.OnRefresh, false);
            AddLifecyclePlan(plan, primary, "OnStackChanged", request.OnStackChanged, true);

            if (!plan.HasActions)
                plan.Warnings.Add("候选图主 Effect 未连接任何可生成 Action；将按面板勾选生成普通空模板。");

            return !plan.HasErrors;
        }

        private static void AddRequestedEmptyLifecyclePlans(BuffGraphEffectCodegenPlan plan, BuffGraphEffectCodegenRequest request)
        {
            if (request == null)
                return;

            AddEmptyLifecyclePlan(plan, "OnApply", request.OnApply, false);
            AddEmptyLifecyclePlan(plan, "OnTick", request.OnTick, false);
            AddEmptyLifecyclePlan(plan, "OnRemove", request.OnRemove, false);
            AddEmptyLifecyclePlan(plan, "OnRefresh", request.OnRefresh, false);
            AddEmptyLifecyclePlan(plan, "OnStackChanged", request.OnStackChanged, true);
        }

        private static void AddEmptyLifecyclePlan(BuffGraphEffectCodegenPlan plan, string lifecycleName, bool include, bool isStackChanged)
        {
            if (!include)
                return;

            plan.LifecyclePlans.Add(new BuffGraphEffectLifecyclePlan
            {
                LifecycleName = lifecycleName,
                IncludeOverride = true,
                IsStackChanged = isStackChanged
            });
        }

        private static void ApplyRequestDefaults(BuffGraphEffectCodegenPlan plan, BuffGraphEffectCodegenRequest request)
        {
            plan.EffectId = request != null ? request.EffectId : 0;
            plan.EffectClassName = request != null ? request.EffectClassName ?? string.Empty : string.Empty;
            plan.Namespace = request != null ? request.Namespace ?? string.Empty : string.Empty;
            plan.TargetFolder = request != null ? request.TargetFolder ?? string.Empty : string.Empty;
            plan.TargetFilePath = request != null ? request.TargetFilePath ?? string.Empty : string.Empty;
        }

        private static void AddLifecyclePlan(BuffGraphEffectCodegenPlan plan, EffectNode effect, string lifecycleName, bool includeWhenEmpty, bool isStackChanged)
        {
            BuffGraphEffectLifecyclePlan lifecycle = new BuffGraphEffectLifecyclePlan
            {
                LifecycleName = lifecycleName,
                IncludeOverride = includeWhenEmpty,
                IsStackChanged = isStackChanged
            };

            NodePort port = effect.GetPort(lifecycleName);
            if (port != null && port.ConnectionCount > 0)
            {
                List<ScriptActionNode> actions = new List<ScriptActionNode>();
                List<NodePort> connections = port.GetConnections();
                for (int i = 0; i < connections.Count; i++)
                {
                    Node connectedNode = connections[i] != null ? connections[i].node : null;
                    if (connectedNode is ScriptActionNode action)
                    {
                        actions.Add(action);
                        continue;
                    }

                    if (connectedNode is EmptyActionPlaceholderNode placeholder)
                    {
                        string placeholderName = !string.IsNullOrWhiteSpace(placeholder.ActionName) ? placeholder.ActionName : placeholder.name;
                        plan.Warnings.Add($"{lifecycleName} 连接到占位节点 {placeholderName}：这是占位功能节点，不会生成可运行调用代码。请替换为 ScriptActionNode。");
                        lifecycle.Todos.Add($"// TODO: 将占位节点 {placeholderName} 替换为 ScriptActionNode。");
                        continue;
                    }

                    plan.Warnings.Add($"{lifecycleName} 连接到未知节点类型：{(connectedNode != null ? connectedNode.GetType().Name : "<null>")}，第一版生成时会忽略。");
                }

                OrderScriptActions(plan, lifecycleName, actions);
                for (int i = 0; i < actions.Count; i++)
                    AddActionCall(plan, lifecycle, actions[i]);
            }

            lifecycle.IncludeOverride = lifecycle.IncludeOverride || lifecycle.Actions.Count > 0 || lifecycle.Todos.Count > 0;
            if (isStackChanged && lifecycle.Actions.Count > 0)
                plan.Warnings.Add("OnStackChanged Action 当前不会接收 delta；第一版只生成 Execute(in context) 调用。");

            if (lifecycle.IncludeOverride)
                plan.LifecyclePlans.Add(lifecycle);

            RecalculateExpectedActionCallCount(plan);
        }

        private static void AddActionCall(BuffGraphEffectCodegenPlan plan, BuffGraphEffectLifecyclePlan lifecycle, ScriptActionNode action)
        {
            BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.RefreshFromScript(action);
            string displayName = GetActionDisplayName(action, validation);

            for (int i = 0; i < validation.Errors.Count; i++)
                plan.Errors.Add($"{lifecycle.LifecycleName}.{displayName}: {validation.Errors[i]}");

            for (int i = 0; i < validation.Warnings.Count; i++)
                plan.Warnings.Add($"{lifecycle.LifecycleName}.{displayName}: {validation.Warnings[i]}");

            Type actionType = validation.ActionType;
            if (actionType == null)
                return;

            ConstructorInfo constructor = actionType.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic)
                plan.Errors.Add($"{lifecycle.LifecycleName}.{displayName}: Action 无 public parameterless constructor，无法生成 new {actionType.Name}()。");

            if (!IsLegalIdentifier(actionType.Name))
                plan.Errors.Add($"{lifecycle.LifecycleName}.{displayName}: Action 类名非法。");

            if (!string.IsNullOrWhiteSpace(actionType.Namespace) && !IsLegalNamespace(actionType.Namespace))
                plan.Errors.Add($"{lifecycle.LifecycleName}.{displayName}: Action namespace 非法。");

            string typeName = BuildTypeName(actionType, plan.Namespace);
            string variableName = AllocateVariableName(plan, typeName, actionType.Name);
            lifecycle.Actions.Add(new BuffGraphEffectActionCallPlan
            {
                ActionTypeName = typeName,
                ActionVariableName = variableName,
                ActionDisplayName = displayName,
                SourceNodeName = action.name,
                ExecutionOrder = action.ExecutionOrder
            });
        }

        private static void RecalculateExpectedActionCallCount(BuffGraphEffectCodegenPlan plan)
        {
            int count = 0;
            for (int i = 0; i < plan.LifecyclePlans.Count; i++)
                count += plan.LifecyclePlans[i].Actions.Count;

            plan.ExpectedActionCallCount = count;
        }

        private static void ValidateDuplicateExecutionOrder(BuffGraphEffectCodegenPlan plan, string lifecycleName, List<ScriptActionNode> actions)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < actions.Count; i++)
            {
                int order = actions[i].ExecutionOrder;
                if (!counts.ContainsKey(order))
                    counts.Add(order, 0);

                counts[order]++;
            }

            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > 1)
                    plan.Errors.Add($"{lifecycleName}: ScriptActionNode ExecutionOrder 重复：{pair.Key}，数量={pair.Value}。");
            }
        }

        private static void OrderScriptActions(BuffGraphEffectCodegenPlan plan, string lifecycleName, List<ScriptActionNode> actions)
        {
            if (actions.Count <= 1)
                return;

            bool hasNext = HasScriptActionNextConnections(actions);
            if (!hasNext)
            {
                actions.Sort(CompareScriptActions);
                ValidateDuplicateExecutionOrder(plan, lifecycleName, actions);
                return;
            }

            List<ScriptActionNode> executionOrder = new List<ScriptActionNode>(actions);
            executionOrder.Sort(CompareScriptActions);
            ValidateDuplicateExecutionOrder(plan, lifecycleName, executionOrder);

            Dictionary<ScriptActionNode, List<ScriptActionNode>> outgoing = new Dictionary<ScriptActionNode, List<ScriptActionNode>>();
            Dictionary<ScriptActionNode, int> incomingCounts = new Dictionary<ScriptActionNode, int>();
            for (int i = 0; i < actions.Count; i++)
            {
                outgoing.Add(actions[i], new List<ScriptActionNode>());
                incomingCounts.Add(actions[i], 0);
            }

            for (int i = 0; i < actions.Count; i++)
            {
                ScriptActionNode action = actions[i];
                NodePort port = action.GetPort("Next");
                if (port == null || port.ConnectionCount == 0)
                    continue;

                List<NodePort> connections = port.GetConnections();
                for (int connectionIndex = 0; connectionIndex < connections.Count; connectionIndex++)
                {
                    Node connectedNode = connections[connectionIndex] != null ? connections[connectionIndex].node : null;
                    if (connectedNode is ScriptActionNode nextAction && incomingCounts.ContainsKey(nextAction))
                    {
                        outgoing[action].Add(nextAction);
                        incomingCounts[nextAction]++;
                        continue;
                    }

                    plan.Warnings.Add($"{lifecycleName}: ScriptActionNode.Next 连接到同生命周期外节点，生成时忽略。");
                }
            }

            for (int i = 0; i < actions.Count; i++)
            {
                if (outgoing[actions[i]].Count > 1)
                    plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 不允许分叉，节点={GetActionDisplayName(actions[i], BuffScriptActionNodeValidator.RefreshFromScript(actions[i]))}。");

                if (incomingCounts[actions[i]] > 1)
                    plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 不允许多个前驱指向同一 Action。");
            }

            List<ScriptActionNode> starts = new List<ScriptActionNode>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (incomingCounts[actions[i]] == 0)
                    starts.Add(actions[i]);
            }

            if (starts.Count != 1)
            {
                plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 链必须有且只能有一个起点，当前起点数量={starts.Count}。");
                actions.Sort(CompareScriptActions);
                return;
            }

            List<ScriptActionNode> chain = new List<ScriptActionNode>();
            HashSet<ScriptActionNode> visited = new HashSet<ScriptActionNode>();
            ScriptActionNode current = starts[0];
            while (current != null)
            {
                if (!visited.Add(current))
                {
                    plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 链存在循环。");
                    break;
                }

                chain.Add(current);
                List<ScriptActionNode> nextList = outgoing[current];
                current = nextList.Count == 1 ? nextList[0] : null;
            }

            if (chain.Count != actions.Count)
                plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 链未覆盖同生命周期全部 Action，链长度={chain.Count}，Action 数={actions.Count}。");

            if (!SameActionOrder(chain, executionOrder))
                plan.Errors.Add($"{lifecycleName}: ScriptActionNode.Next 链与 ExecutionOrder 顺序冲突。");

            if (plan.HasErrors)
            {
                actions.Sort(CompareScriptActions);
                return;
            }

            actions.Clear();
            actions.AddRange(chain);
        }

        private static bool HasScriptActionNextConnections(List<ScriptActionNode> actions)
        {
            for (int i = 0; i < actions.Count; i++)
            {
                NodePort port = actions[i].GetPort("Next");
                if (port != null && port.ConnectionCount > 0)
                    return true;
            }

            return false;
        }

        private static bool SameActionOrder(List<ScriptActionNode> left, List<ScriptActionNode> right)
        {
            if (left.Count != right.Count)
                return false;

            for (int i = 0; i < left.Count; i++)
            {
                if (!ReferenceEquals(left[i], right[i]))
                    return false;
            }

            return true;
        }

        private static string AllocateVariableName(BuffGraphEffectCodegenPlan plan, string actionTypeName, string variableBaseName)
        {
            string baseName = "_" + ToCamelCase(variableBaseName);
            string candidate = baseName;
            int suffix = 2;
            while (ContainsVariable(plan, candidate))
            {
                candidate = baseName + suffix;
                suffix++;
            }

            plan.FieldPlans.Add(new BuffGraphEffectFieldPlan
            {
                ActionTypeName = actionTypeName,
                ActionVariableName = candidate
            });
            return candidate;
        }

        private static bool ContainsVariable(BuffGraphEffectCodegenPlan plan, string variableName)
        {
            for (int i = 0; i < plan.FieldPlans.Count; i++)
            {
                if (plan.FieldPlans[i].ActionVariableName == variableName)
                    return true;
            }

            return false;
        }

        private static string BuildTypeName(Type type, string effectNamespace)
        {
            string typeName = type.FullName != null ? type.FullName.Replace('+', '.') : type.Name;
            if (string.Equals(type.Namespace, effectNamespace, StringComparison.Ordinal))
                return type.Name;

            return "global::" + typeName;
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "action";

            string safe = SanitizeIdentifier(value);
            if (safe.Length == 1)
                return safe.ToLowerInvariant();

            return char.ToLowerInvariant(safe[0]) + safe.Substring(1);
        }

        private static string SanitizeIdentifier(string value)
        {
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if ((i == 0 && (char.IsLetter(c) || c == '_')) || (i > 0 && (char.IsLetterOrDigit(c) || c == '_')))
                    builder.Append(c);
            }

            return builder.Length > 0 ? builder.ToString() : "action";
        }

        private static string GetActionDisplayName(ScriptActionNode action, BuffScriptActionValidationResult validation)
        {
            if (action == null)
                return "<null>";

            if (!string.IsNullOrWhiteSpace(action.ActionDisplayName))
                return action.ActionDisplayName;

            if (!string.IsNullOrWhiteSpace(action.ActionName))
                return action.ActionName;

            if (validation.ActionType != null)
                return validation.ActionType.Name;

            return "<unnamed>";
        }

        private static string BuildEffectSummary(EffectNode effect)
        {
            string className = !string.IsNullOrWhiteSpace(effect.EffectClassName) ? effect.EffectClassName : "<empty-class>";
            return $"ExecutionOrder={effect.ExecutionOrder}, EffectId={effect.EffectId}, Class={className}";
        }

        private static int CompareEffectNodes(EffectNode left, EffectNode right)
        {
            int order = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (order != 0)
                return order;

            int id = left.EffectId.CompareTo(right.EffectId);
            if (id != 0)
                return id;

            return string.Compare(left.EffectClassName, right.EffectClassName, StringComparison.Ordinal);
        }

        private static int CompareScriptActions(ScriptActionNode left, ScriptActionNode right)
        {
            int order = left.ExecutionOrder.CompareTo(right.ExecutionOrder);
            if (order != 0)
                return order;

            return string.Compare(left.ActionDisplayName, right.ActionDisplayName, StringComparison.Ordinal);
        }

        private static bool IsLegalIdentifier(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && IdentifierRegex.IsMatch(value.Trim());
        }

        private static bool IsLegalNamespace(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && NamespaceRegex.IsMatch(value.Trim());
        }
    }
}
