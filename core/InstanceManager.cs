using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    public sealed class InstanceManager
    {
        private readonly IProcessEnvironment _environment;
        private readonly IWeComLaunchPolicy _weComLaunchPolicy;

        public InstanceManager()
            : this(new WindowsProcessEnvironment(), new WindowsWeComLaunchPolicy())
        {
        }

        public InstanceManager(IProcessEnvironment environment)
            : this(environment, new WindowsWeComLaunchPolicy())
        {
        }

        public InstanceManager(IProcessEnvironment environment, IWeComLaunchPolicy weComLaunchPolicy)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _weComLaunchPolicy = weComLaunchPolicy ?? throw new ArgumentNullException(nameof(weComLaunchPolicy));
        }

        public WeComRegistryRecoveryResult RecoverPendingWeComRegistryState()
        {
            return _weComLaunchPolicy.RecoverPendingSession();
        }

        public int GetInstanceCount(AppDefinition application)
        {
            if (application == null)
            {
                return 0;
            }

            var processIds = GetApplicationProcessIds(application);
            if (processIds.Count == 0)
            {
                return 0;
            }

            // WeChat/WeCom helpers normally use different executable names. If a newer
            // version reuses the main name for child processes, count only process roots.
            var parentMap = _environment.GetParentProcessMap();
            var roots = processIds.Count(processId =>
                !parentMap.TryGetValue(processId, out var parentId) || !processIds.Contains(parentId));

            return Math.Max(1, roots);
        }

        public async Task<LaunchResult> EnsureTargetCountAsync(
            AppDefinition application,
            int targetCount,
            Action<string> reportStatus,
            CancellationToken cancellationToken)
        {
            if (application == null || !application.IsAvailable)
            {
                return new LaunchResult
                {
                    Success = false,
                    RequestedCount = targetCount,
                    Message = "未找到可用的程序文件。"
                };
            }

            targetCount = Math.Max(1, Math.Min(10, targetCount));
            var result = new LaunchResult
            {
                RequestedCount = targetCount,
                BeforeCount = GetInstanceCount(application)
            };

            if (result.BeforeCount >= targetCount)
            {
                result.Success = true;
                result.AfterCount = result.BeforeCount;
                result.Message = $"{application.DisplayName}已经运行 {result.BeforeCount} 个实例，无需补开。";
                return result;
            }

            if (application.Kind == AppKind.WeCom && targetCount > 2)
            {
                reportStatus?.Invoke("正在启用企业微信三开兼容模式；原注册表设置会自动恢复…");
            }

            var launchScope = application.Kind == AppKind.WeCom
                ? await Task.Run(() => _weComLaunchPolicy.BeginLaunchSession(targetCount), cancellationToken)
                : EmptyLaunchScope.Instance;

            using (launchScope ?? EmptyLaunchScope.Instance)
            {
                var missing = targetCount - result.BeforeCount;
                for (var index = 0; index < missing; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var currentCount = GetInstanceCount(application);
                    if (currentCount >= targetCount)
                    {
                        break;
                    }

                    reportStatus?.Invoke($"正在准备第 {currentCount + 1} 个{application.DisplayName}实例…");

                    var processIds = GetApplicationProcessIds(application);
                    if (processIds.Count > 0)
                    {
                        var unlock = await Task.Run(
                            () => WindowsHandleUnlocker.ReleaseSingleInstanceLocks(application.Kind, processIds),
                            cancellationToken);
                        result.ReleasedLockCount += unlock.ClosedHandleCount + (unlock.RemovedLockFile ? 1 : 0);

                        if (application.Kind == AppKind.WeCom)
                        {
                            // Current WeCom needs a short hand-off after its exact exclusive
                            // mutex is closed. This mirrors the sequence verified on 5.0.11.6018.
                            await _environment.DelayAsync(TimeSpan.FromMilliseconds(100), cancellationToken);
                        }
                    }

                    var beforeLaunch = GetInstanceCount(application);
                    _environment.StartApplication(application.ExecutablePath);
                    result.StartedCount++;

                    reportStatus?.Invoke($"已发出启动请求，等待{application.DisplayName}窗口出现…");
                    await WaitForInstanceIncreaseAsync(application, beforeLaunch, cancellationToken);
                }
            }

            result.AfterCount = GetInstanceCount(application);
            result.Success = result.AfterCount >= targetCount;
            result.Message = result.Success
                ? $"{application.DisplayName}已达到目标数量：{result.AfterCount} 个。"
                : $"已请求补开 {result.StartedCount} 个，但目前检测到 {result.AfterCount}/{targetCount} 个。" +
                  " 如果窗口稍后出现，状态会自动刷新；若仍未补开，请打开本地诊断并核对官方客户端版本。";
            return result;
        }

        public async Task<ExitAllResult> ExitAllInstancesAsync(
            AppDefinition application,
            Action<string> reportStatus,
            CancellationToken cancellationToken)
        {
            var result = new ExitAllResult();
            if (application == null || !application.IsAvailable)
            {
                result.Message = "未找到可用的程序文件。";
                return result;
            }

            result.BeforeCount = GetInstanceCount(application);
            if (result.BeforeCount == 0)
            {
                result.Success = true;
                result.Message = "当前没有正在运行的" + application.DisplayName + "窗口。";
                return result;
            }

            var processIds = GetApplicationProcessIds(application);
            reportStatus?.Invoke("正在安全退出全部" + application.DisplayName + "窗口…");
            var closeSummary = await Task.Run(() => _environment.CloseProcesses(
                processIds, application.ExecutablePath, TimeSpan.FromSeconds(2),
                cancellationToken), cancellationToken);

            result.GracefulProcessCount = closeSummary.GracefulExitCount;
            result.ForcedProcessCount = closeSummary.ForcedExitCount;
            result.AfterCount = GetInstanceCount(application);
            result.Success = result.AfterCount == 0;
            result.Message = result.Success
                ? application.DisplayName + "已全部退出，共关闭 " + result.BeforeCount + " 个窗口。"
                : application.DisplayName + "仍有 " + result.AfterCount +
                  " 个窗口未退出；请保存工作后手动关闭，或稍后重试。";
            return result;
        }

        public HashSet<int> GetApplicationProcessIds(AppDefinition application)
        {
            var result = new HashSet<int>();
            if (application == null)
            {
                return result;
            }

            var currentSession = _environment.CurrentSessionId;
            foreach (var process in _environment.FindProcesses(application.ProcessNames))
            {
                try
                {
                    if (process.SessionId != currentSession)
                    {
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(application.ExecutablePath) &&
                        (string.IsNullOrWhiteSpace(process.ExecutablePath) ||
                         !PathsEqual(process.ExecutablePath, application.ExecutablePath)))
                    {
                        continue;
                    }

                    result.Add(process.Id);
                }
                catch (Exception)
                {
                    // A process can exit or a test snapshot can be incomplete.
                }
            }

            return result;
        }

        private async Task WaitForInstanceIncreaseAsync(AppDefinition application, int previousCount, CancellationToken cancellationToken)
        {
            for (var attempt = 0; attempt < 32; attempt++)
            {
                await _environment.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
                if (GetInstanceCount(application) > previousCount)
                {
                    // Give the client enough time to establish its single-instance lock before
                    // the next loop tries to release it.
                    var settleDelay = application.Kind == AppKind.WeCom ? 800 : 350;
                    await _environment.DelayAsync(TimeSpan.FromMilliseconds(settleDelay), cancellationToken);
                    return;
                }
            }
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private sealed class EmptyLaunchScope : IDisposable
        {
            internal static readonly EmptyLaunchScope Instance = new EmptyLaunchScope();

            public void Dispose()
            {
            }
        }

    }
}
