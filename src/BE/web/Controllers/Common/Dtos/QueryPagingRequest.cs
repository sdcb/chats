using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Chats.BE.Controllers.Common.Dtos;

public record QueryPagingRequest(string? Query) : PagingRequest;

public record PagingRequest
{
    [DefaultValue(1)]
    public required int Page { get; init; }

    [DefaultValue(20), Range(1, 100)]
    public required int PageSize { get; init; }

    public int Skip => (Page - 1) * PageSize;
}