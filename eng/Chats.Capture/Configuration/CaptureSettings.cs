using Microsoft.Extensions.Configuration;

namespace Chats.Capture.Configuration;

public sealed class CaptureSettings
{
  private static readonly Lazy<string> RepositoryRoot = new(FindRepositoryRoot);

  public const string UsernameEnvVar = "CHATS_USERNAME";
  public const string PasswordEnvVar = "CHATS_PASSWORD";
  public const string ApiUrlEnvVar = "API_URL";

  public string BaseUrl { get; init; } = "http://localhost:3000";
  public string ApiUrl { get; init; } = string.Empty;
  public string OutputRoot { get; init; } = "eng/Chats.Capture/output";
  public string AuthStatePath { get; init; } = "eng/Chats.Capture/.state/auth.json";
  public int WaitAfterNavigationMs { get; init; } = 350;
  public int DefaultParallelism { get; init; } = 3;
  public bool Headless { get; init; } = true;
  public int ViewportWidth { get; init; } = 1600;
  public int ViewportHeight { get; init; } = 1000;
  public bool ReuseStoredAuthState { get; init; } = true;

  public static CaptureSettings FromConfiguration(IConfiguration configuration)
  {
    IConfigurationSection section = configuration.GetSection("Capture");

    return new CaptureSettings
    {
      BaseUrl = section["BaseUrl"] ?? "http://localhost:3000",
      ApiUrl = ResolveApiUrl(section),
      OutputRoot = section["OutputRoot"] ?? "eng/Chats.Capture/output",
      AuthStatePath = section["AuthStatePath"] ?? "eng/Chats.Capture/.state/auth.json",
      WaitAfterNavigationMs = GetInt(section, "WaitAfterNavigationMs", 350),
      DefaultParallelism = GetInt(section, "DefaultParallelism", 3),
      Headless = GetBool(section, "Headless", true),
      ViewportWidth = GetInt(section, "ViewportWidth", 1600),
      ViewportHeight = GetInt(section, "ViewportHeight", 1000),
      ReuseStoredAuthState = GetBool(section, "ReuseStoredAuthState", true),
    };
  }

  public string GetRequiredUsername()
  {
    return Environment.GetEnvironmentVariable(UsernameEnvVar)
      ?? throw new InvalidOperationException($"Missing environment variable: {UsernameEnvVar}");
  }

  public string GetRequiredPassword()
  {
    return Environment.GetEnvironmentVariable(PasswordEnvVar)
      ?? throw new InvalidOperationException($"Missing environment variable: {PasswordEnvVar}");
  }

  public string ResolveOutputRoot()
  {
    return ResolveRepositoryRelativePath(OutputRoot);
  }

  public string ResolveApiUrl()
  {
    return string.IsNullOrWhiteSpace(ApiUrl) ? BaseUrl.TrimEnd('/') : ApiUrl.TrimEnd('/');
  }

  public string ResolveAuthStatePath()
  {
    return ResolveRepositoryRelativePath(AuthStatePath);
  }

  private static int GetInt(IConfigurationSection section, string key, int fallback)
  {
    return int.TryParse(section[key], out int value) ? value : fallback;
  }

  private static bool GetBool(IConfigurationSection section, string key, bool fallback)
  {
    return bool.TryParse(section[key], out bool value) ? value : fallback;
  }

  private static string ResolveApiUrl(IConfigurationSection section)
  {
    string? configured = section["ApiUrl"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
      return configured;
    }

    string? envValue = Environment.GetEnvironmentVariable(ApiUrlEnvVar);
    if (!string.IsNullOrWhiteSpace(envValue))
    {
      return envValue;
    }

    string envFilePath = Path.Combine(RepositoryRoot.Value, "src", "FE", ".env.local");
    if (File.Exists(envFilePath))
    {
      foreach (string line in File.ReadAllLines(envFilePath))
      {
        if (!line.StartsWith("API_URL=", StringComparison.OrdinalIgnoreCase))
        {
          continue;
        }

        string value = line["API_URL=".Length..].Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
          return value;
        }
      }
    }

    return string.Empty;
  }

  private static string ResolveRepositoryRelativePath(string path)
  {
    return Path.IsPathRooted(path)
      ? path
      : Path.GetFullPath(path, RepositoryRoot.Value);
  }

  private static string FindRepositoryRoot()
  {
    foreach (string candidate in GetSearchRoots())
    {
      for (DirectoryInfo? current = new(candidate); current is not null; current = current.Parent)
      {
        if (File.Exists(Path.Combine(current.FullName, "Chats.sln"))
          && Directory.Exists(Path.Combine(current.FullName, "src", "FE")))
        {
          return current.FullName;
        }
      }
    }

    return Environment.CurrentDirectory;
  }

  private static IEnumerable<string> GetSearchRoots()
  {
    yield return AppContext.BaseDirectory;
    yield return Environment.CurrentDirectory;
  }
}