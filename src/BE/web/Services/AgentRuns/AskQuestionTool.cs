using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Chats.BE.Services.AgentRuns;

public sealed record AskQuestionItem
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("question")]
    public required string Question { get; init; }

    /// <summary>single | multi | text</summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("options")]
    public string[] Options { get; init; } = [];

    [JsonPropertyName("default")]
    public string? Default { get; init; }
}

public sealed record AskQuestionAnswer
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("values")]
    public required string[] Values { get; init; }
}

/// <summary>
/// Built-in <c>ask_question</c> tool: the model asks the user 1-3 questions and the run suspends until answered.
/// </summary>
public static class AskQuestionTool
{
    public const string Name = "ask_question";
    public const int MaxQuestions = 3;

    public const string Description =
        "Ask the user 1-3 clarifying questions and pause until they answer. " +
        "Use it only when you cannot reasonably proceed without the user's decision or missing information. " +
        "Each question is single choice, multiple choice or free text; provide concise options for choice questions " +
        "and put your recommended answer in `default`. Call it at most once per response; the conversation pauses after the call.";

    public const string ParametersSchema = """
        {
          "type": "object",
          "properties": {
            "questions": {
              "type": "array",
              "minItems": 1,
              "maxItems": 3,
              "items": {
                "type": "object",
                "properties": {
                  "id": { "type": "string", "description": "Short unique identifier of the question, e.g. q1." },
                  "question": { "type": "string", "description": "The question shown to the user." },
                  "type": { "type": "string", "enum": ["single", "multi", "text"], "description": "single: choose one option; multi: choose any options; text: free text answer." },
                  "options": { "type": "array", "items": { "type": "string" }, "description": "Options for single/multi questions." },
                  "default": { "type": "string", "description": "Recommended or default answer." }
                },
                "required": ["question", "type"]
              }
            }
          },
          "required": ["questions"]
        }
        """;

    public const string PlanModePrompt =
        "## Plan mode\n" +
        "You are in PLAN mode. Do not perform actions with side effects (do not write or delete files, run mutating commands, " +
        "publish websites, or change external systems). You may use read-only tools to investigate. " +
        "Use the ask_question tool to resolve important ambiguities with the user, then present a clear, step-by-step plan " +
        "and stop. Execute the plan only after the user switches back to normal mode or explicitly asks you to proceed.";

    public const string PlanModePromptWithoutTool =
        "## Plan mode\n" +
        "You are in PLAN mode. Do not perform actions with side effects (do not write or delete files, run mutating commands, " +
        "publish websites, or change external systems). You may use read-only tools to investigate. " +
        "If important information is missing, list your questions for the user, then present a clear, step-by-step plan " +
        "and stop. Execute the plan only after the user switches back to normal mode or explicitly asks you to proceed.";

    public const string SkippedResponse = """{"status":"skipped","message":"The user skipped these questions. Proceed with your best judgement."}""";

    public const string SkippedByNewMessageResponse = """{"status":"skipped","message":"The user did not answer and sent a new message instead."}""";

    public static bool TryParse(string? parameters, out AskQuestionItem[] questions, out string? error)
    {
        questions = [];
        error = null;
        JsonNode? root;
        try
        {
            root = JsonNode.Parse(string.IsNullOrWhiteSpace(parameters) ? "{}" : parameters);
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON arguments: {ex.Message}";
            return false;
        }

        JsonNode? questionsNode = root?["questions"];
        if (questionsNode is JsonValue stringValue && stringValue.TryGetValue(out string? questionsText))
        {
            // some models send nested arrays as a JSON string
            try { questionsNode = JsonNode.Parse(questionsText); } catch (JsonException) { }
        }
        if (questionsNode is not JsonArray array || array.Count == 0)
        {
            error = "`questions` must be a non-empty array.";
            return false;
        }
        if (array.Count > MaxQuestions)
        {
            error = $"At most {MaxQuestions} questions are allowed.";
            return false;
        }

        List<AskQuestionItem> result = [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        for (int i = 0; i < array.Count; i++)
        {
            if (array[i] is not JsonObject q)
            {
                error = $"questions[{i}] must be an object.";
                return false;
            }

            string? question = GetString(q, "question")?.Trim();
            if (string.IsNullOrEmpty(question))
            {
                error = $"questions[{i}].question is required.";
                return false;
            }

            string type = (GetString(q, "type") ?? "text").Trim().ToLowerInvariant();
            if (type is not ("single" or "multi" or "text"))
            {
                error = $"questions[{i}].type must be one of single, multi, text.";
                return false;
            }

            string[] options = q["options"] is JsonArray optionArray
                ? [.. optionArray
                    .Select(x => x is JsonValue v && v.TryGetValue(out string? s) ? s?.Trim() : x?.ToJsonString())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Select(x => x!)
                    .Distinct(StringComparer.Ordinal)]
                : [];
            if (type != "text" && options.Length == 0)
            {
                error = $"questions[{i}].options is required for {type} questions.";
                return false;
            }

            string id = GetString(q, "id")?.Trim() ?? "";
            if (id.Length == 0 || !ids.Add(id))
            {
                id = $"q{i + 1}";
                while (!ids.Add(id)) id += "_";
            }

            result.Add(new AskQuestionItem
            {
                Id = id,
                Question = question,
                Type = type,
                Options = type == "text" ? [] : options,
                Default = GetString(q, "default"),
            });
        }

        questions = [.. result];
        return true;

        static string? GetString(JsonObject obj, string key) => obj[key] switch
        {
            JsonValue v when v.TryGetValue(out string? s) => s,
            null => null,
            JsonNode other => other.ToJsonString(),
        };
    }

    public static string FormatAnswers(IReadOnlyList<AskQuestionItem> questions, IReadOnlyList<AskQuestionAnswer> answers)
    {
        Dictionary<string, string[]> map = answers
            .GroupBy(x => x.Id, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.Last().Values, StringComparer.Ordinal);
        JsonArray items = [];
        foreach (AskQuestionItem q in questions)
        {
            string[] values = map.TryGetValue(q.Id, out string[]? v) ? [.. v.Where(x => !string.IsNullOrWhiteSpace(x))] : [];
            items.Add(new JsonObject
            {
                ["id"] = q.Id,
                ["question"] = q.Question,
                ["answer"] = values.Length == 0
                    ? null
                    : q.Type == "multi" ? new JsonArray([.. values.Select(x => (JsonNode?)JsonValue.Create(x))]) : JsonValue.Create(string.Join("\n", values)),
            });
        }

        return new JsonObject
        {
            ["status"] = "answered",
            ["answers"] = items,
        }.ToJsonString(JSON.JsonSerializerOptions);
    }
}
