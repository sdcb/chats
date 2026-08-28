using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.DB;
using Chats.DockerInterface;
using Microsoft.Extensions.DependencyInjection;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterWriteFileTests
{
    [Theory]
    [InlineData("multiple-lines", "line1\nline2\nline3", "Wrote 3 lines")]
    [InlineData("empty", "", "Wrote 0 lines")]
    [InlineData("single-line", "single line", "Wrote 1 lines")]
    [InlineData("crlf", "line1\r\nline2\r\nline3\r\nline4", "Wrote 4 lines")]
    public async Task WriteFile_ShouldReportLineCount(string databaseName, string text, string expectedMessage)
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, ContainerResource resource, _) = await CreateScenarioAsync(databaseName);

        Result<string> result = await executor.WriteFile(context, resource.Name, "/app/test.txt", text, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains(expectedMessage, result.Value);
        Assert.DoesNotContain("bytes", result.Value);
    }

    [Theory]
    [InlineData("relative", "test.py", "/workspace/test.py")]
    [InlineData("absolute", "/tmp/test.py", "/tmp/test.py")]
    public async Task WriteFile_ShouldResolvePathAgainstWorkDir(string databaseName, string path, string expectedPath)
    {
        CodePodConfig config = new() { WorkDir = "/workspace" };
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, ContainerResource resource, FakeDockerService docker) = await CreateScenarioAsync(databaseName, config);

        Result<string> result = await executor.WriteFile(context, resource.Name, path, "print('hello')", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedPath, docker.LastUploadedPath);
        Assert.Contains(expectedPath, result.Value);
    }

    private static async Task<(CodeInterpreterExecutor Executor, CodeInterpreterExecutor.TurnContext Context, ContainerResource Resource, FakeDockerService Docker)> CreateScenarioAsync(string databaseName, CodePodConfig? config = null)
    {
        ServiceProvider serviceProvider = CodeInterpreterToolTestHelper.CreateServiceProvider($"{nameof(CodeInterpreterWriteFileTests)}_{databaseName}");
        FakeDockerService docker = new() { Config = config ?? new CodePodConfig() };
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(serviceProvider, docker, config);
        ContainerResource resource = await CodeInterpreterToolTestHelper.SeedResourceAsync(serviceProvider, "s1", "container-123");
        return (executor, CodeInterpreterToolTestHelper.CreateContext(resource), resource, docker);
    }
}
