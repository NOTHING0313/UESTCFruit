using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 验证 SystemProfileInfo 与 Stopwatch 计时统计接口。
/// </summary>
public sealed class ECSSystemProfileTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS System Profile Test] Start</color>");

        TestSystemProfileRecordsTickCost();
        TestResetAndDisableProfile();
        TestRemoveSystemRemovesProfile();
        TestGetSystemProfilesReturnsSnapshotList();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS System Profile Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS System Profile Test] Failed count = {_failedCount}");
    }

    /// <summary>测试 System Tick 后是否产生性能统计。</summary>
    private void TestSystemProfileRecordsTickCost()
    {
        World world = new World();
        ProfileBusySystem system = new ProfileBusySystem();

        world.AddSystem(system);
        world.Tick(new SimulationContext(1, 0.02f, false));

        bool hasProfile = world.TryGetSystemProfile(system, out SystemProfileInfo profile);
        bool success = hasProfile
            && profile != null
            && profile.tickCount == 1
            && profile.lastMilliseconds >= 0d
            && profile.averageMilliseconds >= 0d
            && profile.maxMilliseconds >= profile.lastMilliseconds;

        Expect(success, "SystemProfileInfo should record tick cost after World.Tick.");
        world.Dispose();
    }

    /// <summary>测试重置统计和关闭统计开关。</summary>
    private void TestResetAndDisableProfile()
    {
        World world = new World();
        ProfileBusySystem system = new ProfileBusySystem();

        world.AddSystem(system);
        world.Tick(new SimulationContext(1, 0.02f, false));
        world.ResetSystemProfiles();

        bool resetSuccess = world.TryGetSystemProfile(system, out SystemProfileInfo profileAfterReset)
            && profileAfterReset.tickCount == 0
            && profileAfterReset.lastMilliseconds == 0d
            && profileAfterReset.maxMilliseconds == 0d
            && profileAfterReset.averageMilliseconds == 0d;

        world.EnableSystemProfile = false;
        world.Tick(new SimulationContext(2, 0.02f, false));

        bool disableSuccess = system.TickCount == 2
            && world.TryGetSystemProfile(system, out SystemProfileInfo profileAfterDisabledTick)
            && profileAfterDisabledTick.tickCount == 0;

        Expect(resetSuccess && disableSuccess, "ResetSystemProfiles should reset data, and disabling profile should not stop systems from ticking.");
        world.Dispose();
    }

    /// <summary>测试移除 System 后 Profile 是否同步移除。</summary>
    private void TestRemoveSystemRemovesProfile()
    {
        World world = new World();
        ProfileBusySystem system = new ProfileBusySystem();

        world.AddSystem(system);
        bool addedProfile = world.SystemProfileCount == 1 && world.TryGetSystemProfile(system, out _);

        bool removed = world.RemoveSystem(system);
        bool removedProfile = removed && world.SystemCount == 0 && world.SystemProfileCount == 0 && !world.TryGetSystemProfile(system, out _);

        Expect(addedProfile && removedProfile, "RemoveSystem should remove its SystemProfileInfo.");
        world.Dispose();
    }

    /// <summary>测试 GetSystemProfiles 返回快照列表，而不是内部集合。</summary>
    private void TestGetSystemProfilesReturnsSnapshotList()
    {
        World world = new World();
        ProfileBusySystem first = new ProfileBusySystem();
        ProfilePassiveSystem second = new ProfilePassiveSystem();

        world.AddSystem(first);
        world.AddSystem(second);

        List<SystemProfileInfo> profiles = world.GetSystemProfiles();
        bool countSuccess = profiles != null && profiles.Count == 2 && world.SystemProfileCount == 2;

        profiles.Clear();
        bool snapshotSuccess = world.SystemProfileCount == 2;

        Expect(countSuccess && snapshotSuccess, "GetSystemProfiles should return an external snapshot list.");
        world.Dispose();
    }

    /// <summary>输出测试结果。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
            return;
        }

        _failedCount++;
        Debug.LogError($"[FAIL] {message}");
    }
}

/// <summary>用于性能统计测试的忙等待 System。</summary>
public sealed class ProfileBusySystem : FixedStepSystemBase
{
    public int TickCount { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.logic;

    public override void Tick(in SimulationContext context)
    {
        TickCount++;

        int value = 0;

        for (int i = 0; i < 2048; i++)
            value += i;

        if (value < 0)
            Debug.Log(value);
    }
}

/// <summary>用于性能统计列表测试的空 System。</summary>
public sealed class ProfilePassiveSystem : FixedStepSystemBase
{
    public int TickCount { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.normal;

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
    }
}
