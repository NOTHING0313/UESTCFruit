using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace FrameWork.NetworkSync.Tests
{
    /// <summary>
    /// 双玩家公网 KCP 随机输入延迟、Prediction、Authority Correction 与 Rollback 压力验证。
    /// </summary>
    public static class PublicKcpRandomRollbackStressValidationTestBootstrap
    {
        private const string ServerAddress="8.137.83.229";
        private const int ServerPort=28015;
        private const uint SessionId=0x11223344u;
        private const int Player1ID=1;
        private const int Player2ID=2;
        private const int TotalFrames=2000;
        private const int MaxInputDelayFrames=6;
        private const int MinDelayedInputCount=1500;
        private const int MinMispredictedFrameCount=1200;
        private const int MinOutOfOrderP2SendCount=100;
        private const int MinOutOfOrderAuthorityCount=100;
        private const uint Seed=20260817u;
        private const float TickLength=1f/60f;
        private const double ConnectTimeoutSeconds=5.0;
        private const double FinalAuthorityFlushTimeoutSeconds=15.0;

        /// <summary>
        /// 保持原 2000 帧随机压力条件，仅将公网 Raw UDP Transport 替换为 kcp2k Reliable。
        /// </summary>
        public static IEnumerator Run()
        {
            TestEnvironment reference=null,predicted=null;
            KcpNetworkInputClient client1=null,client2=null;
            var stopwatch=Stopwatch.StartNew();

            var authoritativeHistory=new FrameInputSet[TotalFrames+1];
            var frameMispredicted=new bool[TotalFrames+1];
            var authorityReceived1=new bool[TotalFrames+1];
            var authorityReceived2=new bool[TotalFrames+1];
            var delayedP2Inputs=new List<PlayerInputSnapshot>[TotalFrames+MaxInputDelayFrames+1];

            int zeroDelayInputCount=0,delayedInputCount=0,maxObservedDelay=0;
            int predictedFrameCount=0,correctPredictionCount=0,mispredictedFrameCount=0;
            int mismatchAuthorityCorrectionCount=0,unresolvedMispredictedFrames=0;
            int authorityReceivedCount1=0,authorityReceivedCount2=0,convergenceCheckpointCount=0;
            int p2OutOfOrderSendCount=0,highestSentP2Frame=0;
            uint delayRandomState=Seed^0xD1B54A35u;

            try
            {
                reference=CreateEnvironment(true);
                predicted=CreateEnvironment(true);

                client1=new KcpNetworkInputClient(ServerAddress,ServerPort,SessionId,Player1ID);
                client2=new KcpNetworkInputClient(ServerAddress,ServerPort,SessionId,Player2ID);

                double connectDeadline=Time.realtimeSinceStartupAsDouble+ConnectTimeoutSeconds;
                while(!client1.IsConnected||!client2.IsConnected)
                {
                    client1.Tick();
                    client2.Tick();
                    ThrowIfClientError(client1);
                    ThrowIfClientError(client2);

                    if(Time.realtimeSinceStartupAsDouble>=connectDeadline)
                        throw new TimeoutException(
                            $"PublicKcpRandomRollbackStressValidationTestBootstrap Run Error: Category=ConnectTimeout, " +
                            $"P1=[{GetClientState(client1)}], P2=[{GetClientState(client2)}]");

                    yield return null;
                }

                var assembler=new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());
                assembler.RegisterPlayer(Player1ID);
                assembler.RegisterPlayer(Player2ID);

                var authorityDriver=new NetworkAuthorityRollbackDriver(assembler,predicted.Coordinator);
                var frameComparer=new FrameInputSetComparer();
                var playerComparer=new PlayerInputSnapshotComparer();

                UnityEngine.Debug.Log(
                    $"PublicKcpRandomRollbackStressValidationTestBootstrap Run Log: [PUBLIC KCP RANDOM ROLLBACK STRESS] " +
                    $"Server={ServerAddress}:{ServerPort}, Session=0x{SessionId:X8}, Frames={TotalFrames}, " +
                    $"MaxInputDelay={MaxInputDelayFrames}, Seed={Seed}, P1Local={client1.LocalEndPoint}, P2Local={client2.LocalEndPoint}");

                for(int frame=1;frame<=TotalFrames;frame++)
                {
                    PlayerInputSnapshot player1=CreatePlayerInput(frame,Player1ID);
                    PlayerInputSnapshot player2=CreatePlayerInput(frame,Player2ID);
                    var authoritative=new FrameInputSet(frame,new[] { player1,player2 });
                    authoritativeHistory[frame]=authoritative;

                    int delay=NextRange(ref delayRandomState,0,MaxInputDelayFrames);
                    if(delay==0) zeroDelayInputCount++; else delayedInputCount++;
                    if(delay>maxObservedDelay) maxObservedDelay=delay;

                    int p2SendFrame=frame+delay;
                    delayedP2Inputs[p2SendFrame]??=new List<PlayerInputSnapshot>();
                    delayedP2Inputs[p2SendFrame].Add(player2);

                    var accumulator=new FrameInputAccumulator(frame);
                    accumulator.TryAddInput(in player1);
                    FrameInputAssemblyResult assembly=assembler.Assemble(accumulator);
                    FrameInputSet predictedInput=assembly.InputSet;

                    Expect(assembly.IsPredicted(Player2ID),
                        $"06D-KCP-02 Prediction Coverage Error: Frame={frame}, P2 Must Be Predicted");
                    Expect(!assembly.IsPredicted(Player1ID),
                        $"06D-KCP-02 Prediction Coverage Error: Frame={frame}, P1 Must Be Real");

                    predictedFrameCount++;

                    Expect(predictedInput.TryGetInput(Player2ID,out PlayerInputSnapshot predictedP2),
                        $"06D-KCP-02 Predicted P2 Missing Error: Frame={frame}");

                    if(playerComparer.IsEqual(predictedP2,player2)) correctPredictionCount++;

                    bool frameMismatch=!frameComparer.IsEqual(predictedInput,authoritative);
                    frameMispredicted[frame]=frameMismatch;
                    if(frameMismatch)
                    {
                        mispredictedFrameCount++;
                        unresolvedMispredictedFrames++;
                    }

                    client1.SendInput(in player1);

                    List<PlayerInputSnapshot> dueP2Inputs=delayedP2Inputs[frame];
                    if(dueP2Inputs!=null)
                    {
                        for(int i=0;i<dueP2Inputs.Count;i++)
                        {
                            PlayerInputSnapshot delayedInput=dueP2Inputs[i];

                            if(highestSentP2Frame>0&&delayedInput.frameNumber<highestSentP2Frame)
                                p2OutOfOrderSendCount++;

                            if(delayedInput.frameNumber>highestSentP2Frame)
                                highestSentP2Frame=delayedInput.frameNumber;

                            client2.SendInput(in delayedInput);
                        }
                    }

                    DriveFrame(reference,frame,authoritative,true);
                    DriveFrame(predicted,frame,predictedInput,true);

                    PumpAuthorities(
                        client1,client2,authorityDriver,authoritativeHistory,frameMispredicted,
                        authorityReceived1,authorityReceived2,frameComparer,reference,predicted,frame,
                        ref authorityReceivedCount1,ref authorityReceivedCount2,
                        ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    yield return null;

                    PumpAuthorities(
                        client1,client2,authorityDriver,authoritativeHistory,frameMispredicted,
                        authorityReceived1,authorityReceived2,frameComparer,reference,predicted,frame,
                        ref authorityReceivedCount1,ref authorityReceivedCount2,
                        ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    if(frame%250==0)
                    {
                        UnityEngine.Debug.Log(
                            $"PublicKcpRandomRollbackStressValidationTestBootstrap Run Log: " +
                            $"Frame={frame}/{TotalFrames}, Authorities={authorityReceivedCount1}, " +
                            $"Mispredicted={mispredictedFrameCount}, Corrections={mismatchAuthorityCorrectionCount}, " +
                            $"Outstanding={unresolvedMispredictedFrames}, OOOAuthority={authorityDriver.OutOfOrderAuthorityCount}");
                    }
                }

                // 与原 06C-2B 一致：World 停在 2000，仅继续发送最后最多 6 帧延迟的 P2 Input。
                for(int networkFrame=TotalFrames+1;networkFrame<=TotalFrames+MaxInputDelayFrames;networkFrame++)
                {
                    List<PlayerInputSnapshot> dueP2Inputs=delayedP2Inputs[networkFrame];

                    if(dueP2Inputs!=null)
                    {
                        for(int i=0;i<dueP2Inputs.Count;i++)
                        {
                            PlayerInputSnapshot delayedInput=dueP2Inputs[i];

                            if(highestSentP2Frame>0&&delayedInput.frameNumber<highestSentP2Frame)
                                p2OutOfOrderSendCount++;

                            if(delayedInput.frameNumber>highestSentP2Frame)
                                highestSentP2Frame=delayedInput.frameNumber;

                            client2.SendInput(in delayedInput);
                        }
                    }

                    PumpAuthorities(
                        client1,client2,authorityDriver,authoritativeHistory,frameMispredicted,
                        authorityReceived1,authorityReceived2,frameComparer,reference,predicted,TotalFrames,
                        ref authorityReceivedCount1,ref authorityReceivedCount2,
                        ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    yield return null;

                    PumpAuthorities(
                        client1,client2,authorityDriver,authoritativeHistory,frameMispredicted,
                        authorityReceived1,authorityReceived2,frameComparer,reference,predicted,TotalFrames,
                        ref authorityReceivedCount1,ref authorityReceivedCount2,
                        ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);
                }

                // 保持原失败测试 15 秒 Final Flush Gate，不通过延长超时隐藏问题。
                double flushDeadline=Time.realtimeSinceStartupAsDouble+FinalAuthorityFlushTimeoutSeconds;
                while(authorityReceivedCount1<TotalFrames||authorityReceivedCount2<TotalFrames)
                {
                    PumpAuthorities(
                        client1,client2,authorityDriver,authoritativeHistory,frameMispredicted,
                        authorityReceived1,authorityReceived2,frameComparer,reference,predicted,TotalFrames,
                        ref authorityReceivedCount1,ref authorityReceivedCount2,
                        ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                        ref convergenceCheckpointCount);

                    if(authorityReceivedCount1>=TotalFrames&&authorityReceivedCount2>=TotalFrames) break;

                    if(Time.realtimeSinceStartupAsDouble>=flushDeadline)
                    {
                        throw new TimeoutException(
                            $"06D-KCP-02 Final Authority Flush Timeout, " +
                            $"Client1Authorities={authorityReceivedCount1}/{TotalFrames}, " +
                            $"Client2Authorities={authorityReceivedCount2}/{TotalFrames}, " +
                            $"OutstandingMismatch={unresolvedMispredictedFrames}, DriverApplied={authorityDriver.AppliedAuthorityCount}, " +
                            $"P1=[{GetClientState(client1)}], P2=[{GetClientState(client2)}]");
                    }

                    yield return null;
                }

                ThrowIfClientError(client1);
                ThrowIfClientError(client2);

                Expect(client1.LastSentSequence==(uint)TotalFrames,
                    $"06D-KCP-02 Client1 Sequence Error: Expected={TotalFrames}, Actual={client1.LastSentSequence}");
                Expect(client2.LastSentSequence==(uint)TotalFrames,
                    $"06D-KCP-02 Client2 Sequence Error: Expected={TotalFrames}, Actual={client2.LastSentSequence}");
                Expect(authorityReceivedCount1==TotalFrames,
                    $"06D-KCP-02 Client1 Authority Count Error: Expected={TotalFrames}, Actual={authorityReceivedCount1}");
                Expect(authorityReceivedCount2==TotalFrames,
                    $"06D-KCP-02 Client2 Authority Count Error: Expected={TotalFrames}, Actual={authorityReceivedCount2}");
                Expect(authorityDriver.AppliedAuthorityCount==TotalFrames,
                    $"06D-KCP-02 Authority Driver Count Error: Expected={TotalFrames}, Actual={authorityDriver.AppliedAuthorityCount}");
                Expect(unresolvedMispredictedFrames==0,
                    $"06D-KCP-02 Unresolved Prediction Error: Actual={unresolvedMispredictedFrames}");
                Expect(delayedInputCount>=MinDelayedInputCount,
                    $"06D-KCP-02 Delay Coverage Error: Expected>={MinDelayedInputCount}, Actual={delayedInputCount}");
                Expect(maxObservedDelay==MaxInputDelayFrames,
                    $"06D-KCP-02 Max Delay Coverage Error: Expected={MaxInputDelayFrames}, Actual={maxObservedDelay}");
                Expect(mispredictedFrameCount>=MinMispredictedFrameCount,
                    $"06D-KCP-02 Misprediction Coverage Error: Expected>={MinMispredictedFrameCount}, Actual={mispredictedFrameCount}");
                Expect(mismatchAuthorityCorrectionCount==mispredictedFrameCount,
                    $"06D-KCP-02 Correction Coverage Error: Mispredicted={mispredictedFrameCount}, Corrections={mismatchAuthorityCorrectionCount}");
                Expect(p2OutOfOrderSendCount>=MinOutOfOrderP2SendCount,
                    $"06D-KCP-02 P2 OutOfOrder Coverage Error: Expected>={MinOutOfOrderP2SendCount}, Actual={p2OutOfOrderSendCount}");
                Expect(authorityDriver.OutOfOrderAuthorityCount>=MinOutOfOrderAuthorityCount,
                    $"06D-KCP-02 Authority OutOfOrder Coverage Error: Expected>={MinOutOfOrderAuthorityCount}, Actual={authorityDriver.OutOfOrderAuthorityCount}");
                Expect(correctPredictionCount>0,"06D-KCP-02 Correct Prediction Coverage Error: No Correct Predictions");
                Expect(client1.LastRejectReason==NetworkInputExchangeRejectReason.None,
                    $"06D-KCP-02 Client1 Reject Error: Reason={client1.LastRejectReason}, Decode={client1.LastDecodeError}");
                Expect(client2.LastRejectReason==NetworkInputExchangeRejectReason.None,
                    $"06D-KCP-02 Client2 Reject Error: Reason={client2.LastRejectReason}, Decode={client2.LastDecodeError}");
                Expect(!client1.LastKcpError.HasValue,
                    $"06D-KCP-02 Client1 KCP Error: Error={client1.LastKcpError}, Message={client1.LastKcpErrorMessage}");
                Expect(!client2.LastKcpError.HasValue,
                    $"06D-KCP-02 Client2 KCP Error: Error={client2.LastKcpError}, Message={client2.LastKcpErrorMessage}");

                AssertWorldEqual(reference,predicted,TotalFrames,"06D-KCP-02 Final");

                uint referenceChecksum=WorldChecksumCalculator.Calculate(reference.World);
                uint predictedChecksum=WorldChecksumCalculator.Calculate(predicted.World);
                stopwatch.Stop();

                var report=new PublicKcpRandomRollbackStressReport(
                    Seed,TotalFrames,MaxInputDelayFrames,zeroDelayInputCount,delayedInputCount,maxObservedDelay,
                    predictedFrameCount,correctPredictionCount,mispredictedFrameCount,mismatchAuthorityCorrectionCount,
                    p2OutOfOrderSendCount,authorityDriver.OutOfOrderAuthorityCount,authorityReceivedCount1,
                    authorityReceivedCount2,authorityDriver.AppliedAuthorityCount,unresolvedMispredictedFrames,
                    convergenceCheckpointCount,referenceChecksum,predictedChecksum,stopwatch.Elapsed.TotalMilliseconds);

                UnityEngine.Debug.Log($"PublicKcpRandomRollbackStressValidationTestBootstrap Run Log: {report.ToDisplayString()}");
            }
            finally
            {
                client1?.Dispose();
                client2?.Dispose();
                reference?.Dispose();
                predicted?.Dispose();
            }
        }

        private static void PumpAuthorities(
            KcpNetworkInputClient client1,KcpNetworkInputClient client2,
            NetworkAuthorityRollbackDriver authorityDriver,FrameInputSet[] authoritativeHistory,
            bool[] frameMispredicted,bool[] authorityReceived1,bool[] authorityReceived2,
            FrameInputSetComparer comparer,TestEnvironment reference,TestEnvironment predicted,int currentFrame,
            ref int authorityReceivedCount1,ref int authorityReceivedCount2,
            ref int mismatchAuthorityCorrectionCount,ref int unresolvedMispredictedFrames,
            ref int convergenceCheckpointCount)
        {
            while(client1.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
            {
                ApplyAuthority(
                    in authority,authorityDriver,authoritativeHistory,frameMispredicted,authorityReceived1,
                    comparer,reference,predicted,currentFrame,ref authorityReceivedCount1,
                    ref mismatchAuthorityCorrectionCount,ref unresolvedMispredictedFrames,
                    ref convergenceCheckpointCount);
            }

            while(client2.TryReceiveAuthority(out ServerAuthorityFramePacket authority))
            {
                int frame=authority.InputSet.frameNumber;

                Expect(frame>0&&frame<=TotalFrames,
                    $"06D-KCP-02 Client2 Authority Frame Error: Frame={frame}, Sequence={authority.Sequence}");
                Expect(!authorityReceived2[frame],
                    $"06D-KCP-02 Client2 Duplicate Authority Error: Frame={frame}, Sequence={authority.Sequence}");
                Expect(comparer.IsEqual(authority.InputSet,authoritativeHistory[frame]),
                    $"06D-KCP-02 Client2 Authority Data Error: Frame={frame}, Sequence={authority.Sequence}");

                authorityReceived2[frame]=true;
                authorityReceivedCount2++;
            }

            ThrowIfClientError(client1);
            ThrowIfClientError(client2);
        }

        private static void ApplyAuthority(
            in ServerAuthorityFramePacket authority,NetworkAuthorityRollbackDriver authorityDriver,
            FrameInputSet[] authoritativeHistory,bool[] frameMispredicted,bool[] authorityReceived,
            FrameInputSetComparer comparer,TestEnvironment reference,TestEnvironment predicted,int currentFrame,
            ref int authorityReceivedCount,ref int mismatchAuthorityCorrectionCount,
            ref int unresolvedMispredictedFrames,ref int convergenceCheckpointCount)
        {
            int authorityFrame=authority.InputSet.frameNumber;

            Expect(authorityFrame>0&&authorityFrame<=TotalFrames,
                $"06D-KCP-02 Authority Frame Error: Frame={authorityFrame}, Sequence={authority.Sequence}");
            Expect(!authorityReceived[authorityFrame],
                $"06D-KCP-02 Duplicate Authority Error: Frame={authorityFrame}, Sequence={authority.Sequence}");
            Expect(comparer.IsEqual(authority.InputSet,authoritativeHistory[authorityFrame]),
                $"06D-KCP-02 Authority Data Error: Frame={authorityFrame}, Sequence={authority.Sequence}");

            bool hadOutstandingMismatch=unresolvedMispredictedFrames>0;
            authorityDriver.Apply(in authority);
            authorityReceived[authorityFrame]=true;
            authorityReceivedCount++;

            if(frameMispredicted[authorityFrame])
            {
                mismatchAuthorityCorrectionCount++;
                unresolvedMispredictedFrames--;

                Expect(unresolvedMispredictedFrames>=0,
                    $"06D-KCP-02 Unresolved Prediction Underflow Error: Frame={authorityFrame}");
            }

            if(hadOutstandingMismatch&&unresolvedMispredictedFrames==0)
            {
                AssertWorldEqual(reference,predicted,currentFrame,$"06D-KCP-02 Convergence AuthorityFrame={authorityFrame}");
                convergenceCheckpointCount++;
            }
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world=new World { EnableSystemProfile=false };
            var players=new Entity[2];

            Entity player1=world.CreateEntity();
            players[0]=player1;
            world.SetComponent(player1,new PlayerTagComponent());
            world.SetComponent(player1,new PlayerInputSnapshotComponent(0,Player1ID,0f,0f));
            world.SetComponent(player1,new MoveSpeedComponent(3.25f));
            world.SetComponent(player1,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player1,new PositionComponent(-5f,0f,0f));

            Entity player2=world.CreateEntity();
            players[1]=player2;
            world.SetComponent(player2,new PlayerTagComponent());
            world.SetComponent(player2,new PlayerInputSnapshotComponent(0,Player2ID,0f,0f));
            world.SetComponent(player2,new MoveSpeedComponent(2.75f));
            world.SetComponent(player2,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player2,new PositionComponent(5f,0f,0f));

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var inputApplier=new FrameInputSetApplier();
            inputApplier.RegisterPlayer(Player1ID,player1);
            inputApplier.RegisterPlayer(Player2ID,player2);

            var commandBuffer=new SimulationFrameCommandBuffer(512);
            var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,512);
            var rollbackAdapter=new WorldRollbackAdapter<FrameInputSet>(world,world,inputApplier,null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));

            var snapshotBuffer=new SnapshotRingBuffer<EcsWorldSnapshot>(512);
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

            var environment=new TestEnvironment(world,players,coordinator,commandBuffer,commandApplier,snapshotBuffer);
            if(saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void DriveFrame(TestEnvironment env,int frame,FrameInputSet input,bool saveSnapshot)
        {
            RollbackStepResult result=env.Coordinator.TryStep(frame,input);
            Expect(result.Succeeded,
                $"06D-KCP-02 DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context=new SimulationContext(frame,TickLength,false);
            env.CommandApplier.ApplyCommandsToWorld(frame,SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame,SimulationFrameCommandTiming.AfterTick);
            if(saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame,int playerID)
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

        private static void AssertWorldEqual(TestEnvironment reference,TestEnvironment predicted,int frame,string stage)
        {
            Expect(reference.Coordinator.CurrentFrame==predicted.Coordinator.CurrentFrame,
                $"{stage} CoordinatorFrame Error: Frame={frame}, Reference={reference.Coordinator.CurrentFrame}, Predicted={predicted.Coordinator.CurrentFrame}");
            Expect(reference.World.AliveEntityCount==predicted.World.AliveEntityCount,
                $"{stage} AliveEntityCount Error: Frame={frame}, Reference={reference.World.AliveEntityCount}, Predicted={predicted.World.AliveEntityCount}");
            Expect(reference.World.CreatedEntityCount==predicted.World.CreatedEntityCount,
                $"{stage} CreatedEntityCount Error: Frame={frame}, Reference={reference.World.CreatedEntityCount}, Predicted={predicted.World.CreatedEntityCount}");

            for(int i=0;i<reference.Players.Length;i++)
                AssertPlayerEqual(reference,predicted,reference.Players[i],predicted.Players[i],frame,$"{stage} P{i+1}");

            uint checksumA=WorldChecksumCalculator.Calculate(reference.World);
            uint checksumB=WorldChecksumCalculator.Calculate(predicted.World);
            Expect(checksumA==checksumB,
                $"{stage} Checksum Error: Frame={frame}, Reference=0x{checksumA:X8}, Predicted=0x{checksumB:X8}");
        }

        private static void AssertPlayerEqual(
            TestEnvironment reference,TestEnvironment predicted,Entity referencePlayer,Entity predictedPlayer,int frame,string stage)
        {
            Expect(reference.World.TryGetComponent(referencePlayer,out PositionComponent positionA),
                $"{stage} Reference Position Missing Error: Frame={frame}");
            Expect(predicted.World.TryGetComponent(predictedPlayer,out PositionComponent positionB),
                $"{stage} Predicted Position Missing Error: Frame={frame}");
            ExpectFloatBits(positionA.x,positionB.x,frame,stage,"Position.X");
            ExpectFloatBits(positionA.y,positionB.y,frame,stage,"Position.Y");
            ExpectFloatBits(positionA.z,positionB.z,frame,stage,"Position.Z");

            Expect(reference.World.TryGetComponent(referencePlayer,out VelocityComponent velocityA),
                $"{stage} Reference Velocity Missing Error: Frame={frame}");
            Expect(predicted.World.TryGetComponent(predictedPlayer,out VelocityComponent velocityB),
                $"{stage} Predicted Velocity Missing Error: Frame={frame}");
            ExpectFloatBits(velocityA.x,velocityB.x,frame,stage,"Velocity.X");
            ExpectFloatBits(velocityA.y,velocityB.y,frame,stage,"Velocity.Y");
            ExpectFloatBits(velocityA.z,velocityB.z,frame,stage,"Velocity.Z");

            Expect(reference.World.TryGetComponent(referencePlayer,out PlayerInputSnapshotComponent inputA),
                $"{stage} Reference Input Missing Error: Frame={frame}");
            Expect(predicted.World.TryGetComponent(predictedPlayer,out PlayerInputSnapshotComponent inputB),
                $"{stage} Predicted Input Missing Error: Frame={frame}");
            Expect(inputA.inputFrame==inputB.inputFrame,
                $"{stage} InputFrame Error: Frame={frame}, Reference={inputA.inputFrame}, Predicted={inputB.inputFrame}");
            Expect(inputA.playerID==inputB.playerID,
                $"{stage} PlayerID Error: Frame={frame}, Reference={inputA.playerID}, Predicted={inputB.playerID}");
            ExpectFloatBits(inputA.moveX,inputB.moveX,frame,stage,"Input.MoveX");
            ExpectFloatBits(inputA.moveY,inputB.moveY,frame,stage,"Input.MoveY");
        }

        private static void ExpectFloatBits(float a,float b,int frame,string stage,string field)
        {
            int bitsA=BitConverter.SingleToInt32Bits(a);
            int bitsB=BitConverter.SingleToInt32Bits(b);
            Expect(bitsA==bitsB,
                $"{stage} {field} Error: Frame={frame}, Reference={a}({bitsA:X8}), Predicted={b}({bitsB:X8})");
        }

        private static void ThrowIfClientError(KcpNetworkInputClient client)
        {
            if(client.LastKcpError.HasValue)
                throw new InvalidOperationException(
                    $"06D-KCP-02 KCP Client Error: PlayerID={client.PlayerID}, Error={client.LastKcpError}, Message={client.LastKcpErrorMessage}");

            if(client.LastRejectReason!=NetworkInputExchangeRejectReason.None)
                throw new InvalidOperationException(
                    $"06D-KCP-02 Client Reject Error: PlayerID={client.PlayerID}, Reason={client.LastRejectReason}, Decode={client.LastDecodeError}");
        }

        private static string GetClientState(KcpNetworkInputClient client)
            =>$"Connected={client.IsConnected}, Local={client.LocalEndPoint}, LastSentSequence={client.LastSentSequence}, " +
              $"LastRejectReason={client.LastRejectReason}, LastDecodeError={client.LastDecodeError}, " +
              $"LastKcpError={client.LastKcpError}, LastKcpErrorMessage={client.LastKcpErrorMessage}";

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        /// <summary>06D-KCP-02 公网 KCP 随机回滚压力统计。</summary>
        public sealed class PublicKcpRandomRollbackStressReport
        {
            public readonly uint Seed;
            public readonly int TotalFrames;
            public readonly int MaxInputDelayFrames;
            public readonly int ZeroDelayInputCount;
            public readonly int DelayedInputCount;
            public readonly int MaxObservedDelay;
            public readonly int PredictedFrameCount;
            public readonly int CorrectPredictionCount;
            public readonly int MispredictedFrameCount;
            public readonly int MismatchAuthorityCorrectionCount;
            public readonly int P2OutOfOrderSendCount;
            public readonly int OutOfOrderAuthorityCount;
            public readonly int Client1AuthorityReceivedCount;
            public readonly int Client2AuthorityReceivedCount;
            public readonly int DriverAppliedAuthorityCount;
            public readonly int OutstandingMismatchCount;
            public readonly int ConvergenceCheckpointCount;
            public readonly uint ReferenceChecksum;
            public readonly uint PredictedChecksum;
            public readonly double ElapsedMilliseconds;

            public PublicKcpRandomRollbackStressReport(
                uint seed,int totalFrames,int maxInputDelayFrames,int zeroDelayInputCount,int delayedInputCount,
                int maxObservedDelay,int predictedFrameCount,int correctPredictionCount,int mispredictedFrameCount,
                int mismatchAuthorityCorrectionCount,int p2OutOfOrderSendCount,int outOfOrderAuthorityCount,
                int client1AuthorityReceivedCount,int client2AuthorityReceivedCount,int driverAppliedAuthorityCount,
                int outstandingMismatchCount,int convergenceCheckpointCount,uint referenceChecksum,
                uint predictedChecksum,double elapsedMilliseconds)
            {
                Seed=seed;
                TotalFrames=totalFrames;
                MaxInputDelayFrames=maxInputDelayFrames;
                ZeroDelayInputCount=zeroDelayInputCount;
                DelayedInputCount=delayedInputCount;
                MaxObservedDelay=maxObservedDelay;
                PredictedFrameCount=predictedFrameCount;
                CorrectPredictionCount=correctPredictionCount;
                MispredictedFrameCount=mispredictedFrameCount;
                MismatchAuthorityCorrectionCount=mismatchAuthorityCorrectionCount;
                P2OutOfOrderSendCount=p2OutOfOrderSendCount;
                OutOfOrderAuthorityCount=outOfOrderAuthorityCount;
                Client1AuthorityReceivedCount=client1AuthorityReceivedCount;
                Client2AuthorityReceivedCount=client2AuthorityReceivedCount;
                DriverAppliedAuthorityCount=driverAppliedAuthorityCount;
                OutstandingMismatchCount=outstandingMismatchCount;
                ConvergenceCheckpointCount=convergenceCheckpointCount;
                ReferenceChecksum=referenceChecksum;
                PredictedChecksum=predictedChecksum;
                ElapsedMilliseconds=elapsedMilliseconds;
            }

            public string ToDisplayString()
            {
                return
                    $"[PUBLIC KCP RANDOM ROLLBACK STRESS]\n" +
                    $"Seed                         = {Seed}\n" +
                    $"Frames                       = {TotalFrames}\n" +
                    $"Max Input Delay              = {MaxInputDelayFrames}\n" +
                    $"Zero-delay P2 Inputs         = {ZeroDelayInputCount}\n" +
                    $"Delayed P2 Inputs            = {DelayedInputCount}\n" +
                    $"Max Observed Delay           = {MaxObservedDelay}\n" +
                    $"Predicted Frames             = {PredictedFrameCount}\n" +
                    $"Correct Predictions          = {CorrectPredictionCount}\n" +
                    $"Mispredicted Frames          = {MispredictedFrameCount}\n" +
                    $"Mismatch Corrections         = {MismatchAuthorityCorrectionCount}\n" +
                    $"Out-of-order P2 Sends        = {P2OutOfOrderSendCount}\n" +
                    $"Out-of-order Authorities     = {OutOfOrderAuthorityCount}\n" +
                    $"Client1 Authorities          = {Client1AuthorityReceivedCount}/{TotalFrames}\n" +
                    $"Client2 Authorities          = {Client2AuthorityReceivedCount}/{TotalFrames}\n" +
                    $"Driver Applied               = {DriverAppliedAuthorityCount}/{TotalFrames}\n" +
                    $"Outstanding Mismatch         = {OutstandingMismatchCount}\n" +
                    $"Convergence Checkpoints      = {ConvergenceCheckpointCount}\n" +
                    $"Reference Checksum           = 0x{ReferenceChecksum:X8}\n" +
                    $"Predicted Checksum           = 0x{PredictedChecksum:X8}\n" +
                    $"Elapsed                      = {ElapsedMilliseconds:F2} ms\n" +
                    $"Result                       = PASS";
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity[] Players;
            public readonly RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(
                World world,Entity[] players,RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,SimulationFrameCommandApplier commandApplier,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World=world;
                Players=players;
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
