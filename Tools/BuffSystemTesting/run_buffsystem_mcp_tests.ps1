param(
    [string]$ProjectRoot = "",
    [string]$Method = "BuffSystem.EditorTesting.BuffSystemMcpTestEntry.RunAllBuffSystemTests",
    [switch]$WaitForReport,
    [int]$TimeoutSeconds = 120
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectRoot)) {
    $ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
}

$reportJson = Join-Path $ProjectRoot "Temp\BuffSystemTestReports\latest.json"
$reportMarkdown = Join-Path $ProjectRoot "Temp\BuffSystemTestReports\latest.md"

Write-Host "BuffSystem MCP test helper"
Write-Host "ProjectRoot: $ProjectRoot"
Write-Host "Unity execute method: $Method"
Write-Host ""
Write-Host "本脚本不硬编码 Unity MCP API。请通过当前可用的 Unity MCP bridge 或 Unity -executeMethod 调用上述静态方法。"
Write-Host "调用完成后，本脚本可用 -WaitForReport 等待并读取 Temp/BuffSystemTestReports/latest.json。"

if (-not $WaitForReport) {
    Write-Host ""
    Write-Host "未指定 -WaitForReport；仅输出调用信息。"
    exit 0
}

$deadline = (Get-Date).AddSeconds($TimeoutSeconds)
while ((Get-Date) -lt $deadline) {
    if (Test-Path -LiteralPath $reportJson) {
        $json = Get-Content -LiteralPath $reportJson -Raw -Encoding UTF8 | ConvertFrom-Json
        Write-Host ""
        Write-Host "ReportJson: $reportJson"
        Write-Host "ReportMarkdown: $reportMarkdown"
        Write-Host ("Summary: {0}" -f $json.Summary)
        Write-Host ("Total={0}, Passed={1}, Failed={2}, Skipped={3}" -f $json.Total, $json.Passed, $json.Failed, $json.Skipped)

        if ($json.Failed -gt 0) {
            exit 1
        }

        exit 0
    }

    Start-Sleep -Seconds 1
}

Write-Error "等待报告超时：$reportJson"
exit 2
