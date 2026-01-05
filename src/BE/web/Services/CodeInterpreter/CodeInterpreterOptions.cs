using Chats.DockerInterface.Models;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterOptions
{
    public string DefaultImage { get; set; } = "mcr.microsoft.com/dotnet/sdk:10.0";

    /// <summary>
    /// A short description of what's available inside <see cref="DefaultImage"/>.
    /// Used to enrich the system prompt sent to the model.
    /// </summary>
    public string? DefaultImageDescription { get; set; }

    /// <summary>
    /// Default command timeout. null means effectively unlimited (implemented as a large timeout).
    /// </summary>
    public int? DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Session idle timeout in seconds. Used to set ExpiresAt.
    /// </summary>
    public int SessionIdleTimeoutSeconds { get; set; } = 30 * 60;

    /// <summary>
    /// Default network mode for new sessions. String is user-friendly in config.
    /// Supported: none | bridge | host.
    /// </summary>
    public string DefaultNetworkMode { get; set; } = "none";

    /// <summary>
    /// Maximum allowed network mode that the model can request when calling tools.
    /// Supported: none | bridge | host.
    ///
    /// Allowed modes are all modes <= this value (None &lt; Bridge &lt; Host).
    /// </summary>
    public string MaxAllowedNetworkMode { get; set; } = "host";

    public ResourceLimitsOptions DefaultResourceLimits { get; set; } = new();

    public ResourceLimitsOptions MaxResourceLimits { get; set; } = new()
    {
        MemoryBytes = null,
        CpuCores = null,
        MaxProcesses = null,
    };

    /// <summary>
    /// Max files to upload from /app/artifacts.
    /// </summary>
    public int MaxArtifactsFilesToUpload { get; set; } = 50;

    /// <summary>
    /// Max single file size to upload (bytes). null means no limit.
    /// </summary>
    public long? MaxSingleUploadBytes { get; set; } = 15L * 1024 * 1024;

    /// <summary>
    /// Max total upload bytes per turn. null means no limit.
    /// </summary>
    public long? MaxTotalUploadBytesPerTurn { get; set; } = 30L * 1024 * 1024;

    public NetworkMode GetDefaultNetworkMode()
    {
        string v = DefaultNetworkMode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(v))
        {
            return NetworkMode.None;
        }

        // Accept both friendly strings and legacy numeric strings (0/1/2).
        return v.ToLowerInvariant() switch
        {
            "none" => NetworkMode.None,
            "bridge" => NetworkMode.Bridge,
            "host" => NetworkMode.Host,
            _ => throw new InvalidOperationException($"Invalid CodeInterpreter:DefaultNetworkMode '{DefaultNetworkMode}'. Expected: none|bridge|host"),
        };
    }

    public NetworkMode GetMaxAllowedNetworkMode()
    {
        string v = MaxAllowedNetworkMode?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(v))
        {
            return NetworkMode.Host;
        }

        return v.ToLowerInvariant() switch
        {
            "none" => NetworkMode.None,
            "bridge" => NetworkMode.Bridge,
            "host" => NetworkMode.Host,
            _ => throw new InvalidOperationException($"Invalid CodeInterpreter:MaxAllowedNetworkMode '{MaxAllowedNetworkMode}'. Expected: none|bridge|host"),
        };
    }

    public string GetAllowedNetworkModesDisplay()
    {
        NetworkMode maxAllowed = GetMaxAllowedNetworkMode();
        IEnumerable<string> allowed = Enum.GetValues<NetworkMode>()
            .Where(m => (int)m <= (int)maxAllowed)
            .Select(m => m.ToString().ToLowerInvariant());
        return string.Join(", ", allowed);
    }

    public ResourceLimits BuildDefaultResourceLimits() => DefaultResourceLimits.ToResourceLimitsOrUnlimitedFallback();

    public ResourceLimits BuildMaxResourceLimits() => MaxResourceLimits.ToResourceLimitsOrUnlimitedFallback();

    public int GetEffectiveTimeoutSeconds(int? requestedTimeoutSeconds)
    {
        int? effective = requestedTimeoutSeconds ?? DefaultTimeoutSeconds;
        if (effective is null)
        {
            // IDockerService requires an int; treat as "effectively unlimited".
            return 24 * 60 * 60;
        }
        return Math.Clamp(effective.Value, 1, 24 * 60 * 60);
    }
}

public sealed class ResourceLimitsOptions
{
    /// <summary>
    /// null means unlimited.
    /// </summary>
    public long? MemoryBytes { get; set; } = 512 * 1024 * 1024;

    /// <summary>
    /// null means unlimited.
    /// </summary>
    public double? CpuCores { get; set; } = 1.0;

    /// <summary>
    /// null means unlimited.
    /// </summary>
    public long? MaxProcesses { get; set; } = 100;

    public ResourceLimits ToResourceLimitsOrUnlimitedFallback()
    {
        return new ResourceLimits
        {
            MemoryBytes = MemoryBytes ?? 0,
            CpuCores = CpuCores ?? 0,
            MaxProcesses = MaxProcesses ?? 0,
        };
    }
}
