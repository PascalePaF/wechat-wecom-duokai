using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WechatDuokai.Core
{
    // Optional capability: legacy deterministic launch environments need not implement Windows shutdown.
    public interface IClientShutdownEnvironment
    {
        IClientShutdownSession BeginClientShutdown(AppDefinition application);
        int GetKnownClientProcessCount(AppDefinition application);
    }

    public interface IClientShutdownSession : IDisposable
    {
        ClientShutdownState Refresh();
        ProcessCloseSummary CloseCurrentProcesses(TimeSpan gracefulTimeout, CancellationToken cancellationToken);
    }

    public sealed class ClientShutdownState
    {
        public IReadOnlyList<ProcessSnapshot> Processes { get; set; } = new ProcessSnapshot[0];
        public int InstanceCount { get; set; }
        public int VisibleWindowCount { get; set; }
        public bool WindowInspectionSucceeded { get; set; }
        public int UnverifiedProcessCount { get; set; }
        public bool InspectionFailed { get; set; }
    }

    internal static class ClientShutdownWorkflow
    {
        internal static async Task<ExitAllResult> RunAsync(IClientShutdownEnvironment environment,
            IProcessEnvironment clock, AppDefinition application, Action<string> report,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = new ExitAllResult();
            using (var session = environment.BeginClientShutdown(application))
            {
                var watch = Stopwatch.StartNew();
                var state = await Task.Run(session.Refresh, cancellationToken);
                result.BeforeCount = state.InstanceCount;
                var consecutiveEmpty = 0;
                var closePasses = 0;
                for (var sample = 0; sample < 40 && watch.Elapsed < TimeSpan.FromSeconds(14); sample++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!state.InspectionFailed && state.UnverifiedProcessCount == 0 && state.Processes.Count == 0)
                    {
                        // A single empty sample is not proof of completion: helpers can briefly restart.
                        if (++consecutiveEmpty >= 3) { result.Success = true; break; }
                    }
                    else
                    {
                        consecutiveEmpty = 0;
                        if (state.InspectionFailed) break;
                        if (state.Processes.Count > 0 && closePasses < 4)
                        {
                            report?.Invoke(closePasses == 0 ? "正在安全退出全部" + application.DisplayName + "窗口及关联后台…"
                                : "正在复查并清理" + application.DisplayName + "的残留后台…");
                            var timeout = closePasses++ == 0 ? TimeSpan.FromSeconds(3) : TimeSpan.FromMilliseconds(150);
                            var summary = await Task.Run(() => session.CloseCurrentProcesses(timeout, cancellationToken), cancellationToken);
                            result.GracefulProcessCount += summary.GracefulExitCount;
                            result.ForcedProcessCount += summary.ForcedExitCount;
                            result.AccessDeniedProcessCount = summary.AccessDeniedProcessCount;
                        }
                    }
                    await clock.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
                    state = await Task.Run(session.Refresh, cancellationToken);
                }
                // Report the final observed state, not a stale pre-termination process count.
                result.AfterCount = state.WindowInspectionSucceeded ? state.VisibleWindowCount : state.InstanceCount;
                result.RemainingProcessCount = state.Processes.Count;
                result.UnverifiedProcessCount = state.UnverifiedProcessCount;
                result.InspectionFailed = state.InspectionFailed;
                result.WindowCountKnown = state.WindowInspectionSucceeded;
                result.Message = FormatResult(application.DisplayName, result, state.WindowInspectionSucceeded);
                return result;
            }
        }

        internal static string FormatResult(string name, ExitAllResult result, bool windowsKnown)
        {
            if (result.Success) return name + "的窗口与已核验关联后台已全部退出。";
            if (result.InspectionFailed) return "暂时无法完成" + name + "的进程复查，未将检查异常当成退出成功。请生成本地诊断后重试。";
            if (result.UnverifiedProcessCount > 0)
                return name + "仍有 " + result.UnverifiedProcessCount + " 个同名或关联候选进程无法核验归属，已保留且不会强行结束。请生成本地诊断。";
            var detail = result.AccessDeniedProcessCount > 0
                ? "部分进程被 Windows 拒绝访问；请先用客户端自身的退出功能关闭，或核对助手与客户端的运行权限。"
                : "它们仍在退出或重新启动；可稍后重试或生成本地诊断。";
            return windowsKnown && result.AfterCount == 0
                ? name + "已没有可见窗口，但仍有 " + result.RemainingProcessCount + " 个已核验后台进程未结束。" + detail
                : name + "仍有 " + result.RemainingProcessCount + " 个已核验进程未结束。" + detail;
        }
    }
}
