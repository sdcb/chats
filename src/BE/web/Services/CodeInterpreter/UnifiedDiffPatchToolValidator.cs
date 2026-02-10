namespace Chats.BE.Services.CodeInterpreter;

internal static class UnifiedDiffPatchToolValidator
{
    internal static bool TryValidate(string? patch, out string error)
    {
        if (string.IsNullOrWhiteSpace(patch))
        {
            error = "patch is required";
            return false;
        }

        // PatchFile already takes a target `path`. To avoid confusing multi-file git patches,
        // we reject git-apply style headers and require *only* unified diff hunks.
        string[] lines = patch.Replace("\r\n", "\n").Split('\n');

        // Allow a trailing newline without creating an extra empty line entry.
        int lastNonEmpty = lines.Length - 1;
        while (lastNonEmpty >= 0 && lines[lastNonEmpty].Length == 0)
        {
            lastNonEmpty--;
        }

        bool inHunk = false;
        bool sawHunkHeader = false;

        for (int i = 0; i <= lastNonEmpty; i++)
        {
            string line = lines[i];

            // Empty lines are only allowed outside hunks.
            // Inside hunks, an empty context line must be represented as a single space ' ' line.
            if (line.Length == 0)
            {
                if (inHunk)
                {
                    error =
                        "Unsupported patch format: empty lines are not allowed inside hunks. " +
                        "Use a single space ' ' line to represent an empty context line.";
                    return false;
                }
                continue;
            }

            // 原因：模型经常输出被包装的 patch（```diff、diff --git、*** Begin Patch）。
            // 这些外层文本本身不影响 hunk 语义，但此前会触发“能读文件、不能改文件”。
            // 这里兼容并忽略包装，真正的变更仍由 hunk 规则严格校验。
            // The target file is already provided via the `path` argument.
            if (line.StartsWith("```", StringComparison.Ordinal) ||
                line.StartsWith("*** Begin Patch", StringComparison.Ordinal) ||
                line.StartsWith("*** End Patch", StringComparison.Ordinal) ||
                line.StartsWith("*** Update File: ", StringComparison.Ordinal) ||
                line.StartsWith("*** Add File: ", StringComparison.Ordinal) ||
                line.StartsWith("*** Delete File: ", StringComparison.Ordinal) ||
                line.StartsWith("*** Move to: ", StringComparison.Ordinal) ||
                line.StartsWith("*** End of File", StringComparison.Ordinal) ||
                line.StartsWith("diff --git ", StringComparison.Ordinal) ||
                line.StartsWith("index ", StringComparison.Ordinal) ||
                line.StartsWith("new file mode ", StringComparison.Ordinal) ||
                line.StartsWith("deleted file mode ", StringComparison.Ordinal) ||
                line.StartsWith("similarity index ", StringComparison.Ordinal) ||
                line.StartsWith("rename from ", StringComparison.Ordinal) ||
                line.StartsWith("rename to ", StringComparison.Ordinal) ||
                line.StartsWith("GIT binary patch", StringComparison.Ordinal) ||
                line.StartsWith("Binary files ", StringComparison.Ordinal) ||
                line.StartsWith("--- ", StringComparison.Ordinal) ||
                line.StartsWith("+++ ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith("@@", StringComparison.Ordinal))
            {
                // Require full header with explicit counts (LLM-friendly + avoids the `@@` ambiguity).
                // Allow optional trailing section text after the closing @@.
                if (!IsValidFullHunkHeader(line))
                {
                    error =
                        $"Invalid hunk header: '{line}'. " +
                        "patch_file only supports unified diff hunks. " +
                        "Each hunk must use a full header like: @@ -oldStart,oldCount +newStart,newCount @@.";
                    return false;
                }

                inHunk = true;
                sawHunkHeader = true;
                continue;
            }

            if (!inHunk)
            {
                error =
                    "Unsupported patch format: patch_file only supports unified diff hunks (only lines starting with '@@', ' ', '+', '-', or '\\ No newline at end of file'). " +
                    "Do not include any headers or wrappers.";
                return false;
            }

            char prefix = line[0];
            if (prefix is ' ' or '+' or '-')
            {
                // ok (note: a single space ' ' is a valid empty context line)
                continue;
            }

            if (line == "\\ No newline at end of file")
            {
                continue;
            }

            error =
                $"Unsupported patch format: invalid hunk line '{line}'. " +
                "Within hunks, each line must start with ' ' (context), '+' (add), '-' (delete), or be exactly '\\ No newline at end of file'.";
            return false;
        }

        if (!sawHunkHeader)
        {
            error =
                "Unsupported patch format: no unified diff hunks found. " +
                "Provide only unified diff hunks starting with a header like: @@ -oldStart,oldCount +newStart,newCount @@.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsValidFullHunkHeader(string line)
    {
        // Minimal strict pattern:
        // @@ -<oldStart>,<oldCount> +<newStart>,<newCount> @@
        // optionally followed by section text.
        if (!line.StartsWith("@@ -", StringComparison.Ordinal)) return false;

        int idx = 4; // after "@@ -"

        if (!TryReadUInt(line, ref idx)) return false;
        if (!TryConsumeChar(line, ref idx, ',')) return false;
        if (!TryReadUInt(line, ref idx)) return false;
        if (!TryConsumeChar(line, ref idx, ' ')) return false;
        if (!TryConsumeChar(line, ref idx, '+')) return false;
        if (!TryReadUInt(line, ref idx)) return false;
        if (!TryConsumeChar(line, ref idx, ',')) return false;
        if (!TryReadUInt(line, ref idx)) return false;
        if (!TryConsumeChar(line, ref idx, ' ')) return false;
        if (!TryConsumeChar(line, ref idx, '@')) return false;
        if (!TryConsumeChar(line, ref idx, '@')) return false;
        return true;
    }

    private static bool TryReadUInt(string line, ref int idx)
    {
        int start = idx;
        while (idx < line.Length && char.IsDigit(line[idx]))
        {
            idx++;
        }
        return idx > start;
    }

    private static bool TryConsumeChar(string line, ref int idx, char c)
    {
        if (idx >= line.Length || line[idx] != c) return false;
        idx++;
        return true;
    }
}
