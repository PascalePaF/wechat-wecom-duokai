using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace WechatDuokai.Core
{
    public sealed class ClientValidationResult
    {
        public bool IsValid { get; set; }

        public string ErrorMessage { get; set; }

        public string NormalizedPath { get; set; }

        public string ProductName { get; set; }

        public string FileVersion { get; set; }

        public string Publisher { get; set; }
    }

    /// <summary>
    /// Accepts only recognizable, Authenticode-signed Tencent desktop clients.
    /// It deliberately does not rely on a version allow-list, so official client
    /// maintenance releases do not require a helper update.
    /// </summary>
    public static class ClientExecutableValidator
    {
        private static readonly HashSet<string> WeChatExecutableNames = new HashSet<string>(
            new[] { "Weixin.exe", "WeChat.exe" }, StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> WeComExecutableNames = new HashSet<string>(
            new[] { "WXWork.exe", "WeCom.exe", "企业微信.exe" }, StringComparer.OrdinalIgnoreCase);

        public static ClientValidationResult Validate(string path, AppKind expectedKind)
        {
            var result = new ClientValidationResult();
            if (string.IsNullOrWhiteSpace(path))
            {
                result.ErrorMessage = "没有选择程序文件。";
                return result;
            }

            try
            {
                result.NormalizedPath = Path.GetFullPath(path.Trim(' ', '"'));
            }
            catch (Exception)
            {
                result.ErrorMessage = "所选路径无效。";
                return result;
            }

            if (!File.Exists(result.NormalizedPath))
            {
                result.ErrorMessage = "所选程序文件不存在。";
                return result;
            }

            var allowedNames = expectedKind == AppKind.WeChat ? WeChatExecutableNames : WeComExecutableNames;
            if (!allowedNames.Contains(Path.GetFileName(result.NormalizedPath)))
            {
                result.ErrorMessage = expectedKind == AppKind.WeChat
                    ? "请选择官方微信的 Weixin.exe 或 WeChat.exe。"
                    : "请选择官方企业微信的 WXWork.exe。";
                return result;
            }

            FileVersionInfo version;
            try
            {
                version = FileVersionInfo.GetVersionInfo(result.NormalizedPath);
                result.ProductName = version.ProductName ?? string.Empty;
                result.FileVersion = version.FileVersion ?? version.ProductVersion ?? string.Empty;
                var identityText = string.Join(" ", new[]
                {
                    version.CompanyName,
                    version.ProductName,
                    version.FileDescription,
                    version.OriginalFilename
                });

                if (identityText.IndexOf("Tencent", StringComparison.OrdinalIgnoreCase) < 0 ||
                    !MatchesProductIdentity(identityText, expectedKind))
                {
                    result.ErrorMessage = "文件的产品信息与所选客户端不一致，已拒绝使用。";
                    return result;
                }
            }
            catch (Exception)
            {
                result.ErrorMessage = "无法读取程序的产品信息，已拒绝使用。";
                return result;
            }

            string publisher;
            if (!HasValidTencentSignature(result.NormalizedPath, out publisher))
            {
                result.ErrorMessage = "没有验证到有效的腾讯数字签名。请从微信或企业微信官网下载客户端。";
                return result;
            }

            result.Publisher = publisher;
            result.IsValid = true;
            return result;
        }

        private static bool MatchesProductIdentity(string identityText, AppKind kind)
        {
            if (kind == AppKind.WeChat)
            {
                return identityText.IndexOf("Weixin", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       identityText.IndexOf("WeChat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       identityText.IndexOf("微信", StringComparison.OrdinalIgnoreCase) >= 0;
            }

            return identityText.IndexOf("WXWork", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identityText.IndexOf("WeCom", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   identityText.IndexOf("企业微信", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool HasValidTencentSignature(string path, out string publisher)
        {
            publisher = null;
            var fileInfo = new WinTrustFileInfo(path);
            var data = new WinTrustData(fileInfo);
            try
            {
                var action = WinTrustActionGenericVerifyV2;
                if (WinVerifyTrust(IntPtr.Zero, ref action, ref data) != 0)
                {
                    return false;
                }

                try
                {
                    var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                    publisher = certificate.Subject;
                    return publisher.IndexOf("Tencent", StringComparison.OrdinalIgnoreCase) >= 0;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            finally
            {
                data.Dispose();
                fileInfo.Dispose();
            }
        }

        private static readonly Guid WinTrustActionGenericVerifyV2 =
            new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

        [DllImport("wintrust.dll", ExactSpelling = true, PreserveSig = true, SetLastError = true)]
        private static extern uint WinVerifyTrust(IntPtr windowHandle, ref Guid actionId, ref WinTrustData trustData);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustFileInfo : IDisposable
        {
            private uint structureSize;
            private IntPtr filePath;
            private IntPtr fileHandle;
            private IntPtr knownSubject;

            internal WinTrustFileInfo(string path)
            {
                structureSize = (uint)Marshal.SizeOf(typeof(WinTrustFileInfo));
                filePath = Marshal.StringToCoTaskMemUni(path);
                fileHandle = IntPtr.Zero;
                knownSubject = IntPtr.Zero;
            }

            public void Dispose()
            {
                if (filePath != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(filePath);
                    filePath = IntPtr.Zero;
                }
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WinTrustData : IDisposable
        {
            private uint structureSize;
            private IntPtr policyCallbackData;
            private IntPtr sipClientData;
            private uint uiChoice;
            private uint revocationChecks;
            private uint unionChoice;
            private IntPtr fileInfo;
            private uint stateAction;
            private IntPtr stateData;
            private IntPtr urlReference;
            private uint providerFlags;
            private uint uiContext;

            internal WinTrustData(WinTrustFileInfo input)
            {
                structureSize = (uint)Marshal.SizeOf(typeof(WinTrustData));
                policyCallbackData = IntPtr.Zero;
                sipClientData = IntPtr.Zero;
                uiChoice = 2; // WTD_UI_NONE
                revocationChecks = 0; // WTD_REVOKE_NONE
                unionChoice = 1; // WTD_CHOICE_FILE
                fileInfo = Marshal.AllocCoTaskMem(Marshal.SizeOf(typeof(WinTrustFileInfo)));
                Marshal.StructureToPtr(input, fileInfo, false);
                stateAction = 0;
                stateData = IntPtr.Zero;
                urlReference = IntPtr.Zero;
                providerFlags = 0x1000; // WTD_CACHE_ONLY_URL_RETRIEVAL
                uiContext = 0;
            }

            public void Dispose()
            {
                if (fileInfo != IntPtr.Zero)
                {
                    Marshal.DestroyStructure(fileInfo, typeof(WinTrustFileInfo));
                    Marshal.FreeCoTaskMem(fileInfo);
                    fileInfo = IntPtr.Zero;
                }
            }
        }
    }
}
