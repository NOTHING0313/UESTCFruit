/*
 * [DEPRECATED] 已被 ECS Core World 的 TryCaptureSnapshot / TryRestoreSnapshot 替代。
 *
 * ECS Core 的 World 已实现 IEcsWorldSnapshotProvider，提供：
 *   World.TryCaptureSnapshot(frame, out snapshot, out result)
 *   World.TryRestoreSnapshot(snapshot, out result)
 *
 * 且已通过 Entity ID/Version 恢复测试（见 ECS_WorldSnapshot_Interface_For_RollBackSystem_Reviewed.md §14）。
 *
 * ReflectionComponentRestore 中通过反射绕过 ECS 内部数据结构的方式
 * 被 World.SetComponent 泛型调用的直接使用取代；不再需要此工具类。
 *
 * 保留此文件仅用于参考对比，不可再被编译引用。
 *

using ECSFrameWork;
using System;
using System.Reflection;

namespace FrameWork.RollBackSystem
{
    public static class ReflectionComponentRestore
    {
        private static readonly MethodInfo
            SetComponentMethod =
                typeof(World)
                .GetMethod(
                    nameof(World.SetComponent));

        public static void RestoreComponent(
            World world,
            Entity entity,
            Type componentType,
            object component)
        {
            MethodInfo genericMethod =
                SetComponentMethod
                    .MakeGenericMethod(
                        componentType);

            object[] args =
            {
                entity,
                component
            };

            genericMethod.Invoke(
                world,
                args);
        }
    }
}
*/
