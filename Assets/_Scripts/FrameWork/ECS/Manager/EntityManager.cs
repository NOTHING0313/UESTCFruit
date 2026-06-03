/*
 * 文件说明：EntityManager 负责 Entity ID 分配、复用、Version 校验与存活状态维护。它不直接持有组件数据。
 * 设计约束：ECS Core 逻辑应尽量保持确定性；Unity 表现、输入采样、外部指令通过 Adapter 或 Buffer 接入。
 */

using System;
using System.Collections.Generic;

namespace ECSFrameWork
{

/// <summary>
/// Entity 生命周期管理器，负责 ID 分配、Version 刷新、存活标记和 ID 复用。
/// </summary>
internal class EntityManager
{
    private EntityData[] _datas = Array.Empty<EntityData>();
    private Stack<int> freeIDs = new Stack<int>();
    private int dataCount;

    public int CreatedEntityCount => dataCount;
    public int EntityCapacity => _datas.Length;
    public int FreeEntityCount => freeIDs.Count;

    public int AliveEntityCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < dataCount; i++)
            {
                EntityData data = _datas[i];

                if (data != null && data.isAlive)
                    count++;
            }

            return count;
        }
    }

    /// <summary>
    /// 捕获 EntityManager 的完整槽位、版本号和未来 ID 复用顺序。
    /// </summary>
    internal EcsEntityManagerSnapshot CaptureSnapshot()
    {
        EcsEntitySlotSnapshot[] slots = new EcsEntitySlotSnapshot[dataCount];

        for (int i = 0; i < dataCount; i++)
        {
            EntityData data = _datas[i];
            int version = data != null ? data.Version : 0;
            bool isAlive = data != null && data.isAlive;
            slots[i] = new EcsEntitySlotSnapshot(i, version, isAlive);
        }

        return new EcsEntityManagerSnapshot(dataCount, slots, freeIDs.ToArray());
    }

    /// <summary>
    /// 从快照恢复 EntityManager 槽位、版本号和未来 ID 复用顺序。
    /// </summary>
    internal void RestoreSnapshot(EcsEntityManagerSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));

        if (snapshot.DataCount < 0)
            throw new InvalidOperationException("EntityManager snapshot DataCount cannot be negative.");

        if (snapshot.Slots.Count != snapshot.DataCount)
            throw new InvalidOperationException("EntityManager snapshot slot count must equal DataCount.");

        EntityData[] restoredDatas = Array.Empty<EntityData>();
        ToolFunction.EnsureArrayLength(ref restoredDatas, snapshot.DataCount);
        bool[] restoredSlotFlags = snapshot.DataCount > 0 ? new bool[snapshot.DataCount] : Array.Empty<bool>();
        bool[] restoredFreeIdFlags = snapshot.DataCount > 0 ? new bool[snapshot.DataCount] : Array.Empty<bool>();

        for (int i = 0; i < snapshot.Slots.Count; i++)
        {
            EcsEntitySlotSnapshot slot = snapshot.Slots[i];

            if (slot == null)
                throw new InvalidOperationException("EntityManager snapshot contains null slot.");

            if (slot.Id < 0 || slot.Id >= snapshot.DataCount)
                throw new InvalidOperationException($"EntityManager snapshot slot id is out of range: {slot.Id}.");

            if (restoredSlotFlags[slot.Id])
                throw new InvalidOperationException($"EntityManager snapshot contains duplicate slot id: {slot.Id}.");

            if (slot.Version < 0)
                throw new InvalidOperationException($"EntityManager snapshot slot version cannot be negative: {slot.Id}.");

            if (slot.IsAlive && slot.Version <= 0)
                throw new InvalidOperationException($"EntityManager snapshot alive slot must have positive version: {slot.Id}.");

            restoredSlotFlags[slot.Id] = true;

            EntityData data = new EntityData();
            data.RestoreSlot(slot.IsAlive, slot.Version);
            restoredDatas[slot.Id] = data;
        }

        Stack<int> restoredFreeIds = new Stack<int>();

        for (int i = snapshot.FreeIdsInPopOrder.Count - 1; i >= 0; i--)
        {
            int freeId = snapshot.FreeIdsInPopOrder[i];

            if (freeId < 0 || freeId >= snapshot.DataCount)
                throw new InvalidOperationException($"EntityManager snapshot free id is out of range: {freeId}.");

            if (restoredFreeIdFlags[freeId])
                throw new InvalidOperationException($"EntityManager snapshot contains duplicate free id: {freeId}.");

            if (restoredDatas[freeId] != null && restoredDatas[freeId].isAlive)
                throw new InvalidOperationException($"EntityManager snapshot free id points to alive slot: {freeId}.");

            restoredFreeIdFlags[freeId] = true;
            restoredFreeIds.Push(freeId);
        }

        for (int i = 0; i < snapshot.DataCount; i++)
        {
            EntityData data = restoredDatas[i];

            if (data == null)
                throw new InvalidOperationException($"EntityManager snapshot is missing slot id: {i}.");

            if (!data.isAlive && !restoredFreeIdFlags[i])
                throw new InvalidOperationException($"EntityManager snapshot dead slot is missing from free id list: {i}.");
        }

        _datas = restoredDatas;
        freeIDs = restoredFreeIds;
        dataCount = snapshot.DataCount;
    }

    /// <summary>
    /// 确保 EntityData 数组容量至少达到指定长度。
    /// 该方法只扩容底层数组，不会创建新的 Entity。
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        if (capacity <= 0)
            return;

        ToolFunction.EnsureArrayLength(ref _datas, capacity);
    }

    /// <summary>
    /// 创建或复用一个实体 ID，并返回带版本号的 Entity。
    /// </summary>
    public Entity GetEntity()
    {
        int id;
        EntityData data;

        if (freeIDs.Count == 0)
        {
            data = new EntityData();
            id = dataCount;
            dataCount++;
        }
        else
        {
            id = freeIDs.Pop();
            data = _datas[id];

            if (data == null)
                data = new EntityData();
        }

        ToolFunction.EnsureArrayLength(ref _datas, id + 1);

        data.ClearMask();
        data.RefreshVersion();
        data.SetAlive(true);

        _datas[id] = data;

        return new Entity(id, data.Version);
    }

    /// <summary>
    /// 校验 Entity 是否仍然对应一个存活实体。
    /// </summary>
    public bool IsAlive(Entity entity)
    {
        if (!entity.IsValid)
            return false;

        if (entity.ID < 0 || entity.ID >= dataCount)
            return false;

        EntityData data = _datas[entity.ID];

        if (data == null)
            return false;

        return data.isAlive && data.Version == entity.Version;
    }

    /// <summary>
    /// 按 Entity ID 从小到大枚举当前存活的实体。
    /// </summary>
    public IEnumerable<Entity> GetAliveEntities()
    {
        for (int i = 0; i < dataCount; i++)
        {
            EntityData data = _datas[i];

            if (data != null && data.isAlive)
                yield return new Entity(i, data.Version);
        }
    }


    /// <summary>
    /// 把当前存活 Entity 写入外部 List，避免 Debug 面板频繁分配枚举器和临时集合。
    /// </summary>
    public int FillAliveEntities(List<Entity> results)
    {
        if (results == null)
            return 0;

        results.Clear();

        for (int i = 0; i < dataCount; i++)
        {
            EntityData data = _datas[i];

            if (data != null && data.isAlive)
                results.Add(new Entity(i, data.Version));
        }

        return results.Count;
    }

    /// <summary>
    /// 尝试获取 Entity 的只读调试信息。
    /// </summary>
    public bool TryGetDebugInfo(Entity entity, out EntityDebugInfo info)
    {
        if (!IsAlive(entity))
        {
            info = new EntityDebugInfo(entity, false, default, 0);
            return false;
        }

        ComponentMask256 mask = _datas[entity.ID].ArcheType;
        info = new EntityDebugInfo(entity, true, mask, mask.CountBits());
        return true;
    }

    /// <summary>
    /// 获取实体当前持有组件组合对应的 ComponentMask256。
    /// </summary>
    public ComponentMask256 GetMask(Entity entity)
    {
        if (!IsAlive(entity))
            return default;

        return _datas[entity.ID].ArcheType;
    }

    /// <summary>
    /// 给实体当前 Mask 设置指定组件类型位。
    /// </summary>
    public void SetMask(Entity entity, int componentTypeId)
    {
        if (!IsAlive(entity))
            return;

        _datas[entity.ID].SetMask(componentTypeId);
    }

    /// <summary>
    /// 从实体当前 Mask 中移除指定组件类型位。
    /// </summary>
    public void RemoveMask(Entity entity, int componentTypeId)
    {
        if (!IsAlive(entity))
            return;

        _datas[entity.ID].RemoveMask(componentTypeId);
    }

    /// <summary>
    /// 清空实体当前 Mask。
    /// </summary>
    public void ClearMask(Entity entity)
    {
        if (!IsAlive(entity))
            return;

        _datas[entity.ID].ClearMask();
    }

    /// <summary>
    /// 销毁实体并回收实体 ID。
    /// </summary>
    public bool DestroyEntity(Entity entity)
    {
        if (!IsAlive(entity))
            return false;

        EntityData data = _datas[entity.ID];

        data.SetAlive(false);
        data.ClearMask();

        freeIDs.Push(entity.ID);
        return true;
    }
}

}
