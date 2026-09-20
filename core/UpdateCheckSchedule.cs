using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WechatDuokai.Core
{
    public static class UpdateCheckSchedule
    {
        internal const string StateFileName = "update-state.ini";
        internal static readonly TimeSpan SuccessInterval = TimeSpan.FromHours(24);
        internal static readonly TimeSpan MaximumBackoff = TimeSpan.FromHours(24);
        private static readonly object SyncRoot = new object();

        public static bool ShouldCheckAutomatically()
        {
            try
            {
                ApplicationStorage.EnsureReady();
                return ShouldCheckAutomatically(ApplicationStorage.DataDirectory,
                    DateTimeOffset.UtcNow);
            }
            catch (Exception)
            {
                // A missing or damaged cache must never permanently disable update checks.
                return true;
            }
        }

        public static TimeSpan GetStartupDelay()
        {
            return CalculateStartupDelay(ApplicationStorage.ApplicationDirectory);
        }

        public static void RecordSuccess(string latestVersion)
        {
            try
            {
                ApplicationStorage.EnsureReady();
                RecordSuccess(ApplicationStorage.DataDirectory, DateTimeOffset.UtcNow,
                    latestVersion);
            }
            catch (Exception)
            {
                // Update checking still works when the optional local cache cannot be written.
            }
        }

        public static void RecordFailure(DateTimeOffset? retryAfterUtc)
        {
            try
            {
                ApplicationStorage.EnsureReady();
                RecordFailure(ApplicationStorage.DataDirectory, DateTimeOffset.UtcNow,
                    retryAfterUtc);
            }
            catch (Exception)
            {
                // Update checking still works when the optional local cache cannot be written.
            }
        }

        internal static bool ShouldCheckAutomatically(string dataDirectory,
            DateTimeOffset now)
        {
            lock (SyncRoot)
            {
                var state = Load(dataDirectory);
                if (!state.NextAutomaticCheckUtc.HasValue)
                {
                    return true;
                }

                // Treat impossible future values as damaged state. Remote response headers and
                // local clock changes are not allowed to suppress checks indefinitely.
                if (state.NextAutomaticCheckUtc.Value > now.Add(MaximumBackoff)
                    .Add(TimeSpan.FromMinutes(5)))
                {
                    return true;
                }
                return now >= state.NextAutomaticCheckUtc.Value;
            }
        }

        internal static TimeSpan CalculateStartupDelay(string applicationDirectory)
        {
            var normalized = Path.GetFullPath(applicationDirectory ?? string.Empty)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .ToUpperInvariant();
            using (var sha256 = SHA256.Create())
            {
                var digest = sha256.ComputeHash(Encoding.UTF8.GetBytes(normalized));
                var value = BitConverter.ToUInt32(digest, 0);
                return TimeSpan.FromSeconds(30 + value % 271); // 30 seconds through 5 minutes.
            }
        }

        internal static void RecordSuccess(string dataDirectory, DateTimeOffset now,
            string latestVersion)
        {
            lock (SyncRoot)
            {
                var state = Load(dataDirectory);
                state.LastAttemptUtc = now;
                state.LastSuccessUtc = now;
                state.NextAutomaticCheckUtc = now.Add(SuccessInterval);
                state.ConsecutiveFailures = 0;
                Version parsed;
                state.LatestVersion = Version.TryParse(latestVersion, out parsed)
                    ? parsed.ToString()
                    : string.Empty;
                Save(dataDirectory, state);
            }
        }

        internal static void RecordFailure(string dataDirectory, DateTimeOffset now,
            DateTimeOffset? retryAfterUtc)
        {
            lock (SyncRoot)
            {
                var state = Load(dataDirectory);
                state.LastAttemptUtc = now;
                state.ConsecutiveFailures = Math.Min(100,
                    Math.Max(0, state.ConsecutiveFailures) + 1);
                var delay = GetFailureDelay(state.ConsecutiveFailures);
                if (retryAfterUtc.HasValue && retryAfterUtc.Value > now)
                {
                    var serverDelay = retryAfterUtc.Value - now;
                    if (serverDelay > delay)
                    {
                        delay = serverDelay;
                    }
                }
                if (delay > MaximumBackoff)
                {
                    delay = MaximumBackoff;
                }
                state.NextAutomaticCheckUtc = now.Add(delay);
                Save(dataDirectory, state);
            }
        }

        internal static TimeSpan GetFailureDelay(int consecutiveFailures)
        {
            if (consecutiveFailures <= 1) return TimeSpan.FromHours(1);
            if (consecutiveFailures == 2) return TimeSpan.FromHours(6);
            return MaximumBackoff;
        }

        internal static UpdateCheckState Load(string dataDirectory)
        {
            var state = new UpdateCheckState();
            try
            {
                var path = GetStatePath(dataDirectory);
                if (!File.Exists(path)) return state;

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
                {
                    var line = (rawLine ?? string.Empty).Trim();
                    var separator = line.IndexOf('=');
                    if (separator <= 0) continue;
                    values[line.Substring(0, separator).Trim()] =
                        line.Substring(separator + 1).Trim();
                }

                state.LastAttemptUtc = ParseTimestamp(values, "LastAttemptUtc");
                state.LastSuccessUtc = ParseTimestamp(values, "LastSuccessUtc");
                state.NextAutomaticCheckUtc = ParseTimestamp(values,
                    "NextAutomaticCheckUtc");
                int failures;
                state.ConsecutiveFailures = values.TryGetValue("ConsecutiveFailures",
                                                out var failureText) &&
                                            int.TryParse(failureText,
                                                NumberStyles.None,
                                                CultureInfo.InvariantCulture, out failures)
                    ? Math.Max(0, Math.Min(100, failures))
                    : 0;
                state.LatestVersion = values.TryGetValue("LatestVersion", out var version)
                    ? version
                    : string.Empty;
            }
            catch (Exception)
            {
                return new UpdateCheckState();
            }
            return state;
        }

        private static DateTimeOffset? ParseTimestamp(IDictionary<string, string> values,
            string key)
        {
            if (!values.TryGetValue(key, out var text)) return null;
            DateTimeOffset parsed;
            return DateTimeOffset.TryParseExact(text, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out parsed)
                ? parsed.ToUniversalTime()
                : (DateTimeOffset?)null;
        }

        private static void Save(string dataDirectory, UpdateCheckState state)
        {
            Directory.CreateDirectory(dataDirectory);
            var path = GetStatePath(dataDirectory);
            var temporary = path + ".new";
            var lines = new[]
            {
                "Format=1",
                "LastAttemptUtc=" + FormatTimestamp(state.LastAttemptUtc),
                "LastSuccessUtc=" + FormatTimestamp(state.LastSuccessUtc),
                "NextAutomaticCheckUtc=" + FormatTimestamp(state.NextAutomaticCheckUtc),
                "ConsecutiveFailures=" + state.ConsecutiveFailures.ToString(
                    CultureInfo.InvariantCulture),
                "LatestVersion=" + (state.LatestVersion ?? string.Empty)
            };
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write,
                       FileShare.None, 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                foreach (var line in lines) writer.WriteLine(line);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(path))
            {
                File.Replace(temporary, path, null, true);
            }
            else
            {
                File.Move(temporary, path);
            }
        }

        private static string FormatTimestamp(DateTimeOffset? value)
        {
            return value.HasValue
                ? value.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
                : string.Empty;
        }

        private static string GetStatePath(string dataDirectory)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
            {
                throw new ArgumentException("Update state directory is required.",
                    nameof(dataDirectory));
            }
            return Path.Combine(Path.GetFullPath(dataDirectory), StateFileName);
        }
    }

    internal sealed class UpdateCheckState
    {
        internal DateTimeOffset? LastAttemptUtc { get; set; }

        internal DateTimeOffset? LastSuccessUtc { get; set; }

        internal DateTimeOffset? NextAutomaticCheckUtc { get; set; }

        internal int ConsecutiveFailures { get; set; }

        internal string LatestVersion { get; set; }
    }
}
