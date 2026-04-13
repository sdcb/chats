using Chats.Capture.Configuration;
using Chats.Capture.Models;
using Chats.Capture.Scenarios;

using Microsoft.Extensions.Logging;

namespace Chats.Capture.Services;

public sealed class CaptureApplication
{
  private readonly RunOptions _runOptions;
  private readonly CaptureRunner _runner;
  private readonly ILogger<CaptureApplication> _logger;

  public CaptureApplication(
    RunOptions runOptions,
    CaptureRunner runner,
    ILogger<CaptureApplication> logger)
  {
    _runOptions = runOptions;
    _runner = runner;
    _logger = logger;
  }

  public async Task<int> RunAsync(CancellationToken cancellationToken)
  {
    IReadOnlyList<CaptureScenario> allScenarios = ScenarioCatalog.Build();
    List<CaptureScenario> selectedScenarios = allScenarios
      .Where(scenario => scenario.Matches(_runOptions))
      .ToList();

    if (selectedScenarios.Count == 0)
    {
      _logger.LogWarning("No scenarios matched the provided filters.");
      return 1;
    }

    if (_runOptions.DryRun)
    {
      foreach (CaptureScenario scenario in selectedScenarios.OrderBy(s => s.Id, StringComparer.OrdinalIgnoreCase))
      {
        Console.WriteLine($"{scenario.Id} | area={scenario.Area} | page={scenario.Page} | feature={scenario.Feature} | route={scenario.Route ?? "<dynamic>"}");
      }

      Console.WriteLine();
      Console.WriteLine($"Scenarios: {selectedScenarios.Count}");
      Console.WriteLine($"Themes: {string.Join(", ", _runOptions.ResolveThemes())}");
      return 0;
    }

    _logger.LogInformation("Executing {ScenarioCount} scenarios across {ThemeCount} theme(s).", selectedScenarios.Count, _runOptions.ResolveThemes().Count);
    IReadOnlyList<CaptureExecutionResult> results = await _runner.ExecuteAsync(selectedScenarios, cancellationToken);

    int successCount = results.Count(result => result.Success);
    int failureCount = results.Count - successCount;
    _logger.LogInformation("Capture finished. Success: {SuccessCount}, Failure: {FailureCount}", successCount, failureCount);

    foreach (CaptureExecutionResult failure in results.Where(result => !result.Success))
    {
      _logger.LogError("Failed: {ScenarioId} [{Theme}] - {Error}", failure.ScenarioId, failure.Theme, failure.Error);
    }

    return failureCount == 0 ? 0 : 1;
  }
}