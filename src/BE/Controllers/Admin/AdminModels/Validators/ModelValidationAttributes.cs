using Chats.BE.Controllers.Admin.AdminModels.Dtos;
using Chats.BE.DB.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Chats.BE.Controllers.Admin.AdminModels.Validators;

/// <summary>
/// 验证温度范围：minTemperature 必须小于或等于 maxTemperature
/// </summary>
public class ValidateTemperatureRangeAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not UpdateModelRequest request)
        {
            return new ValidationResult("Invalid object type for temperature range validation");
        }

        if (request.MinTemperature > request.MaxTemperature)
        {
            return new ValidationResult("MinTemperature must be less than or equal to MaxTemperature", 
                new[] { nameof(UpdateModelRequest.MaxTemperature) });
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// 验证 ChatCompletion/Response API 的上下文窗口和最大响应token数
/// </summary>
public class ValidateChatResponseTokensAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not UpdateModelRequest request)
        {
            return new ValidationResult("Invalid object type for chat response tokens validation");
        }

        // 只对 ChatCompletion 和 Response API 进行验证
        if (request.ApiType == DBApiType.ChatCompletion || request.ApiType == DBApiType.Response)
        {
            if (request.ContextWindow <= 0)
            {
                return new ValidationResult("Context window is required for ChatCompletion/Response API", 
                    new[] { nameof(UpdateModelRequest.ContextWindow) });
            }

            if (request.MaxResponseTokens <= 0)
            {
                return new ValidationResult("Max response tokens is required for ChatCompletion/Response API", 
                    new[] { nameof(UpdateModelRequest.MaxResponseTokens) });
            }

            if (request.MaxResponseTokens >= request.ContextWindow)
            {
                return new ValidationResult("Max response tokens must be less than context window", 
                    new[] { nameof(UpdateModelRequest.MaxResponseTokens) });
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// 验证 ImageGeneration API 的图片尺寸格式
/// </summary>
public class ValidateImageSizesAttribute : ValidationAttribute
{
    private static readonly Regex SizeRegex = new(@"^\d+x\d+$", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not UpdateModelRequest request)
        {
            return new ValidationResult("Invalid object type for image sizes validation");
        }

        // 只对 ImageGeneration API 进行验证
        if (request.ApiType == DBApiType.ImageGeneration)
        {
            if (request.SupportedImageSizes == null || request.SupportedImageSizes.Length == 0)
            {
                return new ValidationResult("Supported image sizes is required for ImageGeneration API", 
                    new[] { nameof(UpdateModelRequest.SupportedImageSizes) });
            }

            // 验证每个尺寸格式
            foreach (var size in request.SupportedImageSizes)
            {
                if (string.IsNullOrWhiteSpace(size) || !SizeRegex.IsMatch(size))
                {
                    return new ValidationResult($"Invalid image size format: '{size}'. Use format like: 1024x1024", 
                        new[] { nameof(UpdateModelRequest.SupportedImageSizes) });
                }
            }
        }

        return ValidationResult.Success;
    }
}

/// <summary>
/// 验证 ImageGeneration API 的最大批量生成图片数量
/// </summary>
public class ValidateImageBatchCountAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (validationContext.ObjectInstance is not UpdateModelRequest request)
        {
            return new ValidationResult("Invalid object type for image batch count validation");
        }

        // 只对 ImageGeneration API 进行验证
        if (request.ApiType == DBApiType.ImageGeneration)
        {
            if (request.MaxResponseTokens <= 0 || request.MaxResponseTokens > 128)
            {
                return new ValidationResult("Max batch count must be between 1 and 128 for ImageGeneration API", 
                    new[] { nameof(UpdateModelRequest.MaxResponseTokens) });
            }
        }

        return ValidationResult.Success;
    }
}
