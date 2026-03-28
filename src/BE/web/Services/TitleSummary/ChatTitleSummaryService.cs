using Chats.BE.Controllers.Chats.Chats.Dtos;
using Chats.BE.Controllers.Users.Usages.Dtos;
using Chats.BE.Infrastructure;
using Chats.BE.Services.Models;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;
using Chats.DB;
using Chats.DB.Enums;
using System.Text;
using System.Threading.Channels;

namespace Chats.BE.Services.TitleSummary;

public sealed class ChatTitleSummaryService(
    CurrentUser currentUser,
    IServiceScopeFactory scopeFactory,
    TitleSummaryConfigService configService,
    ChatRunService chatRunService,
    ILogger<ChatTitleSummaryService> logger)
{
    public async Task StreamTitleAsync(
        int chatId,
        string? systemPrompt,
        UserModel currentSpanUserModel,
        string userPrompt,
        ChannelWriter<SseResponseLine> writer,
        CancellationToken cancellationToken)
    {
        string fallbackTitle = GetFallbackTitle(userPrompt);

        try
        {
            int userId = currentUser.Id;

            TitleSummaryConfig? adminConfig = await configService.GetAdminConfig(cancellationToken);
            TitleSummaryConfig? userConfig = await configService.GetUserConfig(userId, cancellationToken);
            ResolvedTitleSummaryConfig resolved = configService.Resolve(adminConfig, userConfig);

            if (!resolved.Enabled)
            {
                EmitFallback(fallbackTitle);
                return;
            }

            if (resolved.ModelMode == TitleSummaryModelMode.Truncate)
            {
                EmitFallback(fallbackTitle);
                return;
            }

            UserModel? userModel = await SelectUserModel(userId, currentSpanUserModel, resolved, cancellationToken);
            if (userModel == null)
            {
                EmitFallback(fallbackTitle);
                return;
            }

            if (userModel.Model.ApiType == DBApiType.OpenAIImageGeneration)
            {
                EmitFallback(fallbackTitle);
                return;
            }

            string prompt = BuildPrompt(
                resolved.PromptTemplate,
                systemPrompt,
                userPrompt);

            StringBuilder titleBuilder = new();
            bool titleStarted = false;

            ChatRunResult result = await chatRunService.RunAsync(
                new ChatRunRequest
                {
                    UserModel = userModel,
                    ChatRequest = new ChatRequest
                    {
                        EndUserId = $"{chatId}-title-summary",
                        Messages = [NeutralMessage.FromUserText(prompt)],
                        ChatConfig = new ChatConfig
                        {
                            ModelId = userModel.ModelId,
                            Model = userModel.Model,
                            Temperature = null,
                            WebSearchEnabled = false,
                            MaxOutputTokens = null,
                            ReasoningEffortId = 0,
                            CodeExecutionEnabled = false,
                            SystemPrompt = null,
                            ImageSize = null,
                            ThinkingBudget = null,
                        },
                        Source = UsageSource.Summary,
                    },
                },
                async (segmentContext, ct) =>
                {
                    if (segmentContext.Segment is TextChatSegment textSegment && !string.IsNullOrEmpty(textSegment.Text))
                    {
                        if (!titleStarted)
                        {
                            titleStarted = true;
                            writer.TryWrite(new UpdateTitleLine(""));
                        }

                        titleBuilder.Append(textSegment.Text);
                        writer.TryWrite(new TitleSegmentLine(textSegment.Text));
                    }

                    await Task.CompletedTask;
                },
                cancellationToken);

            string finalTitle = NormalizeTitle(titleBuilder.ToString());
            if (result.Exception != null || string.IsNullOrWhiteSpace(finalTitle))
            {
                EmitFallback(fallbackTitle);
                return;
            }

            writer.TryWrite(new SetTitleInternal(finalTitle));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to generate chat title summary for chat {ChatId}", chatId);
            EmitFallback(fallbackTitle);
        }
        finally
        {
            writer.Complete();
        }

        void EmitFallback(string title)
        {
            writer.TryWrite(new UpdateTitleLine(title));
            writer.TryWrite(new SetTitleInternal(title));
        }
    }

    private async Task<UserModel?> SelectUserModel(
        int userId,
        UserModel currentSpanUserModel,
        ResolvedTitleSummaryConfig resolved,
        CancellationToken cancellationToken)
    {
        return resolved.ModelMode switch
        {
            TitleSummaryModelMode.Current => currentSpanUserModel,
            TitleSummaryModelMode.Specified when resolved.ModelId is short modelId
                => await GetSpecifiedUserModel(userId, modelId, cancellationToken),
            _ => null,
        };
    }

    private async Task<UserModel?> GetSpecifiedUserModel(int userId, short modelId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
        UserModelManager userModelManager = scope.ServiceProvider.GetRequiredService<UserModelManager>();
        return await userModelManager.GetUserModel(userId, modelId, cancellationToken);
    }

    internal static string BuildPrompt(string template, string? systemPrompt, string userPrompt)
    {
        return template
            .Replace("{{systemPrompt}}", TruncateMiddle(systemPrompt ?? string.Empty, 1000), StringComparison.Ordinal)
            .Replace("{{userPrompt}}", TruncateMiddle(userPrompt, 1000), StringComparison.Ordinal);
    }

    internal static string GetFallbackTitle(string userPrompt)
    {
        return userPrompt[..Math.Min(50, userPrompt.Length)];
    }

    internal static string NormalizeTitle(string title)
    {
        string normalized = string.Join(" ", title
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return normalized.Trim();
    }

    internal static string TruncateMiddle(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        const string marker = "...";
        int remaining = maxChars - marker.Length;
        if (remaining <= 1)
        {
            return text[..maxChars];
        }

        int left = remaining / 2;
        int right = remaining - left;
        return string.Concat(text.AsSpan(0, left), marker, text.AsSpan(text.Length - right, right));
    }
}
