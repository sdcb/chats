using System.Text.Json;

using Chats.Capture.Configuration;
using Chats.Capture.Infrastructure;
using Chats.Capture.Models;

using Microsoft.Extensions.Logging;

namespace Chats.Capture.Output;

public sealed class CaptureOutputService
{
  private readonly ILogger<CaptureOutputService> _logger;
  private readonly string _runRoot;

  public CaptureOutputService(CaptureSettings settings, ILogger<CaptureOutputService> logger)
  {
    _logger = logger;
    string baseRoot = settings.ResolveOutputRoot();
    _runRoot = Path.Combine(baseRoot, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
  }

  public string RunRoot => _runRoot;

  public void Initialize()
  {
    Directory.CreateDirectory(_runRoot);
    _logger.LogInformation("Capture output root: {RunRoot}", _runRoot);
  }

  public string GetScreenshotPath(CaptureScenario scenario, ThemeKind theme, string? suffix = null)
  {
    string themeFolder = theme == ThemeKind.Dark ? "dark" : "light";
    string directory = Path.Combine(_runRoot, themeFolder, Slug.Create(scenario.Area), Slug.Create(scenario.Page));
    Directory.CreateDirectory(directory);

    string fileName = Slug.Create(suffix is null ? scenario.Id : $"{scenario.Id}-{suffix}") + ".png";
    return Path.Combine(directory, fileName);
  }

  public async Task WriteManifestAsync(IReadOnlyCollection<CaptureExecutionResult> results, CancellationToken cancellationToken)
  {
    string manifestPath = Path.Combine(_runRoot, "manifest.json");
    JsonSerializerOptions options = new()
    {
      PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
      WriteIndented = true,
    };

    await File.WriteAllTextAsync(manifestPath, JsonSerializer.Serialize(results, options), cancellationToken);
  }
}