using Chats.DockerInterface;

namespace Chats.BE.UnitTest.Services;

public class DockerServicePathTests
{
    [Theory]
    [InlineData("/app/artifacts", "./artifacts/hello_artifacts.txt", "/app/artifacts/hello_artifacts.txt")]
    [InlineData("/app/artifacts", "artifacts/hello_artifacts.txt", "/app/artifacts/hello_artifacts.txt")]
    [InlineData("/app/artifacts", "./app/artifacts/hello_artifacts.txt", "/app/artifacts/hello_artifacts.txt")]
    [InlineData("/app/artifacts/", "hello_artifacts.txt", "/app/artifacts/hello_artifacts.txt")]
    [InlineData("/app", "./app/artifacts/hello.txt", "/app/artifacts/hello.txt")]
    public void TryGetFullPathFromArchiveEntry_StripsDuplicatedPrefix(string requestedPath, string entryKey, string expected)
    {
        string? actual = DockerService.TryGetFullPathFromArchiveEntry(requestedPath, entryKey);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("/app/artifacts", "./artifacts/")]
    [InlineData("/app/artifacts", "artifacts/")]
    [InlineData("/app/artifacts", ".")]
    [InlineData("/app/artifacts", "./")]
    public void TryGetFullPathFromArchiveEntry_SkipsRootDirectoryEntry(string requestedPath, string entryKey)
    {
        string? actual = DockerService.TryGetFullPathFromArchiveEntry(requestedPath, entryKey);
        Assert.True(string.IsNullOrEmpty(actual));
    }

    [Fact]
    public void TryGetFullPathFromArchiveEntry_HandlesBackslashes()
    {
        string? actual = DockerService.TryGetFullPathFromArchiveEntry("/app/artifacts", ".\\artifacts\\hello.txt");
        Assert.Equal("/app/artifacts/hello.txt", actual);
    }
}
