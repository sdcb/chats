using Chats.Web.Controllers.Api.OpenAICompatible.Dtos;
using Chats.Web.Controllers.Chats.Chats;
using Chats.Web.Controllers.Users.Usages.Dtos;
using Chats.Web.DB;
using Chats.Web.DB.Enums;
using Chats.Web.Services;
using Chats.Web.Services.FileServices;
using Chats.Web.Services.Models;
using Chats.Web.Services.Models.ChatServices;
using Chats.Web.Services.Models.Dtos;
using Chats.Web.Services.Models.Neutral;
using Chats.Web.Services.OpenAIApiKeySession;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;

namespace Chats.Web.Controllers.Api.OpenAICompatible;

[Authorize(AuthenticationSchemes = "OpenAIApiKey")]
public class OpenAIImageController(
    ChatsDB db,
    CurrentApiKey currentApiKey,
    ChatFactory cf,
    UserModelManager userModelManager,
    ILogger<OpenAIImageController> logger,
    BalanceService balanceService,
    FileUrlProvider fup) : ControllerBase
{
    private static readonly DBApiType[] AllowedApiTypes = [DBApiType.OpenAIImageGeneration];

    /// <summary>
    /// Generate images from a text prompt (v1/images/generations)
    /// Supports both streaming and non-streaming responses
    /// </summary>
    [HttpPost("v1/images/generations")]
    public async Task<ActionResult> ImageGeneration([FromBody] ImageGenerationRequest? request, [FromServices] AsyncClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Prompt))
        {
            return ErrorMessage(DBFinishReason.BadParameter, "prompt is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Model))
        {
            return ErrorMessage(DBFinishReason.BadParameter, "model is required.");
        }

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, request.Model, cancellationToken);
        if (userModel == null) return InvalidModel(request.Model);

        if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
        {
            return ErrorMessage(DBFinishReason.BadParameter, $"The model `{request.Model}` does not support image generation API.");
        }

        bool isStreamed = request.Stream == true;
        return await ProcessImageGeneration(request, userModel, isStreamed, images: null, clientInfoIdTask, cancellationToken);
    }

    /// <summary>
    /// Edit images based on a prompt and input image (v1/images/edits)
    /// Supports multipart/form-data for image upload
    /// Supports both streaming and non-streaming responses
    /// </summary>
    [HttpPost("v1/images/edits")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> ImageEdits([FromServices] AsyncClientInfoManager clientInfoManager, CancellationToken cancellationToken)
    {
        IFormCollection form = await Request.ReadFormAsync(cancellationToken);

        string? prompt = form["prompt"].FirstOrDefault();
        string? model = form["model"].FirstOrDefault();
        string? user = form["user"].FirstOrDefault();
        string? quality = form["quality"].FirstOrDefault();
        string? size = form["size"].FirstOrDefault();
        string? moderation = form["moderation"].FirstOrDefault();
        bool isStreamed = form["stream"].FirstOrDefault()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        int? partialImages = int.TryParse(form["partial_images"].FirstOrDefault(), out int pi) ? pi : null;
        int? n = int.TryParse(form["n"].FirstOrDefault(), out int nValue) ? nValue : null;

        if (string.IsNullOrWhiteSpace(prompt))
        {
            return ErrorMessage(DBFinishReason.BadParameter, "prompt is required.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return ErrorMessage(DBFinishReason.BadParameter, "model is required.");
        }

        Task<int> clientInfoIdTask = clientInfoManager.GetClientInfoId(cancellationToken);
        UserModel? userModel = await userModelManager.GetUserModel(currentApiKey.ApiKey, model, cancellationToken);
        if (userModel == null) return InvalidModel(model);

        if (!AllowedApiTypes.Contains(userModel.Model.ApiType))
        {
            return ErrorMessage(DBFinishReason.BadParameter, $"The model `{model}` does not support image generation API.");
        }

        // Collect uploaded images
        List<(byte[] Data, string ContentType, string FileName, bool IsMask)> images = [];
        foreach (IFormFile file in form.Files)
        {
            using MemoryStream ms = new();
            await file.CopyToAsync(ms, cancellationToken);
            bool isMask = file.Name.Equals("mask", StringComparison.OrdinalIgnoreCase) ||
                         file.FileName?.Contains("mask", StringComparison.OrdinalIgnoreCase) == true;
            images.Add((ms.ToArray(), file.ContentType, file.FileName ?? "image", isMask));
        }

        if (images.Count == 0)
        {
            return ErrorMessage(DBFinishReason.BadParameter, "At least one image is required for image edits.");
        }

        ImageGenerationRequest request = new()
        {
            Prompt = prompt,
            Model = model,
            N = n,
            Quality = quality,
            Size = size,
            User = user,
            Stream = isStreamed,
            PartialImages = partialImages,
            Moderation = moderation
        };

        return await ProcessImageGeneration(request, userModel, isStreamed, images, clientInfoIdTask, cancellationToken);
    }

    private async Task<ActionResult> ProcessImageGeneration(
        ImageGenerationRequest request,
        UserModel userModel,
        bool isStreamed,
        List<(byte[] Data, string ContentType, string FileName, bool IsMask)>? images,
        Task<int> clientInfoIdTask,
        CancellationToken cancellationToken)
    {
        InChatContext icc = new(Stopwatch.GetTimestamp());
        Model cm = userModel.Model;
        ChatService s = cf.CreateChatService(cm);

        UserBalance userBalance = await db.UserBalances
            .Where(x => x.UserId == currentApiKey.User.Id)
            .FirstOrDefaultAsync(cancellationToken) ?? throw new InvalidOperationException("User balance not found.");

        UserModelBalanceCalculator calc = new(BalanceInitialInfo.FromDB([userModel], userBalance.Balance), []);
        ScopedBalanceCalculator scopedCalc = calc.WithScoped("0");

        BadRequestObjectResult? errorToReturn = null;
        bool hasSuccessYield = false;

        try
        {
            // Build the ChatRequest for image generation
            ChatRequest chatRequest = BuildImageChatRequest(request, cm, images);

            if (isStreamed)
            {
                // Streaming response
                List<Base64Image> pendingFinalImages = [];
                ChatTokenUsage? latestUsage = null;

                await foreach (ChatSegment segment in icc.Run(scopedCalc, userModel, s, chatRequest, fup, cancellationToken))
                {
                    switch (segment)
                    {
                        case UsageChatSegment usageSegment:
                            latestUsage = usageSegment.Usage;
                            if (pendingFinalImages.Count > 0)
                            {
                                if (!hasSuccessYield)
                                {
                                    Response.StatusCode = 200;
                                    Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                                    Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                                    Response.Headers.Connection = "keep-alive";
                                }

                                foreach (Base64Image image in pendingFinalImages)
                                {
                                    ImageStreamEvent completedEvent = new()
                                    {
                                        Type = images != null ? "image_edit.completed" : "image_generation.completed",
                                        B64Json = image.Base64,
                                        CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                        Size = request.Size,
                                        Quality = request.Quality,
                                        Background = request.Background ?? "opaque",
                                        OutputFormat = GetOutputFormatFromContentType(image.ContentType),
                                        Usage = ConvertUsage(latestUsage)
                                    };
                                    await YieldStreamEvent(completedEvent, cancellationToken);
                                    hasSuccessYield = true;
                                }
                                pendingFinalImages.Clear();
                            }
                            break;
                        case Base64PreviewImage previewImage:
                            if (!hasSuccessYield)
                            {
                                Response.StatusCode = 200;
                                Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                                Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                                Response.Headers.Connection = "keep-alive";
                            }

                            ImageStreamEvent streamEvent = new()
                            {
                                Type = images != null ? "image_edit.partial_image" : "image_generation.partial_image",
                                PartialImageIndex = 0,
                                B64Json = previewImage.Base64,
                                CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                                Size = request.Size,
                                Quality = request.Quality,
                                Background = request.Background ?? "opaque",
                                OutputFormat = GetOutputFormatFromContentType(previewImage.ContentType)
                            };
                            await YieldStreamEvent(streamEvent, cancellationToken);
                            hasSuccessYield = true;
                            break;
                        case Base64Image finalImage when segment is not Base64PreviewImage:
                            pendingFinalImages.Add(finalImage);
                            break;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        throw new TaskCanceledException();
                    }
                }

                if (pendingFinalImages.Count > 0)
                {
                    ChatTokenUsage usageFallback = latestUsage ?? icc.FullResponse!.Usage;
                    if (!hasSuccessYield)
                    {
                        Response.StatusCode = 200;
                        Response.Headers.ContentType = "text/event-stream; charset=utf-8";
                        Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
                        Response.Headers.Connection = "keep-alive";
                    }

                    foreach (Base64Image image in pendingFinalImages)
                    {
                        ImageStreamEvent completedEvent = new()
                        {
                            Type = images != null ? "image_edit.completed" : "image_generation.completed",
                            B64Json = image.Base64,
                            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                            Size = request.Size,
                            Quality = request.Quality,
                            Background = request.Background ?? "opaque",
                            OutputFormat = GetOutputFormatFromContentType(image.ContentType),
                            Usage = ConvertUsage(usageFallback)
                        };
                        await YieldStreamEvent(completedEvent, cancellationToken);
                        hasSuccessYield = true;
                    }
                }
            }
            else
            {
                // Non-streaming response
                List<ImageData> imageDataList = [];

                await foreach (ChatSegment segment in icc.Run(scopedCalc, userModel, s, chatRequest, fup, cancellationToken))
                {
                    if (segment is Base64Image image && segment is not Base64PreviewImage)
                    {
                        imageDataList.Add(new ImageData { B64Json = image.Base64 });
                    }
                }

                ChatTokenUsage finalUsage = icc.FullResponse!.Usage;

                ImageGenerationResponse response = new()
                {
                    Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Data = imageDataList,
                    Background = request.Background ?? "opaque",
                    OutputFormat = request.OutputFormat ?? "png",
                    Quality = request.Quality,
                    Size = request.Size,
                    Usage = ConvertUsage(finalUsage)
                };

                return Ok(response);
            }
        }
        catch (ChatServiceException cse)
        {
            icc.FinishReason = cse.ErrorCode;
            errorToReturn = await YieldError(hasSuccessYield && isStreamed, cse.ErrorCode, cse.Message, cancellationToken);
        }
        catch (TaskCanceledException)
        {
            icc.FinishReason = DBFinishReason.Cancelled;
        }
        catch (Exception e)
        {
            icc.FinishReason = DBFinishReason.UnknownError;
            logger.LogError(e, "Unknown error");
            errorToReturn = await YieldError(hasSuccessYield && isStreamed, icc.FinishReason, "", cancellationToken);
        }
        finally
        {
            cancellationToken = CancellationToken.None;
        }

        // Save usage
        UserApiUsage usage = new()
        {
            ApiKeyId = currentApiKey.ApiKeyId,
            Usage = icc.ToUserModelUsage(currentApiKey.User.Id, scopedCalc, userModel, await clientInfoIdTask, isApi: true),
        };
        db.UserApiUsages.Add(usage);
        await db.SaveChangesAsync(cancellationToken);

        if (calc.BalanceCost > 0)
        {
            _ = balanceService.AsyncUpdateBalance(currentApiKey.User.Id, CancellationToken.None);
        }
        if (calc.UsageCosts.Any())
        {
            _ = balanceService.AsyncUpdateUsage([userModel.Id], CancellationToken.None);
        }

        if (hasSuccessYield && isStreamed)
        {
            return new EmptyResult();
        }
        else if (errorToReturn != null)
        {
            return errorToReturn;
        }
        else
        {
            // This shouldn't happen for non-streamed responses as we return early
            return new EmptyResult();
        }
    }

    private ChatRequest BuildImageChatRequest(
        ImageGenerationRequest request,
        Model model,
        List<(byte[] Data, string ContentType, string FileName, bool IsMask)>? images)
    {
        List<NeutralContent> contents = [];

        // Add text prompt (Prompt is validated as non-null at controller entry)
        contents.Add(NeutralTextContent.Create(request.Prompt!));

        // Add images if provided (for image edits)
        if (images != null)
        {
            foreach ((byte[] data, string contentType, string fileName, bool isMask) in images)
            {
                // For mask images, we need to indicate it's a mask in the file name
                if (isMask)
                {
                    contents.Add(NeutralFileBlobContent.Create(data, contentType));
                }
                else
                {
                    contents.Add(NeutralFileBlobContent.Create(data, contentType));
                }
            }
        }

        NeutralMessage userMessage = new()
        {
            Role = NeutralChatRole.User,
            Contents = contents
        };

        ChatConfig chatConfig = new()
        {
            Model = model,
            ModelId = model.Id,
            MaxOutputTokens = request.N ?? 1,
            ReasoningEffortId = (byte)ParseQualityToReasoningEffort(request.Quality),
            ImageSize = request.Size,
        };

        bool isStreamed = request.Stream == true;

        return new ChatRequest()
        {
            Source = UsageSource.Api,
            ChatConfig = chatConfig,
            EndUserId = request.User ?? currentApiKey.User.Id.ToString(),
            Streamed = isStreamed,
            Messages = [userMessage],
        };
    }

    private static DBReasoningEffort ParseQualityToReasoningEffort(string? quality)
    {
        return quality?.ToLowerInvariant() switch
        {
            "low" => DBReasoningEffort.Low,
            "medium" => DBReasoningEffort.Medium,
            "high" => DBReasoningEffort.High,
            "auto" => DBReasoningEffort.Default,
            _ => DBReasoningEffort.Default
        };
    }

    private static ImageUsage? ConvertUsage(ChatTokenUsage? usage)
    {
        if (usage == null)
        {
            return null;
        }

        return new ImageUsage
        {
            InputTokens = usage.InputTokens,
            OutputTokens = usage.OutputTokens,
            TotalTokens = usage.InputTokens + usage.OutputTokens,
            InputTokensDetails = new ImageInputTokensDetails
            {
                TextTokens = usage.InputTokens,
                ImageTokens = 0
            }
        };
    }

    private static string GetOutputFormatFromContentType(string contentType)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/png" => "png",
            "image/jpeg" => "jpeg",
            "image/jpg" => "jpeg",
            "image/webp" => "webp",
            _ => "png"
        };
    }

    private readonly static ReadOnlyMemory<byte> dataU8 = "data: "u8.ToArray();
    private readonly static ReadOnlyMemory<byte> lflfU8 = "\n\n"u8.ToArray();

    private async Task YieldStreamEvent(ImageStreamEvent streamEvent, CancellationToken cancellationToken)
    {
        await Response.Body.WriteAsync(dataU8, cancellationToken);
        await JsonSerializer.SerializeAsync(Response.Body, streamEvent, JSON.JsonSerializerOptions, cancellationToken);
        await Response.Body.WriteAsync(lflfU8, cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private BadRequestObjectResult ErrorMessage(DBFinishReason code, string message)
    {
        return BadRequest(new ErrorResponse()
        {
            Error = new ErrorDetail
            {
                Code = code.ToString(),
                Message = message,
                Param = null,
                Type = ""
            }
        });
    }

    private async Task<BadRequestObjectResult> YieldError(bool shouldStreamed, DBFinishReason code, string message, CancellationToken cancellationToken)
    {
        if (shouldStreamed)
        {
            ImageStreamEvent errorEvent = new()
            {
                Type = "error",
                Error = new ImageErrorDetail
                {
                    Type = "image_generation_error",
                    Code = code.ToString(),
                    Message = message,
                    Param = null
                }
            };
            await YieldStreamEvent(errorEvent, cancellationToken);
        }

        return ErrorMessage(code, message);
    }

    private BadRequestObjectResult InvalidModel(string? modelName)
    {
        return ErrorMessage(DBFinishReason.InvalidModel, $"The model `{modelName}` does not exist or you do not have access to it.");
    }
}
