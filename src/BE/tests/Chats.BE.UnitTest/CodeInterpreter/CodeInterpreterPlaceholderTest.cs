using Chats.BE.Services.CodeInterpreter;
using Chats.DockerInterface.Models;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterPlaceholderTest
{
    [Theory]
    [InlineData(512 * 1024 * 1024L, "512MB")]
    [InlineData(1024L * 1024 * 1024, "1GB")]
    [InlineData(1536L * 1024 * 1024, "1.5GB")]
    [InlineData(1024L, "1KB")]
    [InlineData(2048L, "2KB")]
    [InlineData(500L, "500B")]
    [InlineData(1024L * 1024, "1MB")]
    [InlineData(100L * 1024 * 1024, "100MB")]
    public void FormatBytes_ShouldFormatCorrectly(long bytes, string expected)
    {
        string result = BytesFormatter.Format(bytes);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(512 * 1024 * 1024L, 1.0, 100, "memory=512MB, cpu=1 cores, maxProcesses=100")]
    [InlineData(0L, 0.0, 0, "memory=unlimited, cpu=unlimited, maxProcesses=unlimited")]
    [InlineData(1024L * 1024 * 1024, 0.0, 50, "memory=1GB, cpu=unlimited, maxProcesses=50")]
    [InlineData(256 * 1024 * 1024L, 0.5, 200, "memory=256MB, cpu=0.5 cores, maxProcesses=200")]
    public void FormatResourceLimits_ShouldFormatCorrectly(long memoryBytes, double cpuCores, int maxProcesses, string expected)
    {
        ResourceLimits limits = new()
        {
            MemoryBytes = memoryBytes,
            CpuCores = cpuCores,
            MaxProcesses = maxProcesses
        };

        string result = CodeInterpreterExecutor.FormatResourceLimits(limits);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Timeout: {defaultTimeoutSeconds}, Network: {defaultNetworkMode}, Limits: memory={defaultMemoryBytes}, cpu={defaultCpuCores}, maxProcesses={defaultMaxProcesses}", "Timeout: 300, Network: none, Limits: memory=536870912 (512MB), cpu=1, maxProcesses=100")]
    [InlineData("No placeholders here", "No placeholders here")]
    [InlineData("{defaultTimeoutSeconds} and again {defaultTimeoutSeconds}", "300 and again 300")]
    public void ReplacePlaceholders_ShouldReplaceConfiguredTokens(string input, string expected)
    {
        Dictionary<string, string> placeholders = CreateDefaultPlaceholders();

        string result = CodeInterpreterExecutor.ReplacePlaceholders(input, placeholders);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ReplacePlaceholders_InJsonSchema_ShouldReplaceCorrectly()
    {
        string schemaJson = """
        {
          "type": "object",
          "properties": {
            "timeoutSeconds": {
              "type": "integer",
              "description": "Default command timeout seconds (null means use server default: {defaultTimeoutSeconds})."
            },
            "networkMode": {
              "type": "string",
              "description": "Network mode. null means use server default: {defaultNetworkMode}."
            },
            "image": {
              "type": "string",
              "description": "Docker image to use (null means use server default: {defaultImage})."
            }
          }
        }
        """;

        Dictionary<string, string> placeholders = new(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = "300",
            ["{defaultNetworkMode}"] = "none",
            ["{defaultImage}"] = "mcr.microsoft.com/dotnet/sdk:10.0 (Supports .NET SDK, Python, and FFmpeg; suitable for code-related work.)"
        };

        string result = CodeInterpreterExecutor.ReplacePlaceholders(schemaJson, placeholders);

        Assert.Contains("server default: 300", result);
        Assert.Contains("server default: none", result);
        Assert.Contains("server default: mcr.microsoft.com/dotnet/sdk:10.0 (Supports .NET SDK, Python, and FFmpeg; suitable for code-related work.)", result);
        Assert.DoesNotContain("{defaultTimeoutSeconds}", result);
        Assert.DoesNotContain("{defaultNetworkMode}", result);
        Assert.DoesNotContain("{defaultImage}", result);
    }

    private static Dictionary<string, string> CreateDefaultPlaceholders()
    {
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = "300",
            ["{defaultNetworkMode}"] = "none",
            ["{defaultMemoryBytes}"] = "536870912 (512MB)",
            ["{defaultCpuCores}"] = "1",
            ["{defaultMaxProcesses}"] = "100"
        };
    }
}
