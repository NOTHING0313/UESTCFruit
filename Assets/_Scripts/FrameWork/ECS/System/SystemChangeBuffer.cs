/*
 * 文件说明：固定逻辑帧 System 接口、基类、系统变更缓冲和示例系统。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

/// <summary>
/// System 增删延迟缓冲。
/// </summary>
public sealed class SystemChangeBuffer
{
    private enum CommandType
    {
        Add,
        Remove,
        Clear
    }

    private readonly struct SystemCommand
    {
        public readonly CommandType Type;
        public readonly IFixedStepSystem System;

        /// <summary>
        /// 创建一条 System 增删或清空命令。
        /// </summary>
        public SystemCommand(CommandType type, IFixedStepSystem system)
        {
            Type = type;
            System = system;
        }
    }

    private readonly List<SystemCommand> _commands = new List<SystemCommand>();
    private readonly List<SystemCommand> _nextCommands = new List<SystemCommand>();

    private bool _isPlayingBack;

    public int Count => _commands.Count + _nextCommands.Count;

    /// <summary>
    /// 记录延迟添加 System 命令。
    /// </summary>
    public void AddSystem(IFixedStepSystem system)
    {
        if (system == null)
            return;

        AddCommand(new SystemCommand(CommandType.Add, system));
    }

    /// <summary>
    /// 记录延迟移除 System 命令。
    /// </summary>
    public void RemoveSystem(IFixedStepSystem system)
    {
        if (system == null)
            return;

        AddCommand(new SystemCommand(CommandType.Remove, system));
    }

    /// <summary>
    /// 记录延迟清空所有 System 的命令。
    /// </summary>
    public void ClearSystem()
    {
        AddCommand(new SystemCommand(CommandType.Clear, null));
    }

    /// <summary>
    /// 播放当前 System 命令队列，真正增删或清空 System。
    /// </summary>
    public void Playback(SystemManager systemManager)
    {
        if (systemManager == null || _isPlayingBack || _commands.Count == 0)
            return;

        _isPlayingBack = true;

        try
        {
            for (int i = 0; i < _commands.Count; i++)
            {
                SystemCommand command = _commands[i];

                switch (command.Type)
                {
                    case CommandType.Add:
                        systemManager.AddSystemImmediate(command.System);
                        break;

                    case CommandType.Remove:
                        systemManager.RemoveSystemImmediate(command.System);
                        break;

                    case CommandType.Clear:
                        systemManager.ClearSystemImmediately();
                        break;
                }
            }
        }
        finally
        {
            _commands.Clear();

            if (_nextCommands.Count > 0)
            {
                _commands.AddRange(_nextCommands);
                _nextCommands.Clear();
            }

            _isPlayingBack = false;
        }
    }

    /// <summary>
    /// 清空当前和下一轮 System 命令。
    /// </summary>
    public void Clear()
    {
        _commands.Clear();
        _nextCommands.Clear();
    }

    /// <summary>
    /// 创建一条 System 增删或清空命令。
    /// </summary>
    private void AddCommand(SystemCommand command)
    {
        if (_isPlayingBack)
        {
            _nextCommands.Add(command);
            return;
        }

        _commands.Add(command);
    }
}
