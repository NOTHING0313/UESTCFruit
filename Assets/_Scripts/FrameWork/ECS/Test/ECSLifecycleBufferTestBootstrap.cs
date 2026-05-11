using System.Collections.Generic;
using UnityEngine;

namespace ECSFrameWork
{

public class ECSLifecycleBufferTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Lifecycle Buffer Test] Start</color>");

        TestSetComponentDuringTick();
        TestRemoveComponentDuringTick();
        TestDestroyEntityDuringTick();
        TestAddSystemDuringTick();
        TestRemoveSystemDuringTick();
        TestClearSystemDuringTick();
        TestSystemCommandCreatedDuringSystemPlaybackIsDelayedToNextPlayback();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Lifecycle Buffer Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Lifecycle Buffer Test] Failed count = {_failedCount}");
    }

    private void TestSetComponentDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 1] SetComponent During Tick</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new LifePositionComponent { x = 1f, y = 0f, z = 0f });

        LifeSetComponentSystem system = new LifeSetComponentSystem(entity);
        world.AddSystem(system);
        TickWorld(world, 1);

        Expect(system.TickCount == 1, "SetComponentSystem should tick once.");
        Expect(system.ExistingComponentChangedImmediately, "SetComponent on existing component should apply immediately during Tick.");
        Expect(system.NewComponentDeferredDuringTick, "SetComponent on new component should be deferred during Tick.");
        Expect(system.PendingCommandCountDuringTick > 0, "PendingCommandCount should be greater than 0 during Tick after deferred SetComponent.");
        Expect(world.HasComponent<LifeDeferredTagComponent>(entity), "DeferredTagComponent should exist after Tick playback.");
        Expect(Mathf.Approximately(world.GetComponent<LifePositionComponent>(entity).x, 10f), "Position.x should be 10 after immediate existing component update.");
    }

    private void TestRemoveComponentDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 2] RemoveComponent During Tick</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new LifePositionComponent { x = 1f, y = 0f, z = 0f });
        world.SetComponent(entity, new LifeRemoveMeComponent());

        LifeRemoveComponentSystem removeSystem = new LifeRemoveComponentSystem(entity);
        LifeCheckRemoveVisibilitySystem checkSystem = new LifeCheckRemoveVisibilitySystem(entity);
        world.AddSystem(removeSystem);
        world.AddSystem(checkSystem);
        TickWorld(world, 1);

        Expect(removeSystem.TickCount == 1, "RemoveComponentSystem should tick once.");
        Expect(removeSystem.RemoveRequestReturnedTrue, "RemoveComponent request during Tick should return true.");
        Expect(removeSystem.StillHasComponentAfterRemoveRequest, "Entity should still have component immediately after RemoveComponent request during Tick.");
        Expect(checkSystem.SawComponentInSameTick, "Later system in same Tick should still see component before playback.");
        Expect(!world.HasComponent<LifeRemoveMeComponent>(entity), "Component should be removed after Tick playback.");
    }

    private void TestDestroyEntityDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 3] DestroyEntity During Tick</color>");

        World world = new World();
        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new LifePositionComponent { x = 1f, y = 0f, z = 0f });
        world.SetComponent(entity, new LifeDestroyMeComponent());

        LifeDestroyEntitySystem destroySystem = new LifeDestroyEntitySystem(entity);
        LifeCheckAliveSystem checkAliveSystem = new LifeCheckAliveSystem(entity);
        world.AddSystem(destroySystem);
        world.AddSystem(checkAliveSystem);
        TickWorld(world, 1);

        Expect(destroySystem.TickCount == 1, "DestroyEntitySystem should tick once.");
        Expect(destroySystem.StillAliveAfterDestroyRequest, "Entity should still be alive immediately after DestroyEntity request during Tick.");
        Expect(checkAliveSystem.SawAliveInSameTick, "Later system in same Tick should still see entity alive before playback.");
        Expect(!world.IsAlive(entity), "Entity should be destroyed after Tick playback.");
        Expect(!world.HasComponent<LifePositionComponent>(entity), "Destroyed entity should have no Position after playback.");
    }

    private void TestAddSystemDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 4] AddSystem During Tick</color>");

        World world = new World();
        LifePassiveSystem addedSystem = new LifePassiveSystem("AddedSystem");
        LifeAddSystemDuringTickSystem addSystem = new LifeAddSystemDuringTickSystem(addedSystem);

        world.AddSystem(addSystem);
        TickWorld(world, 1);

        Expect(addSystem.TickCount == 1, "AddSystemDuringTickSystem should tick in first Tick.");
        Expect(addedSystem.OnCreateCount == 1, "AddedSystem should receive OnCreate after first Tick system playback.");
        Expect(addedSystem.TickCount == 0, "AddedSystem should not tick in the same frame it was added.");

        TickWorld(world, 2);

        Expect(addedSystem.TickCount == 1, "AddedSystem should tick on the next Tick.");
    }

    private void TestRemoveSystemDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 5] RemoveSystem During Tick</color>");

        World world = new World();
        LifeRemoveSelfSystem removeSelfSystem = new LifeRemoveSelfSystem();

        world.AddSystem(removeSelfSystem);
        TickWorld(world, 1);

        Expect(removeSelfSystem.TickCount == 1, "RemoveSelfSystem should tick once before being removed.");
        Expect(removeSelfSystem.OnDestroyCount == 1, "RemoveSelfSystem should receive OnDestroy after Tick playback.");

        TickWorld(world, 2);

        Expect(removeSelfSystem.TickCount == 1, "RemoveSelfSystem should not tick again after being removed.");
    }

    private void TestClearSystemDuringTick()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 6] ClearSystem During Tick</color>");

        World world = new World();
        LifeClearSystemDuringTickSystem clearSystem = new LifeClearSystemDuringTickSystem();
        LifePassiveSystem passiveSystem = new LifePassiveSystem("PassiveSystem");

        world.AddSystem(clearSystem);
        world.AddSystem(passiveSystem);
        TickWorld(world, 1);

        Expect(clearSystem.TickCount == 1, "ClearSystem should tick once.");
        Expect(passiveSystem.TickCount == 1, "PassiveSystem should still tick in same frame before ClearSystem playback.");
        Expect(clearSystem.OnDestroyCount == 1, "ClearSystem should receive OnDestroy after ClearSystem playback.");
        Expect(passiveSystem.OnDestroyCount == 1, "PassiveSystem should receive OnDestroy after ClearSystem playback.");

        TickWorld(world, 2);

        Expect(clearSystem.TickCount == 1, "ClearSystem should not tick again after ClearSystem playback.");
        Expect(passiveSystem.TickCount == 1, "PassiveSystem should not tick again after ClearSystem playback.");
    }

    private void TestSystemCommandCreatedDuringSystemPlaybackIsDelayedToNextPlayback()
    {
        Debug.Log("<color=cyan>[Lifecycle Test 7] System Command Created During SystemPlayback Is Delayed</color>");

        World world = new World();
        LifePassiveSystem nestedSystem = new LifePassiveSystem("NestedSystem");
        LifeAddNestedOnCreateSystem firstAddedSystem = new LifeAddNestedOnCreateSystem(nestedSystem);
        LifeAddSystemDuringTickSystem requestAddSystem = new LifeAddSystemDuringTickSystem(firstAddedSystem);

        world.AddSystem(requestAddSystem);
        TickWorld(world, 1);

        Expect(firstAddedSystem.OnCreateCount == 1, "First added system should be created during first SystemPlayback.");
        Expect(nestedSystem.OnCreateCount == 0, "Nested system requested during SystemPlayback should not be created in the same playback.");
        Expect(world.PendingSystemCommandCount > 0, "Nested system command should remain pending for next SystemPlayback.");

        TickWorld(world, 2);

        Expect(nestedSystem.OnCreateCount == 1, "Nested system should be created during the next SystemPlayback.");
        Expect(nestedSystem.TickCount == 0, "Nested system should not tick before it has been created.");

        TickWorld(world, 3);

        Expect(nestedSystem.TickCount == 1, "Nested system should tick after being created in previous playback.");
    }

    private void TickWorld(World world, int frameNumber)
    {
        SimulationContext context = new SimulationContext(frameNumber, 1f, false);
        world.Tick(in context);
    }

    private void Expect(bool condition, string message)
    {
        if (condition)
            Debug.Log($"<color=green>[PASS]</color> {message}");
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message}");
        }
    }
}

public struct LifePositionComponent : IComponentData
{
    public float x;
    public float y;
    public float z;
}

public struct LifeDeferredTagComponent : IComponentData
{
}

public struct LifeRemoveMeComponent : IComponentData
{
}

public struct LifeDestroyMeComponent : IComponentData
{
}

public abstract class LifeTestSystemBase : FixedStepSystemBase
{
    public int OnCreateCount { get; private set; }
    public int OnDestroyCount { get; private set; }
    public int TickCount { get; protected set; }
    public override SystemTickSequence sequence => SystemTickSequence.normal;

    protected override void OnSystemCreate()
    {
        OnCreateCount++;
    }

    protected override void OnSystemDestroy()
    {
        OnDestroyCount++;
    }
}

public class LifeSetComponentSystem : LifeTestSystemBase
{
    private readonly Entity _entity;
    public bool ExistingComponentChangedImmediately { get; private set; }
    public bool NewComponentDeferredDuringTick { get; private set; }
    public int PendingCommandCountDuringTick { get; private set; }

    public LifeSetComponentSystem(Entity entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.SetComponent(_entity, new LifePositionComponent { x = 10f, y = 0f, z = 0f });
        ExistingComponentChangedImmediately = Mathf.Approximately(World.GetComponent<LifePositionComponent>(_entity).x, 10f);
        World.SetComponent(_entity, new LifeDeferredTagComponent());
        NewComponentDeferredDuringTick = !World.HasComponent<LifeDeferredTagComponent>(_entity);
        PendingCommandCountDuringTick = World.PendingCommandCount;
    }
}

public class LifeRemoveComponentSystem : LifeTestSystemBase
{
    private readonly Entity _entity;
    public bool RemoveRequestReturnedTrue { get; private set; }
    public bool StillHasComponentAfterRemoveRequest { get; private set; }

    public LifeRemoveComponentSystem(Entity entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        RemoveRequestReturnedTrue = World.RemoveComponent<LifeRemoveMeComponent>(_entity);
        StillHasComponentAfterRemoveRequest = World.HasComponent<LifeRemoveMeComponent>(_entity);
    }
}

public class LifeCheckRemoveVisibilitySystem : LifeTestSystemBase
{
    private readonly Entity _entity;
    public bool SawComponentInSameTick { get; private set; }

    public LifeCheckRemoveVisibilitySystem(Entity entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        SawComponentInSameTick = World.HasComponent<LifeRemoveMeComponent>(_entity);
    }
}

public class LifeDestroyEntitySystem : LifeTestSystemBase
{
    private readonly Entity _entity;
    public bool StillAliveAfterDestroyRequest { get; private set; }

    public LifeDestroyEntitySystem(Entity entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.DestroyEntity(_entity);
        StillAliveAfterDestroyRequest = World.IsAlive(_entity);
    }
}

public class LifeCheckAliveSystem : LifeTestSystemBase
{
    private readonly Entity _entity;
    public bool SawAliveInSameTick { get; private set; }

    public LifeCheckAliveSystem(Entity entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        SawAliveInSameTick = World.IsAlive(_entity);
    }
}

public class LifeAddSystemDuringTickSystem : LifeTestSystemBase
{
    private readonly IFixedStepSystem _systemToAdd;

    public LifeAddSystemDuringTickSystem(IFixedStepSystem systemToAdd)
    {
        _systemToAdd = systemToAdd;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.AddSystem(_systemToAdd);
    }
}

public class LifeRemoveSelfSystem : LifeTestSystemBase
{
    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.RemoveSystem(this);
    }
}

public class LifeClearSystemDuringTickSystem : LifeTestSystemBase
{
    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.ClearSystem();
    }
}

public class LifePassiveSystem : LifeTestSystemBase
{
    private readonly string _name;

    public LifePassiveSystem(string name)
    {
        _name = name;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        Debug.Log($"[LifePassiveSystem] {_name} TickCount = {TickCount}");
    }
}

public class LifeAddNestedOnCreateSystem : LifeTestSystemBase
{
    private readonly IFixedStepSystem _nestedSystem;

    public LifeAddNestedOnCreateSystem(IFixedStepSystem nestedSystem)
    {
        _nestedSystem = nestedSystem;
    }

    protected override void OnSystemCreate()
    {
        base.OnSystemCreate();
        World.AddSystem(_nestedSystem);
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
    }
}

}
