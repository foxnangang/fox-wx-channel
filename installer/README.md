# 安装包说明

`Fox-wx-channel-Setup.exe` 是原生 WinForms 安装向导，单文件，内置程序载荷。

## 安装流程

1. 以管理员身份启动安装向导。
2. 选择安装目录，默认 `%LOCALAPPDATA%\Fox-wx-channel`。如果选择 `Program Files` 或盘符根目录，会自动创建 `Fox-wx-channel` 子文件夹，不会把文件散落在一级目录。
3. 点击“安装”，显示复制进度。
4. 完成后可直接运行程序或打开安装目录。

安装向导会：

- 复制界面程序和后台服务
- 创建桌面、开始菜单快捷方式
- 写入当前用户的卸载信息
- 复制一份 `uninstall.exe`，可从“设置 → 应用”卸载

不会覆盖已有的 `config.yaml`，升级安装时保留用户设置。

## 界面

原生 WinForms，无浏览器控件：

- 左侧导航：下载任务、设置、关于
- 顶部：刷新、打开下载目录
- 下载任务：统计卡片、任务表格、全部开始、全部暂停、删除选中
- 设置：保存目录、代理端口、打开配置文件、打开安装目录
- 关于：版本、安装路径、GitHub 地址

后台核心使用 `-H=windowsgui` 编译，不显示控制台窗口。

## 源码

- `desktop/`：原生界面（.NET 9 WinForms）
- `installer-app/`：安装向导（.NET 9 WinForms，内嵌 `payload.zip`）

编译：

```bash
dotnet build desktop/FoxDesktop.csproj -c Release
dotnet publish installer-app/FoxInstaller.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

安装向导运行需要 Microsoft .NET 9 桌面运行时。
