using System;
using System.IO;
using System.Text;

namespace WechatDuokai.Core
{
    internal static class LocalFileSafety
    {
        internal static void RejectReparsePoints(string path)
        {
            var current = Path.GetFullPath(path);
            while (!string.IsNullOrEmpty(current))
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                        throw new IOException("本地数据路径包含符号链接或目录联接，已停止读写。");
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                current = Path.GetDirectoryName(current);
            }
        }

        internal static void EnsureDirectory(string path)
        {
            RejectReparsePoints(path);
            Directory.CreateDirectory(path);
            RejectReparsePoints(path);
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
            EnsureDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            RejectReparsePoints(path);
            // Unique CreateNew files cannot overwrite pre-existing links or another writer's work.
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".new";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                RejectReparsePoints(path);
                if (File.Exists(path)) File.Replace(temporary, path, null, true);
                else File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        internal static void WriteTextAtomic(string path, string contents)
        {
            WriteBytesAtomic(path, new UTF8Encoding(false).GetBytes(contents));
        }
    }
}
