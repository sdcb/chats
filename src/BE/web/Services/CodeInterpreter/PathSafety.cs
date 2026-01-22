using System.Text;

namespace Chats.BE.Services.CodeInterpreter;

internal static class PathSafety
{
    internal const string WorkDir = "/app";

    internal static string NormalizeUnderWorkDir(string userPath)
    {
        if (string.IsNullOrWhiteSpace(userPath)) throw new ArgumentException("path is required");

        string path = userPath.Replace('\\', '/');
        if (!path.StartsWith('/'))
        {
            path = "/" + path;
        }

        // Allow inputs like /app/foo, app/foo, /foo
        if (path.StartsWith(WorkDir + "/", StringComparison.Ordinal) || path == WorkDir)
        {
            // ok
        }
        else
        {
            // treat as relative to /app
            path = WorkDir.TrimEnd('/') + path;
        }

        string normalized = NormalizePosix(path);

        if (normalized == WorkDir) return normalized;
        if (!normalized.StartsWith(WorkDir + "/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Path must be under {WorkDir}: {userPath}");
        }
        return normalized;
    }

    private static string NormalizePosix(string path)
    {
        // Very small POSIX path normalization: resolve '.', '..', and duplicate slashes.
        string[] parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        Stack<string> stack = new();
        foreach (string part in parts)
        {
            if (part == ".") continue;
            if (part == "..")
            {
                if (stack.Count > 0) stack.Pop();
                continue;
            }
            stack.Push(part);
        }
        StringBuilder sb = new();
        foreach (string p in stack.Reverse())
        {
            sb.Append('/').Append(p);
        }
        return sb.Length == 0 ? "/" : sb.ToString();
    }

    internal static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "file";

        foreach (char c in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(c, '_');
        }
        return fileName;
    }
}
