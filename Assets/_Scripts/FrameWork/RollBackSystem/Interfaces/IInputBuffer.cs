/*
 * 文件说明：IInputBuffer 定义历史输入缓存接口，用于保存、查询和清理逻辑帧输入。
 * 设计约束：输入必须按逻辑帧编号存储，保证回滚与重模拟时可重复读取同一帧输入。
 */

namespace FrameWork.RollBackSystem.Interfaces
{
    public interface IInputBuffer<TInput>
    {
        void Save(
            int frame,
            TInput input);

        bool TryGet(
            int frame,
            out TInput input);

        void ClearBefore(int frame);
    }
}