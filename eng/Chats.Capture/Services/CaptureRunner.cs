using System.Collections.Concurrent;
using System.Diagnostics;

using Chats.Capture.Browser;
using Chats.Capture.Configuration;
using Chats.Capture.Models;
using Chats.Capture.Output;
using Chats.Capture.State;

using Microsoft.Extensions.Logging;

namespace Chats.Capture.Services;

public sealed class CaptureRunner
{
  private readonly CaptureSettings _settings;
  private readonly RunOptions _runOptions;
  private readonly PlaywrightBrowserService _browser;
  private readonly CaptureOutputService _output;
  private readonly StatePreparationService _statePreparation;
  private readonly ILoggerFactory _loggerFactory;
  private readonly ILogger<CaptureRunner> _logger;

  public CaptureRunner(
    CaptureSettings settings,
    RunOptions runOptions,
    PlaywrightBrowserService browser,
    CaptureOutputService output,
    StatePreparationService statePreparation,
    ILoggerFactory loggerFactory,
    ILogger<CaptureRunner> logger)
  {
    _settings = settings;
    _runOptions = runOptions;
    _browser = browser;
    _output = output;
    _statePreparation = statePreparation;
    _loggerFactory = loggerFactory;
    _logger = logger;
  }

  public async Task<IReadOnlyList<CaptureExecutionResult>> ExecuteAsync(IReadOnlyList<CaptureScenario> scenarios, CancellationToken cancellationToken)
  {
    _output.Initialize();

    List<(CaptureScenario Scenario, ThemeKind Theme)> jobs =
      [..
        from scenario in scenarios
        from theme in _runOptions.ResolveThemes()
        select (scenario, theme)
      ];

    ConcurrentBag<CaptureExecutionResult> results = [];

    await Parallel.ForEachAsync(
      jobs,
      new ParallelOptions
      {
        CancellationToken = cancellationToken,
        MaxDegreeOfParallelism = _runOptions.Parallelism ?? _settings.DefaultParallelism,
      },
      async (job, token) =>
      {
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
          await using BrowserPageLease lease = await _browser.OpenAuthenticatedPageAsync(job.Theme, token);
          ILogger scenarioLogger = _loggerFactory.CreateLogger($"Scenario.{job.Scenario.Id}");
          ScenarioContext context = new(
            job.Scenario,
            job.Theme,
            lease.Page,
            _settings,
            _output,
            _statePreparation,
            scenarioLogger);

          await job.Scenario.ExecuteAsync(context, token);
          results.Add(new CaptureExecutionResult(
            job.Scenario.Id,
            job.Theme,
            true,
            context.LastScreenshotPath,
            stopwatch.ElapsedMilliseconds,
            null));
        }
        catch (Exception ex)
        {
          _logger.LogError(ex, "Scenario {ScenarioId} failed in {Theme} theme.", job.Scenario.Id, job.Theme);
          results.Add(new CaptureExecutionResult(
            job.Scenario.Id,
            job.Theme,
            false,
            null,
            stopwatch.ElapsedMilliseconds,
            ex.Message));
        }
      });

    List<CaptureExecutionResult> orderedResults = results
      .OrderBy(result => result.Theme)
      .ThenBy(result => result.ScenarioId, StringComparer.OrdinalIgnoreCase)
      .ToList();

    await _output.WriteManifestAsync(orderedResults, cancellationToken);
    return orderedResults;
  }
}