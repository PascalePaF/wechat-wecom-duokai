# 微窗助手 V1.1.3 版本说明

发布日期：2026-09-20

## 修复部分代理环境无法检查更新

V1.1.2 发布后的正式端到端验证发现：当 Windows 应用由 `HTTPS_PROXY` / `HTTP_PROXY` 环境变量指定
本地代理，但系统 Internet 代理未启用时，.NET Framework 4.8 的默认 `HttpClient` 不一定使用该代理。
浏览器与 curl 可以联网，助手却可能显示“无法连接 GitHub 静态更新清单”。

V1.1.3 完成以下修复：

- GitHub 版本检查与附件下载显式识别规范的 `HTTPS_PROXY`，为空时再识别 `HTTP_PROXY`。
- 只接受 `http://` 或 `https://` 代理地址；拒绝文件 URI、查询参数、片段和带额外路径的代理地址。
- 支持代理 URL 中经过转义的用户名/密码，但凭据只配置给本地 HTTP 代理，不写日志、不写设置、不发送
  到 GitHub 请求头。
- 固定使用 TLS 1.2，避免 .NET Framework 通过部分 CONNECT 代理时协商旧协议或无法建立安全通道。
- 静态清单第一步由无正文的 `HEAD` 探测代替 `GET`，降低代理、HTTPS 扫描和网关对重定向响应正文的
  兼容差异。
- 实际下载仍使用 HTTPS、严格最终 GitHub 交付域、精确大小和多重 SHA-256 校验。

## 实机端到端验证

在本机 `HTTPS_PROXY=http://127.0.0.1:7897`、WinHTTP 直连、Windows Internet ProxyEnable=0 的组合下，
正式 .NET Framework 4.8 进程完成：

1. `latest/download/update-manifest.json` 解析到 V1.1.2；
2. 静态清单与 GitHub API 附件 digest 一致；
3. 下载 580 字节 `SHA256SUMS.txt`；
4. 下载 789,504 字节 V1.1.2 setup；
5. GitHub digest、静态清单、SHA256SUMS 与本机文件全部一致。

V1.1.2 的静态清单、24 小时缓存、30–300 秒错峰、1/6/24 小时退避、手动立即检查、用户确认安装、
事务更新和不可变 Release 设计全部保持不变。
