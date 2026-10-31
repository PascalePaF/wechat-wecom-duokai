using System;
using System.IO;
using System.Text;
using System.Collections.Generic;

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

        internal static string ReadTextBounded(string path, int maximumBytes)
        {
            if (maximumBytes <= 0 || maximumBytes > 1024 * 1024)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));
            RejectReparsePoints(path);
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > maximumBytes) throw new InvalidDataException("本地配置文件超过安全大小限制。");
                var bytes = new byte[maximumBytes + 1];
                var length = 0;
                int count;
                while (length < bytes.Length && (count = input.Read(bytes, length, bytes.Length - length)) > 0)
                    length += count;
                if (length > maximumBytes) throw new InvalidDataException("本地配置文件超过安全大小限制。");
                using (var buffer = new MemoryStream(bytes, 0, length, false))
                using (var reader = new StreamReader(buffer, Encoding.UTF8, true)) return reader.ReadToEnd();
            }
        }

        internal static string[] ReadLinesBounded(string path, int maximumBytes)
        {
            var lines = new List<string>();
            using (var reader = new StringReader(ReadTextBounded(path, maximumBytes)))
            {
                string line;
                while ((line = reader.ReadLine()) != null) lines.Add(line);
            }
            return lines.ToArray();
        }
    }
}
