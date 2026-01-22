using Chats.BE.Services.CodeInterpreter;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class UnifiedDiffApplierTest
{
    [Fact]
    public void Apply_NullDiff_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UnifiedDiffApplier.Apply("a", null!));
    }

    [Fact]
    public void Apply_NoHunks_ReturnsOriginal()
    {
        string original = "a\nb\nc";
        string diff = "--- a/x\n+++ b/x\n";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal(original, result);
    }

    [Fact]
    public void Apply_AddLine_Appends()
    {
        string original = "a\nb\nc";
        string diff = """
--- a/x
+++ b/x
@@ -1,3 +1,4 @@
 a
 b
 c
+d
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nb\nc\nd", result);
    }

    [Fact]
    public void Apply_ReplaceLine_Works()
    {
        string original = "a\nb\nc";
        string diff = """
@@ -2,1 +2,1 @@
-b
+x
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nx\nc", result);
    }

    [Fact]
    public void Apply_MultipleHunks_Works()
    {
        string original = "a\nb\nc\nd\ne\nf";
        string diff = """
@@ -2,1 +2,1 @@
-b
+bb
@@ -5,1 +4,0 @@
-e
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nbb\nc\nd\nf", result);
    }

    [Fact]
    public void Apply_ContextMismatch_ThrowsWithHelpfulMessage()
    {
        string original = "a\nb\nc";
        string diff = """
@@ -2,1 +2,1 @@
-x
+y
""";

        var ex = Assert.Throws<InvalidOperationException>(() => UnifiedDiffApplier.Apply(original, diff));
        Assert.Contains("expected 'x'", ex.Message);
        Assert.Contains("got 'b'", ex.Message);
    }

    [Fact]
    public void Apply_IgnoresNoNewlineMarker()
    {
        string original = "a\nb";
        string diff = """
@@ -1,2 +1,3 @@
 a
 b
+c
\\ No newline at end of file
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nb\nc", result);
    }

    [Fact]
    public void Apply_ContextWhitespaceMismatch_IsToleratedAndPreservesOriginalLine()
    {
        // original contains trailing spaces; diff omits them.
        // We want the patch to apply, and context lines should preserve the original file content.
        string original = "a\nb   \nc";
        string diff = """
@@ -1,3 +1,4 @@
 a
 b
 c
+d
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nb   \nc\nd", result);
    }

    [Fact]
    public void Apply_SmallLineNumberDrift_IsHandledByOffsetSearch()
    {
        // Hunk header points at line 2 (b), but the delete line is actually at line 3 (c).
        string original = "a\nb\nc\nd";
        string diff = """
@@ -2,1 +2,1 @@
-c
+C
""";

        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Equal("a\nb\nC\nd", result);
    }

    [Fact]
    public void Apply_LargeLineNumberDrift_StillThrows()
    {
        // Drift is too large for the built-in tolerance window.
        string original = string.Join("\n", Enumerable.Range(1, 30));
        string diff = """
@@ -1,1 +1,1 @@
-30
+THIRTY
""";

        Assert.Throws<InvalidOperationException>(() => UnifiedDiffApplier.Apply(original, diff));
    }

    [Fact]
    public void PatchToolFormat_GitApplyStylePatch_IsRejectedWithHelpfulMessage()
    {
        string patch = """
diff --git a/x b/x
index 1234567..89abcde 100644
--- a/x
+++ b/x
@@ -1,1 +1,1 @@
-a
+b
""";

        bool ok = UnifiedDiffPatchToolValidator.TryValidate(patch, out string error);

        Assert.False(ok);
        Assert.Contains("diff --git", error, StringComparison.Ordinal);
        Assert.Contains("@@ -oldStart,oldCount +newStart,newCount @@", error, StringComparison.Ordinal);
    }

    [Fact]
    public void PatchToolFormat_BeginPatchWrapper_IsRejectedWithHelpfulMessage()
    {
        string patch = """
*** Begin Patch
*** Update File: x
@@ -1,1 +1,1 @@
-a
+b
*** End Patch
""";

        bool ok = UnifiedDiffPatchToolValidator.TryValidate(patch, out string error);

        Assert.False(ok);
        Assert.Contains("Begin Patch", error, StringComparison.Ordinal);
        Assert.Contains("only unified diff hunks", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PatchToolFormat_MarkdownCodeFence_IsRejectedWithHelpfulMessage()
    {
        string patch = """
```diff
@@ -1,1 +1,1 @@
-a
+b
```
""";

        bool ok = UnifiedDiffPatchToolValidator.TryValidate(patch, out string error);

        Assert.False(ok);
        Assert.Contains("markdown", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("no markdown code fences", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PatchToolFormat_IncompleteHunkHeader_IsRejectedWithHelpfulMessage()
    {
        string patch = """
@@
-a
+b
""";

        bool ok = UnifiedDiffPatchToolValidator.TryValidate(patch, out string error);

        Assert.False(ok);
        Assert.Contains("Invalid hunk header", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@@ -oldStart,oldCount +newStart,newCount @@", error, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_TrailingNewlineInPatch_DoesNotCreatePhantomEmptyContextLine()
    {
        // Regression test: if patch ends with a newline, Split('\n') yields a trailing empty string.
        // That empty string must NOT be treated as an extra context line.
        string original = "a\n// CPU-bound benchmark: count primes up to N.\n// Run in Release\n// Examples:\n//   one\n//   two\nend";
        string diff = """
@@ -2,5 +2,5 @@
-// CPU-bound benchmark: count primes up to N.
+// CPU-bound benchmark: count primes up to N (prime counting).
 // Run in Release
 // Examples:
 //   one
""";

        // Note: diff above intentionally ends with a newline; applying should succeed and preserve the remaining lines.
        string result = UnifiedDiffApplier.Apply(original, diff);

        Assert.Contains("prime counting", result);
        Assert.Contains("//   two", result);
    }

    [Fact]
    public void PatchToolFormat_EmptyLineInsideHunk_IsRejected()
    {
        string patch = "@@ -1,1 +1,1 @@\n\n-a\n+b\n";

        bool ok = UnifiedDiffPatchToolValidator.TryValidate(patch, out string error);

        Assert.False(ok);
        Assert.Contains("empty lines", error, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inside hunks", error, StringComparison.OrdinalIgnoreCase);
    }
}
