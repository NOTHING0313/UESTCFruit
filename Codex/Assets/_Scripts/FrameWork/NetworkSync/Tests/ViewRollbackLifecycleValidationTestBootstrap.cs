using BuffSystem;
using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem;
using System;
using System.Collections.Generic;
using UnityEngine;
using View;

namespace FrameWork.NetworkSync
{
    /// <summary>
    /// View Rollback 生命周期压力审计。
    /// 本阶段只新增测试，不修改生产 View / Rollback 代码。
    /// </summary>
    public static class ViewRollbackLifecycleValidationTestBootstrap
    {
        private const int PlayerID=1;
        private const float TickLength=1f/60f;

        /// <summary>
        /// Snapshot 后创建 Entity + View，随后 Authority 修正为“不创建”。
        /// Rollback 到创建前必须释放旧 View，不能留下 Orphan。
        /// </summary>
        public static void RunCreatedEntityRemovedByRollbackReleasesViewStatic()
        {
            using var env=CreateLifecycleEnvironment(LifecycleMode.CreateEntity, false);

            env.Coordinator.SaveSnapshot();

            DriveFrame(env,1,CreateInput(1,1f));
            DriveFrame(env,2,CreateInput(2,0f));

            Entity created=env.Scenario.LastCreatedEntity;
            Expect(created.IsValid,"ViewRollbackLifecycle CreateEntity Error: Scenario Did Not Create Entity");
            Expect(env.ViewManager.ViewCount==1,$"ViewRollbackLifecycle CreateEntity Error: Expected One View Before Rollback, Actual={env.ViewManager.ViewCount}");
            Expect(env.Provider.LiveCount==1,$"ViewRollbackLifecycle CreateEntity Error: Expected One Live View Before Rollback, Actual={env.Provider.LiveCount}");

            env.Coordinator.ReceiveAuthoritativeInput(1,CreateInput(1,0f));

            var errors=new List<string>();
            if(env.Coordinator.CurrentFrame!=2) errors.Add($"Coordinator Frame Expected=2 Actual={env.Coordinator.CurrentFrame}");
            if(env.World.IsAlive(created)) errors.Add($"Created Entity Still Alive After Corrected Rollback: {created}");
            if(env.ViewManager.ViewCount!=0) errors.Add($"Orphan ViewManager Entry Remains: ViewCount={env.ViewManager.ViewCount}");
            if(env.Provider.LiveCount!=0) errors.Add($"Orphan GameObject Remains: LiveCount={env.Provider.LiveCount}");
            if(env.Provider.ReleaseCount!=1) errors.Add($"Expected Exactly One Release, Actual={env.Provider.ReleaseCount}");
            if(env.Binder.TryGetView(created,out _)) errors.Add("Binder Still Resolves Rolled-Back Created Entity");

            ThrowIfAny(
                "CreatedEntityRemovedByRollback",
                errors,
                $"Spawn={env.Provider.SpawnCount}, Release={env.Provider.ReleaseCount}, Live={env.Provider.LiveCount}, ViewCount={env.ViewManager.ViewCount}");
        }

        /// <summary>
        /// 已拥有 View 的 Entity 被预测销毁并 Release，Authority 随后修正为“不销毁”。
        /// Rollback Restore 后 Entity 与 View 都应恢复且重新绑定。
        /// </summary>
        public static void RunDestroyedEntityRestoredByRollbackRecoversViewStatic()
        {
            using var env=CreateLifecycleEnvironment(LifecycleMode.DestroyEntity, false);

            // F1：ViewSpawnSystem Spawn；ViewComponent 在 Tick 末尾通过 StructuralChangeBuffer 生效。
            DriveFrame(env,1,CreateInput(1,0f));
            // F2：EntityViewBindingSystem 才能看到上一帧新增的 ViewComponent 并完成 Binder 绑定。
            DriveFrame(env,2,CreateInput(2,0f));
            env.Coordinator.SaveSnapshot();

            Expect(env.World.IsAlive(env.Controller),"ViewRollbackLifecycle DestroyEntity Precondition Error: Controller Is Not Alive");
            Expect(env.ViewManager.ViewCount==1,$"ViewRollbackLifecycle DestroyEntity Precondition Error: ViewCount={env.ViewManager.ViewCount}");
            Expect(env.Binder.TryGetView(env.Controller,out GameObject boundBeforeDestroy)&&boundBeforeDestroy!=null,
                "ViewRollbackLifecycle DestroyEntity Precondition Error: Binder Was Not Ready Before Destroy");

            // F3：Scenario 新增 EntityDestroyRequest；请求在 Tick 末尾生效。
            DriveFrame(env,3,CreateInput(3,1f));
            // F4：EntityDestroySystem 消费上一帧请求并 Release View / Destroy Entity。
            DriveFrame(env,4,CreateInput(4,0f));

            Expect(!env.World.IsAlive(env.Controller),"ViewRollbackLifecycle DestroyEntity Precondition Error: Entity Was Not Destroyed After Structural Playback");
            Expect(env.Provider.ReleaseCount==1,$"ViewRollbackLifecycle DestroyEntity Precondition Error: Expected Release=1 Actual={env.Provider.ReleaseCount}");

            // 修正真正导致 DestroyRequest 的 F3 输入，Rollback 到 F2 Snapshot。
            env.Coordinator.ReceiveAuthoritativeInput(3,CreateInput(3,0f));

            var errors=new List<string>();
            if(!env.World.IsAlive(env.Controller)) errors.Add("Entity Was Not Restored");
            if(env.ViewManager.ViewCount!=1) errors.Add($"Restored Entity Has No Single View: ViewCount={env.ViewManager.ViewCount}");
            if(env.Provider.LiveCount!=1) errors.Add($"Restored Entity Has No Live View: LiveCount={env.Provider.LiveCount}");
            if(!env.World.TryGetComponent(env.Controller,out ViewComponent view)||view.viewID<=0)
                errors.Add("Restored Entity Missing ViewComponent");
            if(!env.Binder.TryGetView(env.Controller,out GameObject boundView)||boundView==null)
                errors.Add("Restored Entity Missing Binder View");

            ThrowIfAny(
                "DestroyedEntityRestoredByRollback",
                errors,
                $"Spawn={env.Provider.SpawnCount}, Release={env.Provider.ReleaseCount}, Live={env.Provider.LiveCount}, ViewCount={env.ViewManager.ViewCount}");
        }

        /// <summary>
        /// 已存在 View 被预测 ViewDestroyRequest Release，Authority 修正为“不销毁”。
        /// 使用可复用 Provider 验证 Release -> Respawn / Rebind 后只存在一个有效对象。
        /// </summary>
        public static void RunPooledViewDestroyRollbackRestoresSingleViewStatic()
        {
            using var env=CreateLifecycleEnvironment(LifecycleMode.DestroyView, true);

            // F1 Spawn，F2 Binder 才能稳定拿到 View。
            DriveFrame(env,1,CreateInput(1,0f));
            DriveFrame(env,2,CreateInput(2,0f));
            env.Coordinator.SaveSnapshot();

            GameObject initialView=GetBoundView(env,"Pool Precondition");

            // F3 新增 ViewDestroyRequest；F4 才由 ViewDestroySystem 消费。
            DriveFrame(env,3,CreateInput(3,1f));
            DriveFrame(env,4,CreateInput(4,0f));

            Expect(env.World.IsAlive(env.Controller),"ViewRollbackLifecycle Pool Precondition Error: Entity Should Remain Alive");
            Expect(env.ViewManager.ViewCount==0,$"ViewRollbackLifecycle Pool Precondition Error: View Was Not Released, Count={env.ViewManager.ViewCount}");
            Expect(env.Provider.ReleaseCount==1,$"ViewRollbackLifecycle Pool Precondition Error: Release={env.Provider.ReleaseCount}");

            // 修正产生 ViewDestroyRequest 的 F3。
            env.Coordinator.ReceiveAuthoritativeInput(3,CreateInput(3,0f));

            var errors=new List<string>();
            if(!env.World.IsAlive(env.Controller)) errors.Add("Entity Unexpectedly Missing After View-Only Rollback");
            if(env.ViewManager.ViewCount!=1) errors.Add($"Expected One Recovered View, Actual={env.ViewManager.ViewCount}");
            if(env.Provider.LiveCount!=1) errors.Add($"Expected One Live Pooled View, Actual={env.Provider.LiveCount}");
            if(env.Provider.ReleaseCount!=1) errors.Add($"Expected One Pool Release, Actual={env.Provider.ReleaseCount}");
            if(env.Provider.SpawnCount<2) errors.Add($"Expected Respawn After Pool Release, SpawnCount={env.Provider.SpawnCount}");

            if(env.Binder.TryGetView(env.Controller,out GameObject restoredView)&&restoredView!=null)
            {
                if(!ReferenceEquals(initialView,restoredView))
                    errors.Add("Reusable Provider Did Not Reuse Released Instance");
            }
            else
            {
                errors.Add("Binder Missing Recovered Pooled View");
            }

            ThrowIfAny(
                "PooledViewDestroyRollback",
                errors,
                $"Spawn={env.Provider.SpawnCount}, Release={env.Provider.ReleaseCount}, Reuse={env.Provider.ReuseCount}, Live={env.Provider.LiveCount}, ViewCount={env.ViewManager.ViewCount}");
        }

        /// <summary>
        /// 一个已经在正常帧播放过的表现事件，Rollback/Resimulate 后不能在下一正常帧再次播放。
        /// </summary>
        public static void RunConsumedViewEventIsNotReplayedAfterRollbackStatic()
        {
            World world=new World { EnableSystemProfile=false };
            var bridge=new CountingViewBridge();

            try
            {
                Entity player=world.CreateEntity();
                world.SetComponent(player,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));
                world.AddSystem(new RollbackEventProducerSystem(player));
                world.AddSystem(new WorldViewEventConsumer(bridge));

                var applier=new PlayerSnapshotInputApplier();
                applier.RegisterPlayer(PlayerID,player);

                var commandBuffer=new SimulationFrameCommandBuffer(64);
                var commandApplier=new SimulationFrameCommandApplier(world,commandBuffer,64);
                var rollbackAdapter=new WorldRollbackAdapter<PlayerInputSnapshot>(world,world,applier,null);
                rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));

                var coordinator=new RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot>(
                    new InputBuffer<PlayerInputSnapshot>(),
                    new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                    new SnapshotRingBuffer<EcsWorldSnapshot>(64),
                    rollbackAdapter,
                    new PlayerInputSnapshotComparer(),
                    new ChecksumBuffer(),
                    new AuthoritativeChecksumBuffer())
                {
                    TickLength=TickLength
                };

                coordinator.SaveSnapshot();

                DriveFrame(world,coordinator,1,CreateInput(1,1f));
                Expect(bridge.EffectCount==1,$"ViewRollbackLifecycle Event Precondition Error: Expected EffectCount=1 Actual={bridge.EffectCount}");
                Expect(world.WorldEventCount==0,$"ViewRollbackLifecycle Event Precondition Error: WorldEventCount={world.WorldEventCount}");

                DriveFrame(world,coordinator,2,CreateInput(2,0f));

                // 输入不同以触发 rollback，但两种输入都满足 producer 的 >0 条件，
                // 因而 Frame1 的同一逻辑表现事件会在 Resimulate 中再次产生。
                coordinator.ReceiveAuthoritativeInput(1,CreateInput(1,0.5f));

                int effectsAfterResimulate=bridge.EffectCount;
                int bufferedAfterResimulate=world.WorldEventCount;

                DriveFrame(world,coordinator,3,CreateInput(3,0f));

                var errors=new List<string>();
                if(effectsAfterResimulate!=1)
                    errors.Add($"Effect Was Played During Resimulate: Count={effectsAfterResimulate}");
                if(bridge.EffectCount!=1)
                    errors.Add($"Consumed Historical Effect Replayed After Rollback: Count={bridge.EffectCount}");
                if(world.WorldEventCount!=0)
                    errors.Add($"WorldEventBuffer Not Empty After Normal Consumer: Count={world.WorldEventCount}");

                ThrowIfAny(
                    "ConsumedViewEventRollback",
                    errors,
                    $"EffectAfterResim={effectsAfterResimulate}, BufferedAfterResim={bufferedAfterResimulate}, FinalEffect={bridge.EffectCount}");
            }
            finally
            {
                world.Dispose();
            }
        }

        private static LifecycleEnvironment CreateLifecycleEnvironment(LifecycleMode mode,bool reuseReleasedInstance)
        {
            var world=new World { EnableSystemProfile=false };
            var provider=new TrackingViewInstanceProvider(reuseReleasedInstance);
            var viewManager=new ViewManager(provider);
            var binder=new EntityViewBinder(viewManager,world.IsAlive);
            var prefab=new GameObject("ViewRollbackLifecyclePrefab");
            prefab.SetActive(false);
            viewManager.RegisterPrefab(1,prefab);

            Entity controller=world.CreateEntity();
            world.SetComponent(controller,new PlayerInputSnapshotComponent(0,PlayerID,0f,0f));
            world.SetComponent(controller,new PositionComponent(0f,0f,0f));

            if(mode!=LifecycleMode.CreateEntity)
                world.SetComponent(controller,new PrefabViewRequestComponent(1));

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
            var rollbackAdapter=new WorldRollbackAdapter<PlayerInputSnapshot>(world,world,inputApplier,null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer,commandApplier));
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

            return new LifecycleEnvironment(
                world,controller,prefab,provider,viewManager,binder,scenario,
                coordinator,commandBuffer,snapshotBuffer);
        }

        private static void DriveFrame(LifecycleEnvironment env,int frame,PlayerInputSnapshot input)
            =>DriveFrame(env.World,env.Coordinator,frame,input);

        private static void DriveFrame(
            World world,
            RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> coordinator,
            int frame,
            PlayerInputSnapshot input)
        {
            RollbackStepResult result=coordinator.TryStep(frame,input);
            Expect(result.Succeeded,
                $"ViewRollbackLifecycle DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context=new SimulationContext(frame,TickLength,false);
            world.Tick(in context);
        }

        private static PlayerInputSnapshot CreateInput(int frame,float moveX)
            =>new(frame,PlayerID) { moveX=moveX,moveY=0f };

        private static GameObject GetBoundView(LifecycleEnvironment env,string stage)
        {
            if(!env.Binder.TryGetView(env.Controller,out GameObject view)||view==null)
                throw new InvalidOperationException($"ViewRollbackLifecycle {stage} Error: Binder View Missing");

            return view;
        }

        private static void ThrowIfAny(string scenario,List<string> errors,string metrics)
        {
            if(errors.Count==0) return;
            throw new InvalidOperationException(
                $"ViewRollbackLifecycle {scenario} Error: {string.Join(" | ",errors)} | {metrics}");
        }

        private static void Expect(bool condition,string message)
        {
            if(!condition) throw new InvalidOperationException(message);
        }

        private enum LifecycleMode
        {
            CreateEntity,
            DestroyEntity,
            DestroyView
        }

        private sealed class LifecycleScenarioSystem : FixedStepSystemBase
        {
            private readonly Entity _controller;
            private readonly LifecycleMode _mode;

            public Entity LastCreatedEntity { get; private set; }=Entity.Invalid;
            public override SystemTickSequence sequence=>SystemTickSequence.command;

            public LifecycleScenarioSystem(Entity controller,LifecycleMode mode)
            {
                _controller=controller;
                _mode=mode;
            }

            public override void Tick(in SimulationContext context)
            {
                if(!World.IsAlive(_controller)||
                   !World.TryGetComponent(_controller,out PlayerInputSnapshotComponent input))
                    return;

                if(_mode==LifecycleMode.CreateEntity&&context.frameNumber==1&&input.moveX>0.5f)
                {
                    Entity entity=World.CreateEntity();
                    World.SetComponent(entity,new PositionComponent(2f,0f,0f));
                    World.SetComponent(entity,new PrefabViewRequestComponent(1));
                    LastCreatedEntity=entity;
                    return;
                }

                if(context.frameNumber!=3||input.moveX<=0.5f)
                    return;

                if(_mode==LifecycleMode.DestroyEntity)
                    World.SetComponent(_controller,new EntityDestroyRequestComponent());
                else if(_mode==LifecycleMode.DestroyView)
                    World.SetComponent(_controller,new ViewDestroyRequestComponent());
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

        private sealed class TrackingViewInstanceProvider : IViewInstanceProvider
        {
            private readonly bool _reuseReleasedInstance;
            private readonly List<GameObject> _live=new();
            private GameObject _released;

            public int SpawnCount { get; private set; }
            public int ReleaseCount { get; private set; }
            public int ReuseCount { get; private set; }
            public int LiveCount=>_live.Count;

            public TrackingViewInstanceProvider(bool reuseReleasedInstance)
            {
                _reuseReleasedInstance=reuseReleasedInstance;
            }

            public GameObject Spawn(GameObject prefab,Vector3 position,Quaternion rotation)
            {
                SpawnCount++;

                GameObject instance=null;
                if(_reuseReleasedInstance&&_released!=null)
                {
                    instance=_released;
                    _released=null;
                    ReuseCount++;
                }

                if(instance==null)
                    instance=new GameObject($"ViewRollbackLifecycleInstance_{SpawnCount}");

                instance.transform.SetPositionAndRotation(position,rotation);
                instance.SetActive(true);
                _live.Add(instance);
                return instance;
            }

            public void Release(GameObject instance)
            {
                if(instance==null) return;

                ReleaseCount++;
                _live.Remove(instance);
                instance.SetActive(false);

                if(_reuseReleasedInstance&&_released==null)
                    _released=instance;
                else
                    DestroyObject(instance);
            }

            public void Clear()
            {
                for(int i=_live.Count-1;i>=0;i--)
                    DestroyObject(_live[i]);

                _live.Clear();

                if(_released!=null)
                {
                    DestroyObject(_released);
                    _released=null;
                }
            }
        }

        private sealed class LifecycleEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity Controller;
            public readonly GameObject Prefab;
            public readonly TrackingViewInstanceProvider Provider;
            public readonly ViewManager ViewManager;
            public readonly EntityViewBinder Binder;
            public readonly LifecycleScenarioSystem Scenario;
            public readonly RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public LifecycleEnvironment(
                World world,
                Entity controller,
                GameObject prefab,
                TrackingViewInstanceProvider provider,
                ViewManager viewManager,
                EntityViewBinder binder,
                LifecycleScenarioSystem scenario,
                RollbackCoordinator<PlayerInputSnapshot,EcsWorldSnapshot> coordinator,
                SimulationFrameCommandBuffer commandBuffer,
                SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World=world;
                Controller=controller;
                Prefab=prefab;
                Provider=provider;
                ViewManager=viewManager;
                Binder=binder;
                Scenario=scenario;
                Coordinator=coordinator;
                CommandBuffer=commandBuffer;
                SnapshotBuffer=snapshotBuffer;
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
