using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.DockerSessions.Dtos;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(CommandStdoutLine), "stdout")]
[JsonDerivedType(typeof(CommandStderrLine), "stderr")]
[JsonDerivedType(typeof(CommandExitLine), "exit")]
[JsonDerivedType(typeof(CommandErrorLine), "error")]
public abstract record CommandStreamLine;

public sealed record CommandStdoutLine(
    [property: JsonPropertyName("data")] string Data
) : CommandStreamLine;

public sealed record CommandStderrLine(
    [property: JsonPropertyName("data")] string Data
) : CommandStreamLine;

public sealed record CommandExitLine(
    [property: JsonPropertyName("exitCode")] long ExitCode,
    [property: JsonPropertyName("executionTimeMs")] long ExecutionTimeMs
) : CommandStreamLine;

public sealed record CommandErrorLine(
    [property: JsonPropertyName("message")] string Message
) : CommandStreamLine;

