using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CSharp;
using WechatDuokai.Core;
using WechatDuokai.Installer;

namespace WechatDuokai.Tests
{
    internal static class ShutdownRegressionTests
    {
        private static ProcessSnapshot P(int id, string path, long birth = 10, int session = 7, bool sameUser = true) =>
            new ProcessSnapshot { Id = id, ExecutablePath = path, CreationTimeFileTime = birth, SessionId = session, IsCurrentUser = sameUser };
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }

        internal static void FamilyScope()
        {
            var app = new AppDefinition(AppKind.WeChat, "微信", @"C:\Apps\Weixin\Weixin.exe");
            var nodes = new[] { P(10, app.ExecutablePath), P(11, @"C:\Plugin\WeChatAppEx.exe", 11),
                P(12, @"C:\Plugin\WeChatAppEx.exe", 12), P(20, app.ExecutablePath, 12, 8),
                P(21, app.ExecutablePath, 12, 7, false), P(30, @"C:\AnotherCopy\Weixin.exe"),
                P(31, @"C:\Plugin\WeChatAppEx.exe", 12), P(40, @"C:\Browser\browser.exe", 12),
                P(41, @"C:\Plugin\WeChatAppEx.exe", 13), P(50, @"C:\Plugin\WeChatAppEx.exe", 5) };
            var parents = new Dictionary<int, int> { [11] = 10, [12] = 11, [31] = 30, [40] = 10, [41] = 40, [50] = 10 };
            var selected = ClientProcessGraph.Select(app, 7, nodes, parents, null, _ => false,
                value => value.ExecutablePath == app.ExecutablePath || value.ExecutablePath == @"C:\Plugin\WeChatAppEx.exe", out var unverified);
            Assert(new HashSet<int>(selected.Select(value => value.Id)).SetEquals(new[] { 10, 11, 12 }),
                "Family selection crossed an account/session/copy boundary or trusted a pre-parent birth time.");
            Assert(unverified == 1, "A rejected child must not make its descendants eligible.");
            var unavailable = new ProcessSnapshot { Id = 60, SessionId = 7, ProcessName = "Weixin", InspectionUnavailable = true, IsCurrentUser = false };
            var denied = ClientProcessGraph.Select(app, 7, new[] { unavailable }, new Dictionary<int, int>(), null, _ => false, _ => false, out var deniedCount);
            Assert(denied.Count == 0 && deniedCount == 1, "An unreadable same-name process was silently treated as an empty system.");
        }

        internal static void AncestryIdentity()
        {
            var app = new AppDefinition(AppKind.WeChat, "微信", @"C:\Apps\Weixin\Weixin.exe");
            var parent = P(10, app.ExecutablePath);
            var child = P(11, @"C:\Plugin\WeChatAppEx.exe", 11);
            var parents = new Dictionary<int, int> { [11] = 10 };
            var orphan = ClientProcessGraph.Select(app, 7, new[] { child }, parents, new[] { parent }, _ => true, _ => true, out _);
            Assert(orphan.Count == 1 && orphan[0].Id == 11, "Held dead-parent proof did not capture a late child.");
            var noProof = ClientProcessGraph.Select(app, 7, new[] { child }, parents, new[] { parent }, _ => false, _ => true, out _);
            Assert(noProof.Count == 0, "A stale parent PID was accepted without a held identity proof.");
            var reused = P(10, @"C:\Plugin\WeChatAppEx.exe", 100);
            var replaced = ClientProcessGraph.Select(app, 7, new[] { reused, child }, parents, new[] { parent }, _ => true, _ => true, out _);
            Assert(replaced.Count == 0, "PID reuse revived the previous process's ownership.");
            var remembered = ClientProcessGraph.Select(app, 7, new[] { child }, new Dictionary<int, int>(), new[] { child }, _ => false, _ => true, out _);
            Assert(remembered.Count == 1, "An identical previously verified orphan could not be retried.");
            var unreadable = new ProcessSnapshot { Id = child.Id, SessionId = 7, ProcessName = "WeChatAppEx", InspectionUnavailable = true, IsCurrentUser = false };
            var unknown = ClientProcessGraph.Select(app, 7, new[] { unreadable }, new Dictionary<int, int>(), new[] { child }, _ => false, _ => true, out var unknownCount);
            Assert(unknown.Count == 0 && unknownCount == 1, "A previously captured, now unreadable orphan was reported as fully gone.");
        }

        internal static void HelperTrustBoundary()
        {
            var root = NewDirectory();
            try
            {
                var main = Path.Combine(root, "Weixin.exe"); var helper = Path.Combine(root, "WeChatAppEx.exe");
                var unknown = Path.Combine(root, "Unrelated.exe");
                foreach (var path in new[] { main, helper, unknown }) File.WriteAllText(path, "fixture");
                var app = new AppDefinition(AppKind.WeChat, "微信", main);
                using (var policy = new ClientProcessPolicy(app, (_, __) => true, _ => false))
                    Assert(policy.IsTrusted(P(10, main)) && !policy.IsTrusted(P(11, helper)), "An unsigned/untrusted helper was accepted.");
                using (var policy = new ClientProcessPolicy(app, (_, __) => true, _ => true))
                {
                    Assert(policy.IsTrusted(P(11, helper)) && !policy.IsTrusted(P(12, unknown)), "A signature alone bypassed the executable-name boundary.");
                    try { using (File.Open(helper, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { } throw new Exception("Trusted image was writable during its shutdown lease."); }
                    catch (IOException) { }
                }
                using (File.Open(helper, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) { }
                var wecom = new AppDefinition(AppKind.WeCom, "企业微信", main);
                using (var policy = new ClientProcessPolicy(wecom, (_, __) => true, _ => true))
                    foreach (var excluded in new[] { "WXWorkUpgrader.exe", "WXWorkRepair.exe", "Uninstall.exe", "WeMail.exe", "WXDrive.exe" })
                    {
                        var excludedPath = Path.Combine(root, excluded); File.WriteAllText(excludedPath, "fixture");
                        Assert(!policy.IsTrusted(P(30, excludedPath)), "Shutdown included an independent app or updater: " + excluded);
                    }
            }
            finally { Program.DeleteTestDirectoryWithRetries(root); }
        }

        internal static void ConvergentShutdown()
        {
            WithFakeApp((app, env) =>
            {
                env.Session.States = new[] { State(app, 1), State(app, 0), State(app, 2), State(app, 0), State(app, 0), State(app, 0) };
                var result = new InstanceManager(env).ExitAllInstancesAsync(app, null, CancellationToken.None).GetAwaiter().GetResult();
                Assert(result.Success && result.RemainingProcessCount == 0 && env.Session.CloseCalls == 2 && env.DelayCalls >= 4,
                    "A single empty sample was accepted, or a restarted process was not closed.");
                Assert(env.Session.Disposed, "Shutdown session did not dispose its handles and leases.");
            });
        }

        internal static void PreciseShutdownResults()
        {
            WithFakeApp((app, env) =>
            {
                env.Session.States = new[] { State(app, 1, 0) }; env.Session.Denied = 1;
                var result = new InstanceManager(env).ExitAllInstancesAsync(app, null, CancellationToken.None).GetAwaiter().GetResult();
                Assert(!result.Success && result.AfterCount == 0 && result.RemainingProcessCount == 1 &&
                    result.Message.Contains("没有可见窗口") && result.Message.Contains("拒绝访问") && !result.Message.Contains("个窗口未退出"),
                    "A hidden, denied background process was incorrectly reported as an open window.");
                Assert(env.Session.CloseCalls == 4 && env.DelayCalls <= 40, "Shutdown retries are not bounded.");
                env.Session = new ScriptedSession { States = new[] { new ClientShutdownState { InspectionFailed = true } } };
                result = new InstanceManager(env).ExitAllInstancesAsync(app, null, CancellationToken.None).GetAwaiter().GetResult();
                Assert(!result.Success && result.InspectionFailed && env.Session.CloseCalls == 0, "Inspection failure was treated as success or authorized a kill.");
                env.Session = new ScriptedSession { States = new[] { new ClientShutdownState { UnverifiedProcessCount = 1 } } };
                result = new InstanceManager(env).ExitAllInstancesAsync(app, null, CancellationToken.None).GetAwaiter().GetResult();
                Assert(!result.Success && result.UnverifiedProcessCount == 1 && env.Session.CloseCalls == 0, "Unverified ownership was ignored.");
            });
        }

        internal static void Cancellation()
        {
            WithFakeApp((app, env) =>
            {
                using (var cancellation = new CancellationTokenSource())
                {
                    cancellation.Cancel();
                    try { new InstanceManager(env).ExitAllInstancesAsync(app, null, cancellation.Token).GetAwaiter().GetResult(); throw new Exception("Canceled shutdown continued."); }
                    catch (OperationCanceledException) { }
                    Assert(env.BeginCalls == 0, "Canceled shutdown acquired a process session.");
                }
            });
        }

        internal static void NativeIdentityBoundary()
        {
            using (var child = Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName, "--idle")
                { UseShellExecute = false, CreateNoWindow = true }))
            {
                try
                {
                    Assert(child != null, "Native fixture did not start.");
                    var env = new WindowsProcessEnvironment();
                    var snapshot = NativeProcessControl.ReadSnapshot(child.Id, env.CurrentSessionId);
                    Assert(snapshot != null, "Limited-information process inspection failed.");
                    var original = snapshot.CreationTimeFileTime;
                    snapshot.CreationTimeFileTime++;
                    using (var handle = NativeProcessControl.OpenVerified(snapshot, env.CurrentSessionId, out _)) Assert(handle == null, "A stale birth time opened a termination handle.");
                    snapshot.CreationTimeFileTime = original; snapshot.ExecutablePath += ".other";
                    using (var handle = NativeProcessControl.OpenVerified(snapshot, env.CurrentSessionId, out _)) Assert(handle == null, "A mismatched image opened a termination handle.");
                    snapshot.ExecutablePath = child.MainModule.FileName; snapshot.IsCurrentUser = false;
                    using (var handle = NativeProcessControl.OpenVerified(snapshot, env.CurrentSessionId, out _)) Assert(handle == null, "A foreign user identity opened a termination handle.");
                    Assert(!child.HasExited, "A rejected fixture was terminated.");
                }
                finally { if (child != null && !child.HasExited) child.Kill(); child?.WaitForExit(3000); }
            }
        }

        internal static void RecoveryFailure()
        {
            WithFakeApp((app, environment) =>
            {
                var policy = new FailedRecoveryPolicy();
                var manager = new InstanceManager(environment, policy);
                var recovery = manager.RecoverPendingWeComRegistryState();
                Assert(recovery.Outcome == WeComRegistryRecoveryOutcome.RetryRequired && !recovery.CanStartNewSession,
                    "A failed recovery was promoted to permission to modify registry state.");
                var result = manager.EnsureTargetCountAsync(new AppDefinition(AppKind.WeCom, "企业微信", app.ExecutablePath), 3,
                    null, CancellationToken.None).GetAwaiter().GetResult();
                Assert(!result.Success && !policy.BeginCalled, "WeCom started a registry session after failed startup recovery.");
            });
        }

        private sealed class FailedRecoveryPolicy : IWeComLaunchPolicy
        {
            internal bool BeginCalled;
            public WeComRegistryRecoveryResult RecoverPendingSession() => throw new UnauthorizedAccessException("fixture");
            public IDisposable BeginLaunchSession(int count) { BeginCalled = true; throw new Exception("Unsafe registry session"); }
        }

        internal static void NativeFamilyExit() => NativeFamily(false);
        internal static void NativeLateChild() => NativeFamily(true);

        private static void NativeFamily(bool lateChild)
        {
            var root = NewDirectory();
            var owned = new List<Process>();
            try
            {
                var main = Path.Combine(root, "Weixin.exe"); var helper = Path.Combine(root, "WeChatAppEx.exe");
                CompileFixture(main); File.Copy(main, helper);
                var state = Path.Combine(root, "state.txt");
                var unrelated = Process.Start(new ProcessStartInfo(helper, "--idle") { UseShellExecute = false, CreateNoWindow = true });
                owned.Add(unrelated);
                var parent = Process.Start(new ProcessStartInfo(main, (lateChild ? "--late " : "--root ") + "\"" + state + "\"")
                    { UseShellExecute = false, CreateNoWindow = true });
                owned.Add(parent);
                var wait = Stopwatch.StartNew();
                while (!FixtureReady(state) && wait.Elapsed < TimeSpan.FromSeconds(10)) Thread.Sleep(40);
                Assert(FixtureReady(state), "Family fixture never became ready.");
                var environment = new WindowsProcessEnvironment((path, kind) => NativeProcessControl.PathsEqual(path, main),
                    path => NativeProcessControl.PathsEqual(path, helper));
                var result = new InstanceManager(environment).ExitAllInstancesAsync(new AppDefinition(AppKind.WeChat, "微信", main), null,
                    CancellationToken.None).GetAwaiter().GetResult();
                Assert(result.Success && result.RemainingProcessCount == 0 && result.AfterCount == 0,
                    "Native family shutdown did not converge: " + result.Message);
                Assert(parent.WaitForExit(3000) && !unrelated.HasExited, "A same-named helper outside the family was closed.");
                var childIds = File.ReadAllLines(state).Where(line => line.StartsWith("child=")).Select(line => int.Parse(line.Substring(6))).ToArray();
                Assert(childIds.Length == 1, "Expected one related helper fixture.");
                foreach (var id in childIds)
                {
                    try { using (var process = Process.GetProcessById(id)) Assert(process.HasExited, "An orphan helper survived shutdown."); }
                    catch (ArgumentException) { }
                }
                Assert(result.ForcedProcessCount >= 1, "The non-window helper was not terminated after graceful exit.");
            }
            finally
            {
                var state = Path.Combine(root, "state.txt");
                var childIds = File.Exists(state) ? File.ReadAllLines(state).Where(value => value.StartsWith("child="))
                    .Select(value => int.Parse(value.Substring(6))) : Enumerable.Empty<int>();
                var ids = owned.Where(value => value != null).Select(value => value.Id).Concat(childIds).ToArray();
                var cleanup = new WindowsProcessEnvironment();
                cleanup.CloseProcesses(ids, Path.Combine(root, "Weixin.exe"), TimeSpan.Zero, CancellationToken.None);
                cleanup.CloseProcesses(ids, Path.Combine(root, "WeChatAppEx.exe"), TimeSpan.Zero, CancellationToken.None);
                foreach (var process in owned.Where(value => value != null)) process.Dispose();
                Program.DeleteTestDirectoryWithRetries(root);
            }
        }

        private static void CompileFixture(string path)
        {
            const string source = @"
using System; using System.Diagnostics; using System.IO; using System.Threading; using System.Windows.Forms;
class Fixture {
 [STAThread] static void Main(string[] args) {
  if(args.Length == 0 || args[0] == ""--idle"") { Thread.Sleep(30000); return; }
  string state = args[1]; bool late = args[0] == ""--late"";
  Action spawn = () => { var child = Process.Start(new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,""WeChatAppEx.exe""),""--idle"") { UseShellExecute=false, CreateNoWindow=true }); File.AppendAllText(state,""child=""+child.Id+Environment.NewLine); };
  var form = new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Left=-15000, Top=-15000, Width=160, Height=100, Text=""Shutdown regression fixture"" };
  form.Shown += (s,e) => { if(!late) spawn(); File.AppendAllText(state,""ready""+Environment.NewLine); };
  form.FormClosing += (s,e) => { if(late) spawn(); };
  Application.Run(form);
 }
}";
            using (var provider = new CSharpCodeProvider())
            {
                var options = new CompilerParameters(new[] { "System.dll", "System.Core.dll", "System.Windows.Forms.dll", "System.Drawing.dll" }, path)
                    { GenerateExecutable = true, GenerateInMemory = false, CompilerOptions = "/target:winexe /optimize" };
                var result = provider.CompileAssemblyFromSource(options, source);
                Assert(!result.Errors.HasErrors, "Could not compile isolated process fixture: " + string.Join("; ", result.Errors.Cast<CompilerError>()));
            }
        }

        private static bool FixtureReady(string path)
        {
            try { return File.Exists(path) && File.ReadAllLines(path).Contains("ready"); }
            catch (IOException) { return false; }
        }

        internal static void LockedInstallRecovery()
        {
            var root = Path.Combine(Path.GetTempPath(), "wechat-duokai-locked-install-" + Guid.NewGuid().ToString("N"));
            FileStream held = null;
            try
            {
                try
                {
                    InstallerEngine.ApplyInstallPayloadTransactionForTests(root, 1,
                        staging => held = File.Open(Directory.EnumerateFiles(staging).First(), FileMode.Open, FileAccess.Read, FileShare.None));
                    throw new Exception("Install interruption was not injected.");
                }
                catch (IOException) { }
                Assert(ApplicationStorage.IsInstallationPending(root) && !InstallerEngine.ValidateInstallDirectory(root) &&
                       InstallerEngine.ValidateInstallTarget(root, out _), "Locked staging became a runnable or non-retryable fresh install.");
                held.Dispose(); held = null;
                InstallerEngine.ApplyInstallPayloadTransactionForTests(root, 0);
                Assert(InstallerEngine.ValidateInstallDirectory(root) && !ApplicationStorage.IsInstallationPending(root),
                    "Retry after releasing a staging lock did not finish the installation.");
            }
            finally { held?.Dispose(); Program.DeleteTestDirectoryWithRetries(root); }
        }

        private static string NewDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "wechat-duokai-shutdown-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path); return path;
        }
        private static ClientShutdownState State(AppDefinition app, int id, int visible = 1) => new ClientShutdownState
        { Processes = id == 0 ? new ProcessSnapshot[0] : new[] { P(id, app.ExecutablePath) }, InstanceCount = id == 0 ? 0 : 1,
            VisibleWindowCount = id == 0 ? 0 : visible, WindowInspectionSucceeded = true };
        private static void WithFakeApp(Action<AppDefinition, ScriptedEnvironment> action)
        {
            var root = NewDirectory();
            try { var path = Path.Combine(root, "Weixin.exe"); File.WriteAllText(path, "fixture"); action(new AppDefinition(AppKind.WeChat, "微信", path), new ScriptedEnvironment()); }
            finally { Program.DeleteTestDirectoryWithRetries(root); }
        }
        private sealed class ScriptedSession : IClientShutdownSession
        {
            internal ClientShutdownState[] States = new[] { new ClientShutdownState() };
            internal int Reads, CloseCalls, Denied;
            internal bool Disposed;
            public ClientShutdownState Refresh() => States[Math.Min(Reads++, States.Length - 1)];
            public ProcessCloseSummary CloseCurrentProcesses(TimeSpan timeout, CancellationToken token)
            { token.ThrowIfCancellationRequested(); CloseCalls++; return new ProcessCloseSummary { AccessDeniedProcessCount = Denied }; }
            public void Dispose() { Disposed = true; }
        }
        private sealed class ScriptedEnvironment : IProcessEnvironment, IClientShutdownEnvironment
        {
            internal ScriptedSession Session = new ScriptedSession();
            internal int DelayCalls, BeginCalls;
            public int CurrentSessionId => 7;
            public IClientShutdownSession BeginClientShutdown(AppDefinition app) { BeginCalls++; return Session; }
            public int GetKnownClientProcessCount(AppDefinition app) => 0;
            public IReadOnlyList<ProcessSnapshot> FindProcesses(IEnumerable<string> names) => new ProcessSnapshot[0];
            public IReadOnlyDictionary<int, int> GetParentProcessMap() => new Dictionary<int, int>();
            public void StartApplication(string path) => throw new NotSupportedException();
            public ProcessCloseSummary CloseProcesses(IReadOnlyCollection<int> ids, string path, TimeSpan timeout, CancellationToken token) => throw new NotSupportedException();
            public Task DelayAsync(TimeSpan delay, CancellationToken token) { token.ThrowIfCancellationRequested(); DelayCalls++; return Task.CompletedTask; }
        }
    }
}
