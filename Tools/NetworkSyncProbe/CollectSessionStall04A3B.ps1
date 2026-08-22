param(
    [string]$LogDir = ".\Builds\Network2P\Network2PLogs"
)

$logs=@(
    (Join-Path $LogDir "Player1.log")
    (Join-Path $LogDir "Player2.log")
)

foreach($log in $logs)
{
    Write-Host ""
    Write-Host "===== $log ====="

    if(-not (Test-Path $log))
    {
        Write-Host "MISSING"
        continue
    }

    Write-Host "--- Session Stall / Resume ---"
    Select-String -Path $log -Pattern "EvaluateSessionStall (Warning|Log):" | ForEach-Object { $_.Line }

    Write-Host "--- Runtime Summary ---"
    Select-String -Path $log -Pattern "NetworkRollbackBootstrap LogRuntimeSummary Log:" | ForEach-Object { $_.Line }

    Write-Host "--- Runtime / Pool / View / Exception Errors ---"
    Select-String -Path $log -Pattern " Error:|Exception|GameObjectPoolCenter Release Error|NetworkViewRollbackRuntimeAudit Sample Error|ViewRollbackRestoreListener .*Warning:" | ForEach-Object { $_.Line }
}
