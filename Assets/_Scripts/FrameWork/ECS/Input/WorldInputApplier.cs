/*
 * 文件说明：WorldInputApplier 从 IInputProvider 读取指定逻辑帧的输入，并写入对应 Entity 的 PlayerInputSnapshotComponent。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// 按逻辑帧从输入提供者读取输入快照，并写入对应玩家 Entity 的 PlayerInputSnapshotComponent。
/// </summary>
public sealed class WorldInputApplier
{
    private readonly World _world;
    private readonly IInputProvider _inputProvider;
    private readonly Dictionary<int, Entity> _playerEntities = new Dictionary<int, Entity>();
    private readonly List<int> _sortedPlayerIDs = new List<int>();
    private bool _playerIDListDirty = true;

    public int RegisteredPlayerCount => _playerEntities.Count;

    /// <summary>创建输入应用器，绑定目标 World 与输入提供者。</summary>
    public WorldInputApplier(World world, IInputProvider inputProvider)
    {
        _world = world;
        _inputProvider = inputProvider;
    }

    /// <summary>绑定玩家编号与其对应的 ECS Entity。</summary>
    public void RegisterPlayerEntity(int playerID, Entity entity)
    {
        if (playerID < 0)
            return;

        _playerEntities[playerID] = entity;
        _playerIDListDirty = true;
    }

    /// <summary>解除指定玩家编号的 Entity 绑定。</summary>
    public bool UnregisterPlayerEntity(int playerID)
    {
        bool removed = _playerEntities.Remove(playerID);

        if (removed)
            _playerIDListDirty = true;

        return removed;
    }

    /// <summary>清空全部玩家 Entity 绑定。</summary>
    public void ClearPlayerEntities()
    {
        _playerEntities.Clear();
        _sortedPlayerIDs.Clear();
        _playerIDListDirty = false;
    }

    /// <summary>把指定逻辑帧中所有已注册玩家的输入快照写入 ECS；按 playerID 排序以保证应用顺序稳定。</summary>
    public void ApplyInputToWorld(int frameNumber)
    {
        if (_world == null || _inputProvider == null || frameNumber <= 0)
            return;

        RebuildSortedPlayerIDsIfNeeded();

        for (int i = 0; i < _sortedPlayerIDs.Count; i++)
        {
            int playerID = _sortedPlayerIDs[i];
            Entity entity = _playerEntities[playerID];

            if (!_world.IsAlive(entity))
                continue;

            if (!_inputProvider.TryGetInput(frameNumber, playerID, out PlayerInputSnapshot snapshot))
                continue;

            PlayerInputSnapshotComponent input = PlayerInputSnapshotComponent.FromSnapshot(in snapshot);
            _world.SetComponent(entity, in input);
        }
    }

    private void RebuildSortedPlayerIDsIfNeeded()
    {
        if (!_playerIDListDirty)
            return;

        _sortedPlayerIDs.Clear();

        foreach (int playerID in _playerEntities.Keys)
            _sortedPlayerIDs.Add(playerID);

        _sortedPlayerIDs.Sort();
        _playerIDListDirty = false;
    }
}

}
