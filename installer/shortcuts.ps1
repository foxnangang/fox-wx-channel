param([string]$AppDir)

$WshShell = New-Object -ComObject WScript.Shell
$menuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Fox-wx-channel'
New-Item -ItemType Directory -Force -Path $menuDir | Out-Null

$appLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Fox-wx-channel.lnk'))
$appLink.TargetPath = Join-Path $AppDir 'fox-wx-channel.exe'
$appLink.WorkingDirectory = $AppDir
$appLink.Description = 'Fox-wx-channel video downloader'
$appLink.Save()

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
