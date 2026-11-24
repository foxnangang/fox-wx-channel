param([string]$AppDir)

$WshShell = New-Object -ComObject WScript.Shell
$menuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Fox-wx-channel'
New-Item -ItemType Directory -Force -Path $menuDir | Out-Null

$exePath = Join-Path $AppDir 'fox-wx-channel.exe'
$guiScript = Join-Path $AppDir 'Fox-wx-channel-GUI.ps1'
$psExe = "$env:WINDIR\System32\WindowsPowerShell\v1.0\powershell.exe"

$appLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Fox-wx-channel.lnk'))
$appLink.TargetPath = $psExe
$appLink.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $guiScript + '" -AppDir "' + $AppDir + '"'
$appLink.WorkingDirectory = $AppDir
$appLink.IconLocation = "$exePath,0"
$appLink.Description = 'Fox-wx-channel desktop UI'
$appLink.Save()

$consoleLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Fox-wx-channel Console.lnk'))
$consoleLink.TargetPath = $exePath
$consoleLink.WorkingDirectory = $AppDir
$consoleLink.Description = 'Fox-wx-channel console mode'
$consoleLink.Save()

$configLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Edit Config.lnk'))
$configLink.TargetPath = "$env:WINDIR\System32\notepad.exe"
$configLink.Arguments = '"' + (Join-Path $AppDir 'config.yaml') + '"'
$configLink.Description = 'Edit Fox-wx-channel config'
$configLink.Save()

$usageLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Usage.lnk'))
$usageLink.TargetPath = "$env:WINDIR\System32\notepad.exe"
$usageLink.Arguments = '"' + (Join-Path $AppDir 'USAGE.txt') + '"'
$usageLink.Description = 'Fox-wx-channel usage'
$usageLink.Save()

$desktopDir = [Environment]::GetFolderPath('Desktop')
if ($desktopDir) {
    $desktopLink = $WshShell.CreateShortcut((Join-Path $desktopDir 'Fox-wx-channel.lnk'))
    $desktopLink.TargetPath = $psExe
    $desktopLink.Arguments = '-NoProfile -ExecutionPolicy Bypass -File "' + $guiScript + '" -AppDir "' + $AppDir + '"'
    $desktopLink.WorkingDirectory = $AppDir
    $desktopLink.IconLocation = "$exePath,0"
    $desktopLink.Description = 'Fox-wx-channel desktop UI'
    $desktopLink.Save()
}
