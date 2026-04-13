using Chats.Capture.Configuration;

namespace Chats.Capture.Infrastructure;

public static class CliParser
{
  public static RunOptions Parse(string[] args)
  {
    RunOptions options = new();

    for (int index = 0; index < args.Length; index++)
    {
      string current = args[index];
      if (!current.StartsWith("--", StringComparison.Ordinal))
      {
        continue;
      }

      string raw = current[2..];
      string key = raw;
      string? value = null;
      int equalsIndex = raw.IndexOf('=');
      if (equalsIndex >= 0)
      {
        key = raw[..equalsIndex];
        value = raw[(equalsIndex + 1)..];
      }
      else if (index + 1 < args.Length && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
      {
        value = args[++index];
      }

      switch (key.ToLowerInvariant())
      {
        case "help":
        case "h":
          options.ShowHelp = true;
          break;
        case "dry-run":
        case "list":
          options.DryRun = true;
          break;
        case "scenario":
        case "id":
          AddValues(options.ScenarioIds, value);
          break;
        case "area":
          AddValues(options.Areas, value);
          break;
        case "page":
          AddValues(options.Pages, value);
          break;
        case "feature":
          AddValues(options.Features, value);
          break;
        case "tag":
          AddValues(options.Tags, value);
          break;
        case "theme":
          if (!string.IsNullOrWhiteSpace(value))
          {
            options.Theme = value;
          }
          break;
        case "parallelism":
        case "p":
          if (int.TryParse(value, out int parallelism) && parallelism > 0)
          {
            options.Parallelism = parallelism;
          }
          break;
        case "headed":
          options.HeadlessOverride = false;
          break;
        case "headless":
          options.HeadlessOverride = true;
          break;
      }
    }

    return options;
  }

  public static void WriteHelp()
  {
    Console.WriteLine("Chats.Capture - Playwright C# 截图工具");
    Console.WriteLine();
    Console.WriteLine("  --dry-run                 只列出场景，不执行截图");
    Console.WriteLine("  --scenario <id>           只跑指定场景，可重复或逗号分隔");
    Console.WriteLine("  --area <name>             只跑指定区域");
    Console.WriteLine("  --page <name>             只跑指定页面");
    Console.WriteLine("  --feature <name>          只跑指定功能");
    Console.WriteLine("  --tag <name>              按标签筛选");
    Console.WriteLine("  --theme <all|light|dark>  主题筛选");
    Console.WriteLine("  --parallelism <n>         并行度");
    Console.WriteLine("  --headed                  有头模式");
  }

  private static void AddValues(List<string> target, string? value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return;
    }

    foreach (string item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
      target.Add(item);
    }
  }
}