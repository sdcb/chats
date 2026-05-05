using Chats.BE.Controllers.Admin.ModelKeys.Dtos;
using System.ComponentModel.DataAnnotations;

namespace Chats.BE.UnitTest.Controllers.Admin;

public class UpdateModelKeyRequestValidationTests
{
    private static List<ValidationResult> ValidateRequest(UpdateModelKeyRequest request)
    {
        ValidationContext context = new(request);
        List<ValidationResult> results = [];
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void InvalidCustomHeaders_ShouldFail()
    {
        UpdateModelKeyRequest request = CreateValidRequest() with
        {
            CustomHeaders = "Authorization Bearer token"
        };

        List<ValidationResult> results = ValidateRequest(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelKeyRequest.CustomHeaders)));
    }

    [Fact]
    public void InvalidCustomBody_ShouldFail()
    {
        UpdateModelKeyRequest request = CreateValidRequest() with
        {
            CustomBody = "{\"temperature\":0.7}"
        };

        List<ValidationResult> results = ValidateRequest(request);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelKeyRequest.CustomBody)));
    }

    [Fact]
    public void ValidCustomOverrides_ShouldPass()
    {
        UpdateModelKeyRequest request = CreateValidRequest() with
        {
            CustomHeaders = "Authorization: Bearer token\nX-Trace-Id: demo",
            CustomBody = "[{\"op\":\"add\",\"path\":\"/metadata/source\",\"value\":\"key\"}]"
        };

        List<ValidationResult> results = ValidateRequest(request);

        Assert.Empty(results);
    }

    private static UpdateModelKeyRequest CreateValidRequest()
    {
        return new UpdateModelKeyRequest
        {
            ModelProviderId = 1,
            Name = "test-key",
            Host = "https://example.com",
            Secret = "secret"
        };
    }
}