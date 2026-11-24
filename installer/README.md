# 安装包说明

`Fox-wx-channel-Setup.exe` 为 7-Zip SFX 自解压安装包，发行版中提供。

## 安装内容

- 需要管理员权限运行。
- 默认安装到 `%LOCALAPPDATA%\Fox-wx-channel`。
- `Fox-wx-channel.exe`：原生 WinForms 桌面程序，双击直接打开窗口。
- `fox-wx-channel-core.exe`：Go 后台服务，由桌面程序启动。
- 在开始菜单和桌面创建 `Fox-wx-channel` 快捷方式，直接指向原生 EXE。
- 不创建开机自启。

## 界面

原生 WinForms 界面，不依赖浏览器控件：

- 左侧导航：下载任务、设置、关于
- 顶部：刷新、打开下载目录
- 下载任务页：任务表格、全部开始、全部暂停、删除选中
- 设置页：视频保存目录、代理端口、打开配置文件、打开安装目录
- 关于页：版本、安装位置、GitHub 地址

## 配置

- `config.yaml`：实际生效配置。修改 `download.dir` 可改变视频保存目录，默认 `%UserDownloads%`。
- `config.example.yaml`：带注释的完整示例。程序首次运行可能重写 `config.yaml` 并去除注释，因此参考示例修改。

## 源码

原生桌面程序源码位于仓库 `desktop/` 目录，使用 .NET 9 WinForms，无 WebView2 依赖。

编译：

```bash
dotnet build desktop/FoxDesktop.csproj -c Release
```

重建安装包需准备 7-Zip 控制台版 `7za.exe` 与 LZMA SDK 中的 `7zSD.sfx`：

```bash
7za a -t7z -mx=9 payload.7z payload/*
cat 7zSD.sfx sfx-config.txt payload.7z > Fox-wx-channel-Setup.exe
```
