using Chats.DB;

namespace Chats.BE.Services.Models;

public sealed record ChatRunRequest
{
    public required UserModel UserModel { get; init; }

    public required ChatRequest ChatRequest { get; init; }
}
