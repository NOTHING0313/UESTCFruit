using ECSFrameWork;

namespace Contracts
{

/// <summary>
/// ECS World 快照捕获与恢复接口，供外部回滚系统或调度器在稳定帧边界调用。
/// </summary>
public interface IEcsWorldSnapshotProvider
{
    /// <summary>
    /// 在稳定帧边界捕获 ECS World 快照。
    /// </summary>
    bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result);

    /// <summary>
    /// 在稳定帧边界从快照恢复 ECS World。
    /// </summary>
    bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result);
}

}
