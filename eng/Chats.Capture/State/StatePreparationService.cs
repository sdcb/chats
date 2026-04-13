using Chats.Capture.Configuration;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Chats.Capture.State;

public sealed record SessionManagerState(string ChatId, string EncryptedSessionId, string FilePath);


public sealed class StatePreparationService
{
  private const string BlankChatTitle = "Capture Blank Chat";
  private const string VariablePromptName = "capture_variable_prompt";
  private const string CapturePresetName = "capture-preset";
  private const string SessionLabel = "capture-session";
  private const string SessionFilePath = "/app/copilot-capture.txt";
  private static readonly Regex PromptVariablePattern = new("{{.*?}}", RegexOptions.Compiled);
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true,
  };

  private readonly CaptureSettings _settings;
  private readonly ILogger<StatePreparationService> _logger;
  private readonly SemaphoreSlim _sessionManagerLock = new(1, 1);

  public StatePreparationService(CaptureSettings settings, ILogger<StatePreparationService> logger)
  {
    _settings = settings;
    _logger = logger;
  }

  public string Username => _settings.GetRequiredUsername();

  public async Task<string> GetFirstChatIdAsync(IPage page)
  {
    string sessionId = await GetRequiredSessionIdAsync(page);
    using HttpClient client = CreateApiClient(sessionId);
    string? chatId = (await GetRecentChatsAsync(client, 1)).FirstOrDefault()?.Id;

    return chatId ?? throw new InvalidOperationException("No chat found for message detail screenshots.");
  }

  public async Task<string> EnsureBlankChatAsync(IPage page)
  {
    string sessionId = await GetRequiredSessionIdAsync(page);
    using HttpClient client = CreateApiClient(sessionId);

    ChatRecord? existing = (await GetRecentChatsAsync(client, 50)).FirstOrDefault(chat =>
      string.Equals(chat.Title, BlankChatTitle, StringComparison.Ordinal)
      && string.IsNullOrWhiteSpace(chat.LeafMessageId));

    if (existing is not null)
    {
      return existing.Id;
    }

    ChatRecord created = await PostJsonAsync<ChatRecord>(client, "/api/user/chats", new
    {
      title = BlankChatTitle,
      groupId = (string?)null,
    });

    _logger.LogInformation("Created blank capture chat {ChatId}", created.Id);
    return created.Id;
  }

  public async Task<string> EnsureVariablePromptAsync(IPage page)
  {
    string sessionId = await GetRequiredSessionIdAsync(page);
    using HttpClient client = CreateApiClient(sessionId);

    List<PromptRecord> prompts = await GetJsonAsync<List<PromptRecord>>(client, "/api/prompts");
    PromptRecord? existing = prompts.FirstOrDefault(prompt => PromptVariablePattern.IsMatch(prompt.Content));
    if (existing is not null)
    {
      return existing.Name;
    }

    PromptRecord created = await PostJsonAsync<PromptRecord>(client, "/api/prompts", new
    {
      name = VariablePromptName,
      content = "Summarize {{topic}} for {{audience}}.",
      isDefault = false,
      isSystem = false,
    });

    _logger.LogInformation("Created variable prompt {PromptName}", created.Name);
    return created.Name;
  }

  public async Task<string> EnsureChatPresetAsync(IPage page)
  {
    string sessionId = await GetRequiredSessionIdAsync(page);
    using HttpClient client = CreateApiClient(sessionId);

    List<ChatPresetRecord> presets = await GetJsonAsync<List<ChatPresetRecord>>(client, "/api/chat-preset");
    ChatPresetRecord? existing = presets.FirstOrDefault();
    if (existing is not null)
    {
      return existing.Name;
    }

    string blankChatId = await EnsureBlankChatAsync(page);
    ChatRecord sourceChat = (await GetRecentChatsAsync(client, 50)).First(chat => chat.Id == blankChatId);

    ChatPresetRecord created = await PostJsonAsync<ChatPresetRecord>(client, "/api/chat-preset", new
    {
      name = CapturePresetName,
      spans = sourceChat.Spans.Select(span => new
      {
        modelId = span.ModelId,
        enabled = span.Enabled,
        systemPrompt = span.SystemPrompt,
        temperature = span.Temperature,
        webSearchEnabled = span.WebSearchEnabled,
        codeExecutionEnabled = span.CodeExecutionEnabled,
        maxOutputTokens = span.MaxOutputTokens,
        reasoningEffort = span.ReasoningEffort,
        imageSize = span.ImageSize,
        thinkingBudget = span.ThinkingBudget,
        mcps = span.Mcps,
      }).ToList(),
    });

    _logger.LogInformation("Created capture preset {PresetName}", created.Name);
    return created.Name;
  }

  public async Task<SessionManagerState> EnsureSessionManagerStateAsync(IPage page)
  {
    await _sessionManagerLock.WaitAsync();
    try
    {
      string sessionId = await GetRequiredSessionIdAsync(page);
      using HttpClient client = CreateApiClient(sessionId);

      ChatRecord chat = await EnsureCodeExecutionChatAsync(client);
      DockerSessionRecord session = await EnsureDockerSessionAsync(client, chat.Id);

      await PutJsonAsync(client,
        $"/api/chat/{chat.Id}/docker-sessions/{Uri.EscapeDataString(session.EncryptedSessionId)}/environment-variables",
        new
        {
          variables = new[]
          {
            new { key = "CAPTURE_MODE", value = "playwright" },
            new { key = "CAPTURE_SCENARIO", value = "session-manager" },
          },
        });

      await PutJsonAsync(client,
        $"/api/chat/{chat.Id}/docker-sessions/{Uri.EscapeDataString(session.EncryptedSessionId)}/text-file",
        new
        {
          path = SessionFilePath,
          text = "capture ready\n",
        });

      return new SessionManagerState(chat.Id, session.EncryptedSessionId, SessionFilePath);
    }
    finally
    {
      _sessionManagerLock.Release();
    }
  }

  public async Task<string> GetOrCreateShareIdAsync(IPage page)
  {
    string sessionId = await GetRequiredSessionIdAsync(page);
    using HttpClient client = CreateApiClient(sessionId);

    foreach (string chatId in (await GetRecentChatsAsync(client, 10)).Select(chat => chat.Id))
    {
      string? shareId = await TryGetShareIdAsync(client, chatId);
      if (!string.IsNullOrWhiteSpace(shareId))
      {
        _logger.LogInformation("Resolved existing share id {ShareId} for chat {ChatId}", shareId, chatId);
        return shareId;
      }

      shareId = await TryCreateShareIdAsync(client, chatId);
      if (!string.IsNullOrWhiteSpace(shareId))
      {
        _logger.LogInformation("Created share id {ShareId} for chat {ChatId}", shareId, chatId);
        return shareId;
      }
    }

    throw new InvalidOperationException("Unable to resolve a share id for share page screenshots.");
  }

  private async Task<string> GetRequiredSessionIdAsync(IPage page)
  {
    string? sessionId = null;

    try
    {
      sessionId = await page.EvaluateAsync<string?>(@"() => {
        const raw = localStorage.getItem('session');
        if (!raw) return null;
        try {
          return JSON.parse(raw)?.sessionId ?? null;
        } catch {
          return null;
        }
      }");
    }
    catch (PlaywrightException ex) when (ex.Message.Contains("localStorage", StringComparison.OrdinalIgnoreCase))
    {
      _logger.LogDebug(ex, "Unable to read session from page localStorage; falling back to persisted auth state.");
    }

    sessionId ??= TryReadSessionIdFromAuthState();

    return sessionId ?? throw new InvalidOperationException("No user session found in browser localStorage.");
  }

  private static HttpClient CreateApiClient(string sessionId)
  {
    HttpClient client = new()
    {
      Timeout = TimeSpan.FromSeconds(15),
    };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    return client;
  }

  private async Task<List<ChatRecord>> GetRecentChatsAsync(HttpClient client, int pageSize)
  {
    ChatPageResponse response = await GetJsonAsync<ChatPageResponse>(
      client,
      $"/api/user/chats?page=1&pageSize={pageSize}&query=");

    return response.Rows;
  }

  private async Task<string?> TryGetShareIdAsync(HttpClient client, string chatId)
  {
    using HttpResponseMessage response = await client.GetAsync($"{_settings.ResolveApiUrl()}/api/user/chats/{chatId}/share");
    if (!response.IsSuccessStatusCode)
    {
      _logger.LogWarning("Share query failed for chat {ChatId} with status {StatusCode}", chatId, (int)response.StatusCode);
      return null;
    }

    await using Stream responseStream = await response.Content.ReadAsStreamAsync();
    using JsonDocument document = await JsonDocument.ParseAsync(responseStream);

    if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
    {
      return null;
    }

    JsonElement first = document.RootElement[0];
    return first.TryGetProperty("shareId", out JsonElement shareIdElement)
      ? shareIdElement.GetString()
      : null;
  }

  private async Task<string?> TryCreateShareIdAsync(HttpClient client, string chatId)
  {
    string validBefore = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(7).ToString("O"));
    using HttpResponseMessage response = await client.PostAsync(
      $"{_settings.ResolveApiUrl()}/api/user/chats/{chatId}/share?validBefore={validBefore}",
      content: null);

    if (!response.IsSuccessStatusCode)
    {
      string body = await response.Content.ReadAsStringAsync();
      _logger.LogWarning(
        "Share creation failed for chat {ChatId} with status {StatusCode}. Body: {Body}",
        chatId,
        (int)response.StatusCode,
        body);
      return null;
    }

    await using Stream responseStream = await response.Content.ReadAsStreamAsync();
    using JsonDocument document = await JsonDocument.ParseAsync(responseStream);
    return document.RootElement.TryGetProperty("shareId", out JsonElement shareIdElement)
      ? shareIdElement.GetString()
      : null;
  }

  private async Task<ChatRecord> EnsureCodeExecutionChatAsync(HttpClient client)
  {
    HashSet<int> codeExecutionModelIds = (await GetJsonAsync<List<UserModelRecord>>(client, "/api/models"))
      .Where(model => model.AllowCodeExecution)
      .Select(model => model.ModelId)
      .ToHashSet();

    if (codeExecutionModelIds.Count == 0)
    {
      throw new InvalidOperationException("No code-execution capable models are available.");
    }

    foreach (ChatRecord chat in await GetRecentChatsAsync(client, 50))
    {
      ChatSpanRecord? span = chat.Spans.FirstOrDefault(candidate => codeExecutionModelIds.Contains(candidate.ModelId));
      if (span is null)
      {
        continue;
      }

      if (!span.CodeExecutionEnabled)
      {
        await PutJsonAsync(
          client,
          $"/api/chat/{chat.Id}/span/{span.SpanId}",
          new
          {
            modelId = span.ModelId,
            enabled = span.Enabled,
            systemPrompt = span.SystemPrompt,
            temperature = span.Temperature,
            webSearchEnabled = span.WebSearchEnabled,
            codeExecutionEnabled = true,
            maxOutputTokens = span.MaxOutputTokens,
            reasoningEffort = span.ReasoningEffort,
            imageSize = span.ImageSize,
            thinkingBudget = span.ThinkingBudget,
            mcps = span.Mcps,
          });
      }

      return chat;
    }

    throw new InvalidOperationException("Unable to find a chat with a code-execution capable model span.");
  }

  private async Task<DockerSessionRecord> EnsureDockerSessionAsync(HttpClient client, string chatId)
  {
    List<DockerSessionRecord> sessions = await GetJsonAsync<List<DockerSessionRecord>>(client, $"/api/chat/{chatId}/docker-sessions");
    DockerSessionRecord? existing = sessions.FirstOrDefault(session => string.Equals(session.Label, SessionLabel, StringComparison.Ordinal))
      ?? sessions.FirstOrDefault();

    if (existing is not null)
    {
      return existing;
    }

    DefaultImageRecord defaultImage = await GetJsonAsync<DefaultImageRecord>(client, "/api/docker-sessions/default-image");
    NetworkModesRecord networkModes = await GetJsonAsync<NetworkModesRecord>(client, "/api/docker-sessions/network-modes");

    DockerSessionRecord created = await PostJsonAsync<DockerSessionRecord>(client, $"/api/chat/{chatId}/docker-sessions", new
    {
      label = SessionLabel,
      image = defaultImage.DefaultImage,
      networkMode = networkModes.DefaultNetworkMode,
    });

    _logger.LogInformation("Created docker session {SessionId} for chat {ChatId}", created.EncryptedSessionId, chatId);
    return created;
  }

  private async Task<T> GetJsonAsync<T>(HttpClient client, string relativePath)
  {
    T? value = await client.GetFromJsonAsync<T>(BuildApiUrl(relativePath), JsonOptions);
    return value ?? throw new InvalidOperationException($"Empty JSON response for {relativePath}");
  }

  private async Task<T> PostJsonAsync<T>(HttpClient client, string relativePath, object body)
  {
    using HttpResponseMessage response = await client.PostAsJsonAsync(BuildApiUrl(relativePath), body, JsonOptions);
    response.EnsureSuccessStatusCode();

    T? value = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    return value ?? throw new InvalidOperationException($"Empty JSON response for {relativePath}");
  }

  private async Task PutJsonAsync(HttpClient client, string relativePath, object body)
  {
    using HttpResponseMessage response = await client.PutAsJsonAsync(BuildApiUrl(relativePath), body, JsonOptions);
    response.EnsureSuccessStatusCode();
  }

  private string BuildApiUrl(string relativePath)
  {
    return _settings.ResolveApiUrl().TrimEnd('/') + relativePath;
  }

  private string? TryReadSessionIdFromAuthState()
  {
    string authStatePath = _settings.ResolveAuthStatePath();
    if (!File.Exists(authStatePath))
    {
      return null;
    }

    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(authStatePath));
    if (!document.RootElement.TryGetProperty("origins", out JsonElement originsElement)
      || originsElement.ValueKind != JsonValueKind.Array)
    {
      return null;
    }

    foreach (JsonElement origin in originsElement.EnumerateArray())
    {
      if (!origin.TryGetProperty("localStorage", out JsonElement storageElement)
        || storageElement.ValueKind != JsonValueKind.Array)
      {
        continue;
      }

      foreach (JsonElement item in storageElement.EnumerateArray())
      {
        if (!item.TryGetProperty("name", out JsonElement nameElement)
          || !string.Equals(nameElement.GetString(), "session", StringComparison.Ordinal))
        {
          continue;
        }

        if (!item.TryGetProperty("value", out JsonElement valueElement))
        {
          continue;
        }

        using JsonDocument sessionDocument = JsonDocument.Parse(valueElement.GetString() ?? "{}");
        if (sessionDocument.RootElement.TryGetProperty("sessionId", out JsonElement sessionIdElement))
        {
          return sessionIdElement.GetString();
        }
      }
    }

    return null;
  }

  private sealed record ChatPageResponse(List<ChatRecord> Rows);

  private sealed record ChatRecord(
    string Id,
    string Title,
    string? LeafMessageId,
    List<ChatSpanRecord> Spans);

  private sealed record ChatSpanRecord(
    int SpanId,
    bool Enabled,
    int ModelId,
    string SystemPrompt,
    double? Temperature,
    bool WebSearchEnabled,
    bool CodeExecutionEnabled,
    int? MaxOutputTokens,
    int? ReasoningEffort,
    string? ImageSize,
    int? ThinkingBudget,
    List<ChatSpanMcpRecord>? Mcps);

  private sealed record ChatSpanMcpRecord(int Id, string? CustomHeaders);

  private sealed record PromptRecord(int Id, string Name, string Content);

  private sealed record ChatPresetRecord(string Id, string Name);

  private sealed record UserModelRecord(int ModelId, bool AllowCodeExecution);

  private sealed record DockerSessionRecord(string EncryptedSessionId, string Label);

  private sealed record DefaultImageRecord(string DefaultImage);

  private sealed record NetworkModesRecord(string DefaultNetworkMode);
}