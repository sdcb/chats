using Microsoft.Extensions.Options;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterOptionsValidator : IValidateOptions<CodeInterpreterOptions>
{
    public ValidateOptionsResult Validate(string? name, CodeInterpreterOptions options)
    {
        if (options is null) return ValidateOptionsResult.Fail("CodeInterpreter options cannot be null");

        List<string> failures = [];

        if (options.DefaultTimeoutSeconds is int defaultTimeout
            && (defaultTimeout < 1 || defaultTimeout > 24 * 60 * 60))
        {
            failures.Add("DefaultTimeoutSeconds must be null or between 1 and 86400 seconds.");
        }

        if (options.SessionIdleTimeoutSeconds <= 0)
        {
            failures.Add("SessionIdleTimeoutSeconds must be greater than zero.");
        }

        if (options.MaxArtifactsFilesToUpload < 0)
        {
            failures.Add("MaxArtifactsFilesToUpload must be greater than or equal to zero.");
        }

        if (options.MaxSingleUploadBytes is long maxSingleUploadBytes && maxSingleUploadBytes < 0)
        {
            failures.Add("MaxSingleUploadBytes must be null or greater than or equal to zero.");
        }

        if (options.MaxTotalUploadBytesPerTurn is long maxTotalUploadBytes && maxTotalUploadBytes < 0)
        {
            failures.Add("MaxTotalUploadBytesPerTurn must be null or greater than or equal to zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail([.. failures]);
    }
}

