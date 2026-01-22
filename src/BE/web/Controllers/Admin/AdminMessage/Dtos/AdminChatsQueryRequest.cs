using Chats.BE.Controllers.Common.Dtos;

namespace Chats.BE.Controllers.Admin.AdminMessage.Dtos;

public record AdminChatsQueryRequest(string? User, string? Content) : PagingRequest;
