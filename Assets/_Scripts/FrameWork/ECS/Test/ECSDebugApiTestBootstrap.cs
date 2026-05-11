using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{
/// <summary>
/// ECS Debug API 的最小验证脚本。
/// 挂到空 GameObject 后运行，用于确认 Debug 快照、Entity、ComponentStore、ArcheType、System、Singleton、WorldEvent 信息能够正常读取。
/// </summary>
public sealed class ECSDebugApiTestBootstrap : MonoBehaviour
{
    private readonly List<Entity> _entities = new List<Entity>(16);
    private readonly List<Type> _types = new List<Type>(16);
    private readonly List<ComponentStoreDebugInfo> _stores = new List<ComponentStoreDebugInfo>(16);
    private readonly List<ArcheTypeDebugInfo> _archeTypes = new List<ArcheTypeDebugInfo>(16);
    private readonly List<SystemDebugInfo> _systems = new List<SystemDebugInfo>(16);
    private readonly List<SingletonDebugInfo> _singletons = new List<SingletonDebugInfo>(16);
    private readonly List<WorldEventDebugInfo> _events = new List<WorldEventDebugInfo>(16);
    private readonly List<Entity> _archeTypeEntities = new List<Entity>(16);

    private void Start()
    {
        Debug.Log("[ECS Debug API Test] Start");

        World world = new World();
        world.EnableSystemProfile = true;

        Entity entityA = world.CreateEntity();
        world.SetComponent(entityA, new PositionComponent(1, 2, 3));
        world.SetComponent(entityA, new VelocityComponent(4, 5, 6));

        Entity entityB = world.CreateEntity();
        world.SetComponent(entityB, new HealthComponent(80, 100));

        world.SetSingleton(new PlayerInputSnapshotComponent());
        world.AddSystem(new MovementSystem());
        world.AddWorldEvent(new DamageWorldEvent(3, Entity.Invalid, entityB, 20, 80));

        WorldDebugSnapshot snapshot = world.GetDebugSnapshot();
        Expect(snapshot.statistics.aliveEntityCount == 3, "Snapshot should include 2 normal entities and 1 singleton entity.");
        Expect(snapshot.componentStoreCount >= 4, "Snapshot should include created component stores.");
        Expect(snapshot.worldEventCount == 1, "Snapshot should include one world event.");
        Debug.Log(snapshot.ToString());

        world.FillAliveEntities(_entities);
        Expect(_entities.Count == 3, "FillAliveEntities should return alive entities.");

        bool hasEntityInfo = world.TryGetEntityDebugInfo(entityA, out EntityDebugInfo entityInfo);
        Expect(hasEntityInfo && entityInfo.componentCount == 2, "EntityDebugInfo should report entityA has 2 components.");
        Debug.Log(entityInfo.ToString());

        world.FillEntityComponentTypes(entityA, _types);
        Expect(_types.Contains(typeof(PositionComponent)) && _types.Contains(typeof(VelocityComponent)), "FillEntityComponentTypes should include Position and Velocity.");

        world.FillRegisteredComponentTypes(_types);
        Expect(_types.Contains(typeof(PositionComponent)) && _types.Contains(typeof(PlayerInputSnapshotComponent)), "FillRegisteredComponentTypes should include registered component types.");

        world.FillComponentStoreDebugInfos(_stores);
        Expect(ContainsStore(typeof(PositionComponent)), "FillComponentStoreDebugInfos should include PositionComponent store.");

        world.FillArcheTypeDebugInfos(_archeTypes);
        Expect(_archeTypes.Count >= 2, "FillArcheTypeDebugInfos should return archetype groups.");

        ArcheTypeDebugInfo firstArcheType = _archeTypes[0];
        world.FillEntitiesByArcheType(firstArcheType.mask, _archeTypeEntities);
        Expect(_archeTypeEntities.Count == firstArcheType.entityCount, "FillEntitiesByArcheType count should match ArcheTypeDebugInfo.");

        world.FillComponentTypesByMask(entityInfo.componentMask, _types);
        Expect(_types.Count == 2, "FillComponentTypesByMask should return component types from entity mask.");

        world.FillSystemDebugInfos(_systems);
        Expect(_systems.Count == 1 && _systems[0].systemType == typeof(MovementSystem), "FillSystemDebugInfos should include MovementSystem.");

        world.FillSingletonDebugInfos(_singletons);
        Expect(_singletons.Count == 1 && _singletons[0].componentType == typeof(PlayerInputSnapshotComponent), "FillSingletonDebugInfos should include PlayerInputSnapshotComponent singleton.");

        world.FillSingletonTypes(_types);
        Expect(_types.Count == 1 && _types[0] == typeof(PlayerInputSnapshotComponent), "FillSingletonTypes should include singleton component type.");

        world.FillWorldEventDebugInfos(_events);
        Expect(_events.Count == 1 && _events[0].eventType == typeof(DamageWorldEvent) && _events[0].oldestFrame == 3, "FillWorldEventDebugInfos should include DamageWorldEvent frame info.");

        world.Tick(new SimulationContext(1, 1f, false));
        world.FillSystemDebugInfos(_systems);
        Expect(_systems[0].tickCount == 1, "System debug info should include profile tick count after Tick.");

        world.Dispose();
        Debug.Log("[ECS Debug API Test] Finished");
    }

    /// <summary>判断 Store 调试列表中是否包含指定组件类型。</summary>
    private bool ContainsStore(Type componentType)
    {
        for (int i = 0; i < _stores.Count; i++)
        {
            if (_stores[i].componentType == componentType)
                return true;
        }

        return false;
    }

    /// <summary>输出简单断言结果。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log("[PASS] " + message);
            return;
        }

        Debug.LogError("[FAIL] " + message);
    }
}
}
