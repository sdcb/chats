namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterOptions
{
    /// <summary>
    /// Default command timeout. null means effectively unlimited (implemented as a large timeout).
    /// </summary>
    public int? DefaultTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Session idle timeout in seconds. Used to set ExpiresAt.
    /// </summary>
    public int SessionIdleTimeoutSeconds { get; set; } = 30 * 60;

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
