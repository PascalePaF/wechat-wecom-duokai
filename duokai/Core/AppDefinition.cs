using System;
using System.Collections.Generic;
using System.IO;

namespace shuangkai.Core
{
    internal enum AppKind
    {
        WeChat,
        WeCom
    }

    internal sealed class AppDefinition
    {
        public AppDefinition(AppKind kind, string displayName, string executablePath)
        {
            Kind = kind;
            DisplayName = displayName;
            ExecutablePath = executablePath;
        }

        public AppKind Kind { get; }

        public string DisplayName { get; }

        public string ExecutablePath { get; }

        public bool IsAvailable => !string.IsNullOrWhiteSpace(ExecutablePath) && File.Exists(ExecutablePath);

        public IReadOnlyCollection<string> ProcessNames
        {
            get
            {
                if (Kind == AppKind.WeChat)
                {
                    return new[] { "WeChat", "Weixin" };
                }

                return new[] { "WXWork", "WeCom", "企业微信" };
            }
        }
    }

    internal sealed class LaunchResult
    {
        public bool Success { get; set; }

        public int BeforeCount { get; set; }

        public int AfterCount { get; set; }

        public int RequestedCount { get; set; }

        public int StartedCount { get; set; }

        public int ReleasedLockCount { get; set; }

        public string Message { get; set; }
    }
}
