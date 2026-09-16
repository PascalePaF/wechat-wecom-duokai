using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace WechatDuokai.Core
{
    public enum WeComRegistryRecoveryOutcome
    {
        None,
        Restored,
        ExternalStatePreserved,
        Deferred,
        RetryRequired,
        InvalidJournal
    }

    public sealed class WeComRegistryRecoveryResult
    {
        internal static readonly WeComRegistryRecoveryResult NoPendingRecovery =
            new WeComRegistryRecoveryResult(WeComRegistryRecoveryOutcome.None, null);

        internal WeComRegistryRecoveryResult(WeComRegistryRecoveryOutcome outcome, string message)
        {
            Outcome = outcome;
            Message = message;
        }

        public WeComRegistryRecoveryOutcome Outcome { get; }
        public string Message { get; }
        public bool CanStartNewSession => Outcome == WeComRegistryRecoveryOutcome.None ||
                                          Outcome == WeComRegistryRecoveryOutcome.Restored ||
                                          Outcome == WeComRegistryRecoveryOutcome.ExternalStatePreserved;
    }

    public interface IWeComLaunchPolicy
    {
        IDisposable BeginLaunchSession(int targetCount);
        WeComRegistryRecoveryResult RecoverPendingSession();
    }

    /// <summary>
    /// Applies WeCom's temporary current-user multi-instance state. Before the registry
    /// changes, an atomic journal is flushed below the executable directory. A later
    /// process can recover after a crash or power loss, while a third-party change still
    /// takes precedence over the stale original snapshot.
    /// </summary>
    internal sealed class WindowsWeComLaunchPolicy : IWeComLaunchPolicy
    {
        private const string DefaultRegistryPath = @"SOFTWARE\Tencent\WXWork";
        private const string ValueName = "multi_instances";
        private const string DefaultGateName = @"Local\WechatDuokai.WeComRegistryPolicy";
        private const string JournalFileName = "wecom-registry-transaction.ini";
        private const string AuditFileName = "registry-recovery.log";
        private const long MaximumAuditBytes = 256 * 1024;

        private readonly string _registryPath;
        private readonly string _gateName;
        private readonly string _dataDirectory;

        internal WindowsWeComLaunchPolicy()
            : this(DefaultRegistryPath, DefaultGateName, ApplicationStorage.DataDirectory)
        {
        }

        internal WindowsWeComLaunchPolicy(string registryPath, string gateName)
            : this(registryPath, gateName, ApplicationStorage.DataDirectory)
        {
        }

        internal WindowsWeComLaunchPolicy(string registryPath, string gateName, string dataDirectory)
        {
            _registryPath = registryPath ?? throw new ArgumentNullException(nameof(registryPath));
            _gateName = gateName ?? throw new ArgumentNullException(nameof(gateName));
            _dataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
        }

        internal string JournalPath => Path.Combine(_dataDirectory, "recovery", JournalFileName);
        internal string AuditLogPath => Path.Combine(_dataDirectory, "logs", AuditFileName);

        public WeComRegistryRecoveryResult RecoverPendingSession()
        {
            if (!File.Exists(JournalPath))
            {
                return HasQuarantinedJournal()
                    ? Result(WeComRegistryRecoveryOutcome.InvalidJournal,
                        "存在已隔离的无效注册表恢复日志；为避免覆盖未知状态，本次不启用新的临时注册表会话。",
                        "RECOVERY_BLOCKED quarantined-journal")
                    : WeComRegistryRecoveryResult.NoPendingRecovery;
            }

            Semaphore gate = null;
            var ownsGate = false;
            try
            {
                gate = new Semaphore(1, 1, _gateName);
                ownsGate = gate.WaitOne(TimeSpan.FromSeconds(3));
                if (!ownsGate)
                {
                    return Result(WeComRegistryRecoveryOutcome.Deferred,
                        "检测到另一个助手会话仍在处理企业微信注册表，已暂缓恢复。",
                        "RECOVERY_DEFERRED gate-busy");
                }
                return RecoverPendingSessionUnderGate(true);
            }
            catch (Exception ex)
            {
                return Result(WeComRegistryRecoveryOutcome.RetryRequired,
                    "暂时无法检查上次企业微信注册表会话，将在下次启动时重试。",
                    "RECOVERY_RETRY " + SafeMessage(ex));
            }
            finally
            {
                ReleaseGate(gate, ownsGate);
            }
        }

        public IDisposable BeginLaunchSession(int targetCount)
        {
            if (targetCount <= 1) return EmptyScope.Instance;

            Semaphore gate = null;
            var ownsGate = false;
            RegistryKey key = null;
            RecoveryJournal journal = null;
            try
            {
                EnsureStorageDirectories();
                if (HasQuarantinedJournal())
                {
                    AppendAudit("SESSION_BLOCKED quarantined-journal");
                    return EmptyScope.Instance;
                }
                gate = new Semaphore(1, 1, _gateName);
                ownsGate = gate.WaitOne(TimeSpan.FromSeconds(3));
                if (!ownsGate)
                {
                    gate.Dispose();
                    return EmptyScope.Instance;
                }

                var recovery = RecoverPendingSessionUnderGate(false);
                if (!recovery.CanStartNewSession)
                {
                    ReleaseGate(gate, ownsGate);
                    return EmptyScope.Instance;
                }

                key = Registry.CurrentUser.CreateSubKey(_registryPath);
                if (key == null)
                {
                    ReleaseGate(gate, ownsGate);
                    return EmptyScope.Instance;
                }

                var originalState = ReadValueState(key);
                var temporaryState = targetCount == 2
                    ? new RegistryValueState(true, 2, RegistryValueKind.DWord)
                    : RegistryValueState.Missing;

                journal = RecoveryJournal.Create(_registryPath, ValueName, originalState, temporaryState);
                WriteJournal(journal);
                ApplyState(key, temporaryState);
                key.Flush();
                AppendAudit("SESSION_APPLIED transaction=" + journal.TransactionId +
                            " target=" + targetCount + " ownerPid=" + journal.OwnerProcessId);
                return new RegistryRestoreScope(this, key, gate, ownsGate, journal);
            }
            catch (Exception ex)
            {
                AppendAudit("SESSION_PREPARE_FAILED " + SafeMessage(ex));
                if (key != null && journal != null) Reconcile(key, journal, "prepare-failure");
                key?.Dispose();
                ReleaseGate(gate, ownsGate);
                return EmptyScope.Instance;
            }
        }

        private WeComRegistryRecoveryResult RecoverPendingSessionUnderGate(bool respectLiveOwner)
        {
            if (!File.Exists(JournalPath)) return WeComRegistryRecoveryResult.NoPendingRecovery;

            RecoveryJournal journal;
            try
            {
                journal = RecoveryJournal.Read(JournalPath, _registryPath, ValueName);
            }
            catch (Exception ex)
            {
                QuarantineInvalidJournal();
                return Result(WeComRegistryRecoveryOutcome.InvalidJournal,
                    "注册表恢复日志无效，已隔离且未修改当前注册表。",
                    "RECOVERY_INVALID " + SafeMessage(ex));
            }

            if (respectLiveOwner && journal.IsOwnerStillRunning())
            {
                return Result(WeComRegistryRecoveryOutcome.Deferred,
                    "上一次企业微信启动会话仍在运行，暂不恢复注册表。",
                    "RECOVERY_DEFERRED live-owner transaction=" + journal.TransactionId);
            }

            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(_registryPath))
                {
                    if (key == null)
                    {
                        return Result(WeComRegistryRecoveryOutcome.RetryRequired,
                            "无法打开企业微信注册表，恢复日志已保留供下次重试。",
                            "RECOVERY_RETRY key-unavailable transaction=" + journal.TransactionId);
                    }
                    return Reconcile(key, journal, "startup");
                }
            }
            catch (Exception ex)
            {
                return Result(WeComRegistryRecoveryOutcome.RetryRequired,
                    "本次未能完成企业微信注册表恢复，日志已保留供下次重试。",
                    "RECOVERY_RETRY transaction=" + journal.TransactionId + " " + SafeMessage(ex));
            }
        }

        private WeComRegistryRecoveryResult Reconcile(RegistryKey key, RecoveryJournal journal, string reason)
        {
            try
            {
                var currentState = ReadValueState(key);
                if (!RegistryValueState.AreEqual(currentState, journal.TemporaryState))
                {
                    DeleteJournal();
                    return Result(WeComRegistryRecoveryOutcome.ExternalStatePreserved,
                        "检测到企业微信注册表已由其他程序修改，已保留外部新设置。",
                        "EXTERNAL_STATE_PRESERVED transaction=" + journal.TransactionId + " reason=" + reason);
                }

                ApplyState(key, journal.OriginalState);
                key.Flush();
                DeleteJournal();
                return Result(WeComRegistryRecoveryOutcome.Restored,
                    reason == "startup"
                        ? "已安全恢复上次异常中断前的企业微信注册表设置。"
                        : "企业微信临时注册表设置已恢复。",
                    "REGISTRY_RESTORED transaction=" + journal.TransactionId + " reason=" + reason);
            }
            catch (Exception ex)
            {
                return Result(WeComRegistryRecoveryOutcome.RetryRequired,
                    "企业微信注册表暂未恢复，恢复日志已保留供下次启动重试。",
                    "RECOVERY_RETRY transaction=" + journal.TransactionId + " reason=" + reason + " " + SafeMessage(ex));
            }
        }

        private static RegistryValueState ReadValueState(RegistryKey key)
        {
            var exists = key.GetValueNames().Any(name =>
                string.Equals(name, ValueName, StringComparison.OrdinalIgnoreCase));
            return !exists
                ? RegistryValueState.Missing
                : new RegistryValueState(true,
                    key.GetValue(ValueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames),
                    key.GetValueKind(ValueName));
        }

        private static void ApplyState(RegistryKey key, RegistryValueState state)
        {
            if (state.Exists) key.SetValue(ValueName, state.Value, state.Kind);
            else key.DeleteValue(ValueName, false);
        }

        private void EnsureStorageDirectories()
        {
            if (PathsEqual(_dataDirectory, ApplicationStorage.DataDirectory))
            {
                ApplicationStorage.EnsureReady();
            }
            else
            {
                Directory.CreateDirectory(_dataDirectory);
                var info = new DirectoryInfo(Path.GetFullPath(_dataDirectory));
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Registry recovery storage cannot be a reparse point.");
                File.WriteAllText(Path.Combine(_dataDirectory, ApplicationStorage.DataMarkerName),
                    ApplicationStorage.DataMarkerValue, Encoding.UTF8);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(JournalPath));
            Directory.CreateDirectory(Path.GetDirectoryName(AuditLogPath));
        }

        private void WriteJournal(RecoveryJournal journal)
        {
            EnsureStorageDirectories();
            if (File.Exists(JournalPath))
                throw new IOException("A pending registry recovery journal already exists.");

            var temporaryPath = JournalPath + ".new-" + Guid.NewGuid().ToString("N");
            var bytes = new UTF8Encoding(true).GetBytes(journal.Serialize());
            try
            {
                using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                File.Move(temporaryPath, JournalPath);
            }
            finally
            {
                try { if (File.Exists(temporaryPath)) File.Delete(temporaryPath); }
                catch (Exception) { }
            }
            AppendAudit("JOURNAL_DURABLE transaction=" + journal.TransactionId +
                        " ownerPid=" + journal.OwnerProcessId);
        }

        private void DeleteJournal()
        {
            if (File.Exists(JournalPath)) File.Delete(JournalPath);
        }

        private void QuarantineInvalidJournal()
        {
            try
            {
                if (!File.Exists(JournalPath)) return;
                File.Move(JournalPath, JournalPath + ".invalid-" +
                                      DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
            }
            catch (Exception) { }
        }

        private bool HasQuarantinedJournal()
        {
            try
            {
                var directory = Path.GetDirectoryName(JournalPath);
                return Directory.Exists(directory) && Directory.EnumerateFiles(directory,
                    JournalFileName + ".invalid-*", SearchOption.TopDirectoryOnly).Any();
            }
            catch (Exception)
            {
                return true;
            }
        }

        private WeComRegistryRecoveryResult Result(WeComRegistryRecoveryOutcome outcome,
            string message, string audit)
        {
            AppendAudit(audit);
            return new WeComRegistryRecoveryResult(outcome, message);
        }

        private void AppendAudit(string message)
        {
            try
            {
                Directory.CreateDirectory(_dataDirectory);
                Directory.CreateDirectory(Path.GetDirectoryName(AuditLogPath));
                if (File.Exists(AuditLogPath) && new FileInfo(AuditLogPath).Length > MaximumAuditBytes)
                {
                    var previous = AuditLogPath + ".1";
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(AuditLogPath, previous);
                }
                File.AppendAllText(AuditLogPath,
                    DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture) +
                    " " + message + Environment.NewLine, new UTF8Encoding(true));
            }
            catch (Exception) { }
        }

        private static void ReleaseGate(Semaphore gate, bool ownsGate)
        {
            if (gate == null) return;
            if (ownsGate)
            {
                try { gate.Release(); }
                catch (SemaphoreFullException) { }
            }
            gate.Dispose();
        }

        private static bool PathsEqual(string left, string right)
        {
            try
            {
                return string.Equals(Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar),
                    Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception) { return false; }
        }

        private static string SafeMessage(Exception exception)
        {
            return exception == null ? "unknown" : exception.GetType().Name + ":" +
                (exception.Message ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        }

        private sealed class RegistryRestoreScope : IDisposable
        {
            private WindowsWeComLaunchPolicy _owner;
            private RegistryKey _key;
            private Semaphore _gate;
            private bool _ownsGate;
            private readonly RecoveryJournal _journal;

            internal RegistryRestoreScope(WindowsWeComLaunchPolicy owner, RegistryKey key,
                Semaphore gate, bool ownsGate, RecoveryJournal journal)
            {
                _owner = owner;
                _key = key;
                _gate = gate;
                _ownsGate = ownsGate;
                _journal = journal;
            }

            public void Dispose()
            {
                var owner = Interlocked.Exchange(ref _owner, null);
                var key = Interlocked.Exchange(ref _key, null);
                var gate = Interlocked.Exchange(ref _gate, null);
                var ownsGate = _ownsGate;
                _ownsGate = false;
                try
                {
                    if (owner != null && key != null) owner.Reconcile(key, _journal, "normal-dispose");
                }
                finally
                {
                    key?.Dispose();
                    ReleaseGate(gate, ownsGate);
                }
            }
        }

        private sealed class RecoveryJournal
        {
            private const string Format = "wechat-duokai-wecom-registry-journal:v1";

            internal string TransactionId { get; private set; }
            internal long CreatedUtcTicks { get; private set; }
            internal int OwnerProcessId { get; private set; }
            internal long OwnerStartUtcTicks { get; private set; }
            internal string RegistryPath { get; private set; }
            internal string RegistryValueName { get; private set; }
            internal RegistryValueState OriginalState { get; private set; }
            internal RegistryValueState TemporaryState { get; private set; }

            internal static RecoveryJournal Create(string registryPath, string valueName,
                RegistryValueState originalState, RegistryValueState temporaryState)
            {
                long startTicks;
                try { startTicks = Process.GetCurrentProcess().StartTime.ToUniversalTime().Ticks; }
                catch (Exception) { startTicks = 0; }
                return new RecoveryJournal
                {
                    TransactionId = Guid.NewGuid().ToString("N"),
                    CreatedUtcTicks = DateTime.UtcNow.Ticks,
                    OwnerProcessId = Process.GetCurrentProcess().Id,
                    OwnerStartUtcTicks = startTicks,
                    RegistryPath = registryPath,
                    RegistryValueName = valueName,
                    OriginalState = originalState,
                    TemporaryState = temporaryState
                };
            }

            internal string Serialize()
            {
                var lines = new List<string>
                {
                    "Format=" + Format,
                    "TransactionId=" + TransactionId,
                    "CreatedUtcTicks=" + CreatedUtcTicks.ToString(CultureInfo.InvariantCulture),
                    "OwnerProcessId=" + OwnerProcessId.ToString(CultureInfo.InvariantCulture),
                    "OwnerStartUtcTicks=" + OwnerStartUtcTicks.ToString(CultureInfo.InvariantCulture),
                    "RegistryPath=" + Encode(RegistryPath),
                    "RegistryValueName=" + Encode(RegistryValueName)
                };
                OriginalState.AppendJournalLines("Original", lines);
                TemporaryState.AppendJournalLines("Temporary", lines);
                return string.Join(Environment.NewLine, lines) + Environment.NewLine;
            }

            internal static RecoveryJournal Read(string path, string expectedRegistryPath,
                string expectedValueName)
            {
                var values = File.ReadAllLines(path, Encoding.UTF8)
                    .Select(line => new { Line = line, Separator = line.IndexOf('=') })
                    .Where(item => item.Separator > 0)
                    .ToDictionary(item => item.Line.Substring(0, item.Separator),
                        item => item.Line.Substring(item.Separator + 1), StringComparer.OrdinalIgnoreCase);
                if (Required(values, "Format") != Format)
                    throw new InvalidDataException("Unsupported recovery journal format.");

                var journal = new RecoveryJournal
                {
                    TransactionId = Required(values, "TransactionId"),
                    CreatedUtcTicks = long.Parse(Required(values, "CreatedUtcTicks"), CultureInfo.InvariantCulture),
                    OwnerProcessId = int.Parse(Required(values, "OwnerProcessId"), CultureInfo.InvariantCulture),
                    OwnerStartUtcTicks = long.Parse(Required(values, "OwnerStartUtcTicks"), CultureInfo.InvariantCulture),
                    RegistryPath = Decode(Required(values, "RegistryPath")),
                    RegistryValueName = Decode(Required(values, "RegistryValueName")),
                    OriginalState = RegistryValueState.FromJournal("Original", values),
                    TemporaryState = RegistryValueState.FromJournal("Temporary", values)
                };
                if (!string.Equals(journal.RegistryPath, expectedRegistryPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(journal.RegistryValueName, expectedValueName, StringComparison.OrdinalIgnoreCase) ||
                    journal.OwnerProcessId <= 0 || string.IsNullOrWhiteSpace(journal.TransactionId))
                    throw new InvalidDataException("Recovery journal target or owner is invalid.");
                return journal;
            }

            internal bool IsOwnerStillRunning()
            {
                try
                {
                    using (var process = Process.GetProcessById(OwnerProcessId))
                    {
                        if (process.HasExited) return false;
                        if (OwnerStartUtcTicks <= 0) return true;
                        return process.StartTime.ToUniversalTime().Ticks == OwnerStartUtcTicks;
                    }
                }
                catch (Exception) { return false; }
            }

            private static string Required(IDictionary<string, string> values, string key)
            {
                string value;
                if (!values.TryGetValue(key, out value))
                    throw new InvalidDataException("Recovery journal field is missing: " + key);
                return value;
            }

            private static string Encode(string value)
            {
                return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
            }

            private static string Decode(string value)
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(value));
            }
        }

        private sealed class RegistryValueState
        {
            internal static readonly RegistryValueState Missing =
                new RegistryValueState(false, null, RegistryValueKind.None);

            internal RegistryValueState(bool exists, object value, RegistryValueKind kind)
            {
                Exists = exists;
                Value = value;
                Kind = kind;
            }

            internal bool Exists { get; }
            internal object Value { get; }
            internal RegistryValueKind Kind { get; }

            internal void AppendJournalLines(string prefix, ICollection<string> lines)
            {
                lines.Add(prefix + "Exists=" + (Exists ? "1" : "0"));
                lines.Add(prefix + "Kind=" + ((int)Kind).ToString(CultureInfo.InvariantCulture));
                if (!Exists)
                {
                    lines.Add(prefix + "Format=missing");
                    lines.Add(prefix + "Value=");
                    return;
                }

                string format;
                string serialized;
                switch (Kind)
                {
                    case RegistryValueKind.DWord:
                        format = "int32";
                        serialized = Convert.ToInt32(Value, CultureInfo.InvariantCulture)
                            .ToString(CultureInfo.InvariantCulture);
                        break;
                    case RegistryValueKind.QWord:
                        format = "int64";
                        serialized = Convert.ToInt64(Value, CultureInfo.InvariantCulture)
                            .ToString(CultureInfo.InvariantCulture);
                        break;
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString:
                        format = "string";
                        serialized = Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
                        break;
                    case RegistryValueKind.MultiString:
                        format = "strings";
                        serialized = string.Join(",", ((string[])Value).Select(item =>
                            Convert.ToBase64String(Encoding.UTF8.GetBytes(item ?? string.Empty))));
                        break;
                    case RegistryValueKind.Binary:
                    case RegistryValueKind.None:
                        format = "bytes";
                        serialized = Convert.ToBase64String((byte[])Value);
                        break;
                    default:
                        throw new InvalidDataException("Unsupported registry value kind: " + Kind);
                }
                lines.Add(prefix + "Format=" + format);
                lines.Add(prefix + "Value=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(serialized)));
            }

            internal static RegistryValueState FromJournal(string prefix,
                IDictionary<string, string> values)
            {
                var exists = Get(values, prefix + "Exists") == "1";
                var kind = (RegistryValueKind)int.Parse(Get(values, prefix + "Kind"), CultureInfo.InvariantCulture);
                var format = Get(values, prefix + "Format");
                var encodedValue = Get(values, prefix + "Value");
                if (!exists)
                {
                    if (format != "missing") throw new InvalidDataException("Missing registry state is malformed.");
                    return Missing;
                }

                if (!FormatMatchesKind(kind, format))
                    throw new InvalidDataException("Registry value kind does not match its journal format.");

                var serialized = Encoding.UTF8.GetString(Convert.FromBase64String(encodedValue));
                object value;
                switch (format)
                {
                    case "int32": value = int.Parse(serialized, CultureInfo.InvariantCulture); break;
                    case "int64": value = long.Parse(serialized, CultureInfo.InvariantCulture); break;
                    case "string": value = serialized; break;
                    case "strings":
                        value = string.IsNullOrEmpty(serialized)
                            ? new string[0]
                            : serialized.Split(',').Select(item =>
                                Encoding.UTF8.GetString(Convert.FromBase64String(item))).ToArray();
                        break;
                    case "bytes": value = Convert.FromBase64String(serialized); break;
                    default: throw new InvalidDataException("Unsupported registry journal value format.");
                }
                return new RegistryValueState(true, value, kind);
            }

            private static bool FormatMatchesKind(RegistryValueKind kind, string format)
            {
                switch (kind)
                {
                    case RegistryValueKind.DWord: return format == "int32";
                    case RegistryValueKind.QWord: return format == "int64";
                    case RegistryValueKind.String:
                    case RegistryValueKind.ExpandString: return format == "string";
                    case RegistryValueKind.MultiString: return format == "strings";
                    case RegistryValueKind.Binary:
                    case RegistryValueKind.None: return format == "bytes";
                    default: return false;
                }
            }

            internal static bool AreEqual(RegistryValueState left, RegistryValueState right)
            {
                if (left == null || right == null || left.Exists != right.Exists) return false;
                if (!left.Exists) return true;
                if (left.Kind != right.Kind) return false;

                var leftBytes = left.Value as byte[];
                var rightBytes = right.Value as byte[];
                if (leftBytes != null || rightBytes != null)
                    return leftBytes != null && rightBytes != null && leftBytes.SequenceEqual(rightBytes);
                var leftStrings = left.Value as string[];
                var rightStrings = right.Value as string[];
                if (leftStrings != null || rightStrings != null)
                    return leftStrings != null && rightStrings != null && leftStrings.SequenceEqual(rightStrings);
                return Equals(left.Value, right.Value);
            }

            private static string Get(IDictionary<string, string> values, string key)
            {
                string value;
                if (!values.TryGetValue(key, out value))
                    throw new InvalidDataException("Recovery journal field is missing: " + key);
                return value;
            }
        }

        private sealed class EmptyScope : IDisposable
        {
            internal static readonly EmptyScope Instance = new EmptyScope();
            public void Dispose() { }
        }
    }
}
