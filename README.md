# Fox-wx-channel

本机自用的微信视频号下载工具，基于 `ltaoo/wx_channels_download` v261123 改造。**仅限非商业、个人自用**。

- GitHub：<https://github.com/foxnangang/fox-wx-channel>
- 发行版下载：<https://github.com/foxnangang/fox-wx-channel/releases/tag/v0.1.0-fox>
- 安装包：`Fox-wx-channel-Setup.exe`（管理员运行，默认装到 `%LOCALAPPDATA%\Fox-wx-channel`）
- 反馈：<https://github.com/foxnangang/fox-wx-channel/issues>

## 工作原理

程序在本机启动 HTTP/HTTPS 中间人代理（默认 `127.0.0.1:2023`），首次运行生成**本机专属**根 CA 并安装到 Windows 信任根证书库；微信 PC 端访问视频号页面时，代理向页面注入下载按钮并捕获视频元数据与解密密钥，随后分片下载并还原文件头（前 128 KB）。管理 API 默认只监听 `127.0.0.1:2022`。

## 环境

- Windows 10/11 x86_64
- 微信 PC 客户端最新版（4.x）
- 源码构建需要 Go 1.20+（本机使用便携版 Go 1.20.14）

## 构建

```bash
# Go 已加入 PATH 时
bash build-fox.sh

# 使用便携版 Go 时
FOX_GO="/d/Agent/WorkBuddy/Fox/2026-11-24/toolchain/go/bin/go" bash build-fox.sh
```

## 运行

1. **以管理员身份**运行 `fox-wx-channel.exe`（UAC 提权用于安装根证书与设置系统代理）。
2. 首次启动会在工作目录生成 `certs/Fox-wx-channel.pem`、`.key`，并安装根证书；私钥权限为 0600。
3. 终端提示代理启动成功后，打开微信 PC 端视频号。
4. 播放视频后暂停，点击页面下方下载按钮；无按钮时查看页面侧边/底部悬浮按钮。
5. 默认下载当前清晰度，可下拉选择其他规格；文件默认保存到“下载”目录，命名格式 `标题_清晰度.mp4`。

停止代理：`fox-wx-channel.exe server stop`。卸载根证书：`fox-wx-channel.exe uninstall`。

## 配置

根目录 `config.yaml` 由 `internal/config/config.template.yaml` 生成。常用项：

```yaml
download:
  dir: "%UserDownloads%"          # 下载目录
  filenameTemplate: "{{filename}}_{{spec}}"
  maxRunning: 3
proxy:
  port: 2023
  system: true
api:
  hostname: "127.0.0.1"
  port: 2022
update:
  sources:
    - enabled: false              # 已关闭自动更新，避免覆盖本改造版本
```

## 数据与安全

- 任务与内容元数据保存在工作目录 `data.db`（SQLite）。
- 根 CA 私钥仅保存在本机 `certs/`，不要分享或上传；他人的根证书同样不要信任。
- 本工具会修改系统代理设置与根证书库，退出时应恢复；异常退出后可手动检查系统代理。
- 仅下载你有权保存的内容，遵守平台服务条款与著作权法规。

## 来源与许可证

- 上游项目：<https://github.com/ltaoo/wx_channels_download>（v261123），许可证见 `LICENSE`（MIT + Commons Clause v1.0）。
- **Commons Clause 禁止出售基于该软件功能的商业产品/服务**；本改造版本仅限个人非商业自用。
- 依据 MIT 条款，保留上游版权声明与许可文本；本版本的修改清单与第三方来源见 `NOTICE.md`。
- 上游声明其解密算法来自 `Hanson/WechatSphDecrypt`，前端解密参考 `kanadeblisst00/WechatVideoSniffer2.0`；这两个仓库未声明许可证，请勿将其作为商业分发依据。
