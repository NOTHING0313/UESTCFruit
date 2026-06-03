$ErrorActionPreference = 'Stop'
Write-Host 'AI Image Pipeline MCP Server Install' -ForegroundColor Cyan
Write-Host 'Server Path: H:/GitHub/UESTCFruit/Tools/image-mcp-server' -ForegroundColor Gray
Set-Location 'H:/GitHub/UESTCFruit/Tools/image-mcp-server'

Write-Host 'Node version:' -ForegroundColor Cyan
node -v
Write-Host 'npm version:' -ForegroundColor Cyan
npm -v

Write-Host 'Writing local .npmrc...' -ForegroundColor Cyan
@'
registry=https://registry.npmmirror.com/
audit=false
fund=false
fetch-retries=5
fetch-retry-mintimeout=20000
fetch-retry-maxtimeout=120000
'@ | Set-Content -Path '.npmrc' -Encoding UTF8

Write-Host 'Installing dependencies...' -ForegroundColor Cyan
npm install --registry=https://registry.npmmirror.com/ --no-audit --no-fund --fetch-retries=5 --fetch-retry-mintimeout=20000 --fetch-retry-maxtimeout=120000

Write-Host 'Building MCP server...' -ForegroundColor Cyan
npm run build

if (Test-Path './dist/index.js') {
    Write-Host 'Build succeeded. dist/index.js exists.' -ForegroundColor Green
} else {
    throw 'Build failed: dist/index.js not found.'
}

Write-Host ''
Read-Host 'Press Enter to close this window'
