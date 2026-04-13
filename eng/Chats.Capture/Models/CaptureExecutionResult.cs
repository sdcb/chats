namespace Chats.Capture.Models;

public sealed record CaptureExecutionResult(
  string ScenarioId,
  ThemeKind Theme,
  bool Success,
  string? ScreenshotPath,
  long DurationMs,
  string? Error);