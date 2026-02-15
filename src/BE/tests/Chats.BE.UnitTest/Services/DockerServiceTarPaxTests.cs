using System.Formats.Tar;
using System.Text;

using Chats.DockerInterface;
using Chats.DockerInterface.Models;

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

    [Fact]
    public void ParseLinuxLsOutput_ShouldParseFilesAndDirectories()
    {
        string output = """
            total 12
            drwxr-xr-x 3 root root 4096 2024-01-15 10:30:45.000000000 +0000 .
            drwxr-xr-x 3 root root 4096 2024-01-15 10:30:45.000000000 +0000 ..
            drwxr-xr-x 2 root root 4096 2024-01-15 10:30:45.000000000 +0000 subdir
            -rw-r--r-- 1 root root 1234 2024-01-15 11:20:30.000000000 +0000 hello.txt
            -rw-r--r-- 1 root root 5678 2024-01-15 12:00:00.000000000 +0000 你好.txt
            """;

        List<FileEntry> entries = DockerService.ParseLinuxLsOutput("/app/artifacts", output);

        Assert.Equal(3, entries.Count);

        FileEntry dir = entries.First(e => e.Name == "subdir");
        Assert.True(dir.IsDirectory);
        Assert.Equal("/app/artifacts/subdir", dir.Path);
        Assert.Equal(0, dir.Size);

        FileEntry file1 = entries.First(e => e.Name == "hello.txt");
        Assert.False(file1.IsDirectory);
        Assert.Equal("/app/artifacts/hello.txt", file1.Path);
        Assert.Equal(1234, file1.Size);

        FileEntry file2 = entries.First(e => e.Name == "你好.txt");
        Assert.False(file2.IsDirectory);
        Assert.Equal("/app/artifacts/你好.txt", file2.Path);
        Assert.Equal(5678, file2.Size);
    }

    [Fact]
    public void ParseLinuxLsOutput_ShouldHandleFilenamesWithSpaces()
    {
        string output = """
            total 4
            -rw-r--r-- 1 root root 100 2024-01-15 10:30:45.000000000 +0000 file with spaces.txt
            """;

        List<FileEntry> entries = DockerService.ParseLinuxLsOutput("/app", output);

        FileEntry single = Assert.Single(entries);
        Assert.Equal("file with spaces.txt", single.Name);
        Assert.Equal("/app/file with spaces.txt", single.Path);
        Assert.Equal(100, single.Size);
    }

    [Fact]
    public void ParseWindowsDirOutput_ShouldParseFilesAndDirectories()
    {
        string output = """
             Volume in drive C has no label.
             Volume Serial Number is 1234-5678

             Directory of C:\app\artifacts

            01/15/2024  10:30 AM    <DIR>          .
            01/15/2024  10:30 AM    <DIR>          ..
            01/15/2024  10:30 AM    <DIR>          subdir
            01/15/2024  11:20 AM             1,234 hello.txt
            01/15/2024  12:00 PM             5,678 test.txt
                           2 File(s)          6,912 bytes
                           3 Dir(s)  100,000,000 bytes free
            """;

        List<FileEntry> entries = DockerService.ParseWindowsDirOutput("C:\\app\\artifacts", output);

        Assert.Equal(3, entries.Count);

        FileEntry dir = entries.First(e => e.Name == "subdir");
        Assert.True(dir.IsDirectory);

        FileEntry file1 = entries.First(e => e.Name == "hello.txt");
        Assert.False(file1.IsDirectory);
        Assert.Equal(1234, file1.Size);

        FileEntry file2 = entries.First(e => e.Name == "test.txt");
        Assert.False(file2.IsDirectory);
        Assert.Equal(5678, file2.Size);
    }
}
