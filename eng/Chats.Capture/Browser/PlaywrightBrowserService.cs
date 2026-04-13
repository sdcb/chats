using Chats.Capture.Configuration;
using Chats.Capture.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Chats.Capture.Browser;

public sealed class PlaywrightBrowserService : IAsyncDisposable
{
  private readonly SemaphoreSlim _authLock = new(1, 1);
  private readonly SemaphoreSlim _availabilityLock = new(1, 1);
  private readonly CaptureSettings _settings;
  private readonly RunOptions _runOptions;
  private readonly ILogger<PlaywrightBrowserService> _logger;

  private IPlaywright? _playwright;
  private IBrowser? _browser;
  private bool _baseUrlChecked;

  public PlaywrightBrowserService(
    CaptureSettings settings,
    RunOptions runOptions,
    ILogger<PlaywrightBrowserService> logger)
  {
    _settings = settings;
    _runOptions = runOptions;
    _logger = logger;
  }

  public async Task<BrowserPageLease> OpenAuthenticatedPageAsync(ThemeKind theme, CancellationToken cancellationToken)
  {
    await EnsureInitializedAsync();
    await EnsureBaseUrlReachableAsync(cancellationToken);
    await EnsureAuthenticatedStateAsync(cancellationToken);

    IBrowserContext context = await _browser!.NewContextAsync(new BrowserNewContextOptions
    {
      StorageStatePath = _settings.ResolveAuthStatePath(),
      ViewportSize = new ViewportSize
      {
        Width = _settings.ViewportWidth,
        Height = _settings.ViewportHeight,
      },
      ColorScheme = theme == ThemeKind.Dark ? ColorScheme.Dark : ColorScheme.Light,
    });
    IPage page = await context.NewPageAsync();
    return new BrowserPageLease(context, page);
  }

  public async ValueTask DisposeAsync()
  {
    if (_browser is not null)
    {
      await _browser.CloseAsync();
    }

    _playwright?.Dispose();
  }

  private async Task EnsureInitializedAsync()
  {
    if (_browser is not null)
    {
      return;
    }

    _playwright = await Playwright.CreateAsync();
    _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
      Headless = _runOptions.HeadlessOverride ?? _settings.Headless,
    });
  }

  private async Task EnsureAuthenticatedStateAsync(CancellationToken cancellationToken)
  {
    string authStatePath = _settings.ResolveAuthStatePath();
    Directory.CreateDirectory(Path.GetDirectoryName(authStatePath)!);

    if (_settings.ReuseStoredAuthState && File.Exists(authStatePath) && await IsAuthStateValidAsync(authStatePath))
    {
      return;
    }

    await _authLock.WaitAsync(cancellationToken);
    try
    {
      if (_settings.ReuseStoredAuthState && File.Exists(authStatePath) && await IsAuthStateValidAsync(authStatePath))
      {
        return;
      }

      await LoginAndPersistStateAsync(authStatePath);
    }
    finally
    {
      _authLock.Release();
    }
  }

  private async Task EnsureBaseUrlReachableAsync(CancellationToken cancellationToken)
  {
    if (_baseUrlChecked)
    {
      return;
    }

    await _availabilityLock.WaitAsync(cancellationToken);
    try
    {
      if (_baseUrlChecked)
      {
        return;
      }

      using HttpClient client = new()
      {
        Timeout = TimeSpan.FromSeconds(5),
      };

      try
      {
        using HttpResponseMessage response = await client.GetAsync(_settings.BaseUrl, cancellationToken);
        _baseUrlChecked = response.IsSuccessStatusCode || (int)response.StatusCode < 500;
      }
      catch (Exception ex)
      {
        throw new InvalidOperationException(
          $"Unable to reach {_settings.BaseUrl}. Start the frontend site before running screenshots.",
          ex);
      }
    }
    finally
    {
      _availabilityLock.Release();
    }
  }

  private async Task<bool> IsAuthStateValidAsync(string authStatePath)
  {
    try
    {
      IBrowserContext context = await _browser!.NewContextAsync(new BrowserNewContextOptions
      {
        StorageStatePath = authStatePath,
        ViewportSize = new ViewportSize
        {
          Width = _settings.ViewportWidth,
          Height = _settings.ViewportHeight,
        },
      });

      try
      {
        IPage page = await context.NewPageAsync();
        await page.GotoAsync($"{_settings.BaseUrl}/home");
        await page.WaitForLoadStateAsync(LoadState.DOMContentLoaded);
        return !page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase);
      }
      finally
      {
        await context.CloseAsync();
      }
    }
    catch (Exception ex)
    {
      _logger.LogWarning(ex, "Stored auth state is invalid and will be recreated.");
      return false;
    }
  }

  private async Task LoginAndPersistStateAsync(string authStatePath)
  {
    _logger.LogInformation("Creating authenticated storage state at {AuthStatePath}", authStatePath);
    IBrowserContext context = await _browser!.NewContextAsync(new BrowserNewContextOptions
    {
      ViewportSize = new ViewportSize
      {
        Width = _settings.ViewportWidth,
        Height = _settings.ViewportHeight,
      },
    });

    try
    {
      IPage page = await context.NewPageAsync();
      await page.GotoAsync($"{_settings.BaseUrl}/login");
      await TryOpenAccountLoginTabAsync(page);
      ILocator usernameInput = page.Locator("input[autocomplete='username'], input[name='username']").First;
      ILocator passwordInput = page.Locator("input[autocomplete='current-password'], input[name='password'], input[type='password']").First;
      await usernameInput.FillAsync(_settings.GetRequiredUsername());
      await passwordInput.FillAsync(_settings.GetRequiredPassword());

      ILocator accountForm = page.Locator("form").Filter(new LocatorFilterOptions
      {
        Has = usernameInput,
      }).First;
      await accountForm.Locator("button[type='submit']").First.ClickAsync();
      await page.WaitForFunctionAsync("() => !!localStorage.getItem('session')", null, new PageWaitForFunctionOptions
      {
        Timeout = 15000,
      });
      await context.StorageStateAsync(new BrowserContextStorageStateOptions
      {
        Path = authStatePath,
      });
    }
    finally
    {
      await context.CloseAsync();
    }
  }

  private static async Task TryOpenAccountLoginTabAsync(IPage page)
  {
    ILocator usernameInput = page.Locator("input[autocomplete='username'], input[name='username']");
    if (await usernameInput.CountAsync() > 0)
    {
      return;
    }

    string[] tabTexts = ["Account Login", "账号登录"];
    foreach (string text in tabTexts)
    {
      ILocator tab = page.Locator($"button:has-text('{text}'), [role='tab']:has-text('{text}')");
      try
      {
        await tab.First.WaitForAsync(new LocatorWaitForOptions
        {
          Timeout = 15000,
        });
      }
      catch (TimeoutException)
      {
        continue;
      }

      await tab.First.ClickAsync(new LocatorClickOptions
      {
        Force = true,
      });
      await usernameInput.First.WaitForAsync(new LocatorWaitForOptions
      {
        Timeout = 15000,
      });
      break;
    }
  }
}