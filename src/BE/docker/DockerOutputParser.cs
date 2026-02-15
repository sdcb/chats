using Chats.DockerInterface.Models;

namespace Chats.DockerInterface;

/// <summary>
/// Docker 命令输出解析器
/// </summary>
public static class DockerOutputParser
{
    /// <summary>
    /// 解析 Linux ls -la --full-time 命令的输出
    /// 格式: drwxr-xr-x 2 root root 4096 2024-01-15 10:30:45.000000000 +0000 dirname
    ///       -rw-r--r-- 1 root root 1234 2024-01-15 10:30:45.000000000 +0000 filename
    /// </summary>
    public static List<FileEntry> ParseLinuxLsOutput(string requestedPath, string output)
    {
        List<FileEntry> entries = [];
        string normalizedPath = NormalizeContainerPath(requestedPath).TrimEnd('/');

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (string line in lines)
        {
            // 跳过 total 行和空行
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("total ", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 解析 ls -la 输出
            // drwxr-xr-x 2 root root 4096 2024-01-15 10:30:45.000000000 +0000 dirname
            // 字段: permissions, links, owner, group, size, date, time, timezone, name
            string[] parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 9)
            {
                continue;
            }

            string permissions = parts[0];
            if (permissions.Length < 1)
            {
                continue;
            }

            // 跳过 . 和 .. 目录
            string name = string.Join(" ", parts.Skip(8)); // 文件名可能包含空格
            if (name == "." || name == "..")
            {
                continue;
            }

            bool isDirectory = permissions[0] == 'd';
            long size = 0;
            _ = long.TryParse(parts[4], out size);

            // 解析时间: 2024-01-15 10:30:45.000000000 +0000
            DateTimeOffset? lastModified = null;
            if (parts.Length >= 8)
            {
                string dateStr = $"{parts[5]} {parts[6]} {parts[7]}";
                if (DateTimeOffset.TryParse(dateStr, out DateTimeOffset parsed))
                {
                    lastModified = parsed;
                }
            }

            string fullPath = CombineContainerPath(normalizedPath, name);

            entries.Add(new FileEntry
            {
                Path = fullPath,
                Name = name,
                IsDirectory = isDirectory,
                Size = isDirectory ? 0 : size,
                LastModified = lastModified
            });
        }

        return entries;
    }

    /// <summary>
    /// 解析 Windows dir 命令的输出
    /// 格式: 2024/01/15  10:30    &lt;DIR&gt;          dirname
    ///       2024/01/15  10:30             1,234 filename.txt
    /// </summary>
    public static List<FileEntry> ParseWindowsDirOutput(string requestedPath, string output)
    {
        List<FileEntry> entries = [];
        string normalizedPath = NormalizeContainerPath(requestedPath).TrimEnd('/');

        string[] lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        bool inFileList = false;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            // 跳过头部信息行 (Volume, Directory of 等)
            if (trimmed.StartsWith("Volume", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Directory of", StringComparison.OrdinalIgnoreCase))
            {
                inFileList = true;
                continue;
            }

            // 跳过尾部统计信息行
            if (trimmed.Contains(" File(s)") || trimmed.Contains(" Dir(s)") ||
                trimmed.Contains("个文件") || trimmed.Contains("个目录"))
            {
                continue;
            }

            if (!inFileList)
            {
                continue;
            }

            // 解析 dir 输出行
            // 2024/01/15  10:30    <DIR>          dirname
            // 2024/01/15  10:30             1,234 filename.txt
            // 格式: 日期 时间 <DIR>或大小 文件名

            // 尝试匹配日期格式 (支持多种格式如 2024/01/15, 01/15/2024, 2024-01-15)
            if (trimmed.Length < 20)
            {
                continue;
            }

            // 查找 <DIR> 或数字大小
            int dirIdx = trimmed.IndexOf("<DIR>", StringComparison.OrdinalIgnoreCase);
            bool isDirectory = dirIdx >= 0;

            string name;
            long size = 0;
            DateTimeOffset? lastModified = null;

            if (isDirectory)
            {
                // <DIR> 后面是文件名
                name = trimmed[(dirIdx + 5)..].Trim();
            }
            else
            {
                // 找最后一个数字序列（大小）和文件名
                // 格式: 日期 时间 大小 文件名
                string[] parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4)
                {
                    continue;
                }

                // 最后一个是文件名，倒数第二个是大小
                name = parts[^1];
                string sizeStr = parts[^2].Replace(",", "").Replace(".", "");
                _ = long.TryParse(sizeStr, out size);

                // 尝试从较长的文件名中提取（如果文件名包含空格）
                // 找到大小位置后，后面的都是文件名
                int sizePartIdx = Array.FindIndex(parts, p => p.Replace(",", "").Replace(".", "").All(char.IsDigit) && p.Length > 0 && p != parts[0] && p != parts[1]);
                if (sizePartIdx >= 0 && sizePartIdx < parts.Length - 1)
                {
                    name = string.Join(" ", parts.Skip(sizePartIdx + 1));
                }
            }

            // 跳过 . 和 .. 目录
            if (name == "." || name == "..")
            {
                continue;
            }

            // 尝试解析日期时间 (取前两个部分)
            string[] dateParts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (dateParts.Length >= 2)
            {
                string dateTimeStr = $"{dateParts[0]} {dateParts[1]}";
                if (DateTime.TryParse(dateTimeStr, out DateTime parsed))
                {
                    lastModified = new DateTimeOffset(parsed);
                }
            }

            string fullPath = CombineContainerPath(normalizedPath, name);

            entries.Add(new FileEntry
            {
                Path = fullPath,
                Name = name,
                IsDirectory = isDirectory,
                Size = isDirectory ? 0 : size,
                LastModified = lastModified
            });
        }

        return entries;
    }

    internal static string NormalizeContainerPath(string path)
    {
        string p = path.Replace('\\', '/').Trim();
        if (string.IsNullOrEmpty(p))
        {
            return "/";
        }

        if (!p.StartsWith('/'))
        {
            p = "/" + p;
        }

        // Avoid "//" except for root.
        while (p.Length > 1 && p.Contains("//", StringComparison.Ordinal))
        {
            p = p.Replace("//", "/", StringComparison.Ordinal);
        }

        return p;
    }

    internal static string CombineContainerPath(string basePathNoTrailing, string relativeNoLeading)
    {
        string basePath = NormalizeContainerPath(basePathNoTrailing).TrimEnd('/');
        string rel = relativeNoLeading.Replace('\\', '/').TrimStart('/');

        if (string.IsNullOrEmpty(basePath) || basePath == "/")
        {
            return "/" + rel;
        }

        return basePath + "/" + rel;
    }
}
