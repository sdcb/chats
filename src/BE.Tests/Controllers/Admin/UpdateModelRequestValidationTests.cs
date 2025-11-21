using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.DB.Enums;
using System.ComponentModel.DataAnnotations;

namespace Chats.BE.Tests.Controllers.Admin;

public class UpdateModelRequestValidationTests
{
    private static List<ValidationResult> ValidateModel(UpdateModelRequest request)
    {
        var context = new ValidationContext(request);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, context, results, validateAllProperties: true);
        return results;
    }

    #region 基础字段验证测试

    [Fact]
    public void Name_Required_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { Name = "" };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.Name)));
    }

    [Fact]
    public void DeploymentName_Required_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { DeploymentName = "" };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.DeploymentName)));
    }

    [Fact]
    public void ModelKeyId_Zero_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { ModelKeyId = 0 };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.ModelKeyId)));
    }

    [Fact]
    public void InputTokenPrice_Negative_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { InputTokenPrice1M = -1 };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.InputTokenPrice1M)));
    }

    [Fact]
    public void OutputTokenPrice_Negative_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { OutputTokenPrice1M = -1 };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(UpdateModelRequest.OutputTokenPrice1M)));
    }

    #endregion

    #region 温度范围验证测试

    [Fact]
    public void Temperature_MinGreaterThanMax_ShouldFail()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { MinTemperature = 1.5m, MaxTemperature = 1.0m };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Contains(results, r => 
            r.MemberNames.Contains(nameof(UpdateModelRequest.MaxTemperature)) &&
            r.ErrorMessage!.Contains("MinTemperature must be less than or equal to MaxTemperature"));
    }

    [Fact]
    public void Temperature_MinEqualsMax_ShouldPass()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { MinTemperature = 1.0m, MaxTemperature = 1.0m };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidChatRequest();
        request = request with { ApiType = apiType, ContextWindow = 0 };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidChatRequest();
        request = request with { ApiType = apiType, MaxResponseTokens = 0 };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidChatRequest();
        request = request with 
        { 
            ApiType = apiType, 
            ContextWindow = 100, 
            MaxResponseTokens = 100 
        };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidChatRequest();
        request = request with 
        { 
            ApiType = apiType, 
            ContextWindow = 128000, 
            MaxResponseTokens = 4096 
        };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = Array.Empty<string>() };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = new[] { invalidSize } };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidImageRequest();
        request = request with { SupportedImageSizes = new[] { validSize } };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.DoesNotContain(results, r => r.ErrorMessage!.Contains("image size"));
    }

    [Fact]
    public void ImageAPI_MultipleValidImageSizes_ShouldPass()
    {
        // Arrange
        var request = CreateValidImageRequest();
        request = request with 
        { 
            SupportedImageSizes = new[] { "1024x1024", "1792x1024", "1024x1792" } 
        };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidImageRequest();
        request = request with { MaxResponseTokens = batchCount };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidImageRequest();
        request = request with { MaxResponseTokens = batchCount };

        // Act
        var results = ValidateModel(request);

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
        var request = CreateValidChatRequest();

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidResponseRequest_ShouldPass()
    {
        // Arrange
        var request = CreateValidChatRequest();
        request = request with { ApiType = DBApiType.OpenAIResponse };

        // Act
        var results = ValidateModel(request);

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void ValidImageGenerationRequest_ShouldPass()
    {
        // Arrange
        var request = CreateValidImageRequest();

        // Act
        var results = ValidateModel(request);

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
            InputTokenPrice1M = 1.0m,
            OutputTokenPrice1M = 2.0m,
            AllowSearch = true,
            AllowVision = false,
            AllowSystemPrompt = true,
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
            InputTokenPrice1M = 1.0m,
            OutputTokenPrice1M = 2.0m,
            AllowSearch = false,
            AllowVision = false,
            AllowSystemPrompt = false,
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
        };
    }

    #endregion
}
