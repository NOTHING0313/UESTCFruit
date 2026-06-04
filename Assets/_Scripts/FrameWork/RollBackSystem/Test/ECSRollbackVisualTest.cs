/*
 * ECSRollbackVisualTest — 可视化回滚测试。
 *
 * 两个 Cube：
 *   - 白色 = 本地预测实体（A/D 移动，受回滚影响）
 *   - 红色 = 权威标记（回滚后显示服务器位置）
 *
 * 场景：
 *   白色 Cube 实时响应用户 A/D 输入向前推进。
 *   按 Space 时，假装"服务器"告诉你：你在第 N 帧的输入其实是反方向。
 *   系统回滚到第 N 帧之前，用反方向重新模拟 → 白色 Cube 跳到修正位置。
 *   红色 Cube 标记出这个修正后的位置。
 *
 * 操作：
 *   A/D    左右移动
 *   Space  触发回滚：对最近一帧注入反方向权威输入
 */

using ECSFrameWork;
using UnityEngine;

namespace FrameWork.RollBackSystem.Tests
{
    public class ECSRollbackVisualTest : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 5f;
        [SerializeField] private float _tickLength = 1f / 30f;
        [SerializeField] private int _snapshotCapacity = 120;

        private World _world;
        private SimulateRunner _runner;
        private ViewManager _viewManager;
        private const int PredictedPrefabID = 1;
        private const int AuthPrefabID = 2;

        private Entity _predictedEntity;
        private Entity _authoritativeEntity;

        private RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot> _coordinator;
        private PlayerSnapshotInputApplier _inputApplier;

        private float _tickCounter;
        private bool _rollbackRequested;
        private float _currentHorizontal;

        // 记录最近一个有方向输入的帧号
        private int _lastNonZeroFrame;
        private float _lastNonZeroHorizontal;

        private void Start() => Init();

        private void Update()
        {
            if (_world == null) return;

            // 读取输入（在逻辑帧循环之外，保证 GetKeyDown 只执行一次）
            _currentHorizontal = 0f;
            if (Input.GetKey(KeyCode.D)) _currentHorizontal = 1f;
            if (Input.GetKey(KeyCode.A)) _currentHorizontal = -1f;
            if (Input.GetKeyDown(KeyCode.Space)) _rollbackRequested = true;

            _tickCounter += Time.deltaTime;
            while (_tickCounter >= _tickLength)
            {
                _tickCounter -= _tickLength;
                TickFrame();
            }

            SyncTransform(_predictedEntity);
            SyncTransform(_authoritativeEntity);
        }

        private void OnDestroy()
        {
            _viewManager?.Clear();
            _world?.Dispose();
        }

        //--------------------------------
        // Init
        //--------------------------------

        private void Init()
        {
            _world = new World();
            _world.AddSystem(new InputMoveSystem());
            _world.AddSystem(new MovementSystem());

            _viewManager = new ViewManager(new PoolSystemViewInstanceProvider());
            _viewManager.RegisterPrefab(PredictedPrefabID, MakePrefabCube("Predicted", Color.white));
            _viewManager.RegisterPrefab(AuthPrefabID, MakePrefabCube("Authoritative", Color.red));

            _runner = new SimulateRunner(_world, _tickLength, 5);
            _inputApplier = new PlayerSnapshotInputApplier();

            var adapter = new WorldRollbackAdapter<PlayerInputSnapshot>(
                _world, _world, _runner, _inputApplier);

            _coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                new SnapshotRingBuffer<EcsWorldSnapshot>(_snapshotCapacity),
                adapter, null,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(), new AuthoritativeChecksumBuffer());

            _coordinator.SaveSnapshot();

            _predictedEntity = MakePlayerEntity(PredictedPrefabID, new Vector3(0, 0, 0));
            _inputApplier.RegisterPlayer(1, _predictedEntity);
            SpawnView(_predictedEntity, PredictedPrefabID);

            _authoritativeEntity = MakePlayerEntity(AuthPrefabID, new Vector3(0, 0, 0));
            SpawnView(_authoritativeEntity, AuthPrefabID);
            HideEntity(_authoritativeEntity);

            Debug.Log("[RollbackVisual] A/D=move, Space=rollback to last non-zero frame");
        }

        //--------------------------------
        // Per-Frame Tick
        //--------------------------------

        private void TickFrame()
        {
            var input = new PlayerInputSnapshot { moveX = _currentHorizontal, moveY = 0f };
            _coordinator.Step(input);

            if (_coordinator.CurrentFrame % 10 == 0)
                _coordinator.SaveSnapshot();

            // 记录最近一个有方向输入的帧
            if (_currentHorizontal != 0f)
            {
                _lastNonZeroFrame = _coordinator.CurrentFrame;
                _lastNonZeroHorizontal = _currentHorizontal;
            }

            // 只处理一次空格请求
            if (_rollbackRequested)
            {
                _rollbackRequested = false;
                DoRollback();
            }
        }

        //--------------------------------
        // Rollback
        //--------------------------------

        private void DoRollback()
        {
            // 回滚到最近一个有方向输入的帧
            int target = _lastNonZeroFrame;
            if (target < 1)
            {
                Debug.Log("[Rollback] No non-zero frame yet — move first.");
                return;
            }

            // 权威输入是反方向
            float flipped = _lastNonZeroHorizontal > 0f ? -1f : 1f;
            var auth = new PlayerInputSnapshot { moveX = flipped, moveY = 0f };

            if (_world.TryGetComponent(_predictedEntity, out PositionComponent before))
                Debug.Log($"[Rollback] f{target} auth moveX={flipped}  |  before pos=({before.x:F2})");

            _coordinator.ReceiveAuthoritativeInput(target, in auth);

            if (_world.TryGetComponent(_predictedEntity, out PositionComponent after))
                Debug.Log($"[Rollback] after  pos=({after.x:F2})  frame={_coordinator.CurrentFrame}");

            ShowAuthoritativeMarker();
        }

        //--------------------------------
        // View Helpers
        //--------------------------------

        private void SyncTransform(Entity entity)
        {
            if (!_world.IsAlive(entity)) return;
            if (!_world.TryGetComponent(entity, out PositionComponent pos)) return;
            if (!_world.TryGetComponent(entity, out ViewComponent view)) return;
            if (_viewManager.TryGetTransform(view.viewID, out Transform t))
                t.position = new Vector3(pos.x, pos.y, pos.z);
        }

        private void ShowAuthoritativeMarker()
        {
            if (!_world.TryGetComponent(_predictedEntity, out PositionComponent pp)) return;
            _world.SetComponent(_authoritativeEntity, new PositionComponent(pp.x, pp.y, pp.z));
            if (_world.TryGetComponent(_authoritativeEntity, out ViewComponent av))
                if (_viewManager.TryGetTransform(av.viewID, out Transform t))
                {
                    t.position = new Vector3(pp.x, pp.y, pp.z);
                    t.gameObject.SetActive(true);
                }
        }

        private void HideEntity(Entity entity)
        {
            if (_world.TryGetComponent(entity, out ViewComponent v))
                if (_viewManager.TryGetTransform(v.viewID, out Transform t))
                    t.gameObject.SetActive(false);
        }

        //--------------------------------
        // Factory
        //--------------------------------

        private Entity MakePlayerEntity(int prefabID, Vector3 pos)
        {
            var e = _world.CreateEntity();
            _world.SetComponent(e, new PositionComponent(pos.x, pos.y, pos.z));
            _world.SetComponent(e, new VelocityComponent(0, 0, 0));
            _world.SetComponent(e, new MoveSpeedComponent(_moveSpeed));
            _world.SetComponent(e, new PlayerInputSnapshotComponent(0f, 0f));
            _world.SetComponent(e, new PrefabViewRequestComponent(prefabID));
            return e;
        }

        private void SpawnView(Entity entity, int prefabID)
        {
            if (!_world.TryGetComponent(entity, out PositionComponent pos)) return;
            int vid = _viewManager.SpawnView(prefabID,
                new Vector3(pos.x, pos.y, pos.z), Quaternion.identity);
            if (vid > 0)
            {
                _world.SetComponent(entity, new ViewComponent(vid));
                _world.RemoveComponent<PrefabViewRequestComponent>(entity);
            }
        }

        private static GameObject MakePrefabCube(string name, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = new Vector3(9999, 9999, 9999);
            cube.GetComponent<Renderer>().material.color = color;
            Object.DontDestroyOnLoad(cube);
            return cube;
        }
    }
}
