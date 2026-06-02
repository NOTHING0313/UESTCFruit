/*
 * [DEPRECATED] 反射恢复逻辑已内联到 EcsWorldSnapshot.RestoreComponentViaReflection()。
 *
 * 旧方案：独立静态工具类，供 EntitySnapshotData.Restore() 调用。
 * 新方案：作为 EcsWorldSnapshot 的 private static 方法，
 * 与其他 Capture/Restore 逻辑内聚在同一类中，减少公开 API 面。
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
