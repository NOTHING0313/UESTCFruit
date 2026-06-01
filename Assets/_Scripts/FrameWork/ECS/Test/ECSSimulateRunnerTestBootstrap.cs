using System;
using UnityEngine;

namespace ECSFrameWork
{

public class ECSSimulateRunnerTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS SimulateRunner Test] Start</color>");

        TestRunnerAccumulatesSmallDeltaTime();
        TestRunnerAdvancesMultipleTicks();
        TestRunnerCompensationLimit();
        TestRunnerCompensationLimitDiscardsRemainder();
        TestRunnerRejectsNonPositiveDeltaTime();
        TestRunnerInvalidParametersFallback();
        TestSystemSequenceOrder();
        TestRunnerResetsTickingStateAfterException();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS SimulateRunner Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS SimulateRunner Test] Failed count = {_failedCount}");
    }

    private void TestRunnerAccumulatesSmallDeltaTime()
    {
        Debug.Log("<color=cyan>[Runner Test 1] Runner Accumulates Small DeltaTime</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 10);

        bool firstResult = runner.Update(0.01f);
        bool secondResult = runner.Update(0.01f);

        Expect(!firstResult, "First 0.01 update should not produce a tick when tickLength is 0.02.");
        Expect(secondResult, "Second 0.01 update should produce one tick after accumulation.");
        Expect(system.TickCount == 1, $"System should tick once after accumulated 0.02 seconds. Actual = {system.TickCount}");
        Expect(system.LastFrameNumber == 1, $"First tick frameNumber should be 1. Actual = {system.LastFrameNumber}");
        Expect(Mathf.Approximately(system.LastTickLength, 0.02f), $"Tick length should be 0.02. Actual = {system.LastTickLength}");
    }

    private void TestRunnerAdvancesMultipleTicks()
    {
        Debug.Log("<color=cyan>[Runner Test 2] Runner Advances Multiple Ticks</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 10);

        bool result = runner.Update(0.05f);

        Expect(result, "0.05 update should produce ticks when tickLength is 0.02.");
        Expect(system.TickCount == 2, $"0.05 seconds should produce 2 ticks and keep 0.01 remainder. Actual = {system.TickCount}");
        Expect(system.LastFrameNumber == 2, $"Last frameNumber should be 2. Actual = {system.LastFrameNumber}");
    }

    private void TestRunnerCompensationLimit()
    {
        Debug.Log("<color=cyan>[Runner Test 3] Runner Compensation Limit</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 2);

        bool result = runner.Update(0.20f);

        Expect(result, "Large update should produce ticks.");
        Expect(system.TickCount == 2, $"maxCompensationTickCount = 2 should limit this update to 2 ticks. Actual = {system.TickCount}");
    }

    private void TestRunnerCompensationLimitDiscardsRemainder()
    {
        Debug.Log("<color=cyan>[Runner Test 3.1] Runner Compensation Limit Discards Remainder</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 5);

        bool result = runner.Update(1.0f);

        Expect(result, "Large update should return true when at least one frame is executed.");
        Expect(system.TickCount == 5, $"maxCompensationTickCount = 5 should execute exactly 5 frames. Actual = {system.TickCount}");
        Expect(runner.FrameCount == 5, $"Runner FrameCount should advance to 5 after compensation-limited update. Actual = {runner.FrameCount}");
        Expect(Mathf.Approximately(runner.TickCounter, 0f), $"Runner should discard remaining accumulated time after compensation limit. TickCounter = {runner.TickCounter}");
    }

    private void TestRunnerRejectsNonPositiveDeltaTime()
    {
        Debug.Log("<color=cyan>[Runner Test 3.2] Runner Rejects Non-positive DeltaTime</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 5);

        bool zeroResult = runner.Update(0f);
        bool negativeResult = runner.Update(-1f);

        Expect(!zeroResult, "Update(0) should return false.");
        Expect(!negativeResult, "Update with negative delta time should return false.");
        Expect(system.TickCount == 0, $"Non-positive delta time should not tick systems. Actual = {system.TickCount}");
        Expect(runner.FrameCount == 0, $"Non-positive delta time should not advance frames. Actual = {runner.FrameCount}");
        Expect(Mathf.Approximately(runner.TickCounter, 0f), $"Non-positive delta time should not accumulate time. TickCounter = {runner.TickCounter}");
    }

    private void TestRunnerInvalidParametersFallback()
    {
        Debug.Log("<color=cyan>[Runner Test 4] Runner Invalid Parameters Fallback</color>");

        World world = new World();
        RunnerCountingSystem system = new RunnerCountingSystem();
        world.AddSystem(system);
        SimulateRunner runner = new SimulateRunner(world, -1f, 0);

        bool result = runner.Update(0.05f);

        Expect(result, "Runner with invalid parameters should still tick using fallback values.");
        Expect(system.TickCount == 1, $"Invalid max compensation should fallback to 1 tick. Actual = {system.TickCount}");
        Expect(Mathf.Approximately(system.LastTickLength, 0.02f), $"Invalid tickLength should fallback to 0.02. Actual = {system.LastTickLength}");
    }

    private void TestSystemSequenceOrder()
    {
        Debug.Log("<color=cyan>[Runner Test 5] System Sequence Order</color>");

        World world = new World();
        RunnerOrderRecorder.Reset();

        RunnerOrderedSystem normalSystem = new RunnerOrderedSystem("normal", SystemTickSequence.normal);
        RunnerOrderedSystem vaultSystem = new RunnerOrderedSystem("vault", SystemTickSequence.normal);

        world.AddSystem(normalSystem);
        world.AddSystem(vaultSystem);
        world.Tick(new SimulationContext(1, 1f, false));

        Expect(RunnerOrderRecorder.Order == "normal>vault", $"Systems with the same sequence should keep insertion order. Actual = {RunnerOrderRecorder.Order}");
    }

    private void TestRunnerResetsTickingStateAfterException()
    {
        Debug.Log("<color=cyan>[Runner Test 6] Runner Resets State After Exception</color>");

        World world = new World();
        SimulateRunner runner = new SimulateRunner(world, 0.02f, 10);
        runner.BeforeTick += ThrowBeforeTick;

        bool caught = false;

        try
        {
            runner.TickFrame(1);
        }
        catch (InvalidOperationException)
        {
            caught = true;
        }

        runner.BeforeTick -= ThrowBeforeTick;
        bool canTickAgain = runner.TickFrame(2);

        Expect(caught, "Runner test should catch the injected BeforeTick exception.");
        Expect(!runner.IsTicking, "Runner should reset IsTicking after an exception.");
        Expect(canTickAgain && runner.FrameCount == 2, $"Runner should allow later ticks after exception. FrameCount = {runner.FrameCount}");
    }

    private void ThrowBeforeTick(SimulationContext context)
    {
        throw new InvalidOperationException("Injected runner lifecycle test exception.");
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

public class RunnerCountingSystem : FixedStepSystemBase
{
    public int TickCount { get; private set; }
    public int LastFrameNumber { get; private set; }
    public float LastTickLength { get; private set; }
    public override SystemTickSequence sequence => SystemTickSequence.normal;

    public override void Tick(in SimulationContext context)
    {
        TickCount++;
        LastFrameNumber = context.frameNumber;
        LastTickLength = context.tickLength;
    }
}

public static class RunnerOrderRecorder
{
    public static string Order { get; private set; }

    public static void Reset()
    {
        Order = string.Empty;
    }

    public static void Add(string name)
    {
        if (string.IsNullOrEmpty(Order))
            Order = name;
        else
            Order += ">" + name;
    }
}

public class RunnerOrderedSystem : FixedStepSystemBase
{
    private readonly string _name;
    private readonly SystemTickSequence _sequence;
    public override SystemTickSequence sequence => _sequence;

    public RunnerOrderedSystem(string name, SystemTickSequence sequence)
    {
        _name = name;
        _sequence = sequence;
    }

    public override void Tick(in SimulationContext context)
    {
        RunnerOrderRecorder.Add(_name);
    }
}

}
