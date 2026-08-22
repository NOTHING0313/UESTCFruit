using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 多玩家网络回滚 Simulation Runtime。
    /// 正常帧只在 Runner.BeforeTick 准备 FrameInputSet，World.Tick 仍由 SimulateRunner 唯一驱动。
    /// </summary>
    public sealed class NetworkRollbackSimulationRuntime : IDisposable
    {
        private readonly World _world;
        private readonly SimulateRunner _runner;
        private readonly Func<int,PlayerInputSnapshot> _localInputCollector;
        private readonly FrameInputSetApplier _inputApplier;
        private readonly FrameInputAssembler _assembler;
        private readonly WorldRollbackAdapter<FrameInputSet> _rollbackAdapter;
        private readonly SnapshotRingBuffer<EcsWorldSnapshot> _snapshotBuffer;
        private readonly RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> _coordinator;
        private readonly NetworkRollbackClientRuntime _networkRuntime;
        private readonly RollbackMetricsListener _rollbackMetrics=new();
        private readonly int _snapshotIntervalFrames;
        private bool _mounted;
        private bool _disposed;

        public World World => _world;
        public SimulateRunner Runner => _runner;
        public FrameInputSetApplier InputApplier => _inputApplier;
        public FrameInputAssembler Assembler => _assembler;
        public RollbackCoordinator<FrameInputSet,EcsWorldSnapshot> Coordinator => _coordinator;
        public NetworkRollbackClientRuntime NetworkRuntime => _networkRuntime;
        public bool IsMounted => _mounted;
        public bool IsReady => _networkRuntime.IsReady;
        public NetworkInputClientConnectionState ConnectionState => _networkRuntime.ConnectionState;
        public bool CanReconnect => _networkRuntime.CanReconnect;
        public int LocalPlayerID => _networkRuntime.PlayerID;
        public int PlayerCount => _inputApplier.PlayerCount;
        public int NormalFrameCount { get; private set; }
        public int PredictedFrameCount { get; private set; }
        public int PredictedInputCount { get; private set; }
        public int LastPredictedCount { get; private set; }
        public int RollbackRestoreCount => _rollbackMetrics.RestoreCount;
        public int RollbackResimulateCount => _rollbackMetrics.ResimulateCount;

        /// <summary>底层传输连接状态发生变化。</summary>
        public event Action<NetworkInputClientConnectionState> ConnectionStateChanged
        {
            add=>_networkRuntime.ConnectionStateChanged+=value;
            remove=>_networkRuntime.ConnectionStateChanged-=value;
        }

        /// <summary>
        /// 创建多人网络回滚 Runtime。构造阶段不订阅 Runner；网络握手完成后显式 Mount。
        /// </summary>
        public NetworkRollbackSimulationRuntime(
            World world,
            SimulateRunner runner,
            SimulationFrameCommandBuffer commandBuffer,
            SimulationFrameCommandApplier commandApplier,
            IReadOnlyList<NetworkPlayerBinding> players,
            NetworkInputClientOptions clientOptions,
            Func<int,PlayerInputSnapshot> localInputCollector,
            int snapshotRingCapacity=120,
            int snapshotIntervalFrames=10)
        {
            _world=world??throw new ArgumentNullException(nameof(world));
            _runner=runner??throw new ArgumentNullException(nameof(runner));
            if(!ReferenceEquals(_runner.World,_world)) throw new InvalidOperationException("Network Rollback Runner World Mismatch");
            if(commandBuffer==null) throw new ArgumentNullException(nameof(commandBuffer));
            if(commandApplier==null) throw new ArgumentNullException(nameof(commandApplier));
            if(!ReferenceEquals(commandBuffer,commandApplier.CommandBuffer)) throw new InvalidOperationException("Network Rollback Frame Command Buffer Mismatch");
            if(players==null||players.Count==0) throw new ArgumentException("Network Rollback Players Are Empty",nameof(players));
            if(clientOptions==null) throw new ArgumentNullException(nameof(clientOptions));
            _localInputCollector=localInputCollector??throw new ArgumentNullException(nameof(localInputCollector));
            if(snapshotRingCapacity<=0) throw new ArgumentOutOfRangeException(nameof(snapshotRingCapacity));
            if(snapshotIntervalFrames<=0) throw new ArgumentOutOfRangeException(nameof(snapshotIntervalFrames));

            _snapshotIntervalFrames=snapshotIntervalFrames;
            _inputApplier=new FrameInputSetApplier();
            _assembler=new FrameInputAssembler(new LastKnownPlayerInputPredictionPolicy());

            RegisterPlayers(players,clientOptions.PlayerID);

            _rollbackAdapter=new WorldRollbackAdapter<FrameInputSet>(_world,_world,_inputApplier,null);
            _rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));
            _rollbackAdapter.AddRollbackRestoreListener(_rollbackMetrics);

            _snapshotBuffer=new SnapshotRingBuffer<EcsWorldSnapshot>(snapshotRingCapacity);
            _coordinator=new RollbackCoordinator<FrameInputSet,EcsWorldSnapshot>(
                new InputBuffer<FrameInputSet>(),
                new AuthoritativeInputBuffer<FrameInputSet>(),
                _snapshotBuffer,
                _rollbackAdapter,
                new FrameInputSetComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength=_runner.TickLength
            };

            _networkRuntime=NetworkRollbackClientRuntimeFactory.Create(clientOptions,_assembler,_coordinator);
        }

        /// <summary>
        /// 在 Runner 尚未推进时挂载正常帧事件。网络客户端必须已经 Ready。
        /// </summary>
        public void Mount()
        {
            ThrowIfDisposed();
            if(_mounted) return;
            if(!IsReady) throw new InvalidOperationException("Network Rollback Runtime Cannot Mount Before Network Is Ready");
            if(_runner.IsTicking||_runner.FrameCount>0)
                throw new InvalidOperationException($"Network Rollback Runtime Requires Idle Frame Zero Runner: Frame={_runner.FrameCount}, IsTicking={_runner.IsTicking}");

            _coordinator.SaveSnapshot();
            _runner.BeforeTick+=OnBeforeTick;
            _runner.AfterTick+=OnAfterTick;
            _mounted=true;
        }

        /// <summary>解除 Runner 正常帧 Hook；不会销毁 World/Runner。</summary>
        public void Unmount()
        {
            if(!_mounted) return;
            _runner.BeforeTick-=OnBeforeTick;
            _runner.AfterTick-=OnAfterTick;
            _mounted=false;
        }

        /// <summary>
        /// 推进网络收发。可在挂载前用于 KCP 握手，也可在 Unity Update/测试 Final Flush 中调用。
        /// </summary>
        public int PumpNetwork()
        {
            ThrowIfDisposed();
            return _networkRuntime.Tick();
        }

        /// <summary>在保留当前 World / Runner / Rollback 历史的前提下重建底层 Transport。</summary>
        public void Reconnect()
        {
            ThrowIfDisposed();
            _networkRuntime.Reconnect();
        }

        /// <summary>注册回滚 Restore/Resimulate 后置监听器，例如 Buff Runtime 重建。</summary>
        public void AddRollbackRestoreListener(IRollbackRestoreListener listener)
        {
            ThrowIfDisposed();
            _rollbackAdapter.AddRollbackRestoreListener(listener);
        }

        /// <summary>移除回滚 Restore/Resimulate 后置监听器。</summary>
        public bool RemoveRollbackRestoreListener(IRollbackRestoreListener listener)
        {
            if(_disposed) return false;
            return _rollbackAdapter.RemoveRollbackRestoreListener(listener);
        }

        /// <summary>确认服务端已稳定确认的帧并释放历史缓存。</summary>
        public void ConfirmFrame(int frame)
        {
            ThrowIfDisposed();
            _coordinator.ConfirmFrame(frame);
        }

        public void Dispose()
        {
            if(_disposed) return;
            Unmount();
            _disposed=true;
            _rollbackAdapter.RemoveRollbackRestoreListener(_rollbackMetrics);
            _networkRuntime.Dispose();
            _snapshotBuffer.Clear();
        }

        private void OnBeforeTick(SimulationContext context)
        {
            if(!_mounted) return;
            if(context.isRollback)
                throw new InvalidOperationException($"Network Rollback Runtime Received Runner Rollback Tick: Frame={context.frameNumber}");
            if(!IsReady)
                throw new InvalidOperationException($"Network Rollback Runtime Lost Ready State Before Frame {context.frameNumber}");

            // 先消费已到达 Authority，确保当前正常帧基于最新历史状态开始。
            _networkRuntime.Tick();

            PlayerInputSnapshot localInput=_localInputCollector(context.frameNumber);
            ValidateLocalInput(in localInput,context.frameNumber);

            _networkRuntime.SendInput(in localInput);

            var accumulator=new FrameInputAccumulator(context.frameNumber);
            if(!accumulator.TryAddInput(in localInput))
                throw new InvalidOperationException($"Network Rollback Local Input Duplicate: Frame={context.frameNumber}, PlayerID={localInput.playerID}");

            FrameInputAssemblyResult assembly=_assembler.Assemble(accumulator);
            LastPredictedCount=assembly.PredictedCount;
            if(assembly.HasPrediction) PredictedFrameCount++;
            PredictedInputCount+=assembly.PredictedCount;

            RollbackStepResult result=_coordinator.TryStep(context.frameNumber,assembly.InputSet);
            if(!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Network Rollback TryStep Failed: Frame={context.frameNumber}, Kind={result.FailureKind}, Message={result.Message}");
            }

            NormalFrameCount++;
        }

        private void OnAfterTick(SimulationContext context)
        {
            if(!_mounted) return;
            if(context.isRollback) return;
            if(_coordinator.CurrentFrame!=context.frameNumber)
                throw new InvalidOperationException(
                    $"Network Rollback AfterTick Frame Mismatch: Runner={context.frameNumber}, Coordinator={_coordinator.CurrentFrame}");

            if(_coordinator.CurrentFrame%_snapshotIntervalFrames==0)
                _coordinator.SaveSnapshot();
        }

        private void RegisterPlayers(IReadOnlyList<NetworkPlayerBinding> players,int localPlayerID)
        {
            var ids=new HashSet<int>();
            bool localFound=false;

            for(int i=0;i<players.Count;i++)
            {
                NetworkPlayerBinding binding=players[i];

                if(binding.PlayerID<=0)
                    throw new InvalidOperationException($"Network Rollback Invalid PlayerID: Index={i}, PlayerID={binding.PlayerID}");
                if(!binding.Entity.IsValid||!_world.IsAlive(binding.Entity))
                    throw new InvalidOperationException($"Network Rollback Player Entity Is Not Alive: PlayerID={binding.PlayerID}, Entity={binding.Entity}");
                if(!ids.Add(binding.PlayerID))
                    throw new InvalidOperationException($"Network Rollback Duplicate PlayerID: {binding.PlayerID}");

                _inputApplier.RegisterPlayer(binding.PlayerID,binding.Entity);
                _assembler.RegisterPlayer(binding.PlayerID);

                if(binding.PlayerID==localPlayerID) localFound=true;
            }

            if(!localFound)
                throw new InvalidOperationException($"Network Rollback Local Player Is Not Registered: PlayerID={localPlayerID}");
        }

        private void ValidateLocalInput(in PlayerInputSnapshot input,int frameNumber)
        {
            if(input.frameNumber!=frameNumber)
                throw new InvalidOperationException($"Network Rollback Local Input Frame Mismatch: Expected={frameNumber}, Actual={input.frameNumber}");
            if(input.playerID!=LocalPlayerID)
                throw new InvalidOperationException($"Network Rollback Local Input Player Mismatch: Expected={LocalPlayerID}, Actual={input.playerID}");
        }

        private void ThrowIfDisposed()
        {
            if(_disposed) throw new ObjectDisposedException(nameof(NetworkRollbackSimulationRuntime));
        }

        private sealed class RollbackMetricsListener : IRollbackRestoreListener
        {
            public int RestoreCount { get; private set; }
            public int ResimulateCount { get; private set; }

            public void OnRollbackWorldRestored(World world,int restoredFrame)=>RestoreCount++;
            public void OnRollbackResimulated(World world,int currentFrame)=>ResimulateCount++;
        }
    }
}
