using Chats.DockerInterface.Models;
using Microsoft.Extensions.Options;

namespace Chats.BE.Services.CodeInterpreter;

public sealed class CodeInterpreterOptionsValidator : IValidateOptions<CodeInterpreterOptions>
{
    public ValidateOptionsResult Validate(string? name, CodeInterpreterOptions options)
    {
        if (options == null) return ValidateOptionsResult.Fail("CodeInterpreter options cannot be null");

        NetworkMode defaultMode;
        try
        {
            defaultMode = options.GetDefaultNetworkMode();
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        NetworkMode maxAllowed;
        try
        {
            maxAllowed = options.GetMaxAllowedNetworkMode();
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail(ex.Message);
        }

        if ((int)defaultMode > (int)maxAllowed)
        {
            return ValidateOptionsResult.Fail(
                $"Invalid CodeInterpreter network mode config: DefaultNetworkMode '{defaultMode.ToString().ToLowerInvariant()}' " +
                $"exceeds MaxAllowedNetworkMode '{maxAllowed.ToString().ToLowerInvariant()}'.");
        }

        return ValidateOptionsResult.Success;
    }
}

