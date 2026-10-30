using System;
using System.IO;
using System.Text;

namespace WechatDuokai.IO
{
    // Compiled into each independent executable; keep storage rules identical.
    internal static class SafeLocalFile
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

        internal static void WriteBytesAtomic(string path, byte[] bytes)
        {
            var fullPath = Path.GetFullPath(path);
            EnsureDirectory(Path.GetDirectoryName(fullPath));
            RejectReparsePoints(fullPath);
            var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".new";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                           FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                RejectReparsePoints(fullPath);
                if (File.Exists(fullPath)) File.Replace(temporary, fullPath, null, true);
                else File.Move(temporary, fullPath);
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
