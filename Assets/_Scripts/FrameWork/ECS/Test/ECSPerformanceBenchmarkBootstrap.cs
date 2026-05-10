using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

/// <summary>
/// ECS 性能基准测试入口。
/// 用于粗略观察 Entity 创建、Component 写入、Query Fill、World.Tick 和 Entity 销毁的耗时。
/// </summary>
public sealed class ECSPerformanceBenchmarkBootstrap : MonoBehaviour
{
    [Header("Benchmark Settings")]
    [SerializeField] private bool runOnStart = true;
    [SerializeField] private int entityCount = 10000;
    [SerializeField] private int tickCount = 1000;
    [SerializeField] private int warmupTickCount = 16;
    [SerializeField] private int queryRepeatCount = 100;
    [SerializeField] private float tickLength = 0.02f;
    [SerializeField] private bool enableSystemProfile = true;
    [SerializeField] private bool includeDestroyBenchmark = true;
    [SerializeField] private bool enableCapacityPrewarm = true;

    private readonly List<BenchmarkRecord> _records = new List<BenchmarkRecord>(24);
    private float _benchmarkSink;

    private void Start()
    {
        if (runOnStart)
            RunBenchmark();
    }

    /// <summary>在 Inspector 右键菜单中手动运行 ECS Benchmark。</summary>
    [ContextMenu("Run ECS Benchmark")]
    public void RunBenchmark()
    {
        NormalizeSettings();
        _records.Clear();

        Debug.Log($"<color=cyan>[ECS Benchmark] Start. Entities={entityCount}, WarmupTicks={warmupTickCount}, MeasureTicks={tickCount}, QueryRepeats={queryRepeatCount}, CapacityPrewarm={enableCapacityPrewarm}</color>");

        ForceCollectGarbage();
        long memoryBeforeWorld = GC.GetTotalMemory(true);

        World world = new World();
        world.EnableSystemProfile = enableSystemProfile;
        ApplyCapacityPrewarm(world);

        EntityInfo[] entities = new EntityInfo[entityCount];
        BenchmarkEntityCreation(world, entities);
        BenchmarkQueryFill(world);
        BenchmarkComponentAccessPaths(world);
        BenchmarkComponentMutationPaths();
        BenchmarkMovementTick(world);

        if (includeDestroyBenchmark)
            BenchmarkEntityDestroy(world, entities);

        WorldStatistics statisticsBeforeDispose = world.GetStatistics();
        List<SystemProfileInfo> profiles = world.GetSystemProfiles();

        world.Dispose();
        ForceCollectGarbage();

        long memoryAfterDispose = GC.GetTotalMemory(true);
        long retainedMemoryDelta = memoryAfterDispose - memoryBeforeWorld;

        Debug.Log($"<color=cyan>[ECS Benchmark] Final Statistics Before Dispose: {statisticsBeforeDispose}</color>");
        LogSystemProfiles(profiles);
        LogSummary(retainedMemoryDelta);
    }

    /// <summary>修正 Inspector 中不合理的测试参数。</summary>
    private void NormalizeSettings()
    {
        if (entityCount <= 0)
            entityCount = 10000;

        if (tickCount <= 0)
            tickCount = 1000;

        if (warmupTickCount < 0)
            warmupTickCount = 0;

        if (queryRepeatCount <= 0)
            queryRepeatCount = 100;

        if (tickLength <= 0f)
            tickLength = 0.02f;
    }

    /// <summary>根据配置预热 Entity 与常用 ComponentStore 容量。</summary>
    private void ApplyCapacityPrewarm(World world)
    {
        if (!enableCapacityPrewarm || world == null)
            return;

        world.EnsureEntityCapacity(entityCount);
        world.EnsureComponentCapacity<PositionComponent>(entityCount);
        world.EnsureComponentCapacity<VelocityComponent>(entityCount);
        world.EnsureComponentCapacity<HealthComponent>(entityCount);

        Debug.Log($"<color=cyan>[ECS Benchmark] Capacity Prewarmed. EntityCapacity={world.EntityCapacity}, PositionCapacity={world.GetComponentStoreCapacity<PositionComponent>()}, VelocityCapacity={world.GetComponentStoreCapacity<VelocityComponent>()}, HealthCapacity={world.GetComponentStoreCapacity<HealthComponent>()}</color>");
    }

    /// <summary>测试 Entity 创建和基础 Component 写入耗时。</summary>
    private void BenchmarkEntityCreation(World world, EntityInfo[] entities)
    {
        long memoryBefore = GC.GetTotalMemory(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < entities.Length; i++)
        {
            EntityInfo entity = world.CreateEntity();
            entities[i] = entity;

            world.SetComponent(entity, new PositionComponent(i, 0f, 0f));
            world.SetComponent(entity, new VelocityComponent(1f, 0f, 0f));
            world.SetComponent(entity, new HealthComponent(100, 100));
        }

        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(true);

        Record("Create Entity + Set 3 Components", entities.Length, stopwatch.Elapsed.TotalMilliseconds, memoryAfter - memoryBefore);
        Debug.Log($"<color=cyan>[ECS Benchmark] After Creation: {world.GetStatistics()}</color>");
    }

    /// <summary>测试缓存 QueryDescription 后的 FillQuery 耗时。</summary>
    private void BenchmarkQueryFill(World world)
    {
        EntityQueryDescription moveQuery = world.Query().With<PositionComponent>().With<VelocityComponent>().BuildDescription();
        List<EntityInfo> results = new List<EntityInfo>(entityCount);

        world.FillQuery(moveQuery, results, false);
        bool countValid = results.Count == entityCount;

        long memoryBeforeUnsorted = GC.GetTotalMemory(true);
        Stopwatch unsortedStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < queryRepeatCount; i++)
            world.FillQuery(moveQuery, results, false);

        unsortedStopwatch.Stop();
        long memoryAfterUnsorted = GC.GetTotalMemory(true);

        Record("FillQuery Unsorted", queryRepeatCount, unsortedStopwatch.Elapsed.TotalMilliseconds, memoryAfterUnsorted - memoryBeforeUnsorted);

        long memoryBeforeSorted = GC.GetTotalMemory(true);
        Stopwatch sortedStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < queryRepeatCount; i++)
            world.FillQuery(moveQuery, results, true);

        sortedStopwatch.Stop();
        long memoryAfterSorted = GC.GetTotalMemory(true);

        Record("FillQuery Sorted", queryRepeatCount, sortedStopwatch.Elapsed.TotalMilliseconds, memoryAfterSorted - memoryBeforeSorted);

        if (!countValid)
            Debug.LogError($"[ECS Benchmark] Query result count mismatch. Expected={entityCount}, Actual={results.Count}");
    }

    /// <summary>细分测试 Query + GetComponent 与 ForEach 各组件数量路径的读取耗时。</summary>
    private void BenchmarkComponentAccessPaths(World world)
    {
        EntityQueryDescription moveQuery = world.Query().With<PositionComponent>().With<VelocityComponent>().BuildDescription();
        List<EntityInfo> results = new List<EntityInfo>(entityCount);

        world.FillQuery(moveQuery, results, false);
        _benchmarkSink = 0f;
        int accessOperations = Math.Max(1, queryRepeatCount * results.Count);

        long memoryBeforeQueryAccess = GC.GetTotalMemory(true);
        Stopwatch queryAccessStopwatch = Stopwatch.StartNew();

        for (int repeat = 0; repeat < queryRepeatCount; repeat++)
        {
            world.FillQuery(moveQuery, results, false);

            for (int i = 0; i < results.Count; i++)
            {
                EntityInfo entity = results[i];
                ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity);
                ref VelocityComponent velocity = ref world.GetComponent<VelocityComponent>(entity);
                _benchmarkSink += position.x + velocity.x;
            }
        }

        queryAccessStopwatch.Stop();
        long memoryAfterQueryAccess = GC.GetTotalMemory(true);
        Record("Query + GetComponent<T1,T2>", accessOperations, queryAccessStopwatch.Elapsed.TotalMilliseconds, memoryAfterQueryAccess - memoryBeforeQueryAccess);

        long memoryBeforeForEachSingle = GC.GetTotalMemory(true);
        Stopwatch forEachSingleStopwatch = Stopwatch.StartNew();

        for (int repeat = 0; repeat < queryRepeatCount; repeat++)
            world.ForEach<PositionComponent>(ReadPositionForBenchmark);

        forEachSingleStopwatch.Stop();
        long memoryAfterForEachSingle = GC.GetTotalMemory(true);
        Record("ForEach<T>", accessOperations, forEachSingleStopwatch.Elapsed.TotalMilliseconds, memoryAfterForEachSingle - memoryBeforeForEachSingle);

        long memoryBeforeForEachDouble = GC.GetTotalMemory(true);
        Stopwatch forEachDoubleStopwatch = Stopwatch.StartNew();

        for (int repeat = 0; repeat < queryRepeatCount; repeat++)
            world.ForEach<PositionComponent, VelocityComponent>(ReadPositionVelocityForBenchmark);

        forEachDoubleStopwatch.Stop();
        long memoryAfterForEachDouble = GC.GetTotalMemory(true);
        Record("ForEach<T1,T2>", accessOperations, forEachDoubleStopwatch.Elapsed.TotalMilliseconds, memoryAfterForEachDouble - memoryBeforeForEachDouble);

        long memoryBeforeForEachTriple = GC.GetTotalMemory(true);
        Stopwatch forEachTripleStopwatch = Stopwatch.StartNew();

        for (int repeat = 0; repeat < queryRepeatCount; repeat++)
            world.ForEach<PositionComponent, VelocityComponent, HealthComponent>(ReadPositionVelocityHealthForBenchmark);

        forEachTripleStopwatch.Stop();
        long memoryAfterForEachTriple = GC.GetTotalMemory(true);
        Record("ForEach<T1,T2,T3>", accessOperations, forEachTripleStopwatch.Elapsed.TotalMilliseconds, memoryAfterForEachTriple - memoryBeforeForEachTriple);

        if (float.IsNaN(_benchmarkSink))
            Debug.LogError("[ECS Benchmark] Benchmark sink became NaN.");
    }

    /// <summary>细分测试覆盖已有组件、新增组件和移除组件的结构变更耗时。</summary>
    private void BenchmarkComponentMutationPaths()
    {
        World world = new World();

        if (enableCapacityPrewarm)
        {
            world.EnsureEntityCapacity(entityCount);
            world.EnsureComponentCapacity<PositionComponent>(entityCount);
            world.EnsureComponentCapacity<HealthComponent>(entityCount);
        }

        EntityInfo[] entities = new EntityInfo[entityCount];

        for (int i = 0; i < entities.Length; i++)
        {
            EntityInfo entity = world.CreateEntity();
            entities[i] = entity;
            world.SetComponent(entity, new PositionComponent(i, 0f, 0f));
        }

        long memoryBeforeOverwrite = GC.GetTotalMemory(true);
        Stopwatch overwriteStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < entities.Length; i++)
            world.SetComponent(entities[i], new PositionComponent(i + 1f, 0f, 0f));

        overwriteStopwatch.Stop();
        long memoryAfterOverwrite = GC.GetTotalMemory(true);
        Record("SetComponent Overwrite Existing", entities.Length, overwriteStopwatch.Elapsed.TotalMilliseconds, memoryAfterOverwrite - memoryBeforeOverwrite);

        long memoryBeforeAdd = GC.GetTotalMemory(true);
        Stopwatch addStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < entities.Length; i++)
            world.SetComponent(entities[i], new HealthComponent(100, 100));

        addStopwatch.Stop();
        long memoryAfterAdd = GC.GetTotalMemory(true);
        Record("AddComponent New Type", entities.Length, addStopwatch.Elapsed.TotalMilliseconds, memoryAfterAdd - memoryBeforeAdd);

        long memoryBeforeRemove = GC.GetTotalMemory(true);
        Stopwatch removeStopwatch = Stopwatch.StartNew();

        for (int i = 0; i < entities.Length; i++)
            world.RemoveComponent<HealthComponent>(entities[i]);

        removeStopwatch.Stop();
        long memoryAfterRemove = GC.GetTotalMemory(true);
        Record("RemoveComponent", entities.Length, removeStopwatch.Elapsed.TotalMilliseconds, memoryAfterRemove - memoryBeforeRemove);

        world.Dispose();
    }

    /// <summary>测试 MovementSystem 在固定逻辑帧中的执行耗时。</summary>
    private void BenchmarkMovementTick(World world)
    {
        MovementSystem movementSystem = new MovementSystem();
        world.AddSystem(movementSystem);

        for (int i = 0; i < warmupTickCount; i++)
            world.Tick(new SimulationContext(i + 1, tickLength, false));

        world.ResetSystemProfiles();

        long memoryBefore = GC.GetTotalMemory(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < tickCount; i++)
            world.Tick(new SimulationContext(warmupTickCount + i + 1, tickLength, false));

        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(true);

        Record("World.Tick + MovementSystem", tickCount, stopwatch.Elapsed.TotalMilliseconds, memoryAfter - memoryBefore);
    }

    /// <summary>测试 Entity 销毁及其组件移除耗时。</summary>
    private void BenchmarkEntityDestroy(World world, EntityInfo[] entities)
    {
        long memoryBefore = GC.GetTotalMemory(true);
        Stopwatch stopwatch = Stopwatch.StartNew();

        for (int i = 0; i < entities.Length; i++)
            world.DestroyEntity(entities[i]);

        stopwatch.Stop();
        long memoryAfter = GC.GetTotalMemory(true);

        Record("Destroy Entity + Remove Components", entities.Length, stopwatch.Elapsed.TotalMilliseconds, memoryAfter - memoryBefore);
        Debug.Log($"<color=cyan>[ECS Benchmark] After Destroy: {world.GetStatistics()}</color>");
    }

    /// <summary>Benchmark 单组件 ForEach 读取回调。</summary>
    private void ReadPositionForBenchmark(EntityInfo entity, ref PositionComponent position)
    {
        _benchmarkSink += position.x;
    }

    /// <summary>Benchmark 双组件 ForEach 读取回调。</summary>
    private void ReadPositionVelocityForBenchmark(EntityInfo entity, ref PositionComponent position, ref VelocityComponent velocity)
    {
        _benchmarkSink += position.x + velocity.x;
    }

    /// <summary>Benchmark 三组件 ForEach 读取回调。</summary>
    private void ReadPositionVelocityHealthForBenchmark(EntityInfo entity, ref PositionComponent position, ref VelocityComponent velocity, ref HealthComponent health)
    {
        _benchmarkSink += position.x + velocity.x + health.current;
    }

    /// <summary>记录一条 Benchmark 结果。</summary>
    private void Record(string name, int operations, double milliseconds, long memoryDelta)
    {
        BenchmarkRecord record = new BenchmarkRecord(name, operations, milliseconds, memoryDelta);
        _records.Add(record);
        Debug.Log(record.ToLogString());
    }

    /// <summary>输出当前 System 性能统计。</summary>
    private void LogSystemProfiles(List<SystemProfileInfo> profiles)
    {
        if (profiles == null || profiles.Count == 0)
        {
            Debug.Log("[ECS Benchmark] No system profile data.");
            return;
        }

        for (int i = 0; i < profiles.Count; i++)
            Debug.Log($"<color=yellow>[ECS Benchmark] {profiles[i]}</color>");
    }

    /// <summary>输出本次 Benchmark 总结。</summary>
    private void LogSummary(long retainedMemoryDelta)
    {
        Debug.Log("<color=cyan>[ECS Benchmark] Summary</color>");

        for (int i = 0; i < _records.Count; i++)
            Debug.Log(_records[i].ToCompactString());

        Debug.Log($"<color=cyan>[ECS Benchmark] Retained Managed Memory Delta After Dispose = {FormatBytes(retainedMemoryDelta)}</color>");
        Debug.Log("<color=green>[ECS Benchmark] Finished.</color>");
    }

    /// <summary>强制执行一次 GC，用于降低 Benchmark 前后内存统计噪声。</summary>
    private static void ForceCollectGarbage()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    /// <summary>格式化字节数，便于 Console 查看。</summary>
    private static string FormatBytes(long bytes)
    {
        double value = bytes;
        string[] units = { "B", "KB", "MB", "GB" };
        int unitIndex = 0;

        while (Math.Abs(value) >= 1024d && unitIndex < units.Length - 1)
        {
            value /= 1024d;
            unitIndex++;
        }

        return $"{value:F2} {units[unitIndex]}";
    }

    /// <summary>单项 Benchmark 结果。</summary>
    private readonly struct BenchmarkRecord
    {
        private readonly string _name;
        private readonly int _operations;
        private readonly double _milliseconds;
        private readonly long _memoryDelta;

        public BenchmarkRecord(string name, int operations, double milliseconds, long memoryDelta)
        {
            _name = string.IsNullOrEmpty(name) ? "Unknown" : name;
            _operations = operations <= 0 ? 1 : operations;
            _milliseconds = milliseconds < 0d ? 0d : milliseconds;
            _memoryDelta = memoryDelta;
        }

        public string ToLogString()
        {
            return $"<color=yellow>[ECS Benchmark]</color> {_name}: Total={_milliseconds:F4}ms, Avg={_milliseconds / _operations:F6}ms/op, Ops={_operations}, MemoryDelta={FormatBytes(_memoryDelta)}";
        }

        public string ToCompactString()
        {
            return $"[ECS Benchmark] {_name} => Total={_milliseconds:F4}ms, Avg={_milliseconds / _operations:F6}ms/op, Ops={_operations}, MemoryDelta={FormatBytes(_memoryDelta)}";
        }
    }
}
