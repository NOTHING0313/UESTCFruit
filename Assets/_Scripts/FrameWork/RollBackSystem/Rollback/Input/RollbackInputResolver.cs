/*
 * 文件说明：
 * RollbackInputResolver 用于比较本地预测输入与服务器权威输入。
 *
 * 设计目标：
 * 1. 检测输入预测是否错误。
 * 2. 统一输入比较逻辑。
 * 3. 为 RollbackCoordinator 提供回滚判断依据。
 *
 * 使用场景：
 * - 客户端收到服务器输入后
 * - 输入预测校验
 * - 回滚触发检测
 */

using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackInputResolver<TInput>
    {
        private readonly IInputBuffer<TInput>
            _predictedBuffer;

        private readonly AuthoritativeInputBuffer<TInput>
            _authoritativeBuffer;

        private readonly IInputComparer<TInput>
            _comparer;

        public RollbackInputResolver(
            IInputBuffer<TInput> predictedBuffer,
            AuthoritativeInputBuffer<TInput>
                authoritativeBuffer,
            IInputComparer<TInput> comparer)
        {
            _predictedBuffer =
                predictedBuffer;

            _authoritativeBuffer =
                authoritativeBuffer;

            _comparer = comparer;
        }

        /// <summary>
        /// 比较指定帧的预测输入与服务器输入。
        /// </summary>
        public InputComparisonResult Compare(
            int frame)
        {
            bool predictedFound =
                _predictedBuffer.TryGet(
                    frame,
                    out var predicted);

            bool authoritativeFound =
                _authoritativeBuffer.TryGet(
                    frame,
                    out var authoritative);

            if (!predictedFound ||
                !authoritativeFound)
            {
                return new InputComparisonResult(
                    false,
                    frame);
            }

            bool isEqual =
                _comparer.IsEqual(
                    predicted,
                    authoritative);

            return new InputComparisonResult(
                !isEqual,
                frame);
        }
    }
}