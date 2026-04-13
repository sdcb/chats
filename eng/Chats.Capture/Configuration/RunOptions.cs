using Chats.Capture.Models;

namespace Chats.Capture.Configuration;

public sealed class RunOptions
{
  public List<string> ScenarioIds { get; } = [];
  public List<string> Areas { get; } = [];
  public List<string> Pages { get; } = [];
  public List<string> Features { get; } = [];
  public List<string> Tags { get; } = [];

  public string Theme { get; set; } = "all";
  public int? Parallelism { get; set; }
  public bool DryRun { get; set; }
  public bool ShowHelp { get; set; }
  public bool? HeadlessOverride { get; set; }

  public IReadOnlyList<ThemeKind> ResolveThemes()
  {
    return Theme.ToLowerInvariant() switch
    {
      "light" => [ThemeKind.Light],
      "dark" => [ThemeKind.Dark],
      _ => [ThemeKind.Light, ThemeKind.Dark],
    };
  }
}