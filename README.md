# 微信 · 企业微信多开助手

[![Release](https://img.shields.io/github/v/release/PascalePaF/wechat-wecom-duokai?display_name=tag&style=flat-square)](https://github.com/PascalePaF/wechat-wecom-duokai/releases)
[![Windows build](https://github.com/PascalePaF/wechat-wecom-duokai/actions/workflows/windows-release-validation.yml/badge.svg)](https://github.com/PascalePaF/wechat-wecom-duokai/actions/workflows/windows-release-validation.yml)
![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-5b8def?style=flat-square)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-7057ff?style=flat-square)
[![License](https://img.shields.io/badge/License-Apache--2.0-d3b579?style=flat-square)](LICENSE)

一个面向 Windows 10/11 的轻量级微信、企业微信多开与补开工具。它统计当前用户会话中、来自同一
官方程序路径的真实根实例，只启动距离目标数量还缺少的窗口；误关一个窗口后，无需退出其余客户端，
再次点击即可只补回一个。

> 本项目基于 [CN-Root/wechat-wecom-duokai](https://github.com/CN-Root/wechat-wecom-duokai)
> 持续改进。不会修改、替换或破解微信与企业微信客户端文件，也不是腾讯官方产品。

![V1.0.8 日间主题最小界面](docs/assets/v1.0.8-main-light-901x513.png)

## V1.0.8 有什么变化

- 夜间主题整体降低亮度和饱和度：金色主按钮、绿色状态、蓝色提示、危险色和边框更柔和。
- 微信与企业微信图标外层不再使用写死的浅色方块；夜间分别切换为深墨绿与深蓝灰背景。
- 主界面右下角只保留“设置”，让日常多开界面保持简洁。
- “诊断”移动到设置页左侧，报告仍只保存在 `data\diagnostics`，不会自动上传。
- “发布版本”保留在设置页右侧，与检查更新、一键更新和下载进度归在同一区域。
- 新增夜间颜色亮度与动态图标背景自动断言，继续覆盖 12 场景 UI 回归矩阵。
- 日间主题、共用目标数、增量补开、注册表恢复保护和 V1.0.7 安全更新链保持不变。

完整记录见 [CHANGELOG.md](CHANGELOG.md)。安全更新链、目录限制、主题回归和测试证据见
[V1.0.8 一键更新安全验证报告](docs/V1.0.8-一键更新安全验证报告.md)与
[V1.0.8 UI 回归矩阵](docs/V1.0.8-UI回归矩阵.md)。

## 主要能力

| 能力 | 行为 |
| --- | --- |
| 按需补开 | 目标为 3、误关 1 个后，再次点击只补开缺少的 1 个 |
| 自动记忆 | 微信和企业微信共用一个 1–10 目标窗口数，修改后自动保存 |
| 实时状态 | 左下角分两行显示当前微信与企业微信根窗口数量 |
| 设置中心 | 跟随系统/日间/舒适夜间主题，本地诊断、发布页、启动检查和确认式一键更新 |
| 企业微信扩展模式 | 当前官方 5.0.11.6018 已实测三开；支持崩溃/断电恢复且不覆盖外部新值 |
| 自动定位 | 从注册表、运行进程和官方常见目录识别客户端 |
| 自定义客户端 | 可选择任意安装目录中的微信/企业微信，必须通过腾讯签名验证 |
| 本地数据 | 设置、主题、诊断与恢复证据统一保存在程序目录的 `data` 文件夹 |
| 一键更新 | 固定 GitHub 仓库、三方 SHA-256、独立安装进程、失败回滚；仍保留浏览器下载 |
| 比例界面 | 901×513 起，原生标题栏、自由缩放、最大化、双主题和 Per-Monitor V2 DPI |
| 两种发行方式 | 安装版与绿色免安装 ZIP；安装器和清理器为不同编译身份 |
| 可核验发布 | 源码标签、SHA-256、SPDX SBOM、干净 Windows CI、UI 矩阵和 Kaspersky 日志 |
| 完整卸载 | 可保留源码清理程序，也可在多重校验后删除源码与全部发布包 |

## 下载

只从 [GitHub Releases](https://github.com/PascalePaF/wechat-wecom-duokai/releases) 下载正式版本。

| 文件 | 适用场景 |
| --- | --- |
| `wechat_duokai-setup-v1.0.8.exe` | 推荐；可选择本机安装地址，也是经过校验的一键更新执行程序 |
| `wechat_duokai-portable-v1.0.8.zip` | 绿色版；解压完整目录后直接运行，也支持保持绿色身份原地更新 |
| `wechat_duokai-cleanup-v1.0.8.exe` | 独立完整卸载与清理工具；与安装器不是同一个二进制 |
| `SHA256SUMS.txt` | 核对下载内容是否与发布文件一致 |
| `manifest.spdx.json` | Microsoft SBOM Tool 生成并验证的 SPDX 2.2 软件物料清单 |
| `kaspersky-scan-v1.0.8.txt` | Kaspersky 对正式发布附件原件的原始扫描日志 |

当前版本没有受公共信任的 Authenticode 代码签名。首次运行可能出现“未知发布者”、SmartScreen 或
第三方启发式信誉提示；这不等于文件一定有病毒，也不能反过来当作安全证明。请核对下载域名、Release
标签和 SHA-256，让本机安全软件保持开启。若安全软件报出明确告警，应先隔离并核查报告、哈希和源码，
不要盲目点击忽略，也不要为运行本工具关闭防护或设置永久白名单。

## 安装和升级

1. 下载并运行 `wechat_duokai-setup-v1.0.8.exe`。
2. 使用“选择文件夹”选择或新建一个专用安装目录；路径不需要手动输入。
3. 选择是否创建桌面快捷方式，然后点击“立即安装”。
4. 如果同一安装目录的旧助手正在运行，按提示选择关闭或取消。安装器只处理该目录中的助手，
   不会结束微信、企业微信或其他目录中的同名程序。
5. 安装完成后应用仍保持未启动；只有点击“确认并启动”才会运行，也可选择“稍后启动”。

![V1.0.8 日间主题安装器](docs/assets/v1.0.8-installer-light.png)

## 绿色版

1. 将 `wechat_duokai-portable-v1.0.8.zip` 解压到一个独立文件夹。
2. 运行 `wechat_duokai.exe`。
3. 不要只把主 EXE 移走；它需要同目录的 `WechatDuokai.Core.dll`，完整目录还包含许可证、安全说明和清理器。

## 使用方法

1. 在最右侧设置共用的目标窗口数，范围为 1–10。
2. 点击微信或企业微信的“启动 / 补开”；两个按钮都读取这个共用数字。
3. 程序会统计真实根实例，并只启动缺少的数量。
4. 如果没有自动找到客户端，点击相应卡片中的 `…`，选择官方程序：微信为 `Weixin.exe` 或
   `WeChat.exe`，企业微信通常为 `WXWork.exe`。
5. 如需排查兼容问题，点击右下角“设置”，再在“本地诊断”中生成报告。报告只保存在当前程序目录的
   `data\diagnostics` 文件夹。
6. 页脚左侧会持续显示两类客户端的当前根窗口数量；右下角只保留“设置”这个入口。

共用目标数量、自定义路径和更新偏好保存在程序目录的 `data\settings.ini`，主题保存在
`data\theme.ini`。自定义路径使用 Base64 只是为了避免特殊字符破坏配置格式，不是加密；配置不包含
账号、聊天内容或凭据。V1.0.8 延续 V1.0.7 的迁移规则：把 V1.0.6 分开保存的两个数量合并为一个，
优先沿用微信的旧值；
V1.0.5 及更早版本的 AppData 设置只迁移一次，迁移后不再写回旧目录。

### 企业微信三开说明

企业微信当前的 `multi_instances` 注册表入口只可靠表达双开。在本机 `WXWork 5.0.11.6018` 上，把它
直接设置为 `3` 仍只能得到两个根实例；自 V1.0.6 起不会把任意目标数量永久写入注册表。目标大于 2 时，
程序只在本次启动循环内暂时避开这条双开提示，并在每次补开前处理已知独占锁。真机已验证三个窗口，
但界面允许的 `4–10` 不是对每个企业微信版本的保证；客户端或组织策略变化时可能失败。

修改注册表前会先将恢复事务强制写入 `data\recovery`。正常结束时恢复并删除；若程序异常退出，下次
启动会核对事务所有者和当前注册表状态后再恢复。只有当前值仍等于本助手临时状态时才恢复旧快照；
第三方程序写入了新设置时会保留第三方值并关闭旧事务。由于普通注册表值没有跨进程原子
compare-and-swap，外部程序仍可能在最后一次比较与写入之间竞争；审计日志会保存在 `data\logs`。

### 设置、版本检查与一键更新

设置页提供“跟随 Windows / 日间 / 夜间”三种主题偏好，以及“启动时自动检查新版本”开关。关闭开关后
仍可手动检查。发现更新后会显示目标版本和“一键更新”按钮，也可继续使用浏览器打开发布页。

点击一键更新后还会显示确认框；只有确认后才下载 `SHA256SUMS.txt` 和 setup。下载内容全部进入当前程序
目录的 `data\updates`，并经过 GitHub 摘要、发布校验文件和本机文件三方 SHA-256 验证。安装版会刷新原有
安装登记，绿色版保持绿色身份；二者都不会覆盖 `data`。失败时保留或恢复当前版本。

![V1.0.8 设置页](docs/assets/v1.0.8-settings-light-901x513.png)

<details>
<summary>查看夜间主题与大尺寸适配</summary>

![V1.0.8 夜间主题最小界面](docs/assets/v1.0.8-main-dark-901x513.png)

![V1.0.8 1920×1080 夜间界面](docs/assets/v1.0.8-main-dark-1920x1080.png)

![V1.0.8 3440×1392 超宽屏界面](docs/assets/v1.0.8-main-ultrawide-light-3440x1392.png)

</details>

## 工作原理

```text
主界面 MainWindow
├─ 比例缩放画布                   901×513 基准；统一缩放并按比例填满窗口
├─ 主界面 / 设置页                同窗切换；实时计数、主题、检查更新与下载进度
├─ ApplicationLocator             注册表、进程、常见目录与用户自选路径
├─ ClientExecutableValidator      文件名 + 产品信息 + 腾讯签名验证
├─ InstanceManager                共用目标数、真实根实例和双客户端增量启动编排
│  ├─ IProcessEnvironment         枚举、父子关系、启动、等待的可测试边界
│  ├─ IWeComLaunchPolicy          落盘事务、启动恢复、外部冲突检测与条件恢复
│  └─ WindowsHandleUnlocker       仅处理已知单实例锁白名单
├─ ApplicationStorage             约束所有持久数据位于程序目录的 data 中
├─ DiagnosticReportService        仅写本地 data/diagnostics 报告
├─ ReleaseUpdateChecker           严格解析 GitHub Release、附件 URL/大小/digest
├─ ApplicationUpdateService       三方 SHA-256、受限下载目录、计划落盘与独立安装器交接
└─ UserPreferences                保存数量、更新检查开关与已验证客户端路径

发布程序
├─ setup                           手动安装及安装版/绿色版事务更新、失败回滚
├─ cleanup                         只负责卸载/完整清理及临时清理工作进程
├─ build-release.ps1               重编译、测试、组包、清单与 SHA-256
└─ GitHub Actions                  干净 Windows 重建、哈希复核与 SPDX SBOM
```

微信 4.x 的一个窗口会产生多个同名子进程，所以不能直接把同名进程总数当作窗口数。本项目按父子
关系归并为根实例，再用“目标数量 − 当前根实例数”计算缺口。微信 3.x/4.x 的可执行文件命名和两代
锁策略都保留；企业微信将注册表提示限制在短期作用域，并以精确独占锁作为补开机制。

## 安全与隐私边界

- 不注入 DLL，不创建远程线程，不写客户端内存。
- 不修改、替换或破解微信、企业微信文件。
- 不结束微信或企业微信进程；覆盖安装只处理目标安装目录里的本助手。
- 不读取聊天数据库、消息、联系人、账号、Cookie 或登录凭据。
- 诊断报告仅写本地，不自动上传。
- 用户启用启动检查时，自动网络请求只向 `api.github.com` 读取本项目最新正式 Release 元数据；可关闭。
  只有用户确认一键更新后，才下载同一 Release 的 `SHA256SUMS.txt` 与精确版本 setup。程序不上传诊断、
  路径、账号或日志；GitHub 会看到普通 HTTPS 请求所需的 IP 和 User-Agent。
- 自动覆盖只接受专用标记目录；安装包必须通过 GitHub digest、SHA256SUMS 和本机文件三方校验，替换
  使用暂存/备份事务。绿色版不会因为更新而新增快捷方式或卸载登记。
- 只对已验证客户端路径、当前 Windows 会话及已知锁名/精确 `lock.ini` 路径执行兼容操作。
- 企业微信注册表值在短期启动会话开始前写入落盘恢复事务；正常结束或下次启动时，仅在临时状态仍由
  本助手持有时恢复，不会永久写成 3–10，也不会用旧快照覆盖第三方的新设置。
- 所有持久运行文件都位于程序目录的 `data` 子目录；开始菜单、桌面快捷方式和 Windows 卸载登记属于
  操作系统集成项，完整卸载时一并移除。清理器工作副本只短暂使用系统临时目录并安排自删除。
- 自定义客户端必须有有效腾讯 Authenticode 签名；同名的未签名 EXE 会被拒绝。
- 卸载目标必须同时通过专用标记、目录结构、精确路径和链接检查；删除源码还要额外勾选和确认。

安全资料：

- [V1.0.8 完整安全审计报告](docs/微信企业微信多开助手_V1.0.8_完整安全审计报告.txt)
- [V1.0.8 一键更新安全验证报告](docs/V1.0.8-一键更新安全验证报告.md)
- [V1.0.8 UI 回归矩阵](docs/V1.0.8-UI回归矩阵.md)
- [V1.0.1 源码与旧版二进制安全审计（历史）](SECURITY-AUDIT.md)
- [旧版 EXE 硬编码路径说明](docs/旧版EXE硬编码路径说明与风险评估报告.txt)

反病毒“未检出”是证据，不是绝对安全保证。源码、标签、构建脚本、哈希和原始扫描日志同时公开，
用于让每个发布文件都可以独立复核。

## 完全卸载

从开始菜单“完全卸载”、Windows“已安装的应用”，或运行独立清理工具进入：

1. **删除程序与全部发布包，保留源码**：删除已验证安装目录、快捷方式、安装包、绿色版和用户设置，
   保留本地 Git 仓库。
2. **全部删除，包括源码与全部发布包**：只有源码目录带正确标记且结构匹配时可选；还需要额外复选
   和最后一次确认。未提交的源码修改不可恢复。

![V1.0.8 完整卸载器](docs/assets/v1.0.8-uninstaller-light.png)

## 从源码构建

环境：Visual Studio 2019 或更高版本、.NET Framework 4.8 Developer Pack、MSBuild、Windows 10/11 x64。
项目没有第三方运行时包或 NuGet UI 依赖。

```powershell
MSBuild .\duokai.sln /restore /t:Rebuild /p:Configuration=Release
.\tests\bin\Release\net48\WechatDuokai.Tests.exe
PowerShell -ExecutionPolicy Bypass -File .\build-release.ps1 -Version 1.0.8
```

发布目录为 `artifacts\V1.0.8`。脚本分别复制真正的 setup 与 cleanup 输出，并生成绿色版、发布清单
和 SHA-256。界面回归样本可用 `scripts\capture-ui-matrix.ps1` 重新生成。GitHub 工作流还会在全新的
`windows-2022` 虚拟机中重复这一过程，逐项复核哈希并生成、验证 SPDX 2.2 SBOM。

## 兼容性与限制

- 自动识别与手动选择覆盖微信旧版 `WeChat.exe`、微信 4.x `Weixin.exe` 和企业微信 `WXWork.exe`；
  不以小版本号作为白名单，因此官方维护版本升级不会仅因版本号变化被拒绝。
- V1.0.8 沿用真机基线：微信 `4.1.15.6`、企业微信 `5.0.11.6018`、Windows 11 x64，二者腾讯签名有效；
  企业微信已验证三个根实例同时运行并在测试后正常退出。
- 支持 Windows 10/11 的 100%、125%、150%、175%、200% 缩放逻辑；Per-Monitor V2 允许跨显示器
  重新缩放。每种显卡驱动、辅助技术和超长本地路径仍需社区反馈继续覆盖。
- 腾讯没有公开承诺第三方多开机制长期稳定。客户端改变锁机制时，本工具会失败关闭，不会转向注入
  或修改客户端；请先用测试账号验证，并遵守客户端许可、组织安全策略及账号风控规则。
- V1.0.8 继续使用 WPF/.NET Framework 4.8；现有矢量框架和独立安装器可以实现主题动态资源与安全更新，无需迁移
  WinUI 3、Tauri 或 Python。

## 版本来源

- 上游：[CN-Root/wechat-wecom-duokai](https://github.com/CN-Root/wechat-wecom-duokai)
- 本项目：[PascalePaF/wechat-wecom-duokai](https://github.com/PascalePaF/wechat-wecom-duokai)
- V1.0.0：数量缓存、增量补开、安装/绿色版和完整清理
- V1.0.1：自选安装地址、可移动窗口、双主题和扩展安全审计
- V1.0.2：WPF 矢量界面、安装后确认启动、Core 分层和同类项目研究
- V1.0.3：运行中安全覆盖、腾讯签名路径验证、本地诊断、浏览器更新和独立清理器
- V1.0.4：无滚动条比例界面、901×513 最小基准、超宽屏填充及企业微信三开兼容模式
- V1.0.5：实时双客户端计数、设置中心、注册表外部冲突保护及 Windows CI/SHA-256/SPDX SBOM
- V1.0.6：断电/崩溃注册表恢复日志、微信与企业微信独立目标数量、程序目录内统一数据存储
- V1.0.7：程序内检查与确认式一键更新、三方 SHA-256、安装版/绿色版回滚，并恢复单一共用目标数
- V1.0.8：低亮度舒适夜间主题、主题化客户端图标背景，以及诊断/发布入口归入设置页

## 许可证

项目继承上游的 [Apache License 2.0](LICENSE)。原作者与历次贡献者的版权声明予以保留。
