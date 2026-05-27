/*
 * 文件说明：
 * ComponentSnapshotData 用于保存单个组件快照数据。
 *
 * 设计目标：
 * 1. 保存组件类型。
 * 2. 保存组件运行时值。
 * 3. 用于 EntitySnapshotData 序列化组件状态。
 *
 * 使用场景：
 * - EntitySnapshotData
 * - WorldSnapshot
 * - 回滚恢复
 */

using System;

namespace FrameWork.RollBackSystem
{
    [Serializable]
    public sealed class ComponentSnapshotData
    {
        /// <summary>
        /// 组件类型。
        /// </summary>
        public Type ComponentType;

        /// <summary>
        /// 组件数据副本。
        /// </summary>
        public object ComponentValue;

        public ComponentSnapshotData(
            Type componentType,
            object componentValue)
        {
            ComponentType = componentType;

            ComponentValue = componentValue;
        }
    }
}