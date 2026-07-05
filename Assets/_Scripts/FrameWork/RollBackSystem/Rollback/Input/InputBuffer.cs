/*
 * 文件说明：
 * InputBuffer 用于缓存本地预测输入。
 *
 * 设计目标：
 * 1. 保存每一逻辑帧的本地输入。
 * 2. 支持回滚重模拟时重新读取输入。
 * 3. 支持旧帧输入清理（零 GC）。
 *
 * 使用场景：
 * - 本地输入预测
 * - 回滚重模拟
 * - 输入历史缓存
 */

using FrameWork.RollBackSystem.Interfaces;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class InputBuffer<TInput>
        : IInputBuffer<TInput>
    {
        private readonly Dictionary<int, TInput>
            _inputs = new();

        // 复用缓冲区，避免 ClearBefore 中产生 GC
        private readonly List<int> _recycleKeys = new();

        /// <summary>
        /// 保存指定帧输入。
        /// </summary>
        public void Save(
            int frame,
            TInput input)
        {
            _inputs[frame] = input;
        }

        /// <summary>
        /// 尝试获取指定帧输入。
        /// </summary>
        public bool TryGet(
            int frame,
            out TInput input)
        {
            return _inputs.TryGetValue(
                frame,
                out input);
        }

        /// <summary>
        /// 清理指定帧之前的输入数据（不含该帧）。
        /// 使用成员缓冲区，零 GC。
        /// </summary>
        public void ClearBefore(int frame)
        {
            _recycleKeys.Clear();

            foreach (var pair in _inputs)
            {
                if (pair.Key >= frame)
                    continue;

                _recycleKeys.Add(pair.Key);
            }

            for (int i = 0; i < _recycleKeys.Count; i++)
            {
                _inputs.Remove(_recycleKeys[i]);
            }
        }
    }
}