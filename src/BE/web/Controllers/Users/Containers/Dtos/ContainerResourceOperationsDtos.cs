using Chats.DockerInterface.Models;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Users.Containers.Dtos;

public sealed record RunContainerCommandRequest([Required] string Command, int? TimeoutSeconds);
public sealed record ContainerPathRequest([Required] string Path);
public sealed record SaveContainerTextFileRequest([Required] string Path, string? Text);
public sealed record ContainerTextFileResponse(string Path, bool IsText, long SizeBytes, string? Text);
public sealed record ContainerDirectoryListResponse(string Path, IReadOnlyList<FileEntry> Entries);
public sealed record ContainerEnvironmentVariable(string Key, string Value);
public sealed record ContainerEnvironmentVariablesResponse(
    IReadOnlyList<ContainerEnvironmentVariable> SystemVariables,
    IReadOnlyList<ContainerEnvironmentVariable> UserVariables);
public sealed record SaveContainerEnvironmentVariablesRequest(IReadOnlyList<ContainerEnvironmentVariable> Variables);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(ContainerCommandStdoutLine), "stdout")]
[JsonDerivedType(typeof(ContainerCommandStderrLine), "stderr")]
[JsonDerivedType(typeof(ContainerCommandExitLine), "exit")]
[JsonDerivedType(typeof(ContainerCommandErrorLine), "error")]
public abstract record ContainerCommandStreamLine;

public sealed record ContainerCommandStdoutLine(string Data) : ContainerCommandStreamLine;
public sealed record ContainerCommandStderrLine(string Data) : ContainerCommandStreamLine;
public sealed record ContainerCommandExitLine(long ExitCode, long ExecutionTimeMs) : ContainerCommandStreamLine;
public sealed record ContainerCommandErrorLine(string Message) : ContainerCommandStreamLine;
