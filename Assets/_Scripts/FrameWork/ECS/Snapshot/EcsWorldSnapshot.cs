using Simulation.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ECSFrameWork
{

    /// <summary>
    /// ECS World 的可恢复快照根数据（纯 DTO）。
    /// 实现 ISnapshot 以接入 RollBackSystem 的 SnapshotRingBuffer / RollbackCoordinator。
    ///
    /// Capture / Restore 逻辑由 ECS Core 的 World 类提供：
    ///   World.TryCaptureSnapshot(frame, out snapshot, out result)
    ///   World.TryRestoreSnapshot(snapshot, out result)
    ///
    /// EcsWorldSnapshot 本身不包含业务逻辑，只承载快照数据。
    /// 调用方应将其视为只读数据，不要修改其内容。
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

        private static ReadOnlyCollection<T> CopyAsReadOnly<T>(IEnumerable<T> source)
        {
            return Array.AsReadOnly(source != null ? source.ToArray() : Array.Empty<T>());
        }
    }

}
