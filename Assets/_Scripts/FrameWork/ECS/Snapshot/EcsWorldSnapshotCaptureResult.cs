namespace ECSFrameWork
{

/// <summary>
/// 捕获 ECS World 快照的结果。
/// </summary>
public sealed class EcsWorldSnapshotCaptureResult
{
    public bool Success { get; }
    public string ErrorMessage { get; }
    public EcsWorldSnapshot Snapshot { get; }

    private EcsWorldSnapshotCaptureResult(bool success, string errorMessage, EcsWorldSnapshot snapshot)
    {
        Success = success;
        ErrorMessage = errorMessage ?? string.Empty;
        Snapshot = snapshot;
    }

    public static EcsWorldSnapshotCaptureResult SuccessResult(EcsWorldSnapshot snapshot)
    {
        return new EcsWorldSnapshotCaptureResult(true, string.Empty, snapshot);
    }

    public static EcsWorldSnapshotCaptureResult Failure(string errorMessage)
    {
        return new EcsWorldSnapshotCaptureResult(false, errorMessage, null);
    }
}

}
