using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace Chats.BE.Tests.Controllers.Admin;

public class UpdateModelRequestValidationTests
{
    private static List<ValidationResult> ValidateModel(UpdateModelRequest request)
    {
        ValidationContext context = new ValidationContext(request);
        List<ValidationResult> results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    #region 基础字段验证测试

    [Fact]
    public void Name_Required_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { Name = "" };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.Name)));
    }

    [Fact]
    public void DeploymentName_Required_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { DeploymentName = "" };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.DeploymentName)));
    }

    [Fact]
    public void ModelKeyId_Zero_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { ModelKeyId = 0 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.ModelKeyId)));
    }

    [Fact]
    public void InputFreshTokenPrice_Negative_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { InputFreshTokenPrice1M = -1 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.InputFreshTokenPrice1M)));
    }

    [Fact]
    public void OutputTokenPrice_Negative_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { OutputTokenPrice1M = -1 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.OutputTokenPrice1M)));
    }

    [Fact]
    public void InputCachedTokenPrice_Negative_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { InputCachedTokenPrice1M = -1 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.InputCachedTokenPrice1M)));
    }

    #endregion

    #region 温度范围验证测试

    [Fact]
    public void Temperature_MinGreaterThanMax_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { MinTemperature = 1.5m, MaxTemperature = 1.0m };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.MaxTemperature)) &&
            r.ErrorMessage!.Contains("MinTemperature must be less than or equal to MaxTemperature"));
    }

    [Fact]
    public void Temperature_MinEqualsMax_ShouldPass()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { MinTemperature = 1.0m, MaxTemperature = 1.0m };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.ErrorMessage!.Contains("temperature"));
    }

    #endregion

    #region ChatCompletion/Response API 验证测试

    [Theory]
    [InlineData(DBApiType.OpenAIChatCompletion)]
    [InlineData(DBApiType.OpenAIResponse)]
    public void ChatAPI_ContextWindowZero_ShouldFail(DBApiType apiType)
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { ApiType = apiType, ContextWindow = 0 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.ContextWindow)) &&
            r.ErrorMessage!.Contains("Context window is required"));
    }

    [Theory]
    [InlineData(DBApiType.OpenAIChatCompletion)]
    [InlineData(DBApiType.OpenAIResponse)]
    public void ChatAPI_MaxResponseTokensZero_ShouldFail(DBApiType apiType)
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { ApiType = apiType, MaxResponseTokens = 0 };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.MaxResponseTokens)) &&
            r.ErrorMessage!.Contains("Max response tokens is required"));
    }

    [Theory]
    [InlineData(DBApiType.OpenAIChatCompletion)]
    [InlineData(DBApiType.OpenAIResponse)]
    public void ChatAPI_MaxResponseTokensGreaterThanContextWindow_ShouldFail(DBApiType apiType)
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with 
        { 
            ApiType = apiType, 
            ContextWindow = 100, 
            MaxResponseTokens = 100 
        };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.MaxResponseTokens)) &&
            r.ErrorMessage!.Contains("must be less than context window"));
    }

    [Theory]
    [InlineData(DBApiType.OpenAIChatCompletion)]
    [InlineData(DBApiType.OpenAIResponse)]
    public void ChatAPI_ValidTokenConfiguration_ShouldPass(DBApiType apiType)
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with 
        { 
            ApiType = apiType, 
            ContextWindow = 128000, 
            MaxResponseTokens = 4096 
        };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => 
            r.ErrorMessage!.Contains("context window") || 
            r.ErrorMessage!.Contains("response tokens"));
    }

    #endregion

    #region ImageGeneration API 验证测试

    [Fact]
    public void ImageAPI_SupportedImageSizesEmpty_ShouldFail()
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = Array.Empty<string>() };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.SupportedImageSizes)) &&
            r.ErrorMessage!.Contains("Supported image sizes is required"));
    }

    [Theory]
    [InlineData("1024*1024")] // 使用 * 而不是 x
    [InlineData("1024X1024")] // 大写 X
    [InlineData("1024 x 1024")] // 包含空格
    [InlineData("1024x")] // 缺少高度
    [InlineData("x1024")] // 缺少宽度
    [InlineData("abcxdef")] // 非数字
    [InlineData("1024x1024x1024")] // 多个 x
    public void ImageAPI_InvalidImageSizeFormat_ShouldFail(string invalidSize)
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = new[] { invalidSize } };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.SupportedImageSizes)) &&
            r.ErrorMessage!.Contains("Invalid image size format"));
    }

    [Theory]
    [InlineData("1024x1024")]
    [InlineData("512x512")]
    [InlineData("1792x1024")]
    [InlineData("1024x1792")]
    [InlineData("2048x2048")]
    public void ImageAPI_ValidImageSizeFormat_ShouldPass(string validSize)
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = new[] { validSize } };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.ErrorMessage!.Contains("image size"));
    }

    [Fact]
    public void ImageAPI_MultipleValidImageSizes_ShouldPass()
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with 
        { 
            SupportedImageSizes = new[] { "1024x1024", "1792x1024", "1024x1792" } 
        };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.ErrorMessage!.Contains("image size"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    [InlineData(200)]
    public void ImageAPI_MaxBatchCountOutOfRange_ShouldFail(int batchCount)
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with { MaxResponseTokens = batchCount };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.MaxResponseTokens)) &&
            r.ErrorMessage!.Contains("must be between 1 and 128"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(64)]
    [InlineData(128)]
    public void ImageAPI_ValidMaxBatchCount_ShouldPass(int batchCount)
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();
        request = request with { MaxResponseTokens = batchCount };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => 
            r.ErrorMessage!.Contains("batch count") ||
            r.ErrorMessage!.Contains("Max response tokens"));
    }

    #endregion

    #region 完整有效请求测试

    [Fact]
    public void ValidChatCompletionRequest_ShouldPass()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidResponseRequest_ShouldPass()
    {
        // Arrange
        UpdateModelRequest request = CreateValidChatRequest();
        request = request with { ApiType = DBApiType.OpenAIResponse };

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidImageGenerationRequest_ShouldPass()
    {
        // Arrange
        UpdateModelRequest request = CreateValidImageRequest();

        // Act
        List<ValidationResult> results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region 辅助方法

    private static UpdateModelRequest CreateValidChatRequest()
    {
        return new UpdateModelRequest
        {
            Name = "Test Model",
            Enabled = true,
            DeploymentName = "test-deployment",
            ModelKeyId = 1,
            InputFreshTokenPrice1M = 1.0m,
            OutputTokenPrice1M = 2.0m,
            InputCachedTokenPrice1M = 1.0m,
            AllowSearch = true,
            AllowVision = false,
            AllowStreaming = true,
            AllowCodeExecution = false,
            AllowToolCall = true,
            ThinkTagParserEnabled = false,
            ReasoningEffortOptions = [1, 2, 3, 4],
            MinTemperature = 0.0m,
            MaxTemperature = 2.0m,
            ContextWindow = 128000,
            MaxResponseTokens = 4096,
            SupportedImageSizes = Array.Empty<string>(),
            ApiType = DBApiType.OpenAIChatCompletion,
            UseAsyncApi = false,
            UseMaxCompletionTokens = false,
            IsLegacy = false,
            MaxThinkingBudget = null,
            SupportsVisionLink = false,
        };
    }

    private static UpdateModelRequest CreateValidImageRequest()
    {
        return new UpdateModelRequest
        {
            Name = "Test Image Model",
            Enabled = true,
            DeploymentName = "test-image-deployment",
            ModelKeyId = 1,
            InputFreshTokenPrice1M = 1.0m,
            OutputTokenPrice1M = 2.0m,
            InputCachedTokenPrice1M = 1.0m,
            AllowSearch = false,
            AllowVision = false,
            AllowStreaming = true,
            AllowCodeExecution = false,
            AllowToolCall = false,
            ThinkTagParserEnabled = false,
            ReasoningEffortOptions = [2, 3, 4],
            MinTemperature = 0.0m,
            MaxTemperature = 2.0m,
            ContextWindow = 0,
            MaxResponseTokens = 10,
            SupportedImageSizes = ["1024x1024", "1792x1024", "1024x1792"],
            ApiType = DBApiType.OpenAIImageGeneration,
            UseAsyncApi = false,
            UseMaxCompletionTokens = false,
            IsLegacy = false,
            MaxThinkingBudget = null,
            SupportsVisionLink = false,
        };
    }

    #endregion
}
