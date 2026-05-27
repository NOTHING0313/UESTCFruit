/*
 * 文件说明：
 * ReflectionComponentRestore 用于通过反射恢复 ECS 组件。
 *
 * 设计目标：
 * 1. 支持运行时动态组件恢复。
 * 2. 避免 WorldSnapshot 依赖具体组件类型。
 * 3. 统一 Snapshot Restore 流程。
 *
 * 设计说明：
 * World.SetComponent<T>() 是泛型方法，
 * 回滚恢复阶段只能通过反射动态调用。
 *
 * 使用场景：
 * - WorldSnapshot.Restore
 * - EntitySnapshotData.Restore
 * - ECS 回滚恢复
 */

using ECSFrameWork;
using System;
using System.Reflection;

namespace FrameWork.RollBackSystem
{
    public static class ReflectionComponentRestore
    {
        /// <summary>
        /// World.SetComponent 泛型方法缓存。
        /// </summary>
        private static readonly MethodInfo
            SetComponentMethod =
                typeof(World)
                .GetMethod(
                    nameof(World.SetComponent));

        /// <summary>
        /// 动态恢复指定组件。
        /// </summary>
        public static void RestoreComponent(
            World world,
            Entity entity,
            Type componentType,
            object component)
        {
            //--------------------------------
            // Create Generic Method
            //--------------------------------

            MethodInfo genericMethod =
                SetComponentMethod
                    .MakeGenericMethod(
                        componentType);

            //--------------------------------
            // Build Arguments
            //--------------------------------

            object[] args =
            {
                entity,
                component
            };

            //--------------------------------
            // Invoke SetComponent<T>
            //--------------------------------

            genericMethod.Invoke(
                world,
                args);
        }
    }
}