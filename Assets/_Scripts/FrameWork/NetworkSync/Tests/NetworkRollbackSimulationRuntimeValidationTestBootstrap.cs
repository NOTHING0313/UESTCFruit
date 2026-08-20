using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// 03C-1：使用真实 SimulateRunner 事件验证多人 KCP Runtime，不手工调用实际 World.Tick。
    /// </summary>
    public static class NetworkRollbackSimulationRuntimeValidationTestBootstrap
    {
        private const uint SessionId=0x11223344u;
        private const int Player1ID=1;
        private const int Player2ID=2;
        private const int TotalFrames=100;
        private const int MaxRemoteDelayFrames=3;
        private const int TimeoutMs=5000;
        private const float TickLength=1f/60f;
        private const uint Seed=20260821u;

        public static void RunKcpTwoPlayers100FramesStatic()
        {
            using var server=new KcpNetworkInputServer(0,2,SessionId);
            using var predicted=CreatePredictedEnvironment(server.LocalEndPoint.Port);
            using var reference=CreateReferenceEnvironment();
            using var remoteClient=new KcpNetworkInputClient("127.0.0.1",server.LocalEndPoint.Port,SessionId,Player2ID);

            WaitConnected(server,predicted.Runtime,remoteClient);

            var restoreProbe=new RestoreProbe();
            predicted.Runtime.AddRollbackRestoreListener(restoreProbe);
            predicted.Runtime.Mount();

            var delayedRemoteInputs=new List<PlayerInputSnapshot>[TotalFrames+MaxRemoteDelayFrames+1];
            uint delayState=Seed^0xA511E9B3u;
            int delayedCount=0,outOfOrderRemoteSendCount=0,highestRemoteSentFrame=0;

            for(int frame=1;frame<=TotalFrames;frame++)
            {
                PlayerInputSnapshot remoteInput=CreateInput(frame,Player2ID);
                int delay=NextRange(ref delayState,0,MaxRemoteDelayFrames);
                if(delay>0) delayedCount++;

                int sendFrame=frame+delay;
                delayedRemoteInputs[sendFrame]??=new List<PlayerInputSnapshot>();
                delayedRemoteInputs[sendFrame].Add(remoteInput);

                SendDueRemoteInputs(delayedRemoteInputs[frame],remoteClient,ref highestRemoteSentFrame,ref outOfOrderRemoteSendCount);

                // 先让上轮网络消息有机会到达；真正正常逻辑推进仍只由 Runner.StepNextFrame 完成。
                Pump(server,predicted.Runtime,remoteClient,1);

                FrameInputSet referenceInput=new FrameInputSet(frame,new[]
                {
                    CreateInput(frame,Player1ID),
                    CreateInput(frame,Player2ID)
                });

                reference.InputApplier.Apply(reference.World,referenceInput);
                Expect(reference.Runner.StepNextFrame(),
                    $"03C-1 Reference Runner Step Error: Frame={frame}");

                Expect(predicted.Runner.StepNextFrame(),
                    $"03C-1 Predicted Runner Step Error: Frame={frame}");

                Pump(server,predicted.Runtime,remoteClient,1);
            }

            for(int networkFrame=TotalFrames+1;networkFrame<=TotalFrames+MaxRemoteDelayFrames;networkFrame++)
            {
                SendDueRemoteInputs(delayedRemoteInputs[networkFrame],remoteClient,ref highestRemoteSentFrame,ref outOfOrderRemoteSendCount);
                Pump(server,predicted.Runtime,remoteClient,1);
            }

            Stopwatch flush=Stopwatch.StartNew();
            while(flush.ElapsedMilliseconds<TimeoutMs&&predicted.Runtime.NetworkRuntime.AppliedAuthorityCount<TotalFrames)
            {
                Pump(server,predicted.Runtime,remoteClient,1);
            }

            Expect(predicted.Runtime.NetworkRuntime.AppliedAuthorityCount==TotalFrames,
                $"03C-1 Final Authority Flush Error: Applied={predicted.Runtime.NetworkRuntime.AppliedAuthorityCount}/{TotalFrames}");
            Expect(predicted.Runtime.NetworkRuntime.ReceivedAuthorityCount==TotalFrames,
                $"03C-1 Final Authority Receive Error: Received={predicted.Runtime.NetworkRuntime.ReceivedAuthorityCount}/{TotalFrames}");
            Expect(server.ProcessedMessageCount==TotalFrames*2,
                $"03C-1 Server Processed Error: Expected={TotalFrames*2}, Actual={server.ProcessedMessageCount}");
            Expect(server.RejectedMessageCount==0,
                $"03C-1 Server Reject Error: Actual={server.RejectedMessageCount}");
            Expect(server.AuthorityFrameCount==TotalFrames,
                $"03C-1 Server Authority Error: Expected={TotalFrames}, Actual={server.AuthorityFrameCount}");
            Expect(remoteClient.LastSentSequence==(uint)TotalFrames,
                $"03C-1 Remote Sequence Error: Expected={TotalFrames}, Actual={remoteClient.LastSentSequence}");

            Expect(predicted.Runner.FrameCount==TotalFrames,
                $"03C-1 Predicted Runner Frame Error: Expected={TotalFrames}, Actual={predicted.Runner.FrameCount}");
            Expect(predicted.Runtime.Coordinator.CurrentFrame==TotalFrames,
                $"03C-1 Coordinator Frame Error: Expected={TotalFrames}, Actual={predicted.Runtime.Coordinator.CurrentFrame}");
            Expect(predicted.Runtime.NormalFrameCount==TotalFrames,
                $"03C-1 Runtime Normal Frame Error: Expected={TotalFrames}, Actual={predicted.Runtime.NormalFrameCount}");
            Expect(predicted.TickCounter.NormalTickCount==TotalFrames,
                $"03C-1 Normal World Tick Count Error: Expected={TotalFrames}, Actual={predicted.TickCounter.NormalTickCount}");
            Expect(reference.TickCounter.NormalTickCount==TotalFrames,
                $"03C-1 Reference Normal Tick Count Error: Expected={TotalFrames}, Actual={reference.TickCounter.NormalTickCount}");

            Expect(predicted.Runtime.PredictedFrameCount==TotalFrames,
                $"03C-1 Prediction Coverage Error: Expected={TotalFrames}, Actual={predicted.Runtime.PredictedFrameCount}");
            Expect(predicted.Runtime.PredictedInputCount>=TotalFrames,
                $"03C-1 Predicted Input Coverage Error: Actual={predicted.Runtime.PredictedInputCount}");
            Expect(delayedCount>0,"03C-1 Remote Delay Coverage Error");
            Expect(outOfOrderRemoteSendCount>0,
                $"03C-1 Remote OutOfOrder Send Coverage Error: Actual={outOfOrderRemoteSendCount}");
            Expect(predicted.Runtime.NetworkRuntime.OutOfOrderAuthorityCount>0,
                $"03C-1 Authority OutOfOrder Coverage Error: Actual={predicted.Runtime.NetworkRuntime.OutOfOrderAuthorityCount}");
            Expect(restoreProbe.RestoreCount>0,
                $"03C-1 Rollback Restore Coverage Error: Actual={restoreProbe.RestoreCount}");
            Expect(restoreProbe.ResimulateCount>0,
                $"03C-1 Rollback Resimulate Coverage Error: Actual={restoreProbe.ResimulateCount}");
            Expect(predicted.TickCounter.RollbackTickCount>0,
                $"03C-1 Rollback Tick Coverage Error: Actual={predicted.TickCounter.RollbackTickCount}");

            uint referenceChecksum=WorldChecksumCalculator.Calculate(reference.World);
            uint predictedChecksum=WorldChecksumCalculator.Calculate(predicted.World);

            Expect(referenceChecksum==predictedChecksum,
                $"03C-1 Final Checksum Error: Reference=0x{referenceChecksum:X8}, Predicted=0x{predictedChecksum:X8}");

            predicted.Runtime.RemoveRollbackRestoreListener(restoreProbe);
        }

        private static PredictedEnvironment CreatePredictedEnvironment(int serverPort)
        {
            TestWorldData data=CreateWorld();
            var runner=new SimulateRunner(data.World,TickLength,5);
            var commandBuffer=new SimulationFrameCommandBuffer(256);
            var commandApplier=new SimulationFrameCommandApplier(data.World,commandBuffer,256);

            var players=new[]
            {
                new NetworkPlayerBinding(Player1ID,data.Player1),
                new NetworkPlayerBinding(Player2ID,data.Player2)
            };

            var options=new NetworkInputClientOptions(
                NetworkInputTransportMode.Kcp,
                "127.0.0.1",
                serverPort,
                SessionId,
                Player1ID);

            var runtime=new NetworkRollbackSimulationRuntime(
                data.World,
                runner,
                commandBuffer,
                commandApplier,
                players,
                options,
                frame=>CreateInput(frame,Player1ID),
                256,
                10);

            return new PredictedEnvironment(data.World,runner,data.TickCounter,runtime,commandBuffer);
        }

        private static ReferenceEnvironment CreateReferenceEnvironment()
        {
            TestWorldData data=CreateWorld();
            var runner=new SimulateRunner(data.World,TickLength,5);
            var inputApplier=new FrameInputSetApplier();
            inputApplier.RegisterPlayer(Player1ID,data.Player1);
            inputApplier.RegisterPlayer(Player2ID,data.Player2);
            return new ReferenceEnvironment(data.World,runner,data.TickCounter,inputApplier);
        }

        private static TestWorldData CreateWorld()
        {
            var world=new World { EnableSystemProfile=false };

            Entity player1=world.CreateEntity();
            world.SetComponent(player1,new PlayerTagComponent());
            world.SetComponent(player1,new PlayerInputSnapshotComponent(0,Player1ID,0f,0f));
            world.SetComponent(player1,new MoveSpeedComponent(3.25f));
            world.SetComponent(player1,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player1,new PositionComponent(-5f,0f,0f));

            Entity player2=world.CreateEntity();
            world.SetComponent(player2,new PlayerTagComponent());
            world.SetComponent(player2,new PlayerInputSnapshotComponent(0,Player2ID,0f,0f));
            world.SetComponent(player2,new MoveSpeedComponent(2.75f));
            world.SetComponent(player2,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player2,new PositionComponent(5f,0f,0f));

            var tickCounter=new TickCounterSystem();

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());
            world.AddSystem(tickCounter);

            return new TestWorldData(world,player1,player2,tickCounter);
        }

        private static void WaitConnected(
            KcpNetworkInputServer server,
            NetworkRollbackSimulationRuntime runtime,
            KcpNetworkInputClient remoteClient)
        {
            Stopwatch stopwatch=Stopwatch.StartNew();

            while(stopwatch.ElapsedMilliseconds<TimeoutMs)
            {
                server.Tick();
                runtime.PumpNetwork();
                remoteClient.Tick();

                if(runtime.IsReady&&remoteClient.IsConnected&&server.ConnectedConnectionCount==2)
                    return;

                Thread.Sleep(1);
            }

            throw new InvalidOperationException(
                $"03C-1 KCP Connect Timeout: LocalReady={runtime.IsReady}, RemoteReady={remoteClient.IsConnected}, ServerConnections={server.ConnectedConnectionCount}, LocalError={runtime.NetworkRuntime.LastTransportError}, RemoteError={remoteClient.LastKcpError}:{remoteClient.LastKcpErrorMessage}");
        }

        private static void Pump(
            KcpNetworkInputServer server,
            NetworkRollbackSimulationRuntime runtime,
            KcpNetworkInputClient remoteClient,
            int sleepMilliseconds)
        {
            server.Tick();
            remoteClient.Tick();
            runtime.PumpNetwork();
            if(sleepMilliseconds>0) Thread.Sleep(sleepMilliseconds);
        }

        private static void SendDueRemoteInputs(
            List<PlayerInputSnapshot> inputs,
            KcpNetworkInputClient remoteClient,
            ref int highestSentFrame,
            ref int outOfOrderCount)
        {
            if(inputs==null) return;

            for(int i=0;i<inputs.Count;i++)
            {
                PlayerInputSnapshot input=inputs[i];

                if(highestSentFrame>0&&input.frameNumber<highestSentFrame)
                    outOfOrderCount++;

                if(input.frameNumber>highestSentFrame)
                    highestSentFrame=input.frameNumber;

                remoteClient.SendInput(in input);
            }
        }

        private static PlayerInputSnapshot CreateInput(int frame,int playerID)
        {
            uint state=Seed^unchecked((uint)frame*0x9E3779B9u)^unchecked((uint)playerID*0x85EBCA6Bu);
            state=NextRandom(state);
            float moveX=(int)(state%3u)-1;
            state=NextRandom(state);
            float moveY=(int)(state%3u)-1;
            return new PlayerInputSnapshot(frame,playerID) { moveX=moveX,moveY=moveY };
        }

        private static int NextRange(ref uint state,int minInclusive,int maxInclusive)
        {
            state=NextRandom(state);
            return minInclusive+(int)(state%(uint)(maxInclusive-minInclusive+1));
        }

        private static uint NextRandom(uint value)
        {
            value^=value<<13;
            value^=value>>17;
            value^=value<<5;
            return value;
        }

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        private sealed class RestoreProbe : IRollbackRestoreListener
        {
            public int RestoreCount { get; private set; }
            public int ResimulateCount { get; private set; }

            public void OnRollbackWorldRestored(World world,int restoredFrame)=>RestoreCount++;
            public void OnRollbackResimulated(World world,int currentFrame)=>ResimulateCount++;
        }

        private sealed class TickCounterSystem : IFixedStepSystem
        {
            public int NormalTickCount { get; private set; }
            public int RollbackTickCount { get; private set; }
            public SystemTickSequence sequence => SystemTickSequence.logic;

            public void OnCreate(World world){}

            public void Tick(in SimulationContext context)
            {
                if(context.isRollback) RollbackTickCount++;
                else NormalTickCount++;
            }

            public void OnDestroy(World world){}
        }

        private readonly struct TestWorldData
        {
            public readonly World World;
            public readonly Entity Player1;
            public readonly Entity Player2;
            public readonly TickCounterSystem TickCounter;

            public TestWorldData(World world,Entity player1,Entity player2,TickCounterSystem tickCounter)
            {
                World=world;
                Player1=player1;
                Player2=player2;
                TickCounter=tickCounter;
            }
        }

        private sealed class PredictedEnvironment : IDisposable
        {
            public readonly World World;
            public readonly SimulateRunner Runner;
            public readonly TickCounterSystem TickCounter;
            public readonly NetworkRollbackSimulationRuntime Runtime;
            public readonly SimulationFrameCommandBuffer CommandBuffer;

            public PredictedEnvironment(
                World world,
                SimulateRunner runner,
                TickCounterSystem tickCounter,
                NetworkRollbackSimulationRuntime runtime,
                SimulationFrameCommandBuffer commandBuffer)
            {
                World=world;
                Runner=runner;
                TickCounter=tickCounter;
                Runtime=runtime;
                CommandBuffer=commandBuffer;
            }

            public void Dispose()
            {
                Runtime.Dispose();
                CommandBuffer.Clear();
                World.Dispose();
            }
        }

        private sealed class ReferenceEnvironment : IDisposable
        {
            public readonly World World;
            public readonly SimulateRunner Runner;
            public readonly TickCounterSystem TickCounter;
            public readonly FrameInputSetApplier InputApplier;

            public ReferenceEnvironment(
                World world,
                SimulateRunner runner,
                TickCounterSystem tickCounter,
                FrameInputSetApplier inputApplier)
            {
                World=world;
                Runner=runner;
                TickCounter=tickCounter;
                InputApplier=inputApplier;
            }

            public void Dispose()=>World.Dispose();
        }
    }
}
