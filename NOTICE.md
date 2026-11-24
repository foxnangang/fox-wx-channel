# NOTICE

本文件记录 Fox-wx-channel 的来源、修改与第三方组件说明。

## 上游来源

- 项目：`ltaoo/wx_channels_download`，版本 v261123
- 地址：https://github.com/ltaoo/wx_channels_download
- 版权：Copyright (c) 2025 ltaoo
- 许可证：MIT License + "Commons Clause" License Condition v1.0（见 `LICENSE`）

本版本保留了上游的版权声明与许可文本。Commons Clause 不授予出售权：不得将本软件或其衍生功能作为收费产品或服务提供给第三方。

## 本版本修改

1. Go module 路径由 `wx_channel` 改为 `fox-wx-channel`；CLI 名称改为 `fox-wx-channel`。
2. 版本号设为 `0.1.0-fox`，构建模式默认为 `release`。
3. 移除上游内置的共享 SunnyNet 根证书与私钥（`pkg/certificate/certs/`），禁止复用第三方 CA；启动时始终生成实例专属根证书，CA 名称改为 `Fox-wx-channel`，组织标识改为 `Fox-wx-channel local MITM`。
4. 默认关闭自动更新（`update.sources[0].enabled: false`），防止升级覆盖本改造版本；要求更新校验 checksum。
5. 替换 README 与文档入口，增加本机自用、权限与安全说明。

## 第三方组件与算法来源

- 视频号解密算法：上游代码注释指向 `Hanson/WechatSphDecrypt`，该仓库当前不可访问且无许可证文件。
- 前端解密参考：`kanadeblisst00/WechatVideoSniffer2.0`，仓库未声明许可证。
- 代理与 MITM：`github.com/ltaoo/echo`（MIT）、可选 `github.com/qtgolang/SunnyNet`（MIT）。
- 其余 Go 依赖版本与许可见 `go.mod` / `go.sum`；前端第三方脚本头部保留各自许可声明。

上述算法与脚本来源的许可状态不完整，因此本版本不得用于任何商业分发或收费服务。
