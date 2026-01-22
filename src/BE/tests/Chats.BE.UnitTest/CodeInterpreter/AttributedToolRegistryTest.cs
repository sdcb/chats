using System.Text.Json;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Services.CodeInterpreter;
using Chats.DB;

namespace Chats.BE.UnitTest.CodeInterpreter;

public sealed class AttributedToolRegistryTest
{
    private static JsonObject ParseObj(string json)
    {
        JsonNode? node = JsonNode.Parse(json);
        Assert.NotNull(node);
        JsonObject? obj = node as JsonObject;
        Assert.NotNull(obj);
        return obj;
    }

    private static CodeInterpreterExecutor.TurnContext CreateCtx()
    {
        return new CodeInterpreterExecutor.TurnContext
        {
            MessageTurns = Array.Empty<ChatTurn>(),
            MessageSteps = Array.Empty<Step>(),
            CurrentAssistantTurn = new ChatTurn { Id = 123, ChatId = 1, Chat = null! },
            ClientInfoId = 1,
        };
    }

    private static HashSet<string> Props(JsonObject schema)
    {
        JsonObject props = (JsonObject)schema["properties"]!;
        return props.Select(kvp => kvp.Key).ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> Required(JsonObject schema)
    {
        if (schema["required"] is not JsonArray req) return new HashSet<string>(StringComparer.Ordinal);
        return req.Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
    }

    [Fact]
    public void ToSnakeCase_ShouldFollowRule()
    {
        Assert.Equal("read_file", AttributedToolRegistry.ToSnakeCase("ReadFile"));
        Assert.Equal("create_session", AttributedToolRegistry.ToSnakeCase("CreateSession"));
        Assert.Equal("a1_b2", AttributedToolRegistry.ToSnakeCase("A1B2"));
    }

    private sealed class TestHost
    {
        internal sealed record LimitsArgs(long? MemoryBytes = null, double? CpuCores = null);

        internal enum ModeValue
        {
            None,
            Bridge,
            Host
        }

        [ToolFunction("Echo the given text")]
        private Task<Result<string>> EchoText(
            CodeInterpreterExecutor.TurnContext ctx,
            [ToolParam("Text to echo")][Required] string text,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok($"echo:{text}"));
        }

        [ToolFunction("Optional args")]
        private Task<Result<string>> OptionalArgs(
            CodeInterpreterExecutor.TurnContext ctx,
            [ToolParam("Nullable note")]
            string? note,
            [ToolParam("Count with default")]
            int count = 5,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Result.Ok($"note:{note ?? "<null>"};count:{count}"));
        }

        [ToolFunction("Mode enum")]
        private Task<Result<string>> Mode(
            CodeInterpreterExecutor.TurnContext ctx,
            [ToolParam("Mode")][EnumDataType(typeof(ModeValue))]
            string mode,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok($"mode:{mode}"));
        }

        [ToolFunction("Min items")]
        private Task<Result<string>> NeedTwo(
            CodeInterpreterExecutor.TurnContext ctx,
            [ToolParam("Items")][MinLength(2)]
            string[] items,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok($"items:{items.Length}"));
        }

        [ToolFunction("Nested object")]
        private Task<Result<string>> Limits(
            CodeInterpreterExecutor.TurnContext ctx,
            [ToolParam("Limits")]
            LimitsArgs? limits,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Result.Ok(limits == null ? "limits:<null>" : $"limits:mem={limits.MemoryBytes},cpu={limits.CpuCores}"));
        }
    }

    [Fact]
    public void Discover_ShouldFindTools_AndSortByName()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));

        Assert.True(reg.Contains("echo_text"));
        Assert.True(reg.Contains("optional_args"));
        Assert.True(reg.Contains("mode"));
        Assert.True(reg.Contains("need_two"));
        Assert.True(reg.Contains("limits"));

        // sorted
        string[] names = reg.ToolNames.ToArray();
        string[] sorted = names.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, names);
    }

    [Fact]
    public void Schema_ShouldNotInclude_TurnContextOrCancellationToken()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        JsonObject schema = ParseObj(reg.GetSchema("echo_text"));
        HashSet<string> props = Props(schema);

        Assert.DoesNotContain("ctx", props);
        Assert.DoesNotContain("cancellationToken", props);
        Assert.Contains("text", props);
    }

    [Fact]
    public void Schema_ShouldMarkRequired_AndNullableCorrectly()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));

        JsonObject echoSchema = ParseObj(reg.GetSchema("echo_text"));
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "text" }, Required(echoSchema));

        JsonObject optionalSchema = ParseObj(reg.GetSchema("optional_args"));
        HashSet<string> req = Required(optionalSchema);
        Assert.DoesNotContain("note", req);
        Assert.DoesNotContain("count", req);

        JsonObject props = (JsonObject)optionalSchema["properties"]!;
        JsonObject note = (JsonObject)props["note"]!;
        Assert.Equal(JsonValueKind.Array, note["type"]!.GetValueKind());
        JsonArray noteType = (JsonArray)note["type"]!;
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "string", "null" }, noteType.Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal));
    }

    [Fact]
    public void Schema_ShouldIncludeEnumAndMinItems()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));

        JsonObject modeSchema = ParseObj(reg.GetSchema("mode"));
        JsonObject modeProps = (JsonObject)modeSchema["properties"]!;
        JsonObject mode = (JsonObject)modeProps["mode"]!;
        JsonArray e = (JsonArray)mode["enum"]!;
        Assert.Equal(new HashSet<string>(StringComparer.Ordinal) { "none", "bridge", "host" }, e.Select(x => x!.GetValue<string>()).ToHashSet(StringComparer.Ordinal));

        JsonObject needTwoSchema = ParseObj(reg.GetSchema("need_two"));
        JsonObject needTwoProps = (JsonObject)needTwoSchema["properties"]!;
        JsonObject items = (JsonObject)needTwoProps["items"]!;
        Assert.Equal(2, items["minItems"]!.GetValue<int>());
    }

    [Fact]
    public void Schema_ShouldDescribeNestedObjectProperties()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));

        JsonObject schema = ParseObj(reg.GetSchema("limits"));
        JsonObject props = (JsonObject)schema["properties"]!;
        JsonObject limits = (JsonObject)props["limits"]!;
        Assert.Equal(JsonValueKind.Array, limits["type"]!.GetValueKind());
        JsonObject limitProps = (JsonObject)limits["properties"]!;
        Assert.True(limitProps.ContainsKey("memoryBytes"));
        Assert.True(limitProps.ContainsKey("cpuCores"));
    }

    [Fact]
    public async Task Invoke_ShouldFail_OnInvalidJson()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> r = await InvokeCompatAsync(reg, host, ctx, "echo_text", "{not json");
        Assert.True(r.IsFailure);
        Assert.Contains("Invalid JSON args", r.Error);
    }

    [Fact]
    public async Task Invoke_ShouldFail_WhenArgsIsNotObject()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> r = await InvokeCompatAsync(reg, host, ctx, "echo_text", "[]");
        Assert.True(r.IsFailure);
        Assert.Contains("must be a JSON object", r.Error);
    }

    [Fact]
    public async Task Invoke_ShouldValidateRequiredAndNotEmpty()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> missing = await InvokeCompatAsync(reg, host, ctx, "echo_text", "{}");
        Assert.True(missing.IsFailure);
        Assert.Contains("Missing required parameter: text", missing.Error);

        Result<string> empty = await InvokeCompatAsync(reg, host, ctx, "echo_text", "{\"text\":\"   \"}");
        Assert.True(empty.IsFailure);
        Assert.Contains("cannot be empty", empty.Error);
    }

    [Fact]
    public async Task Invoke_ShouldValidateEnum()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> bad = await InvokeCompatAsync(reg, host, ctx, "mode", "{\"mode\":\"invalid\"}");
        Assert.True(bad.IsFailure);
        Assert.Contains("must be one of", bad.Error);
    }

    [Fact]
    public async Task Invoke_ShouldValidateMinItems()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> bad = await InvokeCompatAsync(reg, host, ctx, "need_two", "{\"items\":[\"a\"]}");
        Assert.True(bad.IsFailure);
        Assert.Contains("at least 2", bad.Error);
    }

    [Fact]
    public async Task Invoke_ShouldBindDefaultsAndNullable()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> r = await InvokeCompatAsync(reg, host, ctx, "optional_args", "{}");
        Assert.True(r.IsSuccess);
        Assert.Equal("note:<null>;count:5", r.Value);

        Result<string> r2 = await InvokeCompatAsync(reg, host, ctx, "optional_args", "{\"note\":\"hi\",\"count\":2}");
        Assert.True(r2.IsSuccess);
        Assert.Equal("note:hi;count:2", r2.Value);
    }

    [Fact]
    public async Task Invoke_ShouldIgnoreUnknownProperties()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> r = await InvokeCompatAsync(reg, host, ctx, "echo_text", "{\"text\":\"x\",\"extra\":123}");
        Assert.True(r.IsSuccess);
        Assert.Equal("echo:x", r.Value);
    }

    [Fact]
    public async Task Invoke_ShouldBindNestedObject_CamelCaseProperties()
    {
        AttributedToolRegistry reg = new(typeof(TestHost));
        TestHost host = new();
        CodeInterpreterExecutor.TurnContext ctx = CreateCtx();

        Result<string> r = await InvokeCompatAsync(reg, host, ctx, "limits", "{\"limits\":{\"memoryBytes\":123,\"cpuCores\":2}}");
        Assert.True(r.IsSuccess);
        Assert.Equal("limits:mem=123,cpu=2", r.Value);
    }

    private static async Task<Result<string>> InvokeCompatAsync(
        AttributedToolRegistry reg,
        object host,
        CodeInterpreterExecutor.TurnContext ctx,
        string toolName,
        string json)
    {
        AttributedToolRegistry.ToolInvokeResult inv = reg.Invoke(host, ctx, toolName, json, CancellationToken.None);
        AttributedToolRegistry.ToolInvokeTask task = Assert.IsType<AttributedToolRegistry.ToolInvokeTask>(inv);
        return await task.Task;
    }

    private sealed class BadHost
    {
        [ToolFunction("Bad return type")]
        private string Bad(CodeInterpreterExecutor.TurnContext ctx, string text, CancellationToken cancellationToken) => text;
    }

    [Fact]
    public void Discover_ShouldThrow_OnInvalidToolSignature()
    {
        Assert.Throws<InvalidOperationException>(() => new AttributedToolRegistry(typeof(BadHost)));
    }
}
