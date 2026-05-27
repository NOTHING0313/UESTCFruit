/*
 * 文件说明：WorldChecksumUtility 负责对 ECS World 当前状态计算确定性校验值。
 * 设计约束：
 * 1. 相同 World 状态必须生成完全一致的 Checksum。
 * 2. Checksum 只依赖逻辑数据，不依赖 Unity 对象或运行时引用。
 * 3. 所有参与同步与回滚的重要组件都应纳入计算。
 */

using BuffSystem;
using ECSFrameWork;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public static class WorldChecksumUtility
    {
        /// <summary>
        /// 计算当前 ECS World 的状态校验值。
        /// </summary>
        public static uint Calculate(
            World world)
        {
            unchecked
            {
                uint hash = 17;

                List<Entity> entities =
                    new List<Entity>();

                world.FillAliveEntities(
                    entities);

                //--------------------------------
                // Entity Count
                //--------------------------------

                hash =
                    hash * 31u
                    + (uint)entities.Count;

                //--------------------------------
                // Components
                //--------------------------------

                for (int i = 0;
                    i < entities.Count;
                    i++)
                {
                    Entity entity =
                        entities[i];

                    //--------------------------------
                    // Health
                    //--------------------------------

                    if (world.TryGetComponent(
                        entity,
                        out HealthComponent health))
                    {
                        hash =
                            hash * 31u
                            + (uint)health.current;

                        hash =
                            hash * 31u
                            + (uint)health.max;
                    }

                    //--------------------------------
                    // Position
                    //--------------------------------

                    if (world.TryGetComponent(
                        entity,
                        out PositionComponent position))
                    {
                        hash =
                            hash * 31u
                            + (uint)position.x.GetHashCode();

                        hash =
                            hash * 31u
                            + (uint)position.y.GetHashCode();

                        hash =
                            hash * 31u
                            + (uint)position.z.GetHashCode();
                    }

                    //--------------------------------
                    // BuffRuntime
                    //--------------------------------

                    if (world.TryGetComponent(
                        entity,
                        out BuffRuntimeComponent buff))
                    {
                        hash =
                            hash * 31u
                            + (uint)buff.configId;

                        hash =
                            hash * 31u
                            + (uint)buff.runtimeHandle;

                        hash =
                            hash * 31u
                            + (uint)buff.stack;

                        hash =
                            hash * 31u
                            + (uint)buff.remainingFrames;
                    }
                }

                return hash;
            }
        }
    }
}