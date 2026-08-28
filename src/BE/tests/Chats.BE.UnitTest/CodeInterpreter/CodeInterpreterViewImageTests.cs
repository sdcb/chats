using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.Models.ChatServices.OpenAI;
using Chats.DB;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterViewImageTests
{
    private static readonly byte[] ValidPngBytes = CreateValidPngBytes();

    [Fact]
    public void AddTools_ShouldHideViewImage_WhenVisionDisabled()
    {
        using ServiceProvider serviceProvider = CodeInterpreterToolTestHelper.CreateServiceProvider(nameof(AddTools_ShouldHideViewImage_WhenVisionDisabled));
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(serviceProvider, new FakeDockerService());
        List<ChatTool> tools = [];

        executor.AddTools(tools, allowVision: false);

        Assert.DoesNotContain(tools.OfType<FunctionTool>(), tool => tool.FunctionName == CodeInterpreterExecutor.ViewImageToolName);
    }

    [Fact]
    public void AddTools_ShouldIncludeViewImage_WhenVisionEnabled()
    {
        using ServiceProvider serviceProvider = CodeInterpreterToolTestHelper.CreateServiceProvider(nameof(AddTools_ShouldIncludeViewImage_WhenVisionEnabled));
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(serviceProvider, new FakeDockerService());
        List<ChatTool> tools = [];

        executor.AddTools(tools, allowVision: true);

        Assert.Contains(tools.OfType<FunctionTool>(), tool => tool.FunctionName == CodeInterpreterExecutor.ViewImageToolName);
    }

    [Fact]
    public async Task ViewImage_ShouldQueueArtifact_WhenImageIsValid()
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource resource) = await CreateScenarioAsync(nameof(ViewImage_ShouldQueueArtifact_WhenImageIsValid));
        docker.AddFile(resource.BackendResourceId, "/app/chart.png", ValidPngBytes);

        Result<string> result = await executor.ViewImage(context, resource.Name, "/app/chart.png", CancellationToken.None);
        List<CodeInterpreterExecutor.PendingFileArtifact> artifacts = executor.DrainPendingArtifacts(context);

        Assert.True(result.IsSuccess);
        Assert.Equal(string.Empty, result.Value);
        CodeInterpreterExecutor.PendingFileArtifact artifact = Assert.Single(artifacts);
        Assert.Equal("chart.png", artifact.FileName);
        Assert.Equal("image/png", artifact.ContentType);
        Assert.Equal(ValidPngBytes, artifact.Bytes);
    }

    [Fact]
    public async Task ViewImage_ShouldRejectNonImageExtension()
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource resource) = await CreateScenarioAsync(nameof(ViewImage_ShouldRejectNonImageExtension));
        docker.AddFile(resource.BackendResourceId, "/app/chart.txt", ValidPngBytes);

        Result<string> result = await executor.ViewImage(context, resource.Name, "/app/chart.txt", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("does not look like an image", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(context));
    }

    [Fact]
    public async Task ViewImage_ShouldRejectEmptyFile()
    {
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource resource) = await CreateScenarioAsync(nameof(ViewImage_ShouldRejectEmptyFile));
        docker.AddFile(resource.BackendResourceId, "/app/chart.png", []);

        Result<string> result = await executor.ViewImage(context, resource.Name, "/app/chart.png", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("is empty", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(context));
    }

    [Fact]
    public async Task ViewImage_ShouldRejectTooLargeImage()
    {
        CodeInterpreterOptions options = new() { MaxSingleUploadBytes = 8 };
        (CodeInterpreterExecutor executor, CodeInterpreterExecutor.TurnContext context, FakeDockerService docker, ContainerResource resource) = await CreateScenarioAsync(nameof(ViewImage_ShouldRejectTooLargeImage), options);
        docker.AddFile(resource.BackendResourceId, "/app/chart.png", ValidPngBytes);

        Result<string> result = await executor.ViewImage(context, resource.Name, "/app/chart.png", CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Contains("too large", result.Error);
        Assert.Empty(executor.DrainPendingArtifacts(context));
    }

    private static async Task<(CodeInterpreterExecutor Executor, CodeInterpreterExecutor.TurnContext Context, FakeDockerService Docker, ContainerResource Resource)> CreateScenarioAsync(
        string databaseName,
        CodeInterpreterOptions? options = null)
    {
        ServiceProvider serviceProvider = CodeInterpreterToolTestHelper.CreateServiceProvider(databaseName);
        FakeDockerService docker = new();
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(serviceProvider, docker, options: options);
        ContainerResource resource = await CodeInterpreterToolTestHelper.SeedResourceAsync(serviceProvider, "s1", "container-123");
        return (executor, CodeInterpreterToolTestHelper.CreateContext(resource), docker, resource);
    }

    private static byte[] CreateValidPngBytes()
    {
        using Image<Rgba32> image = new(1, 1);
        image[0, 0] = new Rgba32(255, 0, 0, 255);
        using MemoryStream stream = new();
        image.Save(stream, new PngEncoder());
        return stream.ToArray();
    }
}
