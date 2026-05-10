using System;
using UnityEngine;

/// <summary>
/// 针对审查中发现的生命周期与帧指令应用问题的回归测试。
/// </summary>
public sealed class ECSReviewIssueRegressionTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Review Issue Regression Test] Start</color>");

        TestDisposeDuringTickKeepsDisposingStateAndStopsPlayback();
        TestFrameCommandApplierCanRetryWhenCommandThrows();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Review Issue Regression Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Review Issue Regression Test] Failed count = {_failedCount}");
    }

    private void TestDisposeDuringTickKeepsDisposingStateAndStopsPlayback()
    {
        Debug.Log("<color=cyan>[Review Regression 1] Dispose During Tick</color>");

        World world = new World();
        EntityInfo entity = world.CreateEntity();
        ReviewDisposeDuringTickSystem disposeSystem = new ReviewDisposeDuringTickSystem(entity);
        ReviewPassiveAfterDisposeSystem passiveSystem = new ReviewPassiveAfterDisposeSystem();

        world.AddSystem(disposeSystem);
        world.AddSystem(passiveSystem);

        SimulationContext context = new SimulationContext(1, 1f, false);
        world.Tick(in context);

        Expect(disposeSystem.TickCount == 1, "Dispose system should tick once.");
        Expect(passiveSystem.TickCount == 0, "Later systems should not tick after World.Dispose() is called during Tick.");
        Expect(world.CurrentState == WorldStates.Disposing, "World should keep Disposing state after Dispose() is called during Tick.");
        Expect(world.PendingCommandCount == 0, "StructuralChangeBuffer should be cleared by Dispose().");
        Expect(world.PendingSystemCommandCount == 0, "SystemChangeBuffer should be cleared by Dispose().");
        Expect(world.SystemCount == 0, "Systems should be cleared by Dispose().");
        Expect(!world.HasComponent<ReviewDeferredComponent>(entity), "Deferred component requested before Dispose() should not be played back after Dispose().");
    }

    private void TestFrameCommandApplierCanRetryWhenCommandThrows()
    {
        Debug.Log("<color=cyan>[Review Regression 2] FrameCommand Retry After Exception</color>");

        World world = new World();
        SimulationFrameCommandBuffer commandBuffer = new SimulationFrameCommandBuffer();
        SimulationFrameCommandApplier applier = new SimulationFrameCommandApplier(world, commandBuffer);

        ReviewThrowOnceFrameCommand throwCommand = new ReviewThrowOnceFrameCommand(1);
        ReviewCountFrameCommand countCommand = new ReviewCountFrameCommand(1);

        commandBuffer.AddCommand(throwCommand, SimulationFrameCommandTiming.BeforeTick);
        commandBuffer.AddCommand(countCommand, SimulationFrameCommandTiming.BeforeTick);

        bool caught = false;

        try
        {
            applier.ApplyCommandsToWorld(1, SimulationFrameCommandTiming.BeforeTick);
        }
        catch (InvalidOperationException)
        {
            caught = true;
        }

        Expect(caught, "First ApplyCommandsToWorld should surface the command exception.");
        Expect(throwCommand.ExecuteCount == 1, "Throw command should execute once during failed apply.");
        Expect(countCommand.ExecuteCount == 0, "Commands after the throwing command should not execute during failed apply.");

        throwCommand.ShouldThrow = false;
        applier.ApplyCommandsToWorld(1, SimulationFrameCommandTiming.BeforeTick);

        Expect(throwCommand.ExecuteCount == 2, "Throw command should be retried because failed frame was not marked as applied.");
        Expect(countCommand.ExecuteCount == 1, "Command after recovered throwing command should execute on retry.");

        applier.ApplyCommandsToWorld(1, SimulationFrameCommandTiming.BeforeTick);

        Expect(throwCommand.ExecuteCount == 2, "Successfully applied frame should not execute again in normal apply mode.");
        Expect(countCommand.ExecuteCount == 1, "Successfully applied frame should remain consumed in normal apply mode.");
    }

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

public struct ReviewDeferredComponent : IComponentData
{
    public int value;
}

public sealed class ReviewDisposeDuringTickSystem : FixedStepSystemBase
{
    private readonly EntityInfo _entity;

    public int TickCount { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.logic;

    public ReviewDisposeDuringTickSystem(EntityInfo entity)
    {
        _entity = entity;
    }

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        World.SetComponent(_entity, new ReviewDeferredComponent { value = 1 });
        World.Dispose();
    }
}

public sealed class ReviewPassiveAfterDisposeSystem : FixedStepSystemBase
{
    public int TickCount { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.movement;

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
    }
}

public sealed class ReviewThrowOnceFrameCommand : ISimulationFrameCommand
{
    public int FrameNumber { get; }
    public int ExecuteCount { get; private set; }
    public bool ShouldThrow { get; set; } = true;

    public ReviewThrowOnceFrameCommand(int frameNumber)
    {
        FrameNumber = frameNumber;
    }

    public void Execute(World world)
    {
        ExecuteCount++;

        if (ShouldThrow)
            throw new InvalidOperationException("Intentional test exception.");
    }
}

public sealed class ReviewCountFrameCommand : ISimulationFrameCommand
{
    public int FrameNumber { get; }
    public int ExecuteCount { get; private set; }

    public ReviewCountFrameCommand(int frameNumber)
    {
        FrameNumber = frameNumber;
    }

    public void Execute(World world)
    {
        ExecuteCount++;
    }
}
