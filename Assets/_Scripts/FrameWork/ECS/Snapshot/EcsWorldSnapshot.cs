using Simulation.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace ECSFrameWork
{

    /// <summary>
    /// ECS World 的可恢复快照根数据。
    /// 实现 ISnapshot 以接入 RollBackSystem 的 SnapshotRingBuffer / RollbackCoordinator。
    /// </summary>
    public sealed class EcsWorldSnapshot : ISnapshot
    {
        public int Frame => FrameNumber;
        public int FrameNumber { get; }
        public IReadOnlyList<Type> RegisteredComponentTypes { get; }
        public EcsEntityManagerSnapshot EntityManager { get; }
        public IReadOnlyList<EcsComponentStoreSnapshot> ComponentStores { get; }
        public IReadOnlyList<EcsSingletonSnapshot> Singletons { get; }

        public EcsWorldSnapshot(int frameNumber, IEnumerable<Type> registeredComponentTypes, EcsEntityManagerSnapshot entityManager, IEnumerable<EcsComponentStoreSnapshot> componentStores, IEnumerable<EcsSingletonSnapshot> singletons)
        {
            FrameNumber = frameNumber;
            RegisteredComponentTypes = CopyAsReadOnly(registeredComponentTypes);
            EntityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
            ComponentStores = CopyAsReadOnly(componentStores);
            Singletons = CopyAsReadOnly(singletons);
        }

        //--------------------------------
        // ISnapshot
        //--------------------------------

        public void Release()
        {
            // 快照数据由 GC 回收；如有池化需求可在此归还。
        }

        //--------------------------------
        // Capture
        //--------------------------------

        private static readonly MethodInfo SetComponentMethod =
            typeof(World).GetMethod(nameof(World.SetComponent));

        /// <summary>
        /// 捕获当前 ECS World 完整状态快照。
        /// </summary>
        public static EcsWorldSnapshot Capture(World world, int frame)
        {
            // 收集所有存活 Entity
            var entities = new List<Entity>();
            world.FillAliveEntities(entities);

            // 按组件类型分组收集所有组件
            var typeToComponents = new Dictionary<Type, List<EcsComponentSnapshot>>();
            var allComponentTypes = new HashSet<Type>();

            foreach (var entity in entities)
            {
                var componentTypes = new List<Type>();
                world.FillEntityComponentTypes(entity, componentTypes);

                foreach (var compType in componentTypes)
                {
                    allComponentTypes.Add(compType);

                    if (!typeToComponents.ContainsKey(compType))
                        typeToComponents[compType] = new List<EcsComponentSnapshot>();

                    if (world.TryGetComponentDebugValue(entity, compType, out object value))
                        typeToComponents[compType].Add(new EcsComponentSnapshot(entity, value));
                }
            }

            // 构建 ComponentStore 快照
            var componentStores = new List<EcsComponentStoreSnapshot>();
            int registerId = 0;
            foreach (var compType in allComponentTypes)
            {
                var dense = typeToComponents.TryGetValue(compType, out var list)
                    ? list
                    : new List<EcsComponentSnapshot>();
                componentStores.Add(new EcsComponentStoreSnapshot(compType, registerId++, dense));
            }

            // 构建 EntityManager 快照
            var slots = entities.Select(e => new EcsEntitySlotSnapshot(e.ID, e.Version, true)).ToList();
            var entityManager = new EcsEntityManagerSnapshot(entities.Count, slots, Array.Empty<int>());

            // Singleton 快照（通过公开 API 捕获受限，预留扩展）
            var singletons = new List<EcsSingletonSnapshot>();

            return new EcsWorldSnapshot(frame, allComponentTypes, entityManager, componentStores, singletons);
        }

        //--------------------------------
        // Restore
        //--------------------------------

        /// <summary>
        /// 从快照恢复 ECS World 状态。
        /// </summary>
        public static void Restore(World world, EcsWorldSnapshot snapshot)
        {
            // 销毁当前所有 Entity
            var alive = new List<Entity>();
            world.FillAliveEntities(alive);
            foreach (var entity in alive)
                world.DestroyEntity(entity);

            // 确定最大实体数（取所有 ComponentStore dense 数组的最大长度）
            int maxEntities = 0;
            foreach (var store in snapshot.ComponentStores)
            {
                if (store.DenseComponents.Count > maxEntities)
                    maxEntities = store.DenseComponents.Count;
            }

            // 按 dense 索引逐行重建：每个索引位置对应一个 Entity
            for (int i = 0; i < maxEntities; i++)
            {
                Entity entity = world.CreateEntity();

                foreach (var store in snapshot.ComponentStores)
                {
                    if (i >= store.DenseComponents.Count)
                        continue;

                    var comp = store.DenseComponents[i];
                    RestoreComponentViaReflection(world, entity, store.ComponentType, comp.ComponentValue);
                }
            }
        }

        private static void RestoreComponentViaReflection(World world, Entity entity, Type componentType, object component)
        {
            MethodInfo genericMethod = SetComponentMethod.MakeGenericMethod(componentType);
            genericMethod.Invoke(world, new object[] { entity, component });
        }

        private static ReadOnlyCollection<T> CopyAsReadOnly<T>(IEnumerable<T> source)
        {
            return Array.AsReadOnly(source != null ? source.ToArray() : Array.Empty<T>());
        }
    }

}
