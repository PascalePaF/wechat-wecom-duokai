# V1.0.6 — 断电恢复、双目标数量与自包含数据目录

V1.0.6 让企业微信临时注册表操作能够在崩溃、强制结束或断电后恢复，同时把微信和企业微信的目标数量
彻底拆开，并把持久运行文件统一收进程序目录。

## 新功能

- 微信和企业微信分别设置、分别保存 1–10 的目标窗口数量。
- 注册表修改前先将恢复事务写入 `data\recovery` 并强制落盘。
- 下次启动自动处理未完成事务；只恢复仍等于本助手临时状态的值。
- 第三方已经修改 `multi_instances` 时保留第三方新状态。
- 恢复结果写入有界的 `data\logs\registry-recovery.log`，不记录注册表原始内容或账号数据。
- 设置、主题、诊断和恢复证据全部位于程序目录的 `data` 子目录。
- 从 V1.0.5 升级时自动迁移旧 AppData 设置，不要求重新配置。

## 安全边界

- 不修改微信或企业微信程序文件。
- 不注入 DLL，不读取聊天、联系人或账号凭据。
- 更新检查仍只读取 GitHub Release 版本号，不下载或执行更新。
- 当前版本仍未使用付费 Authenticode 证书；请从本项目 Release 下载并核对 SHA-256。
- 杀毒“未检出”是证据而不是绝对保证；最终扫描结果和病毒库日期见同一 Release 的原始日志。

## 文件

- `wechat_duokai-setup-v1.0.6.exe`：当前用户安装版，可选择安装目录。
- `wechat_duokai-portable-v1.0.6.zip`：绿色免安装版。
- `wechat_duokai-cleanup-v1.0.6.exe`：独立完整清理器。
- `SHA256SUMS.txt`：正式附件哈希。
- `manifest.spdx.json`：SPDX 2.2 SBOM。
- `kaspersky-scan-v1.0.6.txt`：对正式发布原件执行的 Kaspersky 扫描日志。

