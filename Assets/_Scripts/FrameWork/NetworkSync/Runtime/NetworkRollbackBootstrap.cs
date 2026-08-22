using BuffSystem;
using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using View;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// Unity Scene 网络回滚入口。支持 Inspector 配置与双客户端命令行覆盖。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public sealed class NetworkRollbackBootstrap : MonoBehaviour
    {
        public enum BootstrapState
        {
            Idle,
            Connecting,
            Mounted,
            Faulted
        }

        [Header("启用")]
        [Tooltip("关闭后不接管当前 Scene 的网络输入与回滚。")]
        [SerializeField] private bool _enable=true;

        [Header("网络")]
        [Tooltip("网络传输模式。正式公网验证推荐 KCP。")]
        [SerializeField] private NetworkInputTransportMode _transportMode=NetworkInputTransportMode.Kcp;
        [Tooltip("Authority Server 地址。")]
        [SerializeField] private string _serverAddress="127.0.0.1";
        [Tooltip("Authority Server UDP/KCP 端口。")]
        [SerializeField] private int _serverPort=28015;
        [Tooltip("网络会话 ID。")]
        [SerializeField] private uint _sessionId=0x11223344u;
        [Tooltip("会话玩家总数。")]
        [SerializeField] private int _playerCount=1;
        [Tooltip("KCP 握手最长等待秒数。")]
        [SerializeField] private float _connectTimeoutSeconds=5f;

        [Header("Session 生命周期")]
        [Tooltip("Authority 持续无增长达到该秒数后冻结正常逻辑帧；网络 Pump 继续运行。")]
        [Min(0.1f)]
        [SerializeField] private float _authorityStallTimeoutSeconds=1.5f;

        [Header("回滚")]
        [Tooltip("ECS Snapshot 环形缓存容量。")]
        [SerializeField] private int _snapshotRingCapacity=256;
        [Tooltip("正常模拟每隔多少逻辑帧保存一次 Snapshot。")]
        [SerializeField] private int _snapshotIntervalFrames=10;

        [Header("双客户端 Scene")]
        [Tooltip("相邻 PlayerID 的确定性初始 X 轴间距。")]
        [SerializeField] private float _playerSpawnSpacing=3f;
        [Tooltip("失去窗口焦点后仍保持逻辑帧与网络运行。")]
        [SerializeField] private bool _runInBackground=true;

        [Header("View Rollback 回归")]
        [Tooltip("仅用于 03D-VIEW-01F：持续审计网络玩家 View/Binder/Pool 是否一致。")]
        [SerializeField] private bool _enableViewRollbackAudit=false;

        private SimulationInitializer _initializer;
        private TimeSimulator _timeSimulator;
        private UnityInputAdapter _inputAdapter;
        private NetworkRollbackSimulationRuntime _runtime;
        private BuffRollbackRestoreListener _buffRestoreListener;
        private ViewRollbackRestoreListener _viewRestoreListener;
        private NetworkViewRollbackRuntimeAudit _viewAudit;
        private bool _viewAuditSubscribed;
        private Coroutine _mountCoroutine;
        private BootstrapState _state=BootstrapState.Idle;
        private string _lastError;
        private NetworkClientLaunchOptions _launchOptions;
        private string _launchOptionsError;
        private NetworkSessionStallPolicy _sessionStallPolicy;
        private bool _sessionStalled;
        private int _sessionStallCount;
        private int _sessionResumeCount;
        private int _lastStallFrame;
        private int _lastStallAuthorityCount;
        private NetworkSessionStallReason _lastStallReason=NetworkSessionStallReason.None;

        public bool NetworkEnabled => _enable;
        public BootstrapState State => _state;
        public bool IsMounted => _state==BootstrapState.Mounted&&_runtime!=null&&_runtime.IsMounted;
        public string LastError => _lastError;
        public NetworkRollbackSimulationRuntime Runtime => _runtime;
        public int LocalPlayerID => _inputAdapter!=null?_inputAdapter.PlayerID:(_launchOptions?.PlayerID??0);
        public int PlayerCount => _launchOptions?.PlayerCount??_playerCount;
        public string ServerAddress => _launchOptions?.ServerAddress??_serverAddress;
        public int ServerPort => _launchOptions?.ServerPort??_serverPort;
        public uint SessionId => _launchOptions?.SessionId??_sessionId;
        public float PlayerSpawnSpacing => _playerSpawnSpacing;
        public NetworkInputTransportMode TransportMode => _transportMode;
        public bool IsSessionStalled => _sessionStalled;
        public NetworkSessionStallReason SessionStallReason => _sessionStallPolicy?.StallReason??NetworkSessionStallReason.None;
        public int SessionStallCount => _sessionStallCount;
        public int SessionResumeCount => _sessionResumeCount;

        private void Awake()
        {
            try
            {
                _launchOptions=NetworkClientLaunchOptions.Parse(Environment.GetCommandLineArgs());
                if(_runInBackground) Application.runInBackground=true;

                if(_launchOptions.HasAnyOverride)
                {
                    Debug.Log(
                        $"NetworkRollbackBootstrap Awake Log: CommandLineOverride PlayerID={_launchOptions.PlayerID?.ToString()??"-"}, " +
                        $"Players={_launchOptions.PlayerCount?.ToString()??"-"}, Server={_launchOptions.ServerAddress??"-"}, " +
                        $"Port={_launchOptions.ServerPort?.ToString()??"-"}, Session={(_launchOptions.SessionId.HasValue?$"0x{_launchOptions.SessionId.Value:X8}":"-")}");
                }
            }
            catch(Exception exception)
            {
                _launchOptions=new NetworkClientLaunchOptions();
                _launchOptionsError=$"{exception.GetType().Name}: {exception.Message}";
            }
        }

        /// <summary>
        /// SimulationInitializer 创建 World/Player 之前调用，用命令行 PlayerID 覆盖 Inspector。
        /// </summary>
        internal bool PrepareBeforeSimulationInitialization(UnityInputAdapter inputAdapter,out string message)
        {
            message=string.Empty;

            if(!_enable)
            {
                message="Network Rollback Bootstrap Is Disabled";
                return false;
            }

            if(!string.IsNullOrEmpty(_launchOptionsError))
            {
                message=$"Command Line Parse Failed: {_launchOptionsError}";
                return false;
            }

            if(inputAdapter==null)
            {
                message="UnityInputAdapter Is Missing";
                return false;
            }

            if(_launchOptions?.PlayerID is int playerID)
                inputAdapter.SetPlayerID(playerID);

            if(inputAdapter.PlayerID<=0)
            {
                message=$"Invalid Local PlayerID={inputAdapter.PlayerID}";
                return false;
            }

            if(PlayerCount<=0||PlayerCount>NetworkProtocolConstants.MaxPlayerCount)
            {
                message=$"Invalid PlayerCount={PlayerCount}";
                return false;
            }

            if(inputAdapter.PlayerID>PlayerCount)
            {
                message=$"Local PlayerID={inputAdapter.PlayerID} Exceeds PlayerCount={PlayerCount}";
                return false;
            }

            if(string.IsNullOrWhiteSpace(ServerAddress))
            {
                message="Server Address Is Empty";
                return false;
            }

            if(ServerPort<=0||ServerPort>ushort.MaxValue)
            {
                message=$"Invalid ServerPort={ServerPort}";
                return false;
            }

            if(_playerSpawnSpacing<0f)
            {
                message=$"Invalid PlayerSpawnSpacing={_playerSpawnSpacing}";
                return false;
            }

            if(_runInBackground) Application.runInBackground=true;

            message=$"Network PreInitialization Ready: LocalPlayerID={inputAdapter.PlayerID}, Players={PlayerCount}";
            return true;
        }

        /// <summary>
        /// 由 SimulationInitializer 在 World/Runner/FrameCommand 与确定性玩家集合创建完成后调用。
        /// </summary>
        internal bool TryStartMount(SimulationInitializer initializer,out string message)
        {
            message=string.Empty;

            if(!_enable)
            {
                message="Network Rollback Bootstrap Is Disabled";
                return false;
            }

            if(_state==BootstrapState.Connecting||_state==BootstrapState.Mounted)
            {
                message=$"Network Rollback Bootstrap Already {_state}";
                return true;
            }

            if(initializer==null)
            {
                message="SimulationInitializer Is Null";
                return false;
            }

            RollbackBootstrap singleRollback=initializer.GetComponent<RollbackBootstrap>();
            if(singleRollback!=null&&singleRollback.isActiveAndEnabled)
            {
                message="RollbackBootstrap Must Be Disabled In Network Scene";
                return false;
            }

            _timeSimulator=TimeSimulator.Instance;
            if(_timeSimulator==null)
            {
                message="TimeSimulator.Instance Missing";
                return false;
            }

            if(initializer.RuntimeWorld==null||initializer.RuntimeRunner==null)
            {
                message="SimulationInitializer Runtime World/Runner Is Not Ready";
                return false;
            }

            if(initializer.RuntimeRunner.IsTicking||initializer.RuntimeRunner.FrameCount>0)
            {
                message=$"Runner Already Advanced: Frame={initializer.RuntimeRunner.FrameCount}, IsTicking={initializer.RuntimeRunner.IsTicking}";
                return false;
            }

            if(initializer.RuntimeFrameCommandBuffer==null||initializer.RuntimeFrameCommandApplier==null)
            {
                message="FrameCommand Runtime Is Not Ready";
                return false;
            }

            _inputAdapter=initializer.RuntimeInputAdapter;
            if(_inputAdapter==null)
            {
                message="UnityInputAdapter Is Missing";
                return false;
            }

            IReadOnlyList<NetworkPlayerBinding> players=initializer.RuntimeNetworkPlayers;
            if(players==null||players.Count!=PlayerCount)
            {
                message=$"Deterministic Network Player Set Mismatch: Expected={PlayerCount}, Actual={players?.Count??0}";
                return false;
            }

            bool localFound=false;
            for(int i=0;i<players.Count;i++)
            {
                if(players[i].PlayerID==_inputAdapter.PlayerID)
                {
                    localFound=true;
                    break;
                }
            }

            if(!localFound)
            {
                message=$"Local Player Binding Missing: PlayerID={_inputAdapter.PlayerID}";
                return false;
            }

            _initializer=initializer;

            try
            {
                // 网络握手期间禁止 Runner 正常推进，确保 Runtime 一定在 Frame 0 挂载。
                _timeSimulator.SetSimulationRunning(false);

                // 真正解除旧单机 BeforeTick 输入写入，避免一次性输入被旧路径提前消费。
                _initializer.SetDirectInputWriteEnabled(false);

                var options=new NetworkInputClientOptions(
                    _transportMode,
                    ServerAddress,
                    ServerPort,
                    SessionId,
                    _inputAdapter.PlayerID);

                _runtime=new NetworkRollbackSimulationRuntime(
                    _initializer.RuntimeWorld,
                    _initializer.RuntimeRunner,
                    _initializer.RuntimeFrameCommandBuffer,
                    _initializer.RuntimeFrameCommandApplier,
                    players,
                    options,
                    frame=>_inputAdapter.CollectSnapshot(frame),
                    _snapshotRingCapacity,
                    _snapshotIntervalFrames);

                if(_initializer.RuntimeBuffSystem!=null)
                {
                    _buffRestoreListener=new BuffRollbackRestoreListener(_initializer.RuntimeBuffSystem);
                    _runtime.AddRollbackRestoreListener(_buffRestoreListener);
                }

                if(_initializer.RuntimeViewBinder!=null&&_initializer.RuntimeViewManager!=null)
                {
                    _viewRestoreListener=new ViewRollbackRestoreListener(_initializer.RuntimeViewBinder,_initializer.RuntimeViewManager);
                    _runtime.AddRollbackRestoreListener(_viewRestoreListener);
                }

                if(_enableViewRollbackAudit)
                {
                    _viewAudit=new NetworkViewRollbackRuntimeAudit(
                        _initializer.RuntimeWorld,
                        _initializer.RuntimeViewManager,
                        _initializer.RuntimeViewBinder,
                        players,
                        _initializer.RuntimePlayerPrefab);
                }

                _state=BootstrapState.Connecting;
                _lastError=null;
                _mountCoroutine=StartCoroutine(MountWhenReady());
                message="Network Rollback Bootstrap Connecting";

                Debug.Log(
                    $"NetworkRollbackBootstrap TryStartMount Log: Transport={_transportMode}, Server={ServerAddress}:{ServerPort}, " +
                    $"Session=0x{SessionId:X8}, LocalPlayerID={LocalPlayerID}, Players={PlayerCount}");

                return true;
            }
            catch(Exception exception)
            {
                FailAndStop($"Mount Initialization Failed: {exception.GetType().Name}: {exception.Message}");
                message=_lastError;
                return false;
            }
        }

        private IEnumerator MountWhenReady()
        {
            float deadline=Time.realtimeSinceStartup+Mathf.Max(0.1f,_connectTimeoutSeconds);

            while(_runtime!=null&&!_runtime.IsReady)
            {
                try
                {
                    _runtime.PumpNetwork();
                }
                catch(Exception exception)
                {
                    FailAndStop($"Network Pump Failed During Connect: {exception.GetType().Name}: {exception.Message}");
                    yield break;
                }

                if(Time.realtimeSinceStartup>=deadline)
                {
                    FailAndStop(
                        $"Connect Timeout: Transport={_transportMode}, Server={ServerAddress}:{ServerPort}, " +
                        $"LocalPlayerID={LocalPlayerID}, Timeout={_connectTimeoutSeconds:F2}s");
                    yield break;
                }

                yield return null;
            }

            _mountCoroutine=null;
            if(_runtime==null) yield break;

            try
            {
                // 丢弃握手期间积累的一次性输入；下一 Unity Update 会重新采样 held/axis。
                _inputAdapter.Init(_initializer.RuntimeWorld,_initializer.RuntimeLocalPlayerEntity);

                _runtime.Mount();

                _sessionStallPolicy=new NetworkSessionStallPolicy(Mathf.Max(0.1f,_authorityStallTimeoutSeconds));
                _sessionStallPolicy.Reset(
                    Time.realtimeSinceStartupAsDouble,
                    _runtime.ConnectionState,
                    _runtime.NetworkRuntime.ReceivedAuthorityCount);
                _sessionStalled=false;
                _sessionStallCount=0;
                _sessionResumeCount=0;
                _lastStallFrame=0;
                _lastStallAuthorityCount=0;
                _lastStallReason=NetworkSessionStallReason.None;

                if(_enableViewRollbackAudit&&_viewAudit!=null&&!_viewAuditSubscribed)
                {
                    _initializer.RuntimeRunner.AfterTick+=OnViewAuditAfterTick;
                    _viewAuditSubscribed=true;
                }

                _timeSimulator.SetSimulationRunning(true);
                _state=BootstrapState.Mounted;

                Debug.Log(
                    $"NetworkRollbackBootstrap MountWhenReady Log: Mounted Transport={_transportMode}, " +
                    $"Server={ServerAddress}:{ServerPort}, LocalPlayerID={LocalPlayerID}, Players={PlayerCount}");
            }
            catch(Exception exception)
            {
                FailAndStop($"Runtime Mount Failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private void Update()
        {
            if(_state!=BootstrapState.Mounted||_runtime==null) return;

            // Bootstrap 使用负执行顺序，先于 TimeSimulator.Update Pump 网络并评估 Session Gate。
            // 即使 Simulation 已冻结，这里仍持续 Pump，Authority 恢复后才能自动解除 Stall。
            if(_runtime.ConnectionState==NetworkInputClientConnectionState.Connected)
            {
                try
                {
                    _runtime.PumpNetwork();
                }
                catch(Exception exception)
                {
                    // Transport 在 Tick 内进入 Faulted/Disconnected 属于 Session Stall，不销毁 Runtime。
                    // Protocol/Reject 等仍保持原有 Fail Fast 行为。
                    if(_runtime!=null&&_runtime.ConnectionState!=NetworkInputClientConnectionState.Connected)
                    {
                        EvaluateSessionStall();
                        return;
                    }

                    FailAndStop($"Runtime Network Pump Failed: {exception.GetType().Name}: {exception.Message}");
                    return;
                }
            }

            EvaluateSessionStall();
        }

        private void EvaluateSessionStall()
        {
            if(_runtime==null||_sessionStallPolicy==null) return;

            bool shouldRun=_sessionStallPolicy.Evaluate(
                Time.realtimeSinceStartupAsDouble,
                _runtime.ConnectionState,
                _runtime.NetworkRuntime.ReceivedAuthorityCount);

            if(shouldRun)
            {
                if(_sessionStalled)
                {
                    _sessionStalled=false;
                    _sessionResumeCount++;
                    _timeSimulator?.SetSimulationRunning(true);

                    Debug.Log(
                        $"NetworkRollbackBootstrap EvaluateSessionStall Log: Resumed Frame={_runtime.Runner.FrameCount}, " +
                        $"Authorities={_runtime.NetworkRuntime.ReceivedAuthorityCount}, Connection={_runtime.ConnectionState}, " +
                        $"ResumeCount={_sessionResumeCount}");
                }

                return;
            }

            if(_sessionStalled) return;

            _sessionStalled=true;
            _sessionStallCount++;
            _lastStallFrame=_runtime.Runner.FrameCount;
            _lastStallAuthorityCount=_runtime.NetworkRuntime.ReceivedAuthorityCount;
            _lastStallReason=_sessionStallPolicy.StallReason;
            _timeSimulator?.SetSimulationRunning(false);

            Debug.LogWarning(
                $"NetworkRollbackBootstrap EvaluateSessionStall Warning: Stalled Reason={_lastStallReason}, " +
                $"Frame={_lastStallFrame}, Authorities={_lastStallAuthorityCount}, Connection={_runtime.ConnectionState}, " +
                $"Timeout={_sessionStallPolicy.AuthorityTimeoutSeconds:F2}s, StallCount={_sessionStallCount}");
        }

        private void OnViewAuditAfterTick(SimulationContext context)
        {
            if(context.isRollback||_viewAudit==null) return;
            _viewAudit.Sample(context.frameNumber);
        }

        private void OnDisable()
        {
            if(!Application.isPlaying) return;
            Cleanup(true);
        }

        private void OnDestroy()=>Cleanup(false);

        private void UnsubscribeViewAudit()
        {
            if(!_viewAuditSubscribed) return;

            if(_initializer!=null&&_initializer.RuntimeRunner!=null)
                _initializer.RuntimeRunner.AfterTick-=OnViewAuditAfterTick;

            _viewAuditSubscribed=false;
        }

        private void FailAndStop(string error)
        {
            _lastError=error;
            _state=BootstrapState.Faulted;
            Debug.LogError($"NetworkRollbackBootstrap FailAndStop Error: {error}");

            if(_mountCoroutine!=null)
            {
                StopCoroutine(_mountCoroutine);
                _mountCoroutine=null;
            }

            UnsubscribeViewAudit();
            LogRuntimeSummary("Faulted");

            if(_runtime!=null)
            {
                if(_buffRestoreListener!=null)
                    _runtime.RemoveRollbackRestoreListener(_buffRestoreListener);

                if(_viewRestoreListener!=null)
                    _runtime.RemoveRollbackRestoreListener(_viewRestoreListener);

                _runtime.Dispose();
                _runtime=null;
            }

            _buffRestoreListener=null;
            _viewRestoreListener=null;
            _viewAudit=null;
            _sessionStallPolicy=null;
            _sessionStalled=true;
            _initializer?.SetDirectInputWriteEnabled(false);
            _timeSimulator?.SetSimulationRunning(false);
        }

        private void Cleanup(bool restoreSimulation)
        {
            if(_mountCoroutine!=null)
            {
                StopCoroutine(_mountCoroutine);
                _mountCoroutine=null;
            }

            UnsubscribeViewAudit();
            LogRuntimeSummary("Cleanup");

            if(_runtime!=null)
            {
                if(_buffRestoreListener!=null)
                    _runtime.RemoveRollbackRestoreListener(_buffRestoreListener);

                if(_viewRestoreListener!=null)
                    _runtime.RemoveRollbackRestoreListener(_viewRestoreListener);

                _runtime.Dispose();
                _runtime=null;
            }

            _buffRestoreListener=null;
            _viewRestoreListener=null;
            _viewAudit=null;
            _sessionStallPolicy=null;
            _sessionStalled=false;

            if(restoreSimulation)
            {
                if(_initializer!=null)
                    _initializer.SetDirectInputWriteEnabled(true);

                _timeSimulator?.SetSimulationRunning(true);
            }

            if(_state!=BootstrapState.Faulted)
                _state=BootstrapState.Idle;
        }

        private void LogRuntimeSummary(string reason)
        {
            if(_runtime==null) return;

            string viewAudit=_viewAudit==null
                ?"ViewAudit=Disabled"
                :$"ViewAuditSamples={_viewAudit.SampleCount}, ViewAuditFailures={_viewAudit.FailureCount}, LastSampledFrame={_viewAudit.LastSampledFrame}, " +
                 $"ViewCount={_viewAudit.LastViewCount}, BindingCount={_viewAudit.LastBindingCount}, PoolInUse={_viewAudit.LastPoolInUseCount}, " +
                 $"MaxViewCount={_viewAudit.MaxViewCount}, MaxBindingCount={_viewAudit.MaxBindingCount}, MaxPoolInUse={_viewAudit.MaxPoolInUseCount}, " +
                 $"FirstViewAuditFailure={_viewAudit.FirstFailure??"None"}";

            Debug.Log(
                $"NetworkRollbackBootstrap LogRuntimeSummary Log: Reason={reason}, LocalPlayerID={LocalPlayerID}, " +
                $"Frame={_runtime.Runner.FrameCount}, NormalFrames={_runtime.NormalFrameCount}, " +
                $"Authorities={_runtime.NetworkRuntime.ReceivedAuthorityCount}, Applied={_runtime.NetworkRuntime.AppliedAuthorityCount}, " +
                $"OutOfOrderAuthority={_runtime.NetworkRuntime.OutOfOrderAuthorityCount}, " +
                $"PredictedFrames={_runtime.PredictedFrameCount}, PredictedInputs={_runtime.PredictedInputCount}, " +
                $"RollbackRestore={_runtime.RollbackRestoreCount}, RollbackResimulate={_runtime.RollbackResimulateCount}, " +
                $"Connection={_runtime.ConnectionState}, SessionStalled={_sessionStalled}, StallReason={_sessionStallPolicy?.StallReason??_lastStallReason}, " +
                $"SessionStalls={_sessionStallCount}, SessionResumes={_sessionResumeCount}, LastStallFrame={_lastStallFrame}, " +
                $"LastStallAuthorities={_lastStallAuthorityCount}, LastStallReason={_lastStallReason}, {viewAudit}");
        }
    }
}
