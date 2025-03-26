using Chats.BE.Controllers.Common.Dtos;
using System.Diagnostics.CodeAnalysis;

namespace Chats.BE.Controllers.Chats.UserChats.Dtos;

public record ChatsQuery(string? GroupId, string? Query) : QueryPagingRequest(Query)
{
    [SetsRequiredMembers]
    public ChatsQuery(string? GroupId, int Page, int PageSize, string? Query) : this(GroupId, Query)
    {
        this.Page = Page;
        this.PageSize = PageSize;
    }
}
