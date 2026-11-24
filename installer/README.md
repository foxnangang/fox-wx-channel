# 安装包说明

`Fox-wx-channel-Setup.exe` 为 7-Zip SFX 自解压安装包，发行版中提供。

## 行为

- 需要管理员权限运行。
- 默认安装到 `%LOCALAPPDATA%\Fox-wx-channel`。
- 复制可执行文件、配置、许可证与说明文件。
- 在开始菜单创建 `Fox-wx-channel` 目录，包含：
  - `Fox-wx-channel.lnk`：窗口模式，隐藏控制台并用 Edge 应用窗口打开本地管理界面
  - `Fox-wx-channel Console.lnk`：控制台模式，便于查看日志排错
  - `Edit Config.lnk`：用记事本编辑 `config.yaml`
  - `Usage.lnk`：打开 `USAGE.txt`
- 不创建开机自启。

## 窗口模式

`Fox-wx-channel-GUI.ps1` 启动后台服务后，等待本地 API 端口 2022 就绪，再以
`msedge.exe --app=http://127.0.0.1:2022` 打开独立应用窗口；关闭窗口会停止由它启动的后台服务。
依赖系统已安装 Microsoft Edge。窗口模式不是原生编译 GUI，而是把现有 Web 管理界面以应用窗口呈现。

## 配置文件

- `config.yaml`：实际生效配置。修改 `download.dir` 可改变视频保存目录，默认 `%UserDownloads%`。
- `config.example.yaml`：带注释的完整示例。程序首次运行可能重写 `config.yaml` 并去除注释，因此参考示例修改。

## 重建安装包

需准备 7-Zip 控制台版 `7za.exe` 与 LZMA SDK 中的 `7zSD.sfx`：

```bash
7za a -t7z -mx=9 payload.7z installer-payload/*
cat 7zSD.sfx sfx-config.txt payload.7z > Fox-wx-channel-Setup.exe
```

`sfx-config.txt` 内容：

```text
;!@Install@!UTF-8!
Title="Fox-wx-channel Setup"
BeginPrompt="Install Fox-wx-channel to your local AppData folder?"
Progress="yes"
ExecuteFile="install.bat"
ExecuteParameters=""
;!@InstallEnd@!
```
