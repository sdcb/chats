using Chats.BE.Services.CodeInterpreter;
using Microsoft.Extensions.Options;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class CodeInterpreterOptionsValidatorTests
{
    private readonly CodeInterpreterOptionsValidator _validator = new();

    [Fact]
    public void Validate_DefaultOptions_Succeeds()
    {
        ValidateOptionsResult result = _validator.Validate(Options.DefaultName, new CodeInterpreterOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(86401)]
    public void Validate_InvalidDefaultTimeout_Fails(int timeout)
    {
        ValidateOptionsResult result = _validator.Validate(Options.DefaultName, new CodeInterpreterOptions { DefaultTimeoutSeconds = timeout });

        Assert.False(result.Succeeded);
        Assert.Contains("DefaultTimeoutSeconds", result.FailureMessage);
    }

    [Fact]
    public void Validate_NullDefaultTimeout_Succeeds()
    {
        ValidateOptionsResult result = _validator.Validate(Options.DefaultName, new CodeInterpreterOptions { DefaultTimeoutSeconds = null });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_InvalidPolicyLimits_Fails()
    {
        CodeInterpreterOptions options = new()
        {
            SessionIdleTimeoutSeconds = 0,
            MaxArtifactsFilesToUpload = -1,
            MaxSingleUploadBytes = -1,
            MaxTotalUploadBytesPerTurn = -1,
        };

        ValidateOptionsResult result = _validator.Validate(Options.DefaultName, options);

        Assert.False(result.Succeeded);
        Assert.Contains("SessionIdleTimeoutSeconds", result.FailureMessage);
        Assert.Contains("MaxArtifactsFilesToUpload", result.FailureMessage);
        Assert.Contains("MaxSingleUploadBytes", result.FailureMessage);
        Assert.Contains("MaxTotalUploadBytesPerTurn", result.FailureMessage);
    }
}
