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
    /// Unity Scene 网络回滚入口。负责冻结正常帧、建立网络连接、绑定玩家并挂载 NetworkRollbackSimulationRuntime。
    /// </summary>
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
        [Tooltip("会话玩家总数。03C-2A 单客户端 Scene Smoke 先使用 1。")]
        [SerializeField] private int _playerCount=1;
        [Tooltip("KCP 握手最长等待秒数。")]
        [SerializeField] private float _connectTimeoutSeconds=5f;

        [Header("回滚")]
        [Tooltip("ECS Snapshot 环形缓存容量。")]
        [SerializeField] private int _snapshotRingCapacity=256;
        [Tooltip("正常模拟每隔多少逻辑帧保存一次 Snapshot。")]
        [SerializeField] private int _snapshotIntervalFrames=10;

        [Header("远端玩家")]
        [Tooltip("自动创建远端玩家时，相邻玩家的 X 轴出生间距。")]
        [SerializeField] private float _remoteSpawnSpacing=3f;

        private SimulationInitializer _initializer;
        private TimeSimulator _timeSimulator;
        private UnityInputAdapter _inputAdapter;
        private NetworkRollbackSimulationRuntime _runtime;
        private BuffRollbackRestoreListener _buffRestoreListener;
        private readonly List<Entity> _createdRemotePlayers=new();
        private Coroutine _mountCoroutine;
        private BootstrapState _state=BootstrapState.Idle;
        private string _lastError;

        public bool NetworkEnabled => _enable;
        public BootstrapState State => _state;
        public bool IsMounted => _state==BootstrapState.Mounted&&_runtime!=null&&_runtime.IsMounted;
        public string LastError => _lastError;
        public NetworkRollbackSimulationRuntime Runtime => _runtime;
        public int LocalPlayerID => _inputAdapter!=null?_inputAdapter.PlayerID:0;
        public int PlayerCount => _playerCount;
        public NetworkInputTransportMode TransportMode => _transportMode;

        /// <summary>
        /// 由 SimulationInitializer 在 World/Runner/FrameCommand 初始化完成后调用。
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

            if(_inputAdapter.PlayerID<=0)
            {
                message=$"Invalid Local PlayerID={_inputAdapter.PlayerID}";
                return false;
            }

            if(_playerCount<=0||_playerCount>NetworkProtocolConstants.MaxPlayerCount)
            {
                message=$"Invalid PlayerCount={_playerCount}";
                return false;
            }

            if(_inputAdapter.PlayerID>_playerCount)
            {
                message=$"Local PlayerID={_inputAdapter.PlayerID} Exceeds PlayerCount={_playerCount}";
                return false;
            }

            if(string.IsNullOrWhiteSpace(_serverAddress))
            {
                message="Server Address Is Empty";
                return false;
            }

            if(_serverPort<=0||_serverPort>ushort.MaxValue)
            {
                message=$"Invalid ServerPort={_serverPort}";
                return false;
            }

            _initializer=initializer;

            try
            {
                // 网络握手期间禁止 Runner 正常推进，确保 Runtime 一定在 Frame 0 挂载。
                _timeSimulator.SetSimulationRunning(false);

                // 真正解除旧单机 BeforeTick 输入写入，避免 CollectSnapshot 被提前消费。
                _initializer.SetDirectInputWriteEnabled(false);

                int localPlayerID=_inputAdapter.PlayerID;
                Entity localPlayer=_initializer.RuntimeLocalPlayerEntity;
                _initializer.SetNetworkPlayerIdentity(localPlayer,localPlayerID);

                var players=new List<NetworkPlayerBinding>(_playerCount)
                {
                    new NetworkPlayerBinding(localPlayerID,localPlayer)
                };

                _createdRemotePlayers.Clear();

                for(int playerID=1;playerID<=_playerCount;playerID++)
                {
                    if(playerID==localPlayerID) continue;

                    float direction=playerID<localPlayerID?-1f:1f;
                    float distance=Mathf.Abs(playerID-localPlayerID)*_remoteSpawnSpacing;
                    Entity remote=_initializer.CreateNetworkPlayerEntity(playerID,new Vector3(direction*distance,0f,0f));
                    _createdRemotePlayers.Add(remote);
                    players.Add(new NetworkPlayerBinding(playerID,remote));
                }

                players.Sort((a,b)=>a.PlayerID.CompareTo(b.PlayerID));

                var options=new NetworkInputClientOptions(
                    _transportMode,
                    _serverAddress,
                    _serverPort,
                    _sessionId,
                    localPlayerID);

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

                _state=BootstrapState.Connecting;
                _lastError=null;
                _mountCoroutine=StartCoroutine(MountWhenReady());
                message="Network Rollback Bootstrap Connecting";

                Debug.Log(
                    $"NetworkRollbackBootstrap TryStartMount Log: Transport={_transportMode}, Server={_serverAddress}:{_serverPort}, " +
                    $"Session=0x{_sessionId:X8}, LocalPlayerID={localPlayerID}, Players={_playerCount}");

                return true;
            }
            catch(Exception exception)
            {
                FailAndRestore($"Mount Initialization Failed: {exception.GetType().Name}: {exception.Message}");
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
                    FailAndRestore($"Network Pump Failed During Connect: {exception.GetType().Name}: {exception.Message}");
                    yield break;
                }

                if(Time.realtimeSinceStartup>=deadline)
                {
                    FailAndRestore(
                        $"Connect Timeout: Transport={_transportMode}, Server={_serverAddress}:{_serverPort}, " +
                        $"LocalPlayerID={LocalPlayerID}, Timeout={_connectTimeoutSeconds:F2}s");
                    yield break;
                }

                yield return null;
            }

            _mountCoroutine=null;

            if(_runtime==null) yield break;

            try
            {
                // 丢弃握手等待期间积累的一次性输入；下一 Unity Update 会重新采样 held/axis。
                _inputAdapter.Init(_initializer.RuntimeWorld,_initializer.RuntimeLocalPlayerEntity);

                _runtime.Mount();
                _timeSimulator.SetSimulationRunning(true);
                _state=BootstrapState.Mounted;

                Debug.Log(
                    $"NetworkRollbackBootstrap MountWhenReady Log: Mounted Transport={_transportMode}, " +
                    $"Server={_serverAddress}:{_serverPort}, LocalPlayerID={LocalPlayerID}, Players={_playerCount}");
            }
            catch(Exception exception)
            {
                FailAndRestore($"Runtime Mount Failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private void Update()
        {
            if(_state!=BootstrapState.Mounted||_runtime==null) return;

            try
            {
                _runtime.PumpNetwork();
            }
            catch(Exception exception)
            {
                FailAndRestore($"Runtime Network Pump Failed: {exception.GetType().Name}: {exception.Message}");
            }
        }

        private void OnDisable()
        {
            if(!Application.isPlaying) return;
            Cleanup(true);
        }

        private void OnDestroy()
        {
            Cleanup(false);
        }

        private void FailAndRestore(string error)
        {
            _lastError=error;
            _state=BootstrapState.Faulted;
            Debug.LogError($"NetworkRollbackBootstrap FailAndRestore Error: {error}");
            Cleanup(true,false);
        }

        private void Cleanup(bool restoreSimulation,bool resetState=true)
        {
            if(_mountCoroutine!=null)
            {
                StopCoroutine(_mountCoroutine);
                _mountCoroutine=null;
            }

            if(_runtime!=null)
            {
                if(_buffRestoreListener!=null)
                    _runtime.RemoveRollbackRestoreListener(_buffRestoreListener);

                _runtime.Dispose();
                _runtime=null;
            }

            _buffRestoreListener=null;

            if(restoreSimulation)
            {
                if(_initializer!=null)
                    _initializer.SetDirectInputWriteEnabled(true);

                _timeSimulator?.SetSimulationRunning(true);
            }

            _createdRemotePlayers.Clear();

            if(resetState&&_state!=BootstrapState.Faulted)
                _state=BootstrapState.Idle;
        }
    }
}
