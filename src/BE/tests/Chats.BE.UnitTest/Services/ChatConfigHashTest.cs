using Chats.DB;

namespace Chats.BE.UnitTest.Services;

public class ChatConfigHashTests
{
    public static IEnumerable<object[]> DifferentHashCases()
    {
        yield return HashCase("ModelId", c => c.ModelId = 1, c => c.ModelId = 2);
        yield return HashCase("SystemPrompt", c => c.SystemPrompt = "Prompt A", c => c.SystemPrompt = "Prompt B");
        yield return HashCase("Temperature", c => c.Temperature = 0.5f, c => c.Temperature = 0.6f);
        yield return HashCase("NullTemperature", c => c.Temperature = null, c => c.Temperature = 0.5f);
        yield return HashCase("WebSearchEnabled", c => c.WebSearchEnabled = true, c => c.WebSearchEnabled = false);
        yield return HashCase("CodeExecutionEnabled", c => c.CodeExecutionEnabled = true, c => c.CodeExecutionEnabled = false);
        yield return HashCase("MaxOutputTokens", c => c.MaxOutputTokens = 100, c => c.MaxOutputTokens = 200);
        yield return HashCase("NullMaxOutputTokens", c => c.MaxOutputTokens = null, c => c.MaxOutputTokens = 100);
        yield return HashCase("ReasoningEffort", c => c.Effort = ReasoningEfforts.Minimal, c => c.Effort = ReasoningEfforts.Low);
        yield return HashCase("NullReasoningEffort", c => c.Effort = null, c => c.Effort = ReasoningEfforts.Minimal);
        yield return HashCase("ImageSize", c => c.ImageSize = null, c => c.ImageSize = "1024x1024");
        yield return HashCase("DifferentImageSizes", c => c.ImageSize = "1024x1024", c => c.ImageSize = "1792x1024");
        yield return HashCase("McpIds", c => c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 }), c => c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 }));
        yield return HashCase(
            "McpIdCombinations",
            c =>
            {
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });
            },
            c =>
            {
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });
            });
    }

    public static IEnumerable<object[]> EquivalentHashCases()
    {
        yield return HashCase(
            "IdenticalObjects",
            c => ConfigureCompleteChatConfig(c),
            c => ConfigureCompleteChatConfig(c));
        yield return HashCase("EmptyObject", _ => { }, _ => { });
        yield return HashCase("NullAndEmptySystemPrompt", c => c.SystemPrompt = null, c => c.SystemPrompt = string.Empty);
        yield return HashCase("ExplicitDefaultCodeExecution", _ => { }, c => c.CodeExecutionEnabled = false);
        yield return HashCase("ExplicitNullImageSize", _ => { }, c => c.ImageSize = null);
        yield return HashCase("EmptyMcpIds", _ => { }, _ => { });
        yield return HashCase(
            "SameMcpIdsInDifferentOrder",
            c =>
            {
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });
            },
            c =>
            {
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 1 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 2 });
                c.ChatConfigMcps.Add(new ChatConfigMcp { McpServerId = 3 });
            });
    }

    [Theory]
    [MemberData(nameof(DifferentHashCases))]
    public void GenerateDBHashCode_FieldDifference_ShouldChangeHash(string _, Action<ChatConfig> configureFirst, Action<ChatConfig> configureSecond)
    {
        ChatConfig first = new();
        ChatConfig second = new();
        configureFirst(first);
        configureSecond(second);

        Assert.NotEqual(first.GenerateDBHashCode(), second.GenerateDBHashCode());
    }

    [Theory]
    [MemberData(nameof(EquivalentHashCases))]
    public void GenerateDBHashCode_EquivalentInputs_ShouldKeepHash(string _, Action<ChatConfig> configureFirst, Action<ChatConfig> configureSecond)
    {
        ChatConfig first = new();
        ChatConfig second = new();
        configureFirst(first);
        configureSecond(second);

        Assert.Equal(first.GenerateDBHashCode(), second.GenerateDBHashCode());
    }

    private static object[] HashCase(string name, Action<ChatConfig> configureFirst, Action<ChatConfig> configureSecond)
    {
        return [name, configureFirst, configureSecond];
    }

    private static void ConfigureCompleteChatConfig(ChatConfig config)
    {
        config.ModelId = 1;
        config.SystemPrompt = "Hello, world!";
        config.Temperature = 0.5f;
        config.WebSearchEnabled = true;
        config.MaxOutputTokens = 100;
        config.Effort = ReasoningEfforts.Low;
    }
}
