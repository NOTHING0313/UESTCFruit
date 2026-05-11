using UnityEngine;

namespace ECSFrameWork
{

/// <summary>
/// 测试按帧输入缓存、外部帧指令缓存，以及它们在 World.Tick 前的消费顺序。
/// </summary>
public sealed class ECSFrameSyncBufferTestBootstrap : MonoBehaviour
{
    private int _failedCount;

    private void Start()
    {
        Debug.Log("<color=cyan>[ECS Frame Sync Buffer Test] Start</color>");

        TestInputSnapshotBufferDrivesFrameInput();
        TestFrameComponentCommandConsumedAtTargetFrame();
        TestFrameCommandApplierSkipsDuplicateNormalApply();
        TestFrameEntityDestroyCommandConsumedAtTargetFrame();

        if (_failedCount == 0)
            Debug.Log("<color=green>[ECS Frame Sync Buffer Test] All tests passed.</color>");
        else
            Debug.LogError($"[ECS Frame Sync Buffer Test] Failed count = {_failedCount}");
    }

    /// <summary>验证第 N 帧输入只在第 N 帧被写入并消费。</summary>
    private void TestInputSnapshotBufferDrivesFrameInput()
    {
        World world = new World();
        InputSnapshotBuffer inputBuffer = new InputSnapshotBuffer();
        WorldInputApplier inputApplier = new WorldInputApplier(world, inputBuffer);

        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PlayerInputSnapshotComponent(0f, 0f));
        world.SetComponent(entity, new MoveSpeedComponent(2f));
        world.SetComponent(entity, new VelocityComponent(0f, 0f, 0f));
        world.SetComponent(entity, new PositionComponent(0f, 0f, 0f));

        inputApplier.RegisterPlayerEntity(1, entity);
        world.AddSystem(new InputMoveSystem());
        world.AddSystem(new MovementSystem());

        PlayerInputSnapshot input = new PlayerInputSnapshot(1, 1)
        {
            moveX = 1f,
            moveY = 0f,
        };

        inputBuffer.SetInput(in input);
        inputApplier.ApplyInputToWorld(1);
        SimulationContext context1 = new SimulationContext(1, 0.5f, false);
        world.Tick(in context1);

        ref PositionComponent position = ref world.GetComponent<PositionComponent>(entity);
        ExpectApproximately(position.x, 1f, "Frame 1 input should move entity by speed * tickLength.");

        SimulationContext context2 = new SimulationContext(2, 0.5f, false);
        world.Tick(in context2);
        ref VelocityComponent velocity = ref world.GetComponent<VelocityComponent>(entity);
        ExpectApproximately(velocity.x, 0f, "Frame 2 should not reuse stale frame 1 input.");

        world.Dispose();
    }

    /// <summary>验证外部 SetComponent 指令只在目标逻辑帧开始前被消费。</summary>
    private void TestFrameComponentCommandConsumedAtTargetFrame()
    {
        World world = new World();
        SimulationFrameCommandBuffer commandBuffer = new SimulationFrameCommandBuffer();
        SimulationFrameCommandApplier commandApplier = new SimulationFrameCommandApplier(world, commandBuffer);

        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new MoveSpeedComponent(1f));

        commandBuffer.SetComponentAtFrame(2, entity, new MoveSpeedComponent(5f));

        commandApplier.ApplyCommandsToWorld(1);
        ref MoveSpeedComponent speedBefore = ref world.GetComponent<MoveSpeedComponent>(entity);
        ExpectApproximately(speedBefore.value, 1f, "Frame 1 should not consume frame 2 command.");

        commandApplier.ApplyCommandsToWorld(2);
        ref MoveSpeedComponent speedAfter = ref world.GetComponent<MoveSpeedComponent>(entity);
        ExpectApproximately(speedAfter.value, 5f, "Frame 2 should consume SetComponent command.");

        world.Dispose();
    }

    /// <summary>验证普通模拟不会重复消费同一帧同一时机的指令，同时保留显式回放入口。</summary>
    private void TestFrameCommandApplierSkipsDuplicateNormalApply()
    {
        World world = new World();
        SimulationFrameCommandBuffer commandBuffer = new SimulationFrameCommandBuffer();
        SimulationFrameCommandApplier commandApplier = new SimulationFrameCommandApplier(world, commandBuffer);

        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new MoveSpeedComponent(1f));
        commandBuffer.SetComponentAtFrame(2, entity, new MoveSpeedComponent(5f));

        commandApplier.ApplyCommandsToWorld(2);
        world.SetComponent(entity, new MoveSpeedComponent(7f));
        commandApplier.ApplyCommandsToWorld(2);

        ref MoveSpeedComponent speedAfterDuplicate = ref world.GetComponent<MoveSpeedComponent>(entity);
        ExpectApproximately(speedAfterDuplicate.value, 7f, "Frame command should not be applied twice during normal simulation.");

        commandApplier.ReplayCommandsToWorld(2, SimulationFrameCommandTiming.BeforeTick);
        ref MoveSpeedComponent speedAfterReplay = ref world.GetComponent<MoveSpeedComponent>(entity);
        ExpectApproximately(speedAfterReplay.value, 5f, "ReplayCommandsToWorld should explicitly reapply cached frame commands.");

        world.Dispose();
    }

    /// <summary>验证外部 DestroyEntity 指令只在目标逻辑帧开始前被消费。</summary>
    private void TestFrameEntityDestroyCommandConsumedAtTargetFrame()
    {
        World world = new World();
        SimulationFrameCommandBuffer commandBuffer = new SimulationFrameCommandBuffer();
        SimulationFrameCommandApplier commandApplier = new SimulationFrameCommandApplier(world, commandBuffer);

        Entity entity = world.CreateEntity();
        world.SetComponent(entity, new PositionComponent(0f, 0f, 0f));

        commandBuffer.DestroyEntityAtFrame(3, entity);

        commandApplier.ApplyCommandsToWorld(2);
        Expect(world.IsAlive(entity), "Frame 2 should not consume frame 3 destroy command.");

        commandApplier.ApplyCommandsToWorld(3);
        Expect(!world.IsAlive(entity), "Frame 3 should consume DestroyEntity command.");

        world.Dispose();
    }

    /// <summary>判断浮点值是否近似相等。</summary>
    private void ExpectApproximately(float actual, float expected, string message)
    {
        if (Mathf.Approximately(actual, expected))
        {
            Debug.Log($"<color=green>[PASS]</color> {message} Actual = {actual}");
        }
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message} Expected = {expected}, Actual = {actual}");
        }
    }

    /// <summary>输出普通布尔断言。</summary>
    private void Expect(bool condition, string message)
    {
        if (condition)
        {
            Debug.Log($"<color=green>[PASS]</color> {message}");
        }
        else
        {
            _failedCount++;
            Debug.LogError($"[FAIL] {message}");
        }
    }
}

}
