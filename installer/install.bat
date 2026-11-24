@echo off
setlocal
if defined FOX_INSTALL_DIR (set "APPDIR=%FOX_INSTALL_DIR%") else (set "APPDIR=%LOCALAPPDATA%\Fox-wx-channel")
if not exist "%APPDIR%" mkdir "%APPDIR%"
copy /Y "%~dp0Fox-wx-channel.exe" "%APPDIR%\" >nul
copy /Y "%~dp0Fox-wx-channel.dll" "%APPDIR%\" >nul
copy /Y "%~dp0Fox-wx-channel.deps.json" "%APPDIR%\" >nul
copy /Y "%~dp0Fox-wx-channel.runtimeconfig.json" "%APPDIR%\" >nul
copy /Y "%~dp0Microsoft.Web.WebView2.Core.dll" "%APPDIR%\" >nul
copy /Y "%~dp0Microsoft.Web.WebView2.WinForms.dll" "%APPDIR%\" >nul
xcopy /E /I /Y "%~dp0runtimes" "%APPDIR%\runtimes" >nul
copy /Y "%~dp0fox-wx-channel-core.exe" "%APPDIR%\" >nul
copy /Y "%~dp0config.yaml" "%APPDIR%\" >nul
copy /Y "%~dp0config.example.yaml" "%APPDIR%\" >nul
copy /Y "%~dp0LICENSE" "%APPDIR%\" >nul
copy /Y "%~dp0NOTICE.md" "%APPDIR%\" >nul
copy /Y "%~dp0README.md" "%APPDIR%\" >nul
copy /Y "%~dp0USAGE.txt" "%APPDIR%\" >nul
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0shortcuts.ps1" -AppDir "%APPDIR%"
echo Installed to: %APPDIR%
endlocal
