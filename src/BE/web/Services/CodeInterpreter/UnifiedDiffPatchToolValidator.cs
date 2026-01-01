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
        foreach (string line in lines)
        {
            if (line.StartsWith("diff --git ", StringComparison.Ordinal) ||
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
                error =
                    "Unsupported patch format: git-style patch headers (e.g. 'diff --git', 'index', '---', '+++') are not supported by patch_file. " +
                    "Provide only unified diff hunks. Each hunk must use a full header like: @@ -oldStart,oldCount +newStart,newCount @@. " +
                    "The target file is specified by the 'path' argument.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
