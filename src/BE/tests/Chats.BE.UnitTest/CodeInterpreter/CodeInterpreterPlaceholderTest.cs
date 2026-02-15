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

    [Fact]
    public void FormatResourceLimits_WithAllValues_ShouldFormatCorrectly()
    {
        ResourceLimits limits = new()
        {
            MemoryBytes = 512 * 1024 * 1024,
            CpuCores = 1.0,
            MaxProcesses = 100
        };

        string result = CodeInterpreterExecutor.FormatResourceLimits(limits);

        Assert.Equal("memory=512MB, cpu=1 cores, maxProcesses=100", result);
    }

    [Fact]
    public void FormatResourceLimits_WithUnlimitedValues_ShouldShowUnlimited()
    {
        ResourceLimits limits = new()
        {
            MemoryBytes = 0,
            CpuCores = 0,
            MaxProcesses = 0
        };

        string result = CodeInterpreterExecutor.FormatResourceLimits(limits);

        Assert.Equal("memory=unlimited, cpu=unlimited, maxProcesses=unlimited", result);
    }

    [Fact]
    public void FormatResourceLimits_WithMixedValues_ShouldFormatCorrectly()
    {
        ResourceLimits limits = new()
        {
            MemoryBytes = 1024L * 1024 * 1024,
            CpuCores = 0,
            MaxProcesses = 50
        };

        string result = CodeInterpreterExecutor.FormatResourceLimits(limits);

        Assert.Equal("memory=1GB, cpu=unlimited, maxProcesses=50", result);
    }

    [Fact]
    public void FormatResourceLimits_WithFractionalCpu_ShouldFormatCorrectly()
    {
        ResourceLimits limits = new()
        {
            MemoryBytes = 256 * 1024 * 1024,
            CpuCores = 0.5,
            MaxProcesses = 200
        };

        string result = CodeInterpreterExecutor.FormatResourceLimits(limits);

        Assert.Equal("memory=256MB, cpu=0.5 cores, maxProcesses=200", result);
    }

    [Fact]
    public void ReplacePlaceholders_ShouldReplaceAllOccurrences()
    {
        string input = "Timeout: {defaultTimeoutSeconds}, Network: {defaultNetworkMode}, Limits: memory={defaultMemoryBytes}, cpu={defaultCpuCores}, maxProcesses={defaultMaxProcesses}";
        Dictionary<string, string> placeholders = new(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = "300",
            ["{defaultNetworkMode}"] = "none",
            ["{defaultMemoryBytes}"] = "536870912 (512MB)",
            ["{defaultCpuCores}"] = "1",
            ["{defaultMaxProcesses}"] = "100"
        };

        string result = CodeInterpreterExecutor.ReplacePlaceholders(input, placeholders);

        Assert.Equal("Timeout: 300, Network: none, Limits: memory=536870912 (512MB), cpu=1, maxProcesses=100", result);
    }

    [Fact]
    public void ReplacePlaceholders_WithNoPlaceholders_ShouldReturnOriginal()
    {
        string input = "No placeholders here";
        Dictionary<string, string> placeholders = new(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = "300"
        };

        string result = CodeInterpreterExecutor.ReplacePlaceholders(input, placeholders);

        Assert.Equal("No placeholders here", result);
    }

    [Fact]
    public void ReplacePlaceholders_WithMultipleSamePlaceholder_ShouldReplaceAll()
    {
        string input = "{defaultTimeoutSeconds} and again {defaultTimeoutSeconds}";
        Dictionary<string, string> placeholders = new(StringComparer.Ordinal)
        {
            ["{defaultTimeoutSeconds}"] = "300"
        };

        string result = CodeInterpreterExecutor.ReplacePlaceholders(input, placeholders);

        Assert.Equal("300 and again 300", result);
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
}
