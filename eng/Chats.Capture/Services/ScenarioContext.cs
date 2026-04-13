using Chats.Capture.Configuration;
using Chats.Capture.Models;
using Chats.Capture.Output;
using Chats.Capture.State;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Chats.Capture.Services;

public sealed class ScenarioContext
{
  private string? _lastScreenshotPath;

  public ScenarioContext(
    CaptureScenario scenario,
    ThemeKind theme,
    IPage page,
    CaptureSettings settings,
    CaptureOutputService output,
    StatePreparationService statePreparation,
    ILogger logger)
  {
    Scenario = scenario;
    Theme = theme;
    Page = page;
    Settings = settings;
    Output = output;
    StatePreparation = statePreparation;
    Logger = logger;
  }

  public CaptureScenario Scenario { get; }

  public ThemeKind Theme { get; }

  public IPage Page { get; }

  public CaptureSettings Settings { get; }

  public CaptureOutputService Output { get; }

  public StatePreparationService StatePreparation { get; }

  public ILogger Logger { get; }

  public string? LastScreenshotPath => _lastScreenshotPath;

  public async Task NavigateAsync(string route, CancellationToken cancellationToken, bool waitForNetworkIdle = true)
  {
    string normalizedRoute = route.StartsWith('/') ? route : "/" + route;
    await Page.GotoAsync(Settings.BaseUrl + normalizedRoute);
    await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    await EnsureThemeAsync(waitForNetworkIdle, cancellationToken);
    await WaitForUiSettledAsync(waitForNetworkIdle, cancellationToken);
  }

  public async Task<string> CaptureAsync(string? suffix = null)
  {
    string path = Output.GetScreenshotPath(Scenario, Theme, suffix);
    await Page.ScreenshotAsync(new PageScreenshotOptions
    {
      FullPage = true,
      Path = path,
    });
    _lastScreenshotPath = path;
    return path;
  }

  public async Task ClickButtonByTextsAsync(IEnumerable<string> texts)
  {
    foreach (string text in texts)
    {
      string escaped = text.Replace("'", "\\'");
      ILocator locator = Page.Locator($"button:has-text('{escaped}'), [role='button']:has-text('{escaped}')");
      if (await locator.CountAsync() == 0)
      {
        locator = Page.GetByText(text, new PageGetByTextOptions
        {
          Exact = false,
        }).First;

        if (await locator.CountAsync() == 0)
        {
          continue;
        }
      }

      await locator.First.ClickAsync();
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
      return;
    }

    throw new InvalidOperationException($"Unable to find a clickable button using texts: {string.Join(", ", texts)}");
  }

  public async Task ClickDialogButtonByTextsAsync(IEnumerable<string> texts)
  {
    foreach (string text in texts)
    {
      string escaped = text.Replace("'", "\\'");
      ILocator locator = Page.Locator("[role='dialog'], [data-state='open']")
        .Locator($"button:has-text('{escaped}'), [role='button']:has-text('{escaped}')");

      if (await locator.CountAsync() == 0)
      {
        continue;
      }

      await locator.First.ClickAsync();
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
      return;
    }

    throw new InvalidOperationException($"Unable to find a dialog button using texts: {string.Join(", ", texts)}");
  }

  public async Task ClickButtonByTitleAsync(string title, int index = 0)
  {
    await ClickButtonByTitlesAsync([title], index);
  }

  public async Task ClickButtonByTitlesAsync(IEnumerable<string> titles, int index = 0)
  {
    foreach (string title in titles)
    {
      ILocator locator = Page.Locator($"button[title='{title}'], button[aria-label='{title}']");
      int count = await locator.CountAsync();
      if (count <= index)
      {
        continue;
      }

      await locator.Nth(index).ClickAsync();
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
      return;
    }

    throw new InvalidOperationException($"Unable to find a titled button using: {string.Join(", ", titles)}");
  }

  public async Task FillByPlaceholderAsync(string placeholderFragment, string value)
  {
    await FillByPlaceholdersAsync([placeholderFragment], value);
  }

  public async Task FillByPlaceholdersAsync(IEnumerable<string> placeholderFragments, string value)
  {
    foreach (string placeholderFragment in placeholderFragments)
    {
      ILocator locator = Page.Locator($"textarea[placeholder*='{placeholderFragment}'], input[placeholder*='{placeholderFragment}']");
      if (await locator.CountAsync() == 0)
      {
        continue;
      }

      await locator.First.FillAsync(value);
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
      return;
    }

    throw new InvalidOperationException($"Unable to find input with placeholders: {string.Join(", ", placeholderFragments)}");
  }

  public async Task ClickNearbyButtonForPlaceholderAsync(IEnumerable<string> placeholderFragments)
  {
    foreach (string placeholderFragment in placeholderFragments)
    {
      bool clicked = await Page.EvaluateAsync<bool>("""
        (fragment) => {
          const inputs = Array.from(document.querySelectorAll('textarea, input'));
          const input = inputs.find(element => {
            const placeholder = element.getAttribute('placeholder') || '';
            const rect = element.getBoundingClientRect();
            return placeholder.includes(fragment) && rect.width > 0 && rect.height > 0;
          });

          if (!input) {
            return false;
          }

          const container = input.closest('div.relative');
          const button = container?.querySelector('button');
          if (!button) {
            return false;
          }

          button.click();
          return true;
        }
        """, placeholderFragment);

      if (clicked)
      {
        await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
        return;
      }
    }

    throw new InvalidOperationException($"Unable to find a nearby action button for placeholders: {string.Join(", ", placeholderFragments)}");
  }

  public async Task PressAsync(string key)
  {
    await Page.Keyboard.PressAsync(key);
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task WaitForTextAsync(string text, int timeoutMs = 10000)
  {
    await WaitForTextsAsync([text], timeoutMs);
  }

  public async Task WaitForTextsAsync(IEnumerable<string> texts, int timeoutMs = 10000)
  {
    foreach (string text in texts)
    {
      try
      {
        await Page.GetByText(text, new PageGetByTextOptions
        {
          Exact = false,
        }).First.WaitForAsync(new LocatorWaitForOptions
        {
          Timeout = timeoutMs,
        });
        return;
      }
      catch (TimeoutException)
      {
      }
    }

    throw new TimeoutException($"Unable to find any expected text: {string.Join(", ", texts)}");
  }

  public async Task DoubleClickTextAsync(string text)
  {
    ILocator locator = Page.GetByText(text, new PageGetByTextOptions
    {
      Exact = false,
    }).First;
    await locator.DblClickAsync();
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task ClickHeaderBarButtonAsync(int buttonIndex)
  {
    ILocator headerButtons = Page.Locator("div.flex.p-3.items-center.border-b button");
    int count = await headerButtons.CountAsync();
    if (count <= buttonIndex)
    {
      throw new InvalidOperationException($"Header button index {buttonIndex} is out of range. Found {count} buttons.");
    }

    await headerButtons.Nth(buttonIndex).ClickAsync();
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task OpenUserMenuAsync()
  {
    string escapedUsername = StatePreparation.Username.Replace("'", "\\'");
    ILocator locator = Page.Locator($"button:has-text('{escapedUsername}'), [role='button']:has-text('{escapedUsername}')");
    if (await locator.CountAsync() == 0)
    {
      throw new InvalidOperationException($"Unable to find user menu button for user {StatePreparation.Username}.");
    }

    await locator.First.ClickAsync();
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task EnsureChatbarVisibleAsync()
  {
    bool changed = await Page.EvaluateAsync<bool>("""
      () => {
        const raw = localStorage.getItem('settings');
        const settings = raw ? JSON.parse(raw) : {};
        if (settings.showChatBar === true) {
          return false;
        }

        settings.showChatBar = true;
        settings.chatBarWidth = settings.chatBarWidth || 320;
        localStorage.setItem('settings', JSON.stringify(settings));
        return true;
      }
      """);

    if (!changed)
    {
      return;
    }

    await Page.ReloadAsync();
    await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    await WaitForUiSettledAsync(waitForNetworkIdle: true, CancellationToken.None);
  }

  public async Task OpenSelectedChatActionsAsync()
  {
    bool clicked = await Page.EvaluateAsync<bool>("""
      () => {
        const rows = Array.from(document.querySelectorAll("a[href^='#/']"))
          .map(anchor => anchor.parentElement)
          .filter(row => !!row);

        for (const row of rows) {
          const buttons = Array.from(row.querySelectorAll('button')).filter(button => {
            const rect = button.getBoundingClientRect();
            return rect.width > 0 && rect.height > 0;
          });

          if (buttons.length === 1) {
            buttons[0].click();
            return true;
          }
        }

        return false;
      }
      """);

    if (!clicked)
    {
      throw new InvalidOperationException("Unable to find the selected chat action menu trigger.");
    }

    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task OpenChatActionsForRowTextAsync(string rowText)
  {
    string escaped = rowText.Replace("'", "\\'");
    ILocator row = Page.Locator($"a:has-text('{escaped}')").First.Locator("xpath=..");
    await row.WaitForAsync();
    await row.HoverAsync();

    ILocator button = row.Locator("button").Last;
    if (await button.CountAsync() == 0)
    {
      throw new InvalidOperationException($"Unable to find an action button for chat row '{rowText}'.");
    }

    await button.ClickAsync();
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task ClickFirstChatPresetCardAsync()
  {
    ILocator card = Page.Locator("div.rounded-sm.p-4.cursor-grab").First;
    if (await card.CountAsync() == 0)
    {
      bool clicked = await Page.EvaluateAsync<bool>("""
        () => {
          const card = document.querySelector('div.rounded-sm.p-4.cursor-grab');
          if (!card) {
            return false;
          }

          card.click();
          return true;
        }
        """);

      if (!clicked)
      {
        throw new InvalidOperationException("Unable to click the first chat preset card.");
      }
    }
    else
    {
      await card.ClickAsync();
    }

    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task OpenFirstChatModelSettingsAsync()
  {
    ILocator button = Page.Locator("div.absolute.top-0.left-0.w-full button:has(svg.rotate-90)").First;
    if (await button.CountAsync() == 0)
    {
      throw new InvalidOperationException("Unable to open the first chat model settings dialog.");
    }

    await button.ClickAsync();
    await Page.Locator("[role='dialog']").First.WaitForAsync();
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task OpenSessionManagerAsync()
  {
    if (!await TryOpenSessionManagerButtonAsync())
    {
      await ClickButtonByTextsAsync(["Agent"]);
      if (!await TryOpenSessionManagerButtonAsync())
      {
        throw new InvalidOperationException("Unable to open the Sandbox Manager.");
      }
    }

    await WaitForTextsAsync(["Sandbox Manager", "沙箱管理"]);
    await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
  }

  public async Task ExpandAdminModelProviderUntilActionVisibleAsync(string actionTitle)
  {
    await ExpandAdminModelProviderUntilActionVisibleAsync([actionTitle]);
  }

  public async Task ExpandAdminModelProviderUntilActionVisibleAsync(IEnumerable<string> actionTitles)
  {
    ILocator providers = Page.Locator("div.mb-2 > div.rounded-lg.border.bg-card");
    int count = await providers.CountAsync();
    if (count == 0)
    {
      throw new InvalidOperationException("Unable to find any model provider cards.");
    }

    for (int index = 0; index < count; index++)
    {
      ILocator provider = providers.Nth(index);
      await provider.ClickAsync();
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);

      foreach (string actionTitle in actionTitles)
      {
        if (await Page.Locator($"button[title='{actionTitle}'], button[aria-label='{actionTitle}']").CountAsync() > 0)
        {
          return;
        }
      }

      ILocator keys = provider.Locator("div.rounded-md.border");
      int keyCount = await keys.CountAsync();
      for (int keyIndex = 0; keyIndex < keyCount; keyIndex++)
      {
        await keys.Nth(keyIndex).ClickAsync();
        await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);

        foreach (string actionTitle in actionTitles)
        {
          if (await Page.Locator($"button[title='{actionTitle}'], button[aria-label='{actionTitle}']").CountAsync() > 0)
          {
            return;
          }
        }
      }
    }

    throw new InvalidOperationException($"Unable to find admin model actions after expanding providers: {string.Join(", ", actionTitles)}");
  }

  private async Task<bool> TryOpenSessionManagerButtonAsync()
  {
    bool clicked = await Page.EvaluateAsync<bool>("""
      () => {
        const buttons = Array.from(document.querySelectorAll('button'));
        const agent = buttons.find(button => (button.textContent || '').trim() === 'Agent');
        if (!agent) {
          return false;
        }

        const wrapper = agent.parentElement?.parentElement;
        if (!wrapper) {
          return false;
        }

        const others = Array.from(wrapper.querySelectorAll('button')).filter(button => {
          if (button === agent) {
            return false;
          }

          const rect = button.getBoundingClientRect();
          return rect.width > 0 && rect.height > 0;
        });

        if (others.length == 0) {
          return false;
        }

        others[0].click();
        return true;
      }
      """);

    if (clicked)
    {
      await WaitForUiSettledAsync(waitForNetworkIdle: false, CancellationToken.None);
    }

    return clicked;
  }

  private async Task EnsureThemeAsync(bool waitForNetworkIdle, CancellationToken cancellationToken)
  {
    string targetTheme = Theme == ThemeKind.Dark ? "dark" : "light";
    string currentTheme = await Page.EvaluateAsync<string>("() => localStorage.getItem('theme') || ''");

    if (string.Equals(targetTheme, currentTheme, StringComparison.OrdinalIgnoreCase))
    {
      return;
    }

    await Page.EvaluateAsync("(theme) => localStorage.setItem('theme', theme)", targetTheme);
    await Page.ReloadAsync();
    await Page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
    await WaitForUiSettledAsync(waitForNetworkIdle, cancellationToken);
  }

  private async Task WaitForUiSettledAsync(bool waitForNetworkIdle, CancellationToken cancellationToken)
  {
    await Page.Locator("body").WaitForAsync();

    if (waitForNetworkIdle)
    {
      try
      {
        await Page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
        {
          Timeout = 5000,
        });
      }
      catch (TimeoutException)
      {
        Logger.LogDebug("Network idle wait timed out for {ScenarioId}; using fallback delay.", Scenario.Id);
      }
    }

    await Task.Delay(Settings.WaitAfterNavigationMs, cancellationToken);
  }
}