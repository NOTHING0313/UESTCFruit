namespace ECSFrameWork
{

/// <summary>
/// 恢复 ECS World 快照的结果。
/// </summary>
public sealed class EcsWorldSnapshotRestoreResult
{
    public bool Success { get; }
    public string ErrorMessage { get; }
    public int RestoredEntityCount { get; }
    public int RestoredComponentCount { get; }
    public int RestoredSingletonCount { get; }

    private EcsWorldSnapshotRestoreResult(bool success, string errorMessage, int restoredEntityCount, int restoredComponentCount, int restoredSingletonCount)
    {
        Success = success;
        ErrorMessage = errorMessage ?? string.Empty;
        RestoredEntityCount = restoredEntityCount;
        RestoredComponentCount = restoredComponentCount;
        RestoredSingletonCount = restoredSingletonCount;
    }

    public static EcsWorldSnapshotRestoreResult SuccessResult(int restoredEntityCount, int restoredComponentCount, int restoredSingletonCount)
    {
        return new EcsWorldSnapshotRestoreResult(true, string.Empty, restoredEntityCount, restoredComponentCount, restoredSingletonCount);
    }

    public static EcsWorldSnapshotRestoreResult Failure(string errorMessage)
    {
        return new EcsWorldSnapshotRestoreResult(false, errorMessage, 0, 0, 0);
    }
}

}
