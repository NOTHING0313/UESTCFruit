using BuffSystem;
using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem;
using PoolSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using View;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// 使用真实 GameObjectPoolCenter / GameObjectPoolViewInstanceProvider 的 PlayMode Rollback 验证。
    /// 不修改生产逻辑，只验证 01D 修复在真实 Unity 对象池生命周期下是否成立。
    /// </summary>
    public static class ViewRollbackPlayModeValidationTestBootstrap
    {
        private const int PlayerID=1;
        private const int PrefabID=1;
        private const float TickLength=1f/60f;

        /// <summary>
        /// Snapshot 后创建 Entity + Pool View，Rollback 到创建前后必须 Release orphan，
        /// 随后同 Prefab 再 Spawn 时应复用刚刚 Release 的真实池对象。
        /// </summary>
        public static void RunCreatedEntityRollbackRealPoolStatic()
        {
            using var pool=RealPoolScope.Create();
            using var env=CreateLifecycleEnvironment(pool,ScenarioMode.CreateEntity);

            env.Coordinator.SaveSnapshot();

            env.Step(CreateInput(1,1f));
            env.Step(CreateInput(2,0f));

            Entity created=env.Scenario.LastCreatedEntity;
            Expect(created.IsValid,"ViewRollbackPlayMode CreateEntity Error: Scenario Did Not Create Entity");
            GameObject spawned=GetBoundView(env,created,"CreateEntity Before Rollback");
            PoolItem poolItem=spawned.GetComponent<PoolItem>();

            Expect(poolItem!=null,"ViewRollbackPlayMode CreateEntity Error: Spawned View Missing PoolItem");
            Expect(!poolItem.IsInPool,"ViewRollbackPlayMode CreateEntity Error: Spawned View Is Already In Pool");
            Expect(env.ViewManager.ViewCount==1,$"ViewRollbackPlayMode CreateEntity Error: Expected ViewCount=1 Actual={env.ViewManager.ViewCount}");
            Expect(CountInUsePoolItems(pool.Prefab)==1,
                $"ViewRollbackPlayMode CreateEntity Error: Expected One InUse Pool Item, Actual={CountInUsePoolItems(pool.Prefab)}");

            env.Coordinator.ReceiveAuthoritativeInput(1,CreateInput(1,0f));

            var errors=new List<string>();
            if(env.World.IsAlive(created)) errors.Add($"Rolled-Back Created Entity Still Alive: {created}");
            if(env.ViewManager.ViewCount!=0) errors.Add($"Orphan ViewManager Entry Remains: {env.ViewManager.ViewCount}");
            if(spawned.activeSelf) errors.Add("Rolled-Back View Is Still Active");
            if(poolItem!=null&&!poolItem.IsInPool) errors.Add("Rolled-Back View Was Not Returned To GameObjectPool");
            if(CountInUsePoolItems(pool.Prefab)!=0) errors.Add($"Pool Still Has InUse Item: {CountInUsePoolItems(pool.Prefab)}");

            // 验证真实池复用：Rollback Release 后，用同一 prefab 再建一个表现 Entity。
            Entity replacement=env.World.CreateEntity();
            env.World.SetComponent(replacement,new PositionComponent(3f,0f,0f));
            env.World.SetComponent(replacement,new ViewPrefabComponent(PrefabID));
            env.World.SetComponent(replacement,new PrefabViewRequestComponent(PrefabID));

            env.Step(CreateInput(3,0f));

            if(!env.Binder.TryGetView(replacement,out GameObject reused)||reused==null)
                errors.Add("Replacement Entity Did Not Spawn A View");
            else
            {
                if(!ReferenceEquals(spawned,reused))
                    errors.Add($"Real Pool Did Not Reuse Released Instance: Old={spawned.name}, New={reused.name}");

                PoolItem reusedItem=reused.GetComponent<PoolItem>();
                if(reusedItem==null||reusedItem.IsInPool)
                    errors.Add("Reused View PoolItem State Is Invalid");
            }

            if(env.ViewManager.ViewCount!=1) errors.Add($"Expected One Replacement View, Actual={env.ViewManager.ViewCount}");
            if(CountInUsePoolItems(pool.Prefab)!=1)
                errors.Add($"Expected One InUse Pool Item After Reuse, Actual={CountInUsePoolItems(pool.Prefab)}");

            ThrowIfAny(
                "CreatedEntityRollbackRealPool",
                errors,
                $"ViewCount={env.ViewManager.ViewCount}, InUse={CountInUsePoolItems(pool.Prefab)}, PoolID={(poolItem!=null?poolItem.PoolID:-1)}");
        }

        /// <summary>
        /// 已有 Entity 的真实池 View 被预测销毁/Release 后，Authority 修正必须复活逻辑 Entity，
        /// 并从 ViewPrefabComponent 重新 Spawn，复用同一个 GameObjectPool 实例。
        /// </summary>
        public static void RunDestroyedEntityRollbackRealPoolStatic()
        {
            using var pool=RealPoolScope.Create();
            using var env=CreateLifecycleEnvironment(pool,ScenarioMode.DestroyEntity);

            env.Step(CreateInput(1,0f));
            env.Step(CreateInput(2,0f));
            env.Coordinator.SaveSnapshot();

            GameObject initial=GetBoundView(env,env.Controller,"DestroyEntity Before Destroy");
            PoolItem item=initial.GetComponent<PoolItem>();

            Expect(item!=null,"ViewRollbackPlayMode DestroyEntity Error: Initial View Missing PoolItem");
            Expect(!item.IsInPool,"ViewRollbackPlayMode DestroyEntity Error: Initial View Already In Pool");

            env.Step(CreateInput(3,1f));
            env.Step(CreateInput(4,0f));

            Expect(!env.World.IsAlive(env.Controller),
                "ViewRollbackPlayMode DestroyEntity Precondition Error: Entity Was Not Destroyed");
            Expect(item.IsInPool,
                "ViewRollbackPlayMode DestroyEntity Precondition Error: Released View Is Not In Pool");
            Expect(!initial.activeSelf,
                "ViewRollbackPlayMode DestroyEntity Precondition Error: Released View Is Still Active");
            Expect(env.ViewManager.ViewCount==0,
                $"ViewRollbackPlayMode DestroyEntity Precondition Error: ViewCount={env.ViewManager.ViewCount}");

            env.Coordinator.ReceiveAuthoritativeInput(3,CreateInput(3,0f));

            var errors=new List<string>();

            if(!env.World.IsAlive(env.Controller)) errors.Add("Entity Was Not Restored");

            if(!env.Binder.TryGetView(env.Controller,out GameObject restored)||restored==null)
                errors.Add("Restored Entity Missing Binder View");
            else
            {
                if(!ReferenceEquals(initial,restored))
                    errors.Add($"GameObjectPool Did Not Reuse Released View: Old={initial.name}, Restored={restored.name}");

                PoolItem restoredItem=restored.GetComponent<PoolItem>();
                if(restoredItem==null||restoredItem.IsInPool)
                    errors.Add("Restored View PoolItem State Is Invalid");

                if(!restored.activeSelf)
                    errors.Add("Restored View Is Not Active");
            }

            if(!env.World.TryGetComponent(env.Controller,out ViewComponent view)||view.viewID<=0)
                errors.Add("Restored Entity Missing ViewComponent");

            if(!env.World.TryGetComponent(env.Controller,out ViewPrefabComponent prefab)||prefab.prefabID!=PrefabID)
                errors.Add("Restored Entity Missing Stable ViewPrefabComponent");

            if(env.ViewManager.ViewCount!=1)
                errors.Add($"Expected One Restored View, Actual={env.ViewManager.ViewCount}");

            if(CountInUsePoolItems(pool.Prefab)!=1)
                errors.Add($"Expected One InUse Pool Item, Actual={CountInUsePoolItems(pool.Prefab)}");

            ThrowIfAny(
                "DestroyedEntityRollbackRealPool",
                errors,
                $"ViewCount={env.ViewManager.ViewCount}, InUse={CountInUsePoolItems(pool.Prefab)}, PoolID={(item!=null?item.PoolID:-1)}");
        }

        /// <summary>
        /// PlayMode 下验证 Resimulate 历史表现事件会被丢弃，不会泄漏到下一正常逻辑帧重复播放。
        /// </summary>
        public static void RunRollbackViewEventPlayModeStatic()
        {
            World world=new World { EnableSystemProfile=false };

            try
            {
                Entity player=world.CreateEntity();
                world.SetComponent(player,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));

                var bridge=new CountingViewBridge();
                world.AddSystem(new RollbackEventProducerSystem(player));
                world.AddSystem(new WorldViewEventConsumer(bridge));

                var applier=new PlayerSnapshotInputApplier();
                applier.RegisterPlayer(PlayerID,player);

                var commandBuffer=new SimulationFrameCommandBuffer(64);
                var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,64);
                var adapter=new WorldRollbackAdapter<PlayerInputSnapshot>(world,world,applier,null);
                adapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));

                var coordinator=new RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot>(
                    new InputBuffer<PlayerInputSnapshot>(),
                    new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                    new SnapshotRingBuffer<EcsWorldSnapshot>(64),
                    adapter,
                    new PlayerInputSnapshotComparer(),
                    new ChecksumBuffer(),
                    new AuthoritativeChecksumBuffer())
                {
                    TickLength=TickLength
                };

                var harness=new RunnerHarness(world,coordinator);

                coordinator.SaveSnapshot();
                harness.Step(CreateInput(1,1f));
                harness.Step(CreateInput(2,0f));

                Expect(bridge.EffectCount==1,
                    $"ViewRollbackPlayMode Event Precondition Error: Expected EffectCount=1 Actual={bridge.EffectCount}");

                coordinator.ReceiveAuthoritativeInput(1,CreateInput(1,0.5f));

                int effectAfterResim=bridge.EffectCount;
                int eventCountAfterResim=world.WorldEventCount;

                harness.Step(CreateInput(3,0f));

                var errors=new List<string>();
                if(effectAfterResim!=1) errors.Add($"Effect Played During Resimulate: {effectAfterResim}");
                if(eventCountAfterResim!=0) errors.Add($"Historical Event Remained Buffered After Resimulate: {eventCountAfterResim}");
                if(bridge.EffectCount!=1) errors.Add($"Historical Effect Replayed On Next Normal Frame: {bridge.EffectCount}");
                if(world.WorldEventCount!=0) errors.Add($"WorldEventBuffer Not Empty: {world.WorldEventCount}");

                ThrowIfAny(
                    "RollbackViewEventPlayMode",
                    errors,
                    $"EffectAfterResim={effectAfterResim}, EventAfterResim={eventCountAfterResim}, FinalEffect={bridge.EffectCount}");
            }
            finally
            {
                world.Dispose();
            }
        }

        private static LifecycleEnvironment CreateLifecycleEnvironment(RealPoolScope pool,ScenarioMode mode)
        {
            var world=new World { EnableSystemProfile=false };
            var provider=new GameObjectPoolViewInstanceProvider(pool.WorldViewRoot);
            var viewManager=new ViewManager(provider);
            var binder=new EntityViewBinder(viewManager,world.IsAlive);

            viewManager.RegisterPrefab(PrefabID,pool.Prefab);

            Entity controller=world.CreateEntity();
            world.SetComponent(controller,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));
            world.SetComponent(controller,new PositionComponent(0f,0f,0f));

            if(mode!=ScenarioMode.CreateEntity)
            {
                world.SetComponent(controller,new ViewPrefabComponent(PrefabID));
                world.SetComponent(controller,new PrefabViewRequestComponent(PrefabID));
            }

            var scenario=new LifecycleScenarioSystem(controller,mode);

            world.AddSystem(scenario);
            world.AddSystem(new ViewSpawnSystem(viewManager,binder));
            world.AddSystem(new EntityViewBindingSystem(binder));
            world.AddSystem(new ViewSyncSystem(viewManager));
            world.AddSystem(new ViewDestroySystem(viewManager));
            world.AddSystem(new EntityDestroySystem(viewManager));

            var inputApplier=new PlayerSnapshotInputApplier();
            inputApplier.RegisterPlayer(PlayerID,controller);

            var commandBuffer=new SimulationFrameCommandBuffer(64);
            var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,64);
            var adapter=new WorldRollbackAdapter<PlayerInputSnapshot>(world,world,inputApplier,null);
            adapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));
            adapter.AddRollbackRestoreListener(new ViewRollbackRestoreListener(binder,viewManager));

            var snapshotBuffer=new SnapshotRingBuffer<EcsWorldSnapshot>(64);
            var coordinator=new RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot>(
                new InputBuffer<PlayerInputSnapshot>(),
                new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapshotBuffer,
                adapter,
                new PlayerInputSnapshotComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength=TickLength
            };

            return new LifecycleEnvironment(
                world,controller,viewManager,binder,scenario,coordinator,
                commandBuffer,snapshotBuffer,new RunnerHarness(world,coordinator));
        }

        private static PlayerInputSnapshot CreateInput(int frame,float moveX)
            =>new(frame,PlayerID) { moveX=moveX,moveY=0f };

        private static GameObject GetBoundView(LifecycleEnvironment env,Entity entity,string stage)
        {
            if(!env.Binder.TryGetView(entity,out GameObject view)||view==null)
                throw new InvalidOperationException($"ViewRollbackPlayMode {stage} Error: Binder View Missing, Entity={entity}");

            return view;
        }

        private static int CountInUsePoolItems(GameObject prefab)
        {
            if(prefab==null) return 0;

            int prefabInstanceID=prefab.GetInstanceID();
            PoolItem[] items=UnityEngine.Object.FindObjectsOfType<PoolItem>(true);
            int count=0;

            for(int i=0;i<items.Length;i++)
            {
                PoolItem item=items[i];
                if(item!=null&&item.PrefabInstanceID==prefabInstanceID&&!item.IsInPool)
                    count++;
            }

            return count;
        }

        private static void ThrowIfAny(string scenario,List<string> errors,string metrics)
        {
            if(errors.Count==0) return;

            throw new InvalidOperationException(
                $"ViewRollbackPlayMode {scenario} Error: {string.Join(" | ",errors)} | {metrics}");
        }

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        private enum ScenarioMode
        {
            CreateEntity,
            DestroyEntity
        }

        private sealed class LifecycleScenarioSystem : FixedStepSystemBase
        {
            private readonly Entity _controller;
            private readonly ScenarioMode _mode;

            public Entity LastCreatedEntity { get; private set; }=Entity.Invalid;
            public override SystemTickSequence sequence=>SystemTickSequence.command;

            public LifecycleScenarioSystem(Entity controller,ScenarioMode mode)
            {
                _controller=controller;
                _mode=mode;
            }

            public override void Tick(in SimulationContext context)
            {
                if(!World.IsAlive(_controller)||
                   !World.TryGetComponent(_controller,out PlayerInputSnapshotComponent input))
                    return;

                if(_mode==ScenarioMode.CreateEntity&&context.frameNumber==1&&input.moveX>0.5f)
                {
                    Entity entity=World.CreateEntity();
                    World.SetComponent(entity,new PositionComponent(2f,0f,0f));
                    World.SetComponent(entity,new ViewPrefabComponent(PrefabID));
                    World.SetComponent(entity,new PrefabViewRequestComponent(PrefabID));
                    LastCreatedEntity=entity;
                    return;
                }

                if(_mode==ScenarioMode.DestroyEntity&&context.frameNumber==3&&input.moveX>0.5f)
                    World.SetComponent(_controller,new EntityDestroyRequestComponent());
            }
        }

        private sealed class RollbackEventProducerSystem : FixedStepSystemBase
        {
            private readonly Entity _player;
            public override SystemTickSequence sequence=>SystemTickSequence.logic;

            public RollbackEventProducerSystem(Entity player)
            {
                _player=player;
            }

            public override void Tick(in SimulationContext context)
            {
                if(context.frameNumber!=1||
                   !World.IsAlive(_player)||
                   !World.TryGetComponent(_player,out PlayerInputSnapshotComponent input)||
                   input.moveX<=0f)
                    return;

                World.AddWorldEvent(new DamageWorldEvent(
                    context.frameNumber,
                    _player,
                    _player,
                    1,
                    99));
            }
        }

        private sealed class CountingViewBridge : IViewBridge
        {
            public int EffectCount { get; private set; }

            public void PlayEffect(in ViewEffectCommand command)=>EffectCount++;

            public void SyncBuffUI(Entity target,IBuffSystem buffSystem)
            {
            }
        }

        private sealed class RunnerHarness
        {
            private readonly RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> _coordinator;
            private PlayerInputSnapshot _pendingInput;

            public SimulateRunner Runner { get; }

            public RunnerHarness(World world,RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> coordinator)
            {
                _coordinator=coordinator;
                Runner=new SimulateRunner(world,TickLength,1);
                Runner.BeforeTick+=OnBeforeTick;
            }

            public void Step(PlayerInputSnapshot input)
            {
                _pendingInput=input;
                bool stepped=Runner.StepNextFrame();

                if(!stepped)
                    throw new InvalidOperationException(
                        $"ViewRollbackPlayMode RunnerHarness Error: Runner Failed At NextFrame={Runner.NextFrameNumber}");
            }

            private void OnBeforeTick(SimulationContext context)
            {
                if(_pendingInput.frameNumber!=context.frameNumber)
                    throw new InvalidOperationException(
                        $"ViewRollbackPlayMode RunnerHarness Error: InputFrame={_pendingInput.frameNumber}, TickFrame={context.frameNumber}");

                RollbackStepResult result=_coordinator.TryStep(context.frameNumber,_pendingInput);

                if(!result.Succeeded)
                    throw new InvalidOperationException(
                        $"ViewRollbackPlayMode RunnerHarness Error: Frame={context.frameNumber}, Kind={result.FailureKind}, Message={result.Message}");
            }
        }

        private sealed class LifecycleEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Controller;
            public readonly ViewManager ViewManager;
            public readonly EntityViewBinder Binder;
            public readonly LifecycleScenarioSystem Scenario;
            public readonly RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;
            public readonly RunnerHarness Harness;

            public LifecycleEnvironment(
                World world,
                Entity controller,
                ViewManager viewManager,
                EntityViewBinder binder,
                LifecycleScenarioSystem scenario,
                RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer,
                RunnerHarness harness)
            {
                World=world;
                Controller=controller;
                ViewManager=viewManager;
                Binder=binder;
                Scenario=scenario;
                Coordinator=coordinator;
                CommandBuffer=commandBuffer;
                SnapshotBuffer=snapshotBuffer;
                Harness=harness;
            }

            public void Step(PlayerInputSnapshot input)=>Harness.Step(input);

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                ViewManager.Clear();
                World.Dispose();
            }
        }

        private sealed class RealPoolScope : IDisposable
        {
            private static readonly FieldInfo SceneUIRootField=
                typeof(GameObjectPoolCenter).GetField("_sceneUIRoot",BindingFlags.Instance|BindingFlags.NonPublic);

            public readonly GameObject CenterObject;
            public readonly GameObject UIRootObject;
            public readonly GameObject WorldViewRootObject;
            public readonly GameObject Prefab;
            public readonly GameObjectPoolCenter Center;

            public Transform WorldViewRoot=>WorldViewRootObject.transform;

            private RealPoolScope(
                GameObject centerObject,
                GameObject uiRootObject,
                GameObject worldViewRootObject,
                GameObject prefab,
                GameObjectPoolCenter center)
            {
                CenterObject=centerObject;
                UIRootObject=uiRootObject;
                WorldViewRootObject=worldViewRootObject;
                Prefab=prefab;
                Center=center;
            }

            public static RealPoolScope Create()
            {
                if(GameObjectPoolCenter.Instance!=null)
                    throw new InvalidOperationException(
                        "ViewRollbackPlayMode RealPoolScope Error: Existing GameObjectPoolCenter Found. Use Empty Scene For This Test.");

                var uiRootObject=new GameObject("ViewRollbackPlayMode_UIRoot",typeof(RectTransform));
                var worldViewRootObject=new GameObject("ViewRollbackPlayMode_WorldViewRoot");
                var prefab=new GameObject("ViewRollbackPlayMode_PlayerPrefab");
                prefab.SetActive(false);

                var centerObject=new GameObject("ViewRollbackPlayMode_GameObjectPoolCenter");
                centerObject.SetActive(false);
                var center=centerObject.AddComponent<GameObjectPoolCenter>();

                if(SceneUIRootField==null)
                    throw new InvalidOperationException(
                        "ViewRollbackPlayMode RealPoolScope Error: GameObjectPoolCenter._sceneUIRoot Reflection Field Missing");

                SceneUIRootField.SetValue(center,uiRootObject.GetComponent<RectTransform>());
                centerObject.SetActive(true);

                if(!ReferenceEquals(GameObjectPoolCenter.Instance,center))
                    throw new InvalidOperationException(
                        "ViewRollbackPlayMode RealPoolScope Error: GameObjectPoolCenter Singleton Did Not Awake Correctly");

                return new RealPoolScope(centerObject,uiRootObject,worldViewRootObject,prefab,center);
            }

            public void Dispose()
            {
                Center?.ClearAllPools();
                DestroyObject(Prefab);
                DestroyObject(WorldViewRootObject);
                DestroyObject(CenterObject);
                DestroyObject(UIRootObject);
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
            if(target==null) return;
            UnityEngine.Object.Destroy(target);
        }
    }
}
