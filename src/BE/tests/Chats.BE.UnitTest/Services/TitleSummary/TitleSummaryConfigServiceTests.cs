using Chats.BE.Services.TitleSummary;
using Chats.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Chats.BE.UnitTest.Services.TitleSummary;

public sealed class TitleSummaryConfigServiceTests
{
    [Fact]
    public void Resolve_UserConfigWithoutAdmin_UsesConfiguredModeAndDefaultTemplate()
    {
        IServiceScopeFactory scopeFactory = CreateScopeFactory();
        TitleSummaryConfigService service = new(scopeFactory, NullLogger<TitleSummaryConfigService>.Instance);

        ResolvedTitleSummaryConfig resolved = service.Resolve(
            adminConfig: null,
            userConfig: new TitleSummaryConfig
            {
                ModelMode = TitleSummaryModelMode.Current,
                ModelId = null,
                PromptTemplate = null,
            });

        Assert.True(resolved.Enabled);
        Assert.Equal(TitleSummaryModelMode.Current, resolved.ModelMode);
        Assert.Null(resolved.ModelId);
        Assert.Equal(TitleSummaryConfigService.DefaultPromptTemplate, resolved.PromptTemplate);
    }

    [Fact]
    public void Resolve_EmptyUserTemplate_InheritsAdminTemplate()
    {
        IServiceScopeFactory scopeFactory = CreateScopeFactory();
        TitleSummaryConfigService service = new(scopeFactory, NullLogger<TitleSummaryConfigService>.Instance);

        ResolvedTitleSummaryConfig resolved = service.Resolve(
            adminConfig: new TitleSummaryConfig
            {
                ModelMode = TitleSummaryModelMode.Specified,
                ModelId = 100,
                PromptTemplate = "admin-template",
            },
            userConfig: new TitleSummaryConfig
            {
                ModelMode = TitleSummaryModelMode.Specified,
                ModelId = 200,
                PromptTemplate = string.Empty,
            });

        Assert.True(resolved.Enabled);
        Assert.Equal(TitleSummaryModelMode.Specified, resolved.ModelMode);
        Assert.Equal<short?>(200, resolved.ModelId);
        Assert.Equal("admin-template", resolved.PromptTemplate);
    }

    [Fact]
    public void Resolve_UserTruncateOverride_ClearsModelSelection()
    {
        IServiceScopeFactory scopeFactory = CreateScopeFactory();
        TitleSummaryConfigService service = new(scopeFactory, NullLogger<TitleSummaryConfigService>.Instance);

        ResolvedTitleSummaryConfig resolved = service.Resolve(
            adminConfig: new TitleSummaryConfig
            {
                ModelMode = TitleSummaryModelMode.Specified,
                ModelId = 100,
                PromptTemplate = "admin-template",
            },
            userConfig: new TitleSummaryConfig
            {
                ModelMode = TitleSummaryModelMode.Truncate,
                ModelId = 200,
                PromptTemplate = null,
            });

        Assert.True(resolved.Enabled);
        Assert.Equal(TitleSummaryModelMode.Truncate, resolved.ModelMode);
        Assert.Null(resolved.ModelId);
        Assert.Equal("admin-template", resolved.PromptTemplate);
    }

    [Fact]
    public void BuildPrompt_ReplacesPlaceholders_AndTruncatesMiddle()
    {
        string longSystemPrompt = new('a', 1200);
        string longUserPrompt = new('b', 1200);

        string prompt = ChatTitleSummaryService.BuildPrompt(
            "S={{systemPrompt}}\nU={{userPrompt}}",
            longSystemPrompt,
            longUserPrompt);

        Assert.DoesNotContain("{{systemPrompt}}", prompt);
        Assert.DoesNotContain("{{userPrompt}}", prompt);
        Assert.Contains("...", prompt);
        Assert.Contains("S=", prompt);
        Assert.Contains("U=", prompt);
    }

    private static IServiceScopeFactory CreateScopeFactory()
    {
        ServiceCollection services = new();
        services.AddDbContext<ChatsDB>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString("N")));
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }
}
