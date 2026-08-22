using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using View;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// View Rollback 边界审计。
    /// 本阶段只验证现状，不修改 View / Snapshot / Rollback 生产逻辑。
    /// </summary>
    public static class ViewRollbackBoundaryValidationTestBootstrap
    {
        private const int PlayerID=1;
        private const float TickLength=1f/60f;

        /// <summary>期望逻辑 Snapshot 不包含一次性 Prefab View 请求。</summary>
        public static void RunSnapshotBeforeSpawnBoundaryStatic()
        {
            using var env=CreateEnvironment(false);

            EcsWorldSnapshot snapshot=env.World.CaptureSnapshot(0);
            bool contains=ContainsStore(snapshot,typeof(PrefabViewRequestComponent));

            Expect(!contains,
                "ViewRollback SnapshotBoundary Error: PrefabViewRequestComponent Was Captured In Logic Snapshot");
        }

        /// <summary>期望 View 已创建后，逻辑 Snapshot 不包含运行时 View ID。</summary>
        public static void RunSnapshotAfterSpawnBoundaryStatic()
        {
            using var env=CreateEnvironment(false);

            DriveFrame(env,1,CreateInput(1,0f));
            EcsWorldSnapshot snapshot=env.World.CaptureSnapshot(1);
            bool contains=ContainsStore(snapshot,typeof(ViewComponent));

            Expect(!contains,
                "ViewRollback SnapshotBoundary Error: ViewComponent Was Captured In Logic Snapshot");
        }

        /// <summary>期望逻辑 Checksum 不受 Unity View ID 影响。</summary>
        public static void RunChecksumIgnoresViewIdentityStatic()
        {
            World a=CreateChecksumWorld(1);
            World b=CreateChecksumWorld(99);

            try
            {
                uint checksumA=WorldChecksumCalculator.Calculate(a);
                uint checksumB=WorldChecksumCalculator.Calculate(b);

                Expect(checksumA==checksumB,
                    $"ViewRollback ChecksumBoundary Error: ViewID Changed Logic Checksum: A=0x{checksumA:X8}, B=0x{checksumB:X8}");
            }
            finally
            {
                a.Dispose();
                b.Dispose();
            }
        }

        /// <summary>
        /// 从 Frame 0 Snapshot 回滚并重模拟后，最终只允许存在一个有效 View，
        /// World.ViewComponent、ViewManager 与 EntityViewBinder 必须指向同一对象。
        /// </summary>
        public static void RunRollbackBeforeInitialViewSpawnConsistencyStatic()
        {
            using var env=CreateEnvironment(true);

            PlayerInputSnapshot authoritativeFrame1=CreateInput(1,-1f);

            for(int frame=1;frame<=6;frame++)
            {
                PlayerInputSnapshot input=frame==1?CreateInput(frame,1f):CreateInput(frame,0f);
                DriveFrame(env,frame,input);
            }

            Expect(env.ViewManager.ViewCount==1,
                $"ViewRollback Precondition Error: Expected One View Before Rollback, Actual={env.ViewManager.ViewCount}");
            Expect(env.Provider.LiveCount==1,
                $"ViewRollback Precondition Error: Expected One Live Instance Before Rollback, Actual={env.Provider.LiveCount}");
            Expect(env.World.TryGetComponent(env.Player,out ViewComponent beforeView)&&beforeView.viewID>0,
                "ViewRollback Precondition Error: Player ViewComponent Missing Before Rollback");
            Expect(env.Binder.TryGetView(env.Player,out GameObject beforeBoundView)&&beforeBoundView!=null,
                "ViewRollback Precondition Error: Binder Missing Player View Before Rollback");

            env.Coordinator.ReceiveAuthoritativeInput(1,authoritativeFrame1);

            var errors=new List<string>();

            if(env.Probe.RestoreCount<=0) errors.Add("Rollback Restore Was Not Triggered");
            if(env.Probe.ResimulateCount<=0) errors.Add("Rollback Resimulate Was Not Triggered");
            if(env.Coordinator.CurrentFrame!=6) errors.Add($"Coordinator Frame Expected=6 Actual={env.Coordinator.CurrentFrame}");

            bool hasWorldView=env.World.TryGetComponent(env.Player,out ViewComponent worldView)&&worldView.viewID>0;
            if(!hasWorldView) errors.Add("World ViewComponent Missing After Rollback");

            Transform managerTransform=null;
            bool managerHasWorldView=hasWorldView&&env.ViewManager.TryGetTransform(worldView.viewID,out managerTransform)&&managerTransform!=null;
            if(!managerHasWorldView) errors.Add($"ViewManager Missing World ViewID={(hasWorldView?worldView.viewID:0)}");

            bool binderHasView=env.Binder.TryGetView(env.Player,out GameObject binderView)&&binderView!=null;
            if(!binderHasView) errors.Add("EntityViewBinder Missing Player Binding After Rollback");

            if(managerHasWorldView&&binderHasView&&!ReferenceEquals(managerTransform.gameObject,binderView))
                errors.Add($"Binder/ViewComponent Mismatch: WorldViewID={worldView.viewID}, BinderObject={binderView.name}, ManagerObject={managerTransform.gameObject.name}");

            if(env.ViewManager.ViewCount!=1)
                errors.Add($"ViewManager Duplicate/Leak: ViewCount={env.ViewManager.ViewCount}");

            if(env.Provider.LiveCount!=1)
                errors.Add($"Provider Duplicate/Leak: LiveCount={env.Provider.LiveCount}");

            if(managerHasWorldView&&env.World.TryGetComponent(env.Player,out PositionComponent position))
            {
                Vector3 viewPosition=managerTransform.position;
                if(!NearlyEqual(viewPosition.x,position.x)||!NearlyEqual(viewPosition.y,position.y)||!NearlyEqual(viewPosition.z,position.z))
                    errors.Add($"View Position Mismatch: Logic=({position.x},{position.y},{position.z}) View=({viewPosition.x},{viewPosition.y},{viewPosition.z})");
            }

            if(errors.Count>0)
            {
                throw new InvalidOperationException(
                    "ViewRollback Consistency Error: "+
                    string.Join(" | ",errors)+
                    $" | SpawnCount={env.Provider.SpawnCount}, ReleaseCount={env.Provider.ReleaseCount}, "+
                    $"ViewCount={env.ViewManager.ViewCount}, LiveCount={env.Provider.LiveCount}, "+
                    $"Restore={env.Probe.RestoreCount}, Resimulate={env.Probe.ResimulateCount}");
            }
        }

        private static TestEnvironment CreateEnvironment(bool saveInitialSnapshot)
        {
            var world=new World { EnableSystemProfile=false };
            var provider=new TrackingViewInstanceProvider();
            var viewManager=new ViewManager(provider);
            var binder=new EntityViewBinder(viewManager,world.IsAlive);
            var prefab=new GameObject("ViewRollbackAuditPrefab");
            prefab.SetActive(false);

            viewManager.RegisterPrefab(1,prefab);

            Entity player=world.CreateEntity();
            world.SetComponent(player,new PlayerTagComponent());
            world.SetComponent(player,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));
            world.SetComponent(player,new MoveSpeedComponent(5f));
            world.SetComponent(player,new VelocityComponent(0f,0f,0f));
            world.SetComponent(player,new PositionComponent(0f,0f,0f));
            world.SetComponent(player,new PrefabViewRequestComponent(1));

            world.AddSystem(new ViewSpawnSystem(viewManager));
            world.AddSystem(new EntityViewBindingSystem(binder));
            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());
            world.AddSystem(new ViewSyncSystem(viewManager));
            world.AddSystem(new ViewDestroySystem(viewManager));

            var inputApplier=new PlayerSnapshotInputApplier();
            inputApplier.RegisterPlayer(PlayerID,player);

            var commandBuffer=new SimulationFrameCommandBuffer(64);
            var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,64);
            var rollbackAdapter=new WorldRollbackAdapter<PlayerInputSnapshot>(world,world,inputApplier,null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));

            var probe=new RollbackProbe();
            rollbackAdapter.AddRollbackRestoreListener(probe);
            rollbackAdapter.AddRollbackRestoreListener(new ViewRollbackRestoreListener(binder,viewManager));

            var snapshotBuffer=new SnapshotRingBuffer<EcsWorldSnapshot>(64);
            var coordinator=new RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapshotBuffer,
                rollbackAdapter,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength=TickLength
            };

            var env=new TestEnvironment(world,player,prefab,provider,viewManager,binder,coordinator,commandBuffer,snapshotBuffer,probe);
            if(saveInitialSnapshot) coordinator.SaveSnapshot();
            return env;
        }

        private static World CreateChecksumWorld(int viewID)
        {
            var world=new World { EnableSystemProfile=false };
            Entity entity=world.CreateEntity();
            world.SetComponent(entity,new PlayerTagComponent());
            world.SetComponent(entity,new PositionComponent(1.25f,2.5f,-3.75f));
            world.SetComponent(entity,new VelocityComponent(0f,0f,0f));
            world.SetComponent(entity,new MoveSpeedComponent(5f));
            world.SetComponent(entity,new PlayerInputSnapshotComponent(1,PlayerID,0f,0f));
            world.SetComponent(entity,new ViewComponent(viewID));
            return world;
        }

        private static void DriveFrame(TestEnvironment env,int frame,PlayerInputSnapshot input)
        {
            RollbackStepResult result=env.Coordinator.TryStep(frame,input);
            Expect(result.Succeeded,
                $"ViewRollback DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context=new SimulationContext(frame,TickLength,false);
            env.World.Tick(in context);
        }

        private static PlayerInputSnapshot CreateInput(int frame,float moveX)
            =>new(frame,PlayerID) { moveX=moveX,moveY=0f };

        private static bool ContainsStore(EcsWorldSnapshot snapshot,Type componentType)
        {
            for(int i=0;i<snapshot.ComponentStores.Count;i++)
                if(snapshot.ComponentStores[i].ComponentType==componentType) return true;
            return false;
        }

        private static bool NearlyEqual(float a,float b)=>Mathf.Abs(a-b)<0.0001f;

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        private sealed class RollbackProbe : IRollbackRestoreListener
        {
            public int RestoreCount { get; private set; }
            public int ResimulateCount { get; private set; }

            public void OnRollbackWorldRestored(World world,int restoredFrame)=>RestoreCount++;
            public void OnRollbackResimulated(World world,int currentFrame)=>ResimulateCount++;
        }

        private sealed class TrackingViewInstanceProvider : IViewInstanceProvider
        {
            private readonly List<GameObject> _instances=new();

            public int SpawnCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public int LiveCount => _instances.Count;

            public GameObject Spawn(GameObject prefab,Vector3 position,Quaternion rotation)
            {
                SpawnCount++;
                var instance=new GameObject($"ViewRollbackAuditInstance_{SpawnCount}");
                instance.transform.SetPositionAndRotation(position,rotation);
                instance.SetActive(true);
                _instances.Add(instance);
                return instance;
            }

            public void Release(GameObject instance)
            {
                if(instance==null) return;
                ReleaseCount++;
                _instances.Remove(instance);
                DestroyObject(instance);
            }

            public void Clear()
            {
                for(int i=_instances.Count-1;i>=0;i--) DestroyObject(_instances[i]);
                _instances.Clear();
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Player;
            public readonly GameObject Prefab;
            public readonly TrackingViewInstanceProvider Provider;
            public readonly ViewManager ViewManager;
            public readonly EntityViewBinder Binder;
            public readonly RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;
            public readonly RollbackProbe Probe;

            public TestEnvironment(
                World world,Entity player,GameObject prefab,TrackingViewInstanceProvider provider,
                ViewManager viewManager,EntityViewBinder binder,
                RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer,
                RollbackProbe probe)
            {
                World=world;
                Player=player;
                Prefab=prefab;
                Provider=provider;
                ViewManager=viewManager;
                Binder=binder;
                Coordinator=coordinator;
                CommandBuffer=commandBuffer;
                SnapshotBuffer=snapshotBuffer;
                Probe=probe;
            }

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                World.Dispose();
                ViewManager.Clear();
                DestroyObject(Prefab);
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if(target==null) return;
            if(Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
