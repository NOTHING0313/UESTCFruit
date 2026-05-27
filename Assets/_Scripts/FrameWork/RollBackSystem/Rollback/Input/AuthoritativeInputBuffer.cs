/*
 * 文件说明：
 * AuthoritativeInputBuffer 用于保存服务器权威输入。
 *
 * 设计目标：
 * 1. 保存服务器确认后的真实输入数据。
 * 2. 与本地预测输入进行比对。
 * 3. 为 RollbackCoordinator 提供回滚依据。
 * 4. 不负责回滚逻辑，只负责数据存储。
 *
 * 使用场景：
 * - 客户端收到服务器输入同步包
 * - 帧同步预测修正
 * - 回滚输入校验
 */

using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class AuthoritativeInputBuffer<TInput>
    {
        private readonly Dictionary<int, TInput> _inputs
            = new Dictionary<int, TInput>();

        /// <summary>
        /// 保存指定帧的服务器权威输入。
        /// </summary>
        public void Save(int frame, in TInput input)
        {
            _inputs[frame] = input;
        }

        /// <summary>
        /// 尝试获取指定帧的服务器权威输入。
        /// </summary>
        public bool TryGet(int frame, out TInput input)
        {
            return _inputs.TryGetValue(frame, out input);
        }
    }
}