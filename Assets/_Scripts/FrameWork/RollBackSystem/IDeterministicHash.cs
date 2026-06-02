/*
 * IDeterministicHash 定义组件确定性 Hash 贡献接口。
 *
 * 对于需要参与 Checksum 但无法在 WorldChecksumCalculator 中集中处理
 * 的业务组件，可实现此接口。WorldChecksumCalculator 会在遍历到该组件时
 * 优先调用 AppendHash，避免依赖 object.GetHashCode() 的非确定性。
 *
 * 使用场景：
 * - 自定义业务组件的确定性 Checksum
 * - 回滚状态校验
 */

namespace FrameWork.RollBackSystem
{
    public interface IDeterministicHash
    {
        void AppendHash(ref uint hash);
    }
}
