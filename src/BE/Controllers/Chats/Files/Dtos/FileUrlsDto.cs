﻿using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Chats.Files.Dtos;

public record FileUrlsDto
{
    [JsonPropertyName("getUrl")]
    public required string GetUrl { get; init; }

    [JsonPropertyName("putUrl")]
    public required string PutUrl { get; init; }
}