using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 验证 WorldEventBuffer 的写入、读取、清理，以及 System 内事件产生流程。
/// </summary>
public sealed class ECSWorldEventBufferTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS WorldEventBuffer Test] Start</color>");

        TestAddAndGetWorldEvent();
        TestDifferentEventTypesAreSeparated();
        TestClearWorldEvents();
        TestClearWorldEventsBeforeFrame();
        TestSystemCanWriteWorldEventDuringTick();
        TestDamageResolveSystemWritesDamageAndDeadEvents();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS WorldEventBuffer Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS WorldEventBuffer Test] Failed count = {_failedCount}");
    }

    /// <summary>验证 AddWorldEvent 后可以按类型读取事件。</summary>
    private void TestAddAndGetWorldEvent()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 1] Add And Get WorldEvent</color>");

        World world = new World();
        world.AddWorldEvent(new TestWorldEvent(1, 100));

        IReadOnlyList<TestWorldEvent> events = world.GetWorldEvents<TestWorldEvent>();

        Expect(world.WorldEventCount == 1, $"WorldEventCount should be 1. Actual = {world.WorldEventCount}");
        Expect(events.Count == 1, $"TestWorldEvent count should be 1. Actual = {events.Count}");
        Expect(events.Count == 1 && events[0].value == 100, "Event payload should be preserved.");
    }

    /// <summary>验证不同事件类型会被分开缓存和读取。</summary>
    private void TestDifferentEventTypesAreSeparated()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 2] Different Event Types Are Separated</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();

        world.AddWorldEvent(new TestWorldEvent(1, 10));
        world.AddWorldEvent(new EntityDeadWorldEvent(1, entity));

        IReadOnlyList<TestWorldEvent> testEvents = world.GetWorldEvents<TestWorldEvent>();
        IReadOnlyList<EntityDeadWorldEvent> deadEvents = world.GetWorldEvents<EntityDeadWorldEvent>();
        IReadOnlyList<DamageWorldEvent> damageEvents = world.GetWorldEvents<DamageWorldEvent>();

        Expect(world.WorldEventCount == 2, $"Total event count should be 2. Actual = {world.WorldEventCount}");
        Expect(testEvents.Count == 1, $"TestWorldEvent count should be 1. Actual = {testEvents.Count}");
        Expect(deadEvents.Count == 1, $"EntityDeadWorldEvent count should be 1. Actual = {deadEvents.Count}");
        Expect(damageEvents.Count == 0, $"DamageWorldEvent count should be 0. Actual = {damageEvents.Count}");
    }

    /// <summary>验证 ClearWorldEvents 会清空所有事件。</summary>
    private void TestClearWorldEvents()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 3] Clear WorldEvents</color>");

        World world = new World();
        world.AddWorldEvent(new TestWorldEvent(1, 10));
        world.AddWorldEvent(new TestWorldEvent(2, 20));
        world.ClearWorldEvents();

        IReadOnlyList<TestWorldEvent> events = world.GetWorldEvents<TestWorldEvent>();

        Expect(world.WorldEventCount == 0, $"WorldEventCount should be 0 after clear. Actual = {world.WorldEventCount}");
        Expect(events.Count == 0, $"TestWorldEvent count should be 0 after clear. Actual = {events.Count}");
    }

    /// <summary>验证 ClearWorldEventsBeforeFrame 只清理指定帧之前的事件。</summary>
    private void TestClearWorldEventsBeforeFrame()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 4] Clear Before Frame</color>");

        World world = new World();
        world.AddWorldEvent(new TestWorldEvent(1, 10));
        world.AddWorldEvent(new TestWorldEvent(2, 20));
        world.AddWorldEvent(new TestWorldEvent(3, 30));

        world.ClearWorldEventsBeforeFrame(3);

        IReadOnlyList<TestWorldEvent> events = world.GetWorldEvents<TestWorldEvent>();

        Expect(world.WorldEventCount == 1, $"Only frame 3 event should remain. Actual total = {world.WorldEventCount}");
        Expect(events.Count == 1 && events[0].frameNumber == 3 && events[0].value == 30, "ClearBeforeFrame should keep events whose frameNumber is not earlier than the specified frame.");
    }

    /// <summary>验证 System.Tick 中可以写入 WorldEvent，且不影响 Entity / Component 状态。</summary>
    private void TestSystemCanWriteWorldEventDuringTick()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 5] System Can Write Event During Tick</color>");

        World world = new World();
        world.AddSystem(new TestEventWriteSystem());

        EntityInfo entity = world.CreateEntity();
        world.SetComponent(entity, new HealthComponent(5, 5));

        world.Tick(new SimulationContext(7, 0.02f, false));

        IReadOnlyList<TestWorldEvent> events = world.GetWorldEvents<TestWorldEvent>();
        bool healthUnchanged = world.TryGetComponent(entity, out HealthComponent health) && health.current == 5 && health.max == 5;

        Expect(events.Count == 1, $"System should write one event. Actual = {events.Count}");
        Expect(events.Count == 1 && events[0].frameNumber == 7 && events[0].value == 777, "System-written event should keep context frame and payload.");
        Expect(healthUnchanged, "Writing WorldEvent should not modify existing components.");
    }

    /// <summary>验证 DamageResolveSystem 会写入 DamageWorldEvent，并在首次死亡时写入 EntityDeadWorldEvent。</summary>
    private void TestDamageResolveSystemWritesDamageAndDeadEvents()
    {
        Debug.Log("<color=cyan>[WorldEvent Test 6] DamageResolveSystem Writes Damage And Dead Events</color>");

        World world = new World();
        world.AddSystem(new DamageResolveSystem());

        EntityInfo source = world.CreateEntity();
        EntityInfo target = world.CreateEntity();
        EntityInfo request = world.CreateEntity();

        world.SetComponent(target, new HealthComponent(10, 10));
        world.SetComponent(request, new DamageRequestComponent(source, target, 4));
        world.Tick(new SimulationContext(11, 0.02f, false));

        IReadOnlyList<DamageWorldEvent> damageEvents = world.GetWorldEvents<DamageWorldEvent>();
        IReadOnlyList<EntityDeadWorldEvent> deadEvents = world.GetWorldEvents<EntityDeadWorldEvent>();

        bool damageEventValid = damageEvents.Count == 1
            && damageEvents[0].frameNumber == 11
            && damageEvents[0].source == source
            && damageEvents[0].target == target
            && damageEvents[0].amount == 4
            && damageEvents[0].remainingHealth == 6;

        Expect(damageEventValid, "DamageResolveSystem should write one valid DamageWorldEvent.");
        Expect(deadEvents.Count == 0, $"Target should not emit dead event while health remains above zero. Actual = {deadEvents.Count}");

        world.ClearWorldEvents();

        EntityInfo killRequest = world.CreateEntity();
        world.SetComponent(killRequest, new DamageRequestComponent(source, target, 20));
        world.Tick(new SimulationContext(12, 0.02f, false));

        damageEvents = world.GetWorldEvents<DamageWorldEvent>();
        deadEvents = world.GetWorldEvents<EntityDeadWorldEvent>();

        bool deadEventValid = deadEvents.Count == 1
            && deadEvents[0].frameNumber == 12
            && deadEvents[0].entity == target
            && world.HasComponent<DeadTagComponent>(target);

        Expect(damageEvents.Count == 1 && damageEvents[0].remainingHealth == 0, "Killing damage should emit DamageWorldEvent with zero remaining health.");
        Expect(deadEventValid, "First lethal damage should emit EntityDeadWorldEvent and add DeadTagComponent after structural playback.");
    }

    /// <summary>测试用 World 事件。</summary>
    private readonly struct TestWorldEvent : IWorldEvent
    {
        public int frameNumber { get; }
        public readonly int value;

        public TestWorldEvent(int frameNumber, int value)
        {
            this.frameNumber = frameNumber;
            this.value = value;
        }
    }

    /// <summary>测试用事件写入系统。</summary>
    private sealed class TestEventWriteSystem : FixedStepSystemBase
    {
        public override SystemTickSequence sequence => SystemTickSequence.logic;

        public override void Tick(in SimulationContext context)
        {
            World.AddWorldEvent(new TestWorldEvent(context.frameNumber, 777));
        }
    }

    /// <summary>输出测试断言结果。</summary>
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
