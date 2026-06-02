using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public static class WorldChecksumCalculator
    {
        /// <summary>
        /// 计算当前 ECS World 的确定性状态校验值。
        ///
        /// 确定性保证：
        /// - Entity 按 (ID, Version) 排序后再遍历。
        /// - Component 类型按 FullName 稳定排序。
        /// - 不使用 object.GetHashCode()，只对已知值类型字段做 hash。
        ///
        /// 注意：对非已知业务组件（无法识别字段的组件）当前不参与 hash，
        /// 避免非确定性污染。新业务组件应在下面 switch 中显式添加字段 hash。
        /// </summary>
        public static uint Calculate(
            World world)
        {
            unchecked
            {
                uint hash = 17;

                var entities =
                    new List<Entity>();

                world.FillAliveEntities(
                    entities);

                // 稳定排序：先 ID 后 Version
                entities.Sort(EntityComparer.Instance);

                hash = hash * 31u + (uint)entities.Count;

                for (int i = 0;
                     i < entities.Count;
                     i++)
                {
                    Entity entity =
                        entities[i];

                    hash = hash * 31u + (uint)entity.ID;
                    hash = hash * 31u + (uint)entity.Version;

                    var componentTypes =
                        new List<Type>();

                    world.FillEntityComponentTypes(
                        entity,
                        componentTypes);

                    // 稳定排序：按 FullName
                    componentTypes.Sort(
                        TypeNameComparer.Instance);

                    for (int j = 0;
                         j < componentTypes.Count;
                         j++)
                    {
                        Type componentType =
                            componentTypes[j];

                        // FullName 确定性 hash
                        string name = componentType.FullName;
                        if (name != null)
                        {
                            for (int k = 0; k < name.Length; k++)
                                hash = hash * 31u + (uint)name[k];
                        }

                        bool success =
                            world.TryGetComponentDebugValue(
                                entity,
                                componentType,
                                out object component);

                        if (!success || component == null)
                            continue;

                        // 仅对已知值类型字段做 hash
                        AppendComponentHash(
                            componentType,
                            component,
                            ref hash);
                    }
                }

                return hash;
            }
        }

        /// <summary>
        /// 对已知业务组件的值类型字段做确定性 hash。
        /// 新增业务组件时在此方法中添加对应 hash 逻辑。
        /// </summary>
        private static void AppendComponentHash(
            Type componentType,
            object component,
            ref uint hash)
        {
            unchecked
            {
                // 使用 FullName 做 switch 不可行，改为 if/else 模式
                // 新组件在此接入即可
                string name = componentType.FullName;

                // 通用回退：如果组件实现了 IDeterministicHash，使用它
                if (component is IDeterministicHash dh)
                {
                    dh.AppendHash(ref hash);
                    return;
                }

                // 未知组件类型：跳过，避免非确定性 GetHashCode()
                // 开发者应在确认组件字段全是值类型后，在此添加 hash 逻辑
            }
        }

        /// <summary>
        /// Entity 按 (ID, Version) 稳定排序。
        /// </summary>
        private sealed class EntityComparer
            : IComparer<Entity>
        {
            public static readonly EntityComparer Instance =
                new EntityComparer();

            public int Compare(Entity x, Entity y)
            {
                int cmp = x.ID.CompareTo(y.ID);
                if (cmp != 0) return cmp;
                return x.Version.CompareTo(y.Version);
            }
        }

        /// <summary>
        /// Type 按 FullName 稳定排序。
        /// </summary>
        private sealed class TypeNameComparer
            : IComparer<Type>
        {
            public static readonly TypeNameComparer Instance =
                new TypeNameComparer();

            public int Compare(Type x, Type y)
            {
                return string.CompareOrdinal(
                    x.FullName,
                    y.FullName);
            }
        }
    }
}
