using ECSFrameWork;
using UnityEngine;

/// <summary>
/// ECSWorldDebuggerWindow 的运行时测试入口。
/// 挂到场景 GameObject 后，EditorWindow 可以通过 IECSRuntimeDebugSource 找到这个测试 World。
/// </summary>
public sealed class ECSWorldDebuggerWindowTestBootstrap : MonoBehaviour, IECSRuntimeDebugSource, IECSFrameCommandDebugSource
{
    private World _world;
    private SimulateRunner _runner;
    private SimulationFrameCommandBuffer _commandBuffer;
    private SimulationFrameCommandApplier _commandApplier;
    private Entity _mover;
    private Entity _staticTarget;
    private int _spawnIndex;

    public World DebugWorld => _world;
    public SimulateRunner DebugRunner => _runner;
    public string DebugSourceName => $"Window Test Source - {name}";
    public SimulationFrameCommandBuffer DebugFrameCommandBuffer => _commandBuffer;
    public SimulationFrameCommandApplier DebugFrameCommandApplier => _commandApplier;

    private void Awake()
    {
        _world = new World();
        _runner = new SimulateRunner(_world, 0.02f, 4);
        _commandBuffer = new SimulationFrameCommandBuffer();
        _commandApplier = new SimulationFrameCommandApplier(_world, _commandBuffer);

        _runner.BeforeTick += OnBeforeTick;
        _runner.AfterTick += OnAfterTick;

        _world.EnableSystemProfile = true;

        CreateTestWorld();
        CreateTestCommands();
    }

    private void Update()
    {
        _runner?.Update(Time.deltaTime);
    }

    private void OnDestroy()
    {
        if (_runner != null)
        {
            _runner.BeforeTick -= OnBeforeTick;
            _runner.AfterTick -= OnAfterTick;
        }

        _world?.Dispose();
        _world = null;
        _runner = null;
        _commandBuffer = null;
        _commandApplier = null;
    }

    /// <summary>逻辑帧开始前消费测试用 BeforeTick Command。</summary>
    private void OnBeforeTick(SimulationContext context)
    {
        _commandApplier?.ApplyCommandsToWorld(context.frameNumber, SimulationFrameCommandTiming.BeforeTick);
    }

    /// <summary>逻辑帧结束后消费测试用 AfterTick Command。</summary>
    private void OnAfterTick(SimulationContext context)
    {
        _commandApplier?.ApplyCommandsToWorld(context.frameNumber, SimulationFrameCommandTiming.AfterTick);
    }

    /// <summary>创建一组固定测试数据，供 ECSWorldDebuggerWindow 显示。</summary>
    private void CreateTestWorld()
    {
        _world.AddSystem(new WindowDebugMoveSystem());

        _world.SetSingleton(new WindowDebugTimeSingleton(0));

        Entity mover = _world.CreateEntity();
        _mover = mover;
        _world.SetComponent(mover, new WindowDebugPositionComponent(0, 0, 0));
        _world.SetComponent(mover, new WindowDebugVelocityComponent(1, 0, 0));
        _world.SetComponent(mover, new WindowDebugHealthComponent(100, 100));

        Entity staticTarget = _world.CreateEntity();
        _staticTarget = staticTarget;
        _world.SetComponent(staticTarget, new WindowDebugPositionComponent(5, 0, 0));
        _world.SetComponent(staticTarget, new WindowDebugHealthComponent(50, 50));

        Entity positionOnly = _world.CreateEntity();
        _world.SetComponent(positionOnly, new WindowDebugPositionComponent(-3, 0, 0));

        _world.AddWorldEvent(new WindowDebugSpawnEvent(0, mover));
        _world.AddWorldEvent(new WindowDebugSpawnEvent(0, staticTarget));
    }

    /// <summary>创建一组会在前几帧执行的测试 Command，方便 Commands 页直接看到 CommandHistory 和 ExecutionHistory。</summary>
    private void CreateTestCommands()
    {
        if (_commandBuffer == null)
            return;

        _commandBuffer.AddCommand(new SetComponentFrameCommand<WindowDebugVelocityComponent>(1, _mover, new WindowDebugVelocityComponent(2, 0, 0)), SimulationFrameCommandTiming.BeforeTick);
        _commandBuffer.AddCommand(new SetComponentFrameCommand<WindowDebugHealthComponent>(2, _staticTarget, new WindowDebugHealthComponent(40, 50)), SimulationFrameCommandTiming.BeforeTick);

        CreateEntityFrameCommand createCommand = new CreateEntityFrameCommand(3)
            .WithComponent(new WindowDebugPositionComponent(8, 0, 0))
            .WithComponent(new WindowDebugHealthComponent(25, 25));
        _commandBuffer.AddCommand(createCommand, SimulationFrameCommandTiming.AfterTick);
    }

    /// <summary>右键组件菜单调用，用于测试 EditorWindow 自动刷新后数据是否变化。</summary>
    [ContextMenu("Spawn One More Entity")]
    private void SpawnOneMoreEntity()
    {
        if (_world == null)
            return;

        _spawnIndex++;

        Entity entity = _world.CreateEntity();
        _world.SetComponent(entity, new WindowDebugPositionComponent(_spawnIndex * 2, 0, 0));
        _world.SetComponent(entity, new WindowDebugHealthComponent(10 + _spawnIndex, 10 + _spawnIndex));

        int frame = _runner != null ? _runner.CurrentFrameNumber : 0;
        _world.AddWorldEvent(new WindowDebugSpawnEvent(frame, entity));

        Debug.Log($"[ECSWindowTest] Spawned {entity}");
    }
}

/// <summary>测试用位置组件。</summary>
public struct WindowDebugPositionComponent : IComponentData
{
    public float x;
    public float y;
    public float z;

    public WindowDebugPositionComponent(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

/// <summary>测试用速度组件。</summary>
public struct WindowDebugVelocityComponent : IComponentData
{
    public float x;
    public float y;
    public float z;

    public WindowDebugVelocityComponent(float x, float y, float z)
    {
        this.x = x;
        this.y = y;
        this.z = z;
    }
}

/// <summary>测试用生命值组件。</summary>
public struct WindowDebugHealthComponent : IComponentData
{
    public int currentHealth;
    public int maxHealth;

    public WindowDebugHealthComponent(int currentHealth, int maxHealth)
    {
        this.currentHealth = currentHealth;
        this.maxHealth = maxHealth;
    }
}

/// <summary>测试用 SingletonComponent。</summary>
public struct WindowDebugTimeSingleton : IComponentData
{
    public int frame;

    public WindowDebugTimeSingleton(int frame)
    {
        this.frame = frame;
    }
}

/// <summary>测试用 WorldEvent，同时提供 FrameNumber 和 frameNumber，兼容不同事件帧号读取习惯。</summary>
public readonly struct WindowDebugSpawnEvent : IWorldEvent
{
    public int FrameNumber { get; }
    public int frameNumber => FrameNumber;
    public readonly Entity entity;

    public WindowDebugSpawnEvent(int frameNumber, Entity entity)
    {
        FrameNumber = frameNumber;
        this.entity = entity;
    }
}

/// <summary>测试用 System，用于让 Systems 页显示 Profile 数据。</summary>
public sealed class WindowDebugMoveSystem : FixedStepSystemBase
{
    public override SystemTickSequence sequence => default;

    public override void Tick(in SimulationContext context)
    {
        float tickLength = context.tickLength;

        World.ForEach<WindowDebugPositionComponent, WindowDebugVelocityComponent>((Entity entity, ref WindowDebugPositionComponent position, ref WindowDebugVelocityComponent velocity) =>
        {
            position.x += velocity.x * tickLength;
            position.y += velocity.y * tickLength;
            position.z += velocity.z * tickLength;
        });
    }
}
