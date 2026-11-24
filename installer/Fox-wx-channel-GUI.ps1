param([string]$AppDir = $PSScriptRoot)

$exePath = Join-Path $AppDir 'fox-wx-channel.exe'
$apiPort = 2022
$startedByLauncher = $false
$backendProcess = $null

function Test-LocalPort {
    param([int]$Port)
    $client = New-Object System.Net.Sockets.TcpClient
    try {
        $client.Connect('127.0.0.1', $Port)
        return $true
    } catch {
        return $false
    } finally {
        $client.Close()
    }
}

if (-not (Test-LocalPort $apiPort)) {
    $backendProcess = Start-Process -FilePath $exePath -WorkingDirectory $AppDir -WindowStyle Hidden -PassThru
    $startedByLauncher = $true
    for ($i = 0; $i -lt 40; $i++) {
        Start-Sleep -Milliseconds 500
        if (Test-LocalPort $apiPort) { break }
    }
}

$edgePath = $null
$candidates = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
)
foreach ($candidate in $candidates) {
    if (Test-Path $candidate) { $edgePath = $candidate; break }
}

$profileDir = Join-Path $AppDir 'edge-profile'
$url = "http://127.0.0.1:$apiPort"

if ($edgePath) {
    $edgeArgs = @(
        "--app=$url",
        "--window-size=1200,800",
        "--user-data-dir=$profileDir",
        "--no-first-run",
        "--disable-features=TranslateUI"
    )
    $edgeProcess = Start-Process -FilePath $edgePath -ArgumentList $edgeArgs -PassThru
    $edgeProcess.WaitForExit()
} else {
    Start-Process $url
    Write-Host "Edge not found; opened $url in the default browser."
    Read-Host "Press Enter to close the backend"
}

if ($startedByLauncher -and $backendProcess) {
    Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
}
