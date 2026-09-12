# 微信 · 企业微信多开助手

Windows 10/11 上的微信与企业微信多开、补开工具。V1.0.0 基于
[CN-Root/wechat-wecom-duokai](https://github.com/CN-Root/wechat-wecom-duokai) 改进。

## V1.0.0 功能

- 记住上一次设置的目标实例数量（1–10），下次启动无需重新输入。
- 自动检测当前运行数量；目标为 3、关闭 1 个后，再次点击只补开 1 个。
- 微信兼容旧版命名互斥锁及微信 4.x `lock.ini` 文件锁。
- 企业微信同时设置官方 `multi_instances` 用户注册表值，并处理独占互斥锁。
- 微信与企业微信图标位于左侧；双开数量与数字输入框位于最右侧。
- 同时提供 Windows 安装版、绿色免安装版、完整卸载工具和 SHA-256 校验文件。

## 使用方法

1. 在窗口最右侧设置希望保持的实例总数。
2. 点击左侧对应图标，或点击“启动 / 补开”。
3. 程序会统计已运行实例，仅启动缺少的部分。
4. 若之后关闭了一个客户端，再次点击同一按钮即可补开。

数量设置保存在 `%LOCALAPPDATA%\WechatDuokai\settings.ini`。该目录带有独立项目标记，完全卸载时会被精确清理。程序不会读取聊天记录、账号或消息内容。

## 发布包

运行以下脚本会先重新编译、执行自动化验证，再生成安装版与绿色版：

```powershell
PowerShell -ExecutionPolicy Bypass -File .\build-release.ps1
```

输出目录：`artifacts\V1.0.0`

- `installer\wechat_duokai-setup-v1.0.0.exe`：当前用户安装版，会加入开始菜单和 Windows“已安装的应用”。
- `portable\wechat_duokai-portable-v1.0.0.zip`：绿色免安装压缩包。
- `installer\wechat_duokai-cleanup-v1.0.0.exe`：独立完整卸载工具。
- `SHA256SUMS.txt`：发布文件完整性校验值。

安装位置为 `%LOCALAPPDATA%\Programs\WechatDuokai`，无需管理员权限。

## 完全卸载

可通过开始菜单的“完全卸载”、Windows“已安装的应用”，或独立清理工具进入卸载界面：

1. **删除程序与全部发布包，保留源码**：删除安装目录、快捷方式、安装包和绿色版，保留源码。
2. **全部删除，包括源码与全部发布包**：仅当源码目录含专用安全标记且项目结构完全匹配时可选；还需要额外勾选并二次确认。

清理器不会凭目录名称猜测目标，也不会把磁盘根目录、用户目录或未知目录作为递归删除目标。

## 技术与安全边界

- 不注入 DLL。
- 不修改或替换微信、企业微信的程序文件。
- 不结束微信、企业微信进程。
- 只关闭白名单内的单实例互斥锁，或精确匹配 `%APPDATA%\Tencent\xwechat\lock\lock.ini` 的文件句柄。
- 企业微信会写入当前用户下的 `HKCU\SOFTWARE\Tencent\WXWork\multi_instances` 值。
- Windows 内部句柄接口可能随系统或客户端更新而变化；失败时程序会给出提示，不会改用终止全部客户端的方式。

完整审计结论见 [SECURITY-AUDIT.md](SECURITY-AUDIT.md)。

## 开发环境

- Visual Studio 2019 或更高版本
- .NET Framework 4.8 Developer Pack
- WinForms / C# 7.3

```powershell
MSBuild .\duokai.sln /t:Rebuild /p:Configuration=Release
.\tests\bin\Release\WechatDuokai.Tests.exe
```

## 兼容性说明

微信与企业微信会不定期修改单实例机制。V1.0.0 同时覆盖公开可验证的旧版互斥锁与微信 4.x 文件锁方案，但不能承诺未来所有版本持续兼容。建议先使用测试账号确认，并遵守客户端许可协议和所在组织的规定。

本机验收环境中的微信 `4.1.15.6` 会为 3 个客户端产生 15 个同名子进程；V1.0.0 能将它们正确归并为 3 个实例。只读检查后进行了一次增量启动，成功出现第 4 个窗口，并随即只关闭了该测试窗口，原有 3 个实例未被退出。该版本不需要修改客户端文件。

## 许可证

项目继承上游的 Apache License 2.0，详见 [LICENSE](LICENSE)。
