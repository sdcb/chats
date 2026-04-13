using Chats.Capture.Models;
using Chats.Capture.Services;
using Chats.Capture.State;

namespace Chats.Capture.Scenarios;

public static class ScenarioCatalog
{
  public static IReadOnlyList<CaptureScenario> Build()
  {
    return
    [
      Route("public.root", "public", "root", "page", "/", "baseline", "page"),
      Route("public.home", "public", "home", "page", "/home", "baseline", "page"),
      Route("public.login", "public", "login", "page", "/login", "baseline", "page"),
      Authorizing(),
      Route("public.model-prices", "public", "model-prices", "page", "/model-prices", "baseline", "page"),
      Route("public.not-found", "public", "404", "page", "/404", "baseline", "page"),

      Route("settings.general", "settings", "settings", "general-tab", "/settings?t=general", "baseline", "page", "tab"),
      Route("settings.prompts", "settings", "settings", "prompts-tab", "/settings?t=prompts", "baseline", "page", "tab"),
      Route("settings.summary", "settings", "settings", "summary-tab", "/settings?t=summary", "baseline", "page", "tab"),
      Route("settings.mcp", "settings", "settings", "mcp-tab", "/settings?t=mcp", "baseline", "page", "tab"),
      Route("settings.usage", "settings", "settings", "usage-tab", "/settings?t=usage", "baseline", "page", "tab"),
      Route("settings.account", "settings", "settings", "account-tab", "/settings?t=account", "baseline", "page", "tab"),

      Route("build.api-key", "build", "api-key", "page", "/build/api-key", "baseline", "page"),
      Route("build.docs", "build", "docs", "page", "/build/docs", "baseline", "page"),
      Route("build.usage", "build", "usage", "page", "/build/usage", "baseline", "page"),

      Route("admin.root", "admin", "admin", "page", "/admin", "baseline", "page"),
      Route("admin.dashboard", "admin", "dashboard", "page", "/admin/dashboard", "baseline", "page"),
      Route("admin.model", "admin", "model", "page", "/admin/model", "baseline", "page"),
      Route("admin.users", "admin", "users", "page", "/admin/users", "baseline", "page"),
      Route("admin.user-models", "admin", "user-models", "page", "/admin/user-models", "baseline", "page"),
      Route("admin.messages", "admin", "messages", "page", "/admin/messages", "baseline", "page"),
      Route("admin.file-service", "admin", "file-service", "page", "/admin/file-service", "baseline", "page"),
      Route("admin.login-service", "admin", "login-service", "page", "/admin/login-service", "baseline", "page"),
      Route("admin.usage", "admin", "usage", "page", "/admin/usage", "baseline", "page"),
      Route("admin.request-logs", "admin", "request-logs", "page", "/admin/request-logs", "baseline", "page"),
      Route("admin.request-trace", "admin", "request-trace", "page", "/admin/request-trace", "baseline", "page"),
      Route("admin.security-logs.password", "admin", "security-logs", "password-tab", "/admin/security-logs?tab=password", "baseline", "page", "tab"),
      Route("admin.security-logs.keycloak", "admin", "security-logs", "keycloak-tab", "/admin/security-logs?tab=keycloak", "baseline", "page", "tab"),
      Route("admin.security-logs.sms", "admin", "security-logs", "sms-tab", "/admin/security-logs?tab=sms", "baseline", "page", "tab"),
      Route("admin.user-config", "admin", "user-config", "page", "/admin/user-config", "baseline", "page"),
      Route("admin.global-configs", "admin", "global-configs", "page", "/admin/global-configs", "baseline", "page"),
      Route("admin.invitation-code", "admin", "invitation-code", "page", "/admin/invitation-code", "baseline", "page"),

      Dynamic("admin.message-detail", "admin", "message", "readonly-view", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/home", cancellationToken);
        string chatId = await context.StatePreparation.GetFirstChatIdAsync(context.Page);
        await context.NavigateAsync($"/message/{chatId}", cancellationToken);
        await context.CaptureAsync();
      }, "baseline", "page", "dynamic"),
      Dynamic("public.share-detail", "public", "share", "readonly-view", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/home", cancellationToken);
        string shareId = await context.StatePreparation.GetOrCreateShareIdAsync(context.Page);
        await context.NavigateAsync($"/share/{shareId}", cancellationToken);
        await context.CaptureAsync();
      }, "baseline", "page", "dynamic"),

      Feature("build.api-key.create-dialog", "build", "api-key", "create-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/build/api-key", cancellationToken);
        await context.ClickButtonByTextsAsync(["New Key", "新建", "新增"]);
        await context.CaptureAsync();
      }, "feature", "dialog"),
      Feature("build.user-menu.popover", "build", "api-key", "user-menu-popover", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/build/api-key", cancellationToken);
        await context.OpenUserMenuAsync();
        await context.CaptureAsync();
      }, "feature", "popover"),
      Feature("settings.prompts.create-dialog", "settings", "settings", "prompt-create-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/settings?t=prompts", cancellationToken);
        await context.ClickButtonByTextsAsync(["New Prompt", "Create your first prompt", "新建", "新增"]);
        await context.CaptureAsync();
      }, "feature", "dialog"),
      Feature("admin.request-trace.inbound-config", "admin", "request-trace", "inbound-config-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/request-trace", cancellationToken);
        await context.ClickHeaderBarButtonAsync(1);
        await context.CaptureAsync();
      }, "feature", "dialog"),
      Feature("admin.request-trace.outbound-config", "admin", "request-trace", "outbound-config-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/request-trace", cancellationToken);
        await context.ClickHeaderBarButtonAsync(2);
        await context.CaptureAsync();
      }, "feature", "dialog"),

      Feature("home.chat.config-dialog", "home", "chat", "config-dialog", async (context, cancellationToken) =>
      {
        string chatId = await context.StatePreparation.EnsureBlankChatAsync(context.Page);
        await context.NavigateAsync($"/home#/{chatId}", cancellationToken);
        await context.OpenFirstChatModelSettingsAsync();
        await context.CaptureAsync();
      }, "feature", "dialog", "chat"),
      Feature("home.chat.preset-first-item", "home", "chat", "preset-first-item", async (context, cancellationToken) =>
      {
        string chatId = await context.StatePreparation.EnsureBlankChatAsync(context.Page);
        await context.StatePreparation.EnsureChatPresetAsync(context.Page);
        await context.NavigateAsync($"/home#/{chatId}", cancellationToken);
        await context.ClickFirstChatPresetCardAsync();
        await context.CaptureAsync();
      }, "feature", "page", "chat"),
      Feature("home.chat.variable-dialog", "home", "chat", "variable-dialog", async (context, cancellationToken) =>
      {
        string chatId = await context.StatePreparation.EnsureBlankChatAsync(context.Page);
        string promptName = await context.StatePreparation.EnsureVariablePromptAsync(context.Page);
        await context.NavigateAsync($"/home#/{chatId}", cancellationToken);
        await context.FillByPlaceholdersAsync(["Type a message", "输入一条消息"], "/" + promptName);
        await context.PressAsync("Enter");
        await context.WaitForTextsAsync([promptName]);
        await context.CaptureAsync();
      }, "feature", "dialog", "chat", "dynamic"),
      Feature("home.chat.share-dialog", "home", "chat", "share-dialog", async (context, cancellationToken) =>
      {
        string chatId = await context.StatePreparation.EnsureBlankChatAsync(context.Page);
        await context.NavigateAsync($"/home#/{chatId}", cancellationToken);
        await context.EnsureChatbarVisibleAsync();
        await context.OpenChatActionsForRowTextAsync("Capture Blank Chat");
        await context.ClickButtonByTextsAsync(["Share", "分享"]);
        await context.WaitForTextsAsync(["Share Message", "分享聊天"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "chat"),
      Feature("home.chat.user-menu-popover", "home", "chat", "user-menu-popover", async (context, cancellationToken) =>
      {
        string chatId = await context.StatePreparation.EnsureBlankChatAsync(context.Page);
        await context.NavigateAsync($"/home#/{chatId}", cancellationToken);
        await context.EnsureChatbarVisibleAsync();
        await context.OpenUserMenuAsync();
        await context.CaptureAsync();
      }, "feature", "popover", "chat"),

      Feature("home.session-manager.info-tab", "home", "session-manager", "info-tab", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),
      Feature("home.session-manager.create-pane", "home", "session-manager", "create-pane", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.ClickButtonByTitlesAsync(["Create session", "创建Session"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),
      Feature("home.session-manager.env-tab", "home", "session-manager", "env-tab", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.ClickButtonByTextsAsync(["Environment Variables", "环境变量"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),
      Feature("home.session-manager.command-output", "home", "session-manager", "command-output", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.ClickButtonByTextsAsync(["Run command", "执行命令"]);
        await context.FillByPlaceholdersAsync(["Enter a shell command", "输入 shell 命令"], "pwd");
        await context.ClickNearbyButtonForPlaceholderAsync(["Enter a shell command", "输入 shell 命令"]);
        await context.WaitForTextsAsync(["ExitCode:"], timeoutMs: 15000);
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),
      Feature("home.session-manager.files-tab", "home", "session-manager", "files-tab", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.ClickButtonByTextsAsync(["File manager", "文件管理"]);
        await context.WaitForTextsAsync(["copilot-capture.txt"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),
      Feature("home.session-manager.editor-tab", "home", "session-manager", "editor-tab", async (context, cancellationToken) =>
      {
        SessionManagerState session = await context.StatePreparation.EnsureSessionManagerStateAsync(context.Page);
        await context.NavigateAsync($"/home#/{session.ChatId}", cancellationToken);
        await context.OpenSessionManagerAsync();
        await context.ClickButtonByTextsAsync(["File manager", "文件管理"]);
        await context.DoubleClickTextAsync("copilot-capture.txt");
        await context.WaitForTextsAsync([session.FilePath]);
        await context.CaptureAsync();
      }, "feature", "dialog", "session-manager", "dynamic"),

      Feature("admin.model.provider-expanded", "admin", "model", "provider-expanded", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/model", cancellationToken);
        await context.ExpandAdminModelProviderUntilActionVisibleAsync(["Add Model", "添加模型"]);
        await context.CaptureAsync();
      }, "feature", "page", "admin-model"),
      Feature("admin.model.add-key-dialog", "admin", "model", "add-key-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/model", cancellationToken);
        await context.ClickButtonByTitlesAsync(["Add Model Key", "添加密钥"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "admin-model"),
      Feature("admin.model.quick-add-dialog", "admin", "model", "quick-add-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/model", cancellationToken);
        await context.ExpandAdminModelProviderUntilActionVisibleAsync(["Fast Add Models", "快速添加模型"]);
        await context.ClickButtonByTitlesAsync(["Fast Add Models", "快速添加模型"]);
        await context.WaitForTextsAsync(["Quick Add Models", "快速添加模型"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "admin-model"),
      Feature("admin.model.add-model-dialog", "admin", "model", "add-model-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/model", cancellationToken);
        await context.ExpandAdminModelProviderUntilActionVisibleAsync(["Add Model", "添加模型"]);
        await context.ClickButtonByTitlesAsync(["Add Model", "添加模型"]);
        await context.WaitForTextsAsync(["Model Display Name", "模型显示名称"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "admin-model"),
      Feature("admin.model.edit-model-dialog", "admin", "model", "edit-model-dialog", async (context, cancellationToken) =>
      {
        await context.NavigateAsync("/admin/model", cancellationToken);
        await context.ExpandAdminModelProviderUntilActionVisibleAsync(["Edit Model", "编辑模型"]);
        await context.ClickButtonByTitlesAsync(["Edit Model", "编辑模型"]);
        await context.WaitForTextsAsync(["Model Display Name", "模型显示名称"]);
        await context.CaptureAsync();
      }, "feature", "dialog", "admin-model")
    ];
  }

  private static CaptureScenario Route(string id, string area, string page, string feature, string route, params string[] tags)
  {
    return new CaptureScenario(
      id,
      area,
      page,
      feature,
      route,
      tags,
      async (context, cancellationToken) =>
      {
        await context.NavigateAsync(route, cancellationToken);
        await context.CaptureAsync();
      });
  }

  private static CaptureScenario Dynamic(string id, string area, string page, string feature, Func<ScenarioContext, CancellationToken, Task> action, params string[] tags)
  {
    return new CaptureScenario(id, area, page, feature, null, tags, action);
  }

  private static CaptureScenario Feature(string id, string area, string page, string feature, Func<ScenarioContext, CancellationToken, Task> action, params string[] tags)
  {
    return new CaptureScenario(id, area, page, feature, null, tags, action);
  }

  private static CaptureScenario Authorizing()
  {
    return new CaptureScenario(
      "public.authorizing",
      "public",
      "authorizing",
      "page",
      "/authorizing?code=fake&provider=fake",
      ["baseline", "page"],
      async (context, cancellationToken) =>
      {
        const string routePattern = "**/api/public/account-login";
        TaskCompletionSource<bool> requestStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        await context.Page.RouteAsync(routePattern, async route =>
        {
          requestStarted.TrySetResult(true);
          await Task.Delay(8000);
          await route.AbortAsync();
        });

        try
        {
          await context.NavigateAsync("/authorizing?code=fake&provider=fake", cancellationToken, waitForNetworkIdle: false);
          await requestStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), cancellationToken);
          await Task.Delay(500, cancellationToken);
          await context.CaptureAsync();
        }
        finally
        {
          await context.Page.UnrouteAsync(routePattern);
        }
      });
  }
}