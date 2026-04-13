using Chats.Capture.Configuration;

namespace Chats.Capture.Models;

public sealed record CaptureScenario(
  string Id,
  string Area,
  string Page,
  string Feature,
  string? Route,
  IReadOnlyList<string> Tags,
  Func<Services.ScenarioContext, CancellationToken, Task> ExecuteAsync)
{
  public bool Matches(RunOptions options)
  {
    return MatchesList(options.ScenarioIds, Id)
      && MatchesList(options.Areas, Area)
      && MatchesList(options.Pages, Page)
      && MatchesList(options.Features, Feature)
      && MatchesTags(options.Tags);
  }

  private bool MatchesTags(List<string> requestedTags)
  {
    if (requestedTags.Count == 0)
    {
      return true;
    }

    return Tags.Any(tag => requestedTags.Contains(tag, StringComparer.OrdinalIgnoreCase));
  }

  private static bool MatchesList(List<string> requested, string value)
  {
    return requested.Count == 0 || requested.Contains(value, StringComparer.OrdinalIgnoreCase);
  }
}