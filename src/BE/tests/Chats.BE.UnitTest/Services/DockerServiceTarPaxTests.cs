using System.Formats.Tar;
using System.Text;

using Chats.DockerInterface;

namespace Chats.BE.UnitTest.Services;

public sealed class DockerServiceTarPaxTests
{
    [Fact]
    public async Task ListDirectoryFromTarStream_ShouldIgnorePaxHeaders_AndReturnUnicodeFileName()
    {
        using MemoryStream ms = new();
        using (TarWriter writer = new(ms, TarEntryFormat.Pax, leaveOpen: true))
        {
            writer.WriteEntry(new PaxTarEntry(TarEntryType.Directory, "artifacts/"));

            PaxTarEntry file = new(TarEntryType.RegularFile, "artifacts/你好.txt")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("test"), writable: false)
            };
            writer.WriteEntry(file);
        }

        ms.Position = 0;
        List<Chats.DockerInterface.Models.FileEntry> entries = await DockerService.ListDirectoryFromTarStreamAsync("/app/artifacts", ms, CancellationToken.None);

        Chats.DockerInterface.Models.FileEntry single = Assert.Single(entries);
        Assert.Equal("/app/artifacts/你好.txt", single.Path);
        Assert.Equal("你好.txt", single.Name);
        Assert.False(single.IsDirectory);
        Assert.Equal(4, single.Size);
    }

    [Fact]
    public async Task ExtractFirstFileBytesFromTarStream_ShouldReturnFileContent_NotPaxHeader()
    {
        using MemoryStream ms = new();
        using (TarWriter writer = new(ms, TarEntryFormat.Pax, leaveOpen: true))
        {
            PaxTarEntry file = new(TarEntryType.RegularFile, "你好.txt")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("test"), writable: false)
            };
            writer.WriteEntry(file);
        }

        ms.Position = 0;
        byte[]? bytes = await DockerService.ExtractFirstFileBytesFromTarStreamAsync(ms, CancellationToken.None);

        Assert.NotNull(bytes);
        Assert.Equal("test", Encoding.UTF8.GetString(bytes!));
    }
}
