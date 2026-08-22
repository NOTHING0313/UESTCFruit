param(
    [string]$LogDir=".\Builds\Network2P\Network2PLogs"
)

$ErrorActionPreference="Stop"

$logs=@(
    (Join-Path $LogDir "Player1.log")
    (Join-Path $LogDir "Player2.log")
)

foreach($log in $logs) {
    Write-Host ""
    Write-Host "===== $log ====="

    if(!(Test-Path $log)) {
        Write-Host "MISSING"
        continue
    }

    Write-Host "--- Runtime Summary ---"
    Select-String -Path $log -Pattern "LogRuntimeSummary"

    Write-Host "--- View Audit Error ---"
    Select-String -Path $log -Pattern "NetworkViewRollbackRuntimeAudit Sample Error"

    Write-Host "--- Runtime / Pool / Exception Errors ---"
    Select-String -Path $log -Pattern "FailAndStop|GameObjectPoolCenter .* Error:|ViewRollbackRestoreListener .* Warning:|Exception| Error:"
}
