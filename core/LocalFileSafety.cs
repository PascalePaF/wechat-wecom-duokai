using System;
using System.IO;
using WechatDuokai.IO;

namespace WechatDuokai.Core
{
    internal static class LocalFileSafety
    {
        internal static void RejectReparsePoints(string path)
        {
            SafeLocalFile.RejectReparsePoints(path);
        }

        internal static void EnsureDirectory(string path)
        {
            SafeLocalFile.EnsureDirectory(path);
        }

        internal static void EnsureDirectoryUnderRoot(string root, string directory)
        {
            var fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            var fullDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
            if (!string.Equals(fullRoot, fullDirectory, StringComparison.OrdinalIgnoreCase) &&
                !fullDirectory.StartsWith(fullRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new IOException("本地数据目录必须位于指定程序目录内。");
            EnsureDirectory(fullDirectory);
        }

        internal static void WriteBytesAtomic(string path, byte[] bytes)
        {
            SafeLocalFile.WriteBytesAtomic(path, bytes);
        }

        internal static void WriteTextAtomic(string path, string contents)
        {
            SafeLocalFile.WriteTextAtomic(path, contents);
        }
    }
}
