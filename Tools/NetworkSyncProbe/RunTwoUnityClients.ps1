param(
    [Parameter(Mandatory=$true)]
    [string]$ExePath,

    [string]$Server="8.137.83.229",
    [int]$Port=28015,
    [string]$Session="0x11223344",
    [int]$Width=900,
    [int]$Height=600
)

$ErrorActionPreference="Stop"
$resolvedExe=(Resolve-Path $ExePath).Path
$exeDir=Split-Path $resolvedExe -Parent
$logDir=Join-Path $exeDir "Network2PLogs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null

function Start-NetworkClient([int]$PlayerID) {
    $logPath=Join-Path $logDir "Player$PlayerID.log"
    $arguments=@(
        "--network-player-id=$PlayerID",
        "--network-player-count=2",
        "--network-server=$Server",
        "--network-port=$Port",
        "--network-session=$Session",
        "-screen-fullscreen","0",
        "-screen-width",$Width,
        "-screen-height",$Height,
        "-logFile",$logPath
    )

    Write-Host "Starting Player $PlayerID -> $Server`:$Port"
    Write-Host "Log: $logPath"
    Start-Process -FilePath $resolvedExe -ArgumentList $arguments -WorkingDirectory $exeDir
}

Start-NetworkClient 1
Start-Sleep -Milliseconds 800
Start-NetworkClient 2

Write-Host ""
Write-Host "Both clients launched."
Write-Host "P1 initial position: left"
Write-Host "P2 initial position: right"
Write-Host "Keep both windows open; network mode forces Application.runInBackground=true."
