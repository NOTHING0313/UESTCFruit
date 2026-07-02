using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using XNode;

namespace BuffSystem.Editor.AuthoringGraphs
{
    /// <summary>
    /// 从 BuffCandidateGraph 构建 CompositeEffect 草稿计划。
    /// 该 Builder 只读取图和脚本元信息；ECS Component、BuffConfigData、registry、whitelist 都仍是外部权威状态。
    /// </summary>
    internal static class BuffGraphCompositeEffectPlanBuilder
    {
        private static readonly Regex IdentifierRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        private static readonly Regex NamespaceRegex = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*(\.[A-Za-z_][A-Za-z0-9_]*)*$", RegexOptions.Compiled);

        private static readonly string[] LifecycleNames =
        {
            "OnApply",
            "OnTick",
            "OnRemove",
            "OnRefresh",
            "OnStackChanged"
        };

        internal static bool TryBuild(
            BuffCandidateGraph graph,
            BuffGraphGeneratePlan basePlan,
            out BuffGraphCompositeEffectPlan plan,
            out string error)
        {
            plan = new BuffGraphCompositeEffectPlan();
            error = string.Empty;
            ApplyBasePlan(plan, basePlan);

            if (graph == null)
            {
                AddError(plan, "未选择候选图，无法构建 CompositeEffect。");
                return Finish(plan, out error);
            }

            ValidateCompositeMetadata(plan);
            ValidateLegacyBinding(graph, plan);

            BuffGraphEffectOrderUtility.OrderResult orderResult = BuffGraphEffectOrderUtility.Build(graph);
            plan.EffectOrderMode = orderResult.ModeLabel;
            AppendRange(plan.Errors, orderResult.Errors);
            AppendRange(plan.Warnings, orderResult.Warnings);

            List<EffectNode> effectNodes = orderResult.OrderedEffects;
            plan.EffectNodeCount = effectNodes.Count;
            if (effectNodes.Count == 0)
            {
                AddError(plan, "图中没有 EffectNode，无法构建 CompositeEffect。");
                return Finish(plan, out error);
            }

            plan.HasMultipleEffectNodes = effectNodes.Count > 1;
            if (effectNodes.Count == 1)
                plan.Infos.Add("图中只有一个 EffectNode；可以继续使用主 Effect 生成，CompositeEffect 仍允许构建。");
            else
                plan.Infos.Add("CompositeEffect 会把多个 EffectNode 合成为一个普通 BuffEffectExecutorBase 派生类。");

            plan.Infos.Add($"EffectNode 顺序模式：{orderResult.ModeLabel}。");

            for (int i = 0; i < effectNodes.Count; i++)
                AddEffectPart(plan, effectNodes[i]);

            RebuildGlobalLifecyclePlans(plan);
            RecalculateExpectedActionCallCount(plan);
            AddBoundaryInfos(plan);
            return Finish(plan, out error);
        }

        private static void ApplyBasePlan(BuffGraphCompositeEffectPlan plan, BuffGraphGeneratePlan basePlan)
        {
            if (basePlan == null)
                return;

            plan.CompositeEffectId = basePlan.EffectId;
            plan.CompositeEffectName = basePlan.EffectName ?? string.Empty;
            plan.CompositeEffectClassName = basePlan.EffectClassName ?? string.Empty;
            plan.Namespace = basePlan.EffectNamespace ?? string.Empty;
            plan.TargetFolder = basePlan.EffectTargetFolder ?? string.Empty;
            plan.TargetFilePath = basePlan.EffectScriptPath ?? string.Empty;
            plan.BuffConfigId = basePlan.BuffConfigId;
            plan.BuffName = basePlan.BuffName ?? string.Empty;
            plan.BuffConfigAssetPath = basePlan.BuffConfigAssetPath ?? string.Empty;
        }

        private static void ValidateCompositeMetadata(BuffGraphCompositeEffectPlan plan)
        {
            if (plan.CompositeEffectId <= 0)
            {
                AddError(plan, "CompositeEffectId 无效，必须大于 0。");
            }
            else
            {
                ValidateCompositeEffectIdConflict(plan);
            }

            if (!IsLegalIdentifier(plan.CompositeEffectClassName))
                AddError(plan, "CompositeEffectClassName 非法。");

            if (!string.IsNullOrWhiteSpace(plan.Namespace) && !IsLegalNamespace(plan.Namespace))
                AddError(plan, "CompositeEffect namespace 非法。");

            if (!string.IsNullOrWhiteSpace(plan.TargetFilePath))
            {
                string normalized = plan.TargetFilePath.Replace('\\', '/');
                if (!normalized.StartsWith("Assets/", StringComparison.Ordinal))
                    AddError(plan, "目标 CompositeEffect.cs 路径不在 Assets 下。");

                if (File.Exists(plan.TargetFilePath))
                    AddError(plan, "目标 CompositeEffect.cs 已存在，本阶段不会覆盖已有文件。");
            }
        }

        private static void ValidateCompositeEffectIdConflict(BuffGraphCompositeEffectPlan plan)
        {
            global::BuffSystem.EffectRegistryCheckResult registryResult = global::BuffSystem.BuffAuthoringValidationUtility.CheckProductionEffectRegistered(plan.CompositeEffectId);
            if (registryResult.IsRegistered)
                AddError(plan, $"CompositeEffectId 已在 production registry 注册：{plan.CompositeEffectId}。");
            else if (registryResult.IsUnknown)
                plan.Warnings.Add($"无法确认 CompositeEffectId registry 状态：{registryResult.ErrorMessage}");

            string scanFolder = !string.IsNullOrWhiteSpace(plan.TargetFolder)
                ? plan.TargetFolder
                : "Assets/_Scripts/FrameWork/BuffSystem/Effects";
            List<global::BuffSystem.EffectIdConstantHit> hits = global::BuffSystem.BuffAuthoringValidationUtility.ScanEffectIdConstants(scanFolder, plan.CompositeEffectId);
            for (int i = 0; i < hits.Count; i++)
                AddError(plan, $"CompositeEffectId 已被 EffectId const 使用：{hits[i].FilePath}。");
        }

        private static void ValidateLegacyBinding(BuffCandidateGraph graph, BuffGraphCompositeEffectPlan plan)
        {
            if (graph.FindSingleNode<EffectBindingNode>() != null)
                plan.Warnings.Add("图中仍存在旧 EffectBindingNode；Composite 模式只读取 EffectNode / ScriptActionNode，旧绑定节点会被忽略。");
        }

        private static void ValidateDuplicateEffectOrder(List<EffectNode> effectNodes, BuffGraphCompositeEffectPlan plan)
        {
            Dictionary<int, int> counts = new Dictionary<int, int>();
            for (int i = 0; i < effectNodes.Count; i++)
            {
                int order = effectNodes[i].ExecutionOrder;
                if (!counts.ContainsKey(order))
                    counts.Add(order, 0);

                counts[order]++;
            }

            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > 1)
                    AddError(plan, $"EffectNode.ExecutionOrder 重复：{pair.Key}，数量={pair.Value}。");
            }
        }

        private static void AddEffectPart(BuffGraphCompositeEffectPlan plan, EffectNode effect)
        {
            BuffGraphCompositeEffectPartPlan part = new BuffGraphCompositeEffectPartPlan
            {
                EffectNodeName = effect != null ? effect.name : string.Empty,
                EffectNodeExecutionOrder = effect != null ? effect.ExecutionOrder : 0,
                SourceEffectId = effect != null ? effect.EffectId : 0,
                SourceEffectName = effect != null ? effect.EffectName ?? string.Empty : string.Empty,
                SourceEffectClassName = effect != null ? effect.EffectClassName ?? string.Empty : string.Empty
            };

            if (effect == null)
                return;

            int actionCountBefore = CountActions(part);
            for (int i = 0; i < LifecycleNames.Length; i++)
            {
                string lifecycleName = LifecycleNames[i];
                BuffGraphCompositeLifecyclePlan lifecycle = BuildLifecyclePlan(plan, effect, lifecycleName, lifecycleName == "OnStackChanged");
                if (lifecycle.Actions.Count > 0 || lifecycle.Todos.Count > 0)
                    part.LifecyclePlans.Add(lifecycle);
            }

            if (CountActions(part) == actionCountBefore)
                plan.Warnings.Add($"EffectNode {BuildEffectDisplayName(effect)} 没有任何 lifecycle Action。");

            plan.Parts.Add(part);
        }

        private static BuffGraphCompositeLifecyclePlan BuildLifecyclePlan(BuffGraphCompositeEffectPlan plan, EffectNode effect, string lifecycleName, bool isStackChanged)
        {
            BuffGraphCompositeLifecyclePlan lifecycle = new BuffGraphCompositeLifecyclePlan
            {
                LifecycleName = lifecycleName,
                IsStackChanged = isStackChanged
            };

            NodePort port = effect.GetPort(lifecycleName);
            if (port == null || port.ConnectionCount == 0)
                return lifecycle;

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
                    plan.Warnings.Add($"{BuildEffectDisplayName(effect)}.{lifecycleName} 连接到占位节点 {placeholderName}：CompositeEffect 会忽略该节点。");
                    lifecycle.Todos.Add($"// TODO: 将占位节点 {placeholderName} 替换为 ScriptActionNode。");
                    continue;
                }

                plan.Warnings.Add($"{BuildEffectDisplayName(effect)}.{lifecycleName} 连接到未知节点类型：{(connectedNode != null ? connectedNode.GetType().Name : "<null>")}，CompositeEffect 会忽略。");
            }

            OrderScriptActions(plan, effect, lifecycleName, actions);
            for (int i = 0; i < actions.Count; i++)
                AddActionCall(plan, lifecycle, effect, actions[i]);

            if (isStackChanged && lifecycle.Actions.Count > 0)
                plan.Warnings.Add($"{BuildEffectDisplayName(effect)}.OnStackChanged Action 当前不会接收 delta；第一版只生成 Execute(in context) 调用。");

            return lifecycle;
        }

        private static void AddActionCall(BuffGraphCompositeEffectPlan plan, BuffGraphCompositeLifecyclePlan lifecycle, EffectNode effect, ScriptActionNode action)
        {
            BuffScriptActionValidationResult validation = BuffScriptActionNodeValidator.Validate(action);
            string displayName = GetActionDisplayName(action, validation);
            string prefix = $"{BuildEffectDisplayName(effect)}.{lifecycle.LifecycleName}.{displayName}";

            for (int i = 0; i < validation.Errors.Count; i++)
                AddError(plan, $"{prefix}: {validation.Errors[i]}");

            for (int i = 0; i < validation.Warnings.Count; i++)
                plan.Warnings.Add($"{prefix}: {validation.Warnings[i]}");

            Type actionType = validation.ActionType;
            if (actionType == null)
                return;

            ConstructorInfo constructor = actionType.GetConstructor(Type.EmptyTypes);
            if (constructor == null || !constructor.IsPublic)
                AddError(plan, $"{prefix}: Action 无 public parameterless constructor，无法生成 new {actionType.Name}()。");

            if (!IsLegalIdentifier(actionType.Name))
                AddError(plan, $"{prefix}: Action 类名非法。");

            if (!string.IsNullOrWhiteSpace(actionType.Namespace) && !IsLegalNamespace(actionType.Namespace))
                AddError(plan, $"{prefix}: Action namespace 非法。");

            string typeName = BuildTypeName(actionType, plan.Namespace);
            string variableName = AllocateOrReuseVariableName(plan, typeName, actionType.Name);
            lifecycle.Actions.Add(new BuffGraphEffectActionCallPlan
            {
                ActionTypeName = typeName,
                ActionVariableName = variableName,
                ActionDisplayName = displayName,
                SourceNodeName = action != null ? action.name : string.Empty,
                ExecutionOrder = action != null ? action.ExecutionOrder : 0
            });
        }

        private static void ValidateDuplicateActionOrder(BuffGraphCompositeEffectPlan plan, EffectNode effect, string lifecycleName, List<ScriptActionNode> actions)
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
                    AddError(plan, $"{BuildEffectDisplayName(effect)}.{lifecycleName}: ScriptActionNode.ExecutionOrder 重复：{pair.Key}，数量={pair.Value}。");
            }
        }

        private static void OrderScriptActions(BuffGraphCompositeEffectPlan plan, EffectNode effect, string lifecycleName, List<ScriptActionNode> actions)
        {
            if (actions.Count <= 1)
                return;

            string scope = $"{BuildEffectDisplayName(effect)}.{lifecycleName}";
            bool hasNext = HasScriptActionNextConnections(actions);
            if (!hasNext)
            {
                actions.Sort(CompareScriptActions);
                ValidateDuplicateActionOrder(plan, effect, lifecycleName, actions);
                return;
            }

            int errorCountBefore = plan.Errors.Count;
            List<ScriptActionNode> executionOrder = new List<ScriptActionNode>(actions);
            executionOrder.Sort(CompareScriptActions);
            ValidateDuplicateActionOrder(plan, effect, lifecycleName, executionOrder);

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

                    plan.Warnings.Add($"{scope}: ScriptActionNode.Next 连接到当前 Effect/lifecycle 外节点，CompositeEffect 会忽略该连接。");
                }
            }

            for (int i = 0; i < actions.Count; i++)
            {
                ScriptActionNode action = actions[i];
                if (outgoing[action].Count > 1)
                    AddError(plan, $"{scope}: ScriptActionNode.Next 不允许分叉，节点={GetActionDisplayName(action, BuffScriptActionNodeValidator.RefreshFromScript(action))}。");

                if (incomingCounts[action] > 1)
                    AddError(plan, $"{scope}: ScriptActionNode.Next 不允许多个前驱指向同一 Action，节点={GetActionDisplayName(action, BuffScriptActionNodeValidator.RefreshFromScript(action))}。");
            }

            List<ScriptActionNode> starts = new List<ScriptActionNode>();
            for (int i = 0; i < actions.Count; i++)
            {
                if (incomingCounts[actions[i]] == 0)
                    starts.Add(actions[i]);
            }

            if (starts.Count != 1)
                AddError(plan, $"{scope}: ScriptActionNode.Next 链必须有且只能有一个起点，当前起点数量={starts.Count}。");

            List<ScriptActionNode> chain = starts.Count == 1
                ? WalkScriptActionChain(starts[0], outgoing, plan, scope)
                : new List<ScriptActionNode>();

            if (starts.Count == 1 && chain.Count != actions.Count)
                AddError(plan, $"{scope}: ScriptActionNode.Next 链未覆盖同生命周期全部 Action，链长度={chain.Count}，Action 数={actions.Count}。");

            if (starts.Count == 1 && !SameActionOrder(chain, executionOrder))
                AddError(plan, $"{scope}: ScriptActionNode.Next 链与 ExecutionOrder 顺序冲突。");

            if (plan.Errors.Count > errorCountBefore)
            {
                actions.Sort(CompareScriptActions);
                return;
            }

            actions.Clear();
            actions.AddRange(chain);
        }

        private static List<ScriptActionNode> WalkScriptActionChain(
            ScriptActionNode start,
            Dictionary<ScriptActionNode, List<ScriptActionNode>> outgoing,
            BuffGraphCompositeEffectPlan plan,
            string scope)
        {
            List<ScriptActionNode> chain = new List<ScriptActionNode>();
            HashSet<ScriptActionNode> visited = new HashSet<ScriptActionNode>();
            ScriptActionNode current = start;
            while (current != null)
            {
                if (!visited.Add(current))
                {
                    AddError(plan, $"{scope}: ScriptActionNode.Next 链存在循环。");
                    break;
                }

                chain.Add(current);
                List<ScriptActionNode> nextList = outgoing[current];
                current = nextList.Count == 1 ? nextList[0] : null;
            }

            return chain;
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

        private static void RebuildGlobalLifecyclePlans(BuffGraphCompositeEffectPlan plan)
        {
            plan.LifecyclePlans.Clear();
            for (int lifecycleIndex = 0; lifecycleIndex < LifecycleNames.Length; lifecycleIndex++)
            {
                string lifecycleName = LifecycleNames[lifecycleIndex];
                BuffGraphCompositeLifecyclePlan merged = new BuffGraphCompositeLifecyclePlan
                {
                    LifecycleName = lifecycleName,
                    IsStackChanged = lifecycleName == "OnStackChanged"
                };

                for (int partIndex = 0; partIndex < plan.Parts.Count; partIndex++)
                {
                    BuffGraphCompositeEffectPartPlan part = plan.Parts[partIndex];
                    BuffGraphCompositeLifecyclePlan lifecycle = FindLifecycle(part, lifecycleName);
                    if (lifecycle == null)
                        continue;

                    for (int todoIndex = 0; todoIndex < lifecycle.Todos.Count; todoIndex++)
                        merged.Todos.Add(lifecycle.Todos[todoIndex]);

                    for (int actionIndex = 0; actionIndex < lifecycle.Actions.Count; actionIndex++)
                        merged.Actions.Add(lifecycle.Actions[actionIndex]);
                }

                if (merged.Actions.Count > 0 || merged.Todos.Count > 0)
                    plan.LifecyclePlans.Add(merged);
            }
        }

        private static BuffGraphCompositeLifecyclePlan FindLifecycle(BuffGraphCompositeEffectPartPlan part, string lifecycleName)
        {
            for (int i = 0; i < part.LifecyclePlans.Count; i++)
            {
                if (part.LifecyclePlans[i].LifecycleName == lifecycleName)
                    return part.LifecyclePlans[i];
            }

            return null;
        }

        private static void RecalculateExpectedActionCallCount(BuffGraphCompositeEffectPlan plan)
        {
            int count = 0;
            for (int i = 0; i < plan.LifecyclePlans.Count; i++)
                count += plan.LifecyclePlans[i].Actions.Count;

            plan.ExpectedActionCallCount = count;
        }

        private static int CountActions(BuffGraphCompositeEffectPartPlan part)
        {
            int count = 0;
            for (int i = 0; i < part.LifecyclePlans.Count; i++)
                count += part.LifecyclePlans[i].Actions.Count;

            return count;
        }

        private static string AllocateOrReuseVariableName(BuffGraphCompositeEffectPlan plan, string actionTypeName, string variableBaseName)
        {
            for (int i = 0; i < plan.FieldPlans.Count; i++)
            {
                if (plan.FieldPlans[i].ActionTypeName == actionTypeName)
                    return plan.FieldPlans[i].ActionVariableName;
            }

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

        private static bool ContainsVariable(BuffGraphCompositeEffectPlan plan, string variableName)
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

        private static string BuildEffectDisplayName(EffectNode effect)
        {
            if (effect == null)
                return "<null-effect>";

            string name = !string.IsNullOrWhiteSpace(effect.EffectClassName)
                ? effect.EffectClassName
                : (!string.IsNullOrWhiteSpace(effect.EffectName) ? effect.EffectName : effect.name);
            return $"EffectNode[{effect.ExecutionOrder}] {name}";
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

        private static void AddBoundaryInfos(BuffGraphCompositeEffectPlan plan)
        {
            plan.Infos.Add("CompositeEffect 是普通 Effect executor。");
            plan.Infos.Add("CompositeEffect 不会自动加入 whitelist。");
            plan.Infos.Add("CompositeEffect 不证明 rollback-ready。");
            plan.Infos.Add("生成后仍需要 Validator / Runner / 场景验证。");
            plan.Warnings.Add("自动注册阶段只应注册 CompositeEffect，不应注册子 EffectNode。");
        }

        private static void AddError(BuffGraphCompositeEffectPlan plan, string message)
        {
            if (!plan.Errors.Contains(message))
                plan.Errors.Add(message);
        }

        private static void AppendRange(List<string> target, List<string> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (!target.Contains(source[i]))
                    target.Add(source[i]);
            }
        }

        private static bool Finish(BuffGraphCompositeEffectPlan plan, out string error)
        {
            error = plan.HasErrors ? string.Join("\n", plan.Errors) : string.Empty;
            return !plan.HasErrors;
        }
    }
}
