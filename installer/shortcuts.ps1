param([string]$AppDir)

$WshShell = New-Object -ComObject WScript.Shell
$exePath = Join-Path $AppDir 'Fox-wx-channel.exe'

$menuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\Fox-wx-channel'
New-Item -ItemType Directory -Force -Path $menuDir | Out-Null

$appLink = $WshShell.CreateShortcut((Join-Path $menuDir 'Fox-wx-channel.lnk'))
$appLink.TargetPath = $exePath
$appLink.WorkingDirectory = $AppDir
$appLink.IconLocation = "$exePath,0"
$appLink.Description = 'Fox-wx-channel'
$appLink.Save()

$desktopDir = [Environment]::GetFolderPath('Desktop')
if ($desktopDir) {
    $desktopLink = $WshShell.CreateShortcut((Join-Path $desktopDir 'Fox-wx-channel.lnk'))
    $desktopLink.TargetPath = $exePath
    $desktopLink.WorkingDirectory = $AppDir
    $desktopLink.IconLocation = "$exePath,0"
    $desktopLink.Description = 'Fox-wx-channel'
    $desktopLink.Save()
}
