/*
 * 文件说明：EntityDefinitionValidation 提供 GameplayEntityDefinitionSO 与 EntityPrefabSO 创建前校验结果类型。
 */

using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

/// <summary>DefinitionSO 与 PrefabSO 不匹配时的处理策略。</summary>
public enum EntityDefinitionMismatchPolicy
{
    /// <summary>允许 DefinitionSO 添加 PrefabSO 中不存在的组件，不输出警告。</summary>
    AllowAdd = 0,

    /// <summary>允许添加，但输出 Warning。默认推荐策略。</summary>
    WarnAndAdd = 1,

    /// <summary>不允许 DefinitionSO 添加 PrefabSO 中不存在的组件，创建失败。</summary>
    Reject = 2,
}

/// <summary>配置校验消息等级。</summary>
public enum EntityDefinitionValidationSeverity
{
    /// <summary>警告，不阻止创建。</summary>
    Warning = 0,

    /// <summary>错误，阻止创建。</summary>
    Error = 1,
}

/// <summary>一条 Definition / Prefab 校验消息。</summary>
public readonly struct EntityDefinitionValidationMessage
{
    public readonly EntityDefinitionValidationSeverity severity;
    public readonly string message;

    /// <summary>创建校验消息。</summary>
    public EntityDefinitionValidationMessage(EntityDefinitionValidationSeverity severity, string message)
    {
        this.severity = severity;
        this.message = message;
    }
}

/// <summary>Definition / Prefab 校验结果。</summary>
public sealed class EntityDefinitionValidationResult
{
    private readonly List<EntityDefinitionValidationMessage> _messages = new List<EntityDefinitionValidationMessage>();

    /// <summary>所有校验消息。</summary>
    public IReadOnlyList<EntityDefinitionValidationMessage> Messages => _messages;

    /// <summary>是否存在错误。</summary>
    public bool HasError { get; private set; }

    /// <summary>是否存在任何消息。</summary>
    public bool HasMessage => _messages.Count > 0;

    /// <summary>清空校验结果。</summary>
    public void Clear()
    {
        _messages.Clear();
        HasError = false;
    }

    /// <summary>添加警告消息。</summary>
    public void AddWarning(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        _messages.Add(new EntityDefinitionValidationMessage(EntityDefinitionValidationSeverity.Warning, message));
    }

    /// <summary>添加错误消息。</summary>
    public void AddError(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        HasError = true;
        _messages.Add(new EntityDefinitionValidationMessage(EntityDefinitionValidationSeverity.Error, message));
    }

    /// <summary>把当前结果输出到 Unity Console。</summary>
    public void LogToUnity(Object context = null)
    {
        for (int i = 0; i < _messages.Count; i++)
        {
            EntityDefinitionValidationMessage item = _messages[i];

            if (item.severity == EntityDefinitionValidationSeverity.Error)
                Debug.LogError(item.message, context);
            else
                Debug.LogWarning(item.message, context);
        }
    }
}

}
