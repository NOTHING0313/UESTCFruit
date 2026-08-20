using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// NetworkRollbackClientRuntime 双后端验证入口。
    /// 放在非 Editor 目录，使其与 Rollback 内部绑定类型处于 Assembly-CSharp，
    /// Editor NUnit 仅调用公开验证入口，不跨程序集访问 internal Rollback API。
    /// </summary>
    public static class NetworkRollbackClientRuntimeValidationTestBootstrap
    {
        private const uint SessionId=0x11223344u;
        private const int PlayerID=1;
        private const int TimeoutMs=3000;
        private const int FrameCount=20;
        private const float TickLength=1f/60f;

        /// <summary>验证 Raw UDP 经统一 Runtime 主链完成 20 帧 Authority 应用。</summary>
        public static void RunRawUdp20FramesStatic()
        {
            using var environment=CreateEnvironment();
            using var server=new LocalNetworkInputServer(
                new UdpTransportConfig("127.0.0.1",0,NetworkProtocolConstants.MaxDatagramSize),
                SessionId);

            var assembler=CreateAssembler();

            using var runtime=NetworkRollbackClientRuntimeFactory.Create(
                new NetworkInputClientOptions(
                    NetworkInputTransportMode.RawUdp,
                    "127.0.0.1",
                    server.LocalEndPoint.Port,
                    SessionId,
                    PlayerID,
                    "127.0.0.1"),
                assembler,
                environment.Coordinator);

            server.RegisterPlayer(PlayerID,runtime.LocalEndPoint);

            Expect(runtime.IsReady,"03B Raw UDP Runtime Ready Error");

            RunRawUdpFrames(environment,server,runtime,assembler,FrameCount);

            Expect(runtime.ReceivedAuthorityCount==FrameCount,
                $"03B Raw UDP Received Authority Count Error: Expected={FrameCount}, Actual={runtime.ReceivedAuthorityCount}");
            Expect(runtime.AppliedAuthorityCount==FrameCount,
                $"03B Raw UDP Applied Authority Count Error: Expected={FrameCount}, Actual={runtime.AppliedAuthorityCount}");
            Expect(server.AuthorityFrameCount==FrameCount,
                $"03B Raw UDP Server Authority Count Error: Expected={FrameCount}, Actual={server.AuthorityFrameCount}");
            Expect(server.RejectedDatagramCount==0,
                $"03B Raw UDP Server Reject Error: Actual={server.RejectedDatagramCount}");
        }

        /// <summary>验证 KCP 经统一 Runtime 主链完成 20 帧 Authority 应用。</summary>
        public static void RunKcp20FramesStatic()
        {
            using var environment=CreateEnvironment();
            using var server=new KcpNetworkInputServer(0,1,SessionId);
            var assembler=CreateAssembler();

            using var runtime=NetworkRollbackClientRuntimeFactory.Create(
                new NetworkInputClientOptions(
                    NetworkInputTransportMode.Kcp,
                    "127.0.0.1",
                    server.LocalEndPoint.Port,
                    SessionId,
                    PlayerID),
                assembler,
                environment.Coordinator);

            WaitKcpReady(server,runtime);
            RunKcpFrames(environment,server,runtime,assembler,FrameCount);

            Expect(runtime.ReceivedAuthorityCount==FrameCount,
                $"03B KCP Received Authority Count Error: Expected={FrameCount}, Actual={runtime.ReceivedAuthorityCount}");
            Expect(runtime.AppliedAuthorityCount==FrameCount,
                $"03B KCP Applied Authority Count Error: Expected={FrameCount}, Actual={runtime.AppliedAuthorityCount}");
            Expect(server.AuthorityFrameCount==FrameCount,
                $"03B KCP Server Authority Count Error: Expected={FrameCount}, Actual={server.AuthorityFrameCount}");
            Expect(server.RejectedMessageCount==0,
                $"03B KCP Server Reject Error: Actual={server.RejectedMessageCount}");
            Expect(runtime.LastTransportError==null,
                $"03B KCP Transport Error: {runtime.LastTransportError}");
        }

        private static FrameInputAssembler CreateAssembler()
        {
            var assembler=new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
            assembler.RegisterPlayer(PlayerID);
            return assembler;
        }

        private static void RunRawUdpFrames(
            TestEnvironment environment,
            LocalNetworkInputServer server,
            NetworkRollbackClientRuntime runtime,
            FrameInputAssembler assembler,
            int frames)
        {
            for(int frame=1;frame<=frames;frame++)
            {
                PlayerInputSnapshot input=CreateInput(frame);
                FrameInputSet frameInput=Assemble(assembler,in input);

                runtime.SendInput(in input);
                DriveFrame(environment,frame,frameInput);

                Stopwatch stopwatch=Stopwatch.StartNew();

                while(stopwatch.ElapsedMilliseconds<TimeoutMs&&runtime.AppliedAuthorityCount<frame)
                {
                    server.TryProcessOneDatagram(out _);
                    runtime.Tick();

                    if(runtime.AppliedAuthorityCount<frame)
                        Thread.Sleep(1);
                }

                Expect(runtime.AppliedAuthorityCount==frame,
                    $"03B Raw UDP Authority Timeout: Frame={frame}, Applied={runtime.AppliedAuthorityCount}");
                Expect(environment.Coordinator.CurrentFrame==frame,
                    $"03B Raw UDP Coordinator Frame Error: Expected={frame}, Actual={environment.Coordinator.CurrentFrame}");
            }
        }

        private static void RunKcpFrames(
            TestEnvironment environment,
            KcpNetworkInputServer server,
            NetworkRollbackClientRuntime runtime,
            FrameInputAssembler assembler,
            int frames)
        {
            for(int frame=1;frame<=frames;frame++)
            {
                PlayerInputSnapshot input=CreateInput(frame);
                FrameInputSet frameInput=Assemble(assembler,in input);

                runtime.SendInput(in input);
                DriveFrame(environment,frame,frameInput);

                Stopwatch stopwatch=Stopwatch.StartNew();

                while(stopwatch.ElapsedMilliseconds<TimeoutMs&&runtime.AppliedAuthorityCount<frame)
                {
                    server.Tick();
                    runtime.Tick();

                    if(runtime.AppliedAuthorityCount<frame)
                        Thread.Sleep(1);
                }

                Expect(runtime.AppliedAuthorityCount==frame,
                    $"03B KCP Authority Timeout: Frame={frame}, Applied={runtime.AppliedAuthorityCount}, Error={runtime.LastTransportError}");
                Expect(environment.Coordinator.CurrentFrame==frame,
                    $"03B KCP Coordinator Frame Error: Expected={frame}, Actual={environment.Coordinator.CurrentFrame}");
            }
        }

        private static void WaitKcpReady(KcpNetworkInputServer server,NetworkRollbackClientRuntime runtime)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs&&!runtime.IsReady)
            {
                server.Tick();
                runtime.Tick();
                Thread.Sleep(1);
            }

            Expect(runtime.IsReady,
                $"03B KCP Runtime Connect Timeout: Error={runtime.LastTransportError}");
        }

        private static FrameInputSet Assemble(FrameInputAssembler assembler,in PlayerInputSnapshot input)
        {
            var accumulator=new FrameInputAccumulator(input.frameNumber);
            Expect(accumulator.TryAddInput(in input),
                $"03B FrameInputAccumulator Add Error: Frame={input.frameNumber}");

            FrameInputAssemblyResult result=assembler.Assemble(accumulator);

            Expect(!result.HasPrediction,
                $"03B Unexpected Prediction Error: Frame={input.frameNumber}");

            return result.InputSet;
        }

        private static PlayerInputSnapshot CreateInput(int frame)
            =>new(frame,PlayerID)
            {
                moveX=(frame%3)-1,
                moveY=((frame+1)%3)-1
            };

        private static void DriveFrame(TestEnvironment environment,int frame,FrameInputSet input)
        {
            RollbackStepResult result=environment.Coordinator.TryStep(frame,input);

            Expect(result.Succeeded,
                $"03B DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context=new SimulationContext(frame,TickLength,false);

            environment.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.BeforeTick);

            environment.World.Tick(in context);

            environment.CommandApplier.ApplyCommandsToWorld(
                frame,
                SimulationFrameCommandTiming.AfterTick);

            environment.Coordinator.SaveSnapshot();
        }

        private static TestEnvironment CreateEnvironment()
        {
            var world=new World { EnableSystemProfile=false };
            Entity player=world.CreateEntity();

            world.SetComponent(player,new PlayerTagComponent());
            world.SetComponent(player,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));
            world.SetComponent(player,new MoveSpeedComponent(3f));
            world.SetComponent(player,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player,new PositionComponent(0f,0f,0f));
            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var inputApplier=new FrameInputSetApplier();
            inputApplier.RegisterPlayer(PlayerID,player);

            var commandBuffer=new SimulationFrameCommandBuffer(128);
            var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,128);

            var rollbackAdapter=new WorldRollbackAdapter<FrameInputSet>(
                world,
                world,
                inputApplier,
                null);

            // internal Rollback API：此 Bootstrap 位于 Assembly-CSharp，因此可以合法接线。
            rollbackAdapter.SetFrameCommandReplayBinding(
                new RollbackFrameCommandReplayBinding(
                    commandBuffer,
                    commandApplier));

            var snapshotBuffer=new SnapshotRingBuffer<EcsWorldSnapshot>(128);

            var coordinator=new RollbackCoordinator<FrameInputSet,EcsWorldSnapshot>(
                new InputBuffer<FrameInputSet>(),
                new AuthoritativeInputBuffer<FrameInputSet>(),
                snapshotBuffer,
                rollbackAdapter,
                new FrameInputSetComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength=TickLength
            };

            coordinator.SaveSnapshot();

            return new TestEnvironment(
                world,
                coordinator,
                commandBuffer,
                commandApplier,
                snapshotBuffer);
        }

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(
                World world,
                RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,
                SimulationFrameCommandApplier commandApplier,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World=world;
                Coordinator=coordinator;
                CommandBuffer=commandBuffer;
                CommandApplier=commandApplier;
                SnapshotBuffer=snapshotBuffer;
            }

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                World.Dispose();
            }
        }
    }
}
