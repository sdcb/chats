using Chats.Web.Controllers.Common.Dtos;

namespace Chats.Web.Controllers.Admin.AdminMessage.Dtos;

public record AdminChatsQueryRequest(string? User, string? Content) : PagingRequest;
