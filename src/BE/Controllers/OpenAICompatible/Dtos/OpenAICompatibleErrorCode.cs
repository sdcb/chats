﻿namespace Chats.BE.Controllers.OpenAICompatible.Dtos;

public enum OpenAICompatibleErrorCode
{
    Unknown = 4000,
    InsufficientBalance = 4001,
    UpstreamError = 4002,
    InvalidModel = 4003,
    SubscriptionExpired = 4004,
    BadParameter = 4005,
}