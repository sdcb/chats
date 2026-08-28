using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.BE.Services.FileServices;
using Chats.DB;
using Chats.DB.Enums;
using Chats.DockerInterface;
using Microsoft.Extensions.DependencyInjection;
using DBFile = Chats.DB.File;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class DownloadChatFilesToolTests
{
    private sealed class FakeFileServiceFactory(IReadOnlyDictionary<string, byte[]> blobs) : IFileServiceFactory
    {
        public IFileService Create(FileService dbfs) => new InMemoryFileService(blobs);
    }

    private sealed class InMemoryFileService(IReadOnlyDictionary<string, byte[]> blobs) : IFileService
    {
        public Task<string> Upload(FileUploadRequest request, CancellationToken cancellationToken = default) => throw new NotImplementedException();
        public Task<Stream> Download(string storageKey, CancellationToken cancellationToken = default)
        {
            if (!blobs.TryGetValue(storageKey, out byte[]? bytes)) throw new FileNotFoundException(storageKey);
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
        public string CreateDownloadUrl(CreateDownloadUrlRequest request) => throw new NotImplementedException();
        public Task<bool> Delete(string storageKey, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    [Fact]
    public async Task DownloadChatFiles_ShouldOnlyListAndUploadMatchedFiles()
    {
        byte[] zipBytes = [0x50, 0x4B, 0x03, 0x04, 0x00];
        byte[] pngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D];
        Dictionary<string, byte[]> blobs = new(StringComparer.Ordinal) { ["maze_game.zip"] = zipBytes, ["maze.png"] = pngBytes };
        FileService localFs = new() { Id = 1, FileServiceTypeId = (byte)DBFileServiceType.Local, Name = "local", Configs = "in-memory", IsDefault = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        DBFile zip = new() { Id = 1, FileName = "maze_game.zip", StorageKey = "maze_game.zip", Size = zipBytes.Length, MediaType = "application/zip", FileServiceId = 1, FileService = localFs, ClientInfoId = 1, CreateUserId = 1, CreatedAt = DateTime.UtcNow, ClientInfo = null!, CreateUser = null! };
        DBFile png = new() { Id = 2, FileName = "maze.png", StorageKey = "maze.png", Size = pngBytes.Length, MediaType = "image/png", FileServiceId = 1, FileService = localFs, ClientInfoId = 1, CreateUserId = 1, CreatedAt = DateTime.UtcNow, ClientInfo = null!, CreateUser = null! };
        Step step = new() { TurnId = 1, ChatRoleId = 1, CreatedAt = DateTime.UtcNow, Turn = new ChatTurn { Id = 1, ChatId = 1, Chat = null! }, StepContents = [StepContent.FromFile(png), StepContent.FromFile(zip)] };

        using ServiceProvider serviceProvider = CodeInterpreterToolTestHelper.CreateServiceProvider(nameof(DownloadChatFiles_ShouldOnlyListAndUploadMatchedFiles));
        ContainerResource resource = await CodeInterpreterToolTestHelper.SeedResourceAsync(serviceProvider, "s1", "container-1");
        FakeDockerService docker = new() { Config = new CodePodConfig { WorkDir = "/workspace" } };
        CodeInterpreterExecutor executor = CodeInterpreterToolTestHelper.CreateExecutor(serviceProvider, docker, new CodePodConfig { WorkDir = "/workspace" }, fileServiceFactory: new FakeFileServiceFactory(blobs));
        CodeInterpreterExecutor.TurnContext context = CodeInterpreterToolTestHelper.CreateContext(resource, [step]);

        Result<string> result = await executor.DownloadChatFiles(context, resource.Name, ["maze_game.zip"], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Contains("maze_game.zip", result.Value);
        Assert.DoesNotContain("maze.png", result.Value);
        Assert.Single(docker.Uploads);
        Assert.Equal("container-1", docker.Uploads[0].ContainerId);
        Assert.Equal("/workspace/maze_game.zip", docker.Uploads[0].Path);
        Assert.Equal(zipBytes, docker.Uploads[0].Content);
    }
}
