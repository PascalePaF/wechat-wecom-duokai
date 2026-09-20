# V1.1.1 — 更新检查限流修复

V1.1.1 是一个针对“检查失败”的可靠性与可诊断性热修复，不改变 V1.1.0 的主界面布局、多开、
分端全部退出、企业微信恢复事务或安装/绿色版更新边界。

## 已修复

- V1.0.11/V1.1.0 直接访问 GitHub 未登录 REST API。共享网络出口用完每小时 60 次配额后，GitHub 返回
  `HTTP 403` 和 `X-RateLimit-Remaining: 0`；旧界面隐藏了该原因，只显示“检查失败”。
- 最新版号改为先从本项目 `github.com/.../releases/latest` 的 HTTPS 跳转解析。这不是 REST API，
  当前已是最新版时不会再消耗公开 API 配额。
- 发现新版本后，程序仍优先读取 GitHub 附件 digest。API 限流、超时或临时连接失败时，自动使用精确的
  仓库、版本标签和附件名构造下载地址，并通过 HEAD 检查 GitHub 最终交付域、附件存在性和大小上限。
- 修正 HttpClient 超时被错误当作用户取消的问题；真正失败时会显示具体、可操作的原因。

## 一键更新安全边界

- 未经用户确认，只读取版本跳转和附件头信息，不下载 setup 或校验文件。
- 用户确认后才把 `SHA256SUMS.txt` 与 setup 下载到程序目录的 `data\updates`。
- API 可用时继续要求 GitHub digest、`SHA256SUMS.txt` 和下载文件三方 SHA-256 一致。
- API 不可用时仍要求固定的本项目 HTTPS Release 地址、严格文件大小、清单中的唯一 setup 记录以及
  下载文件 SHA-256 一致；任一步失败都不会执行安装包。
- 下载完成后到启动独立安装器前继续锁定文件并复算哈希；覆盖仍使用原有暂存、备份和失败回滚事务。

## 验证

- 新增模拟 `HTTP 403`、`X-RateLimit-Remaining: 0` 的自动回归用例。
- 在本机真实 API 配额为 0 的状态下，修复版成功识别 V1.1.0，并确认 setup 为 761,856 字节、
  `SHA256SUMS.txt` 为 580 字节，可进入一键更新流程。
- 完整 Release 编译与自动测试、发布哈希、GitHub Actions 干净 Windows 构建和 Kaspersky 发布扫描
  仍作为发布门禁。

## 下载

- `wechat_duokai-setup-v1.1.1.exe`：推荐安装版及一键更新程序。
- `wechat_duokai-portable-v1.1.1.zip`：绿色免安装版。
- `wechat_duokai-cleanup-v1.1.1.exe`：独立完整清理器。
- `SHA256SUMS.txt`：全部主要发布文件的 SHA-256。

如果仍在使用 V1.0.11，旧版本必须等当前网络出口的 GitHub API 配额恢复后才能自动发现 V1.1.1；
也可以直接从本项目 Release 页面下载 V1.1.1 覆盖升级。不要关闭安全软件或把程序加入永久白名单。
