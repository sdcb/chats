using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace Chats.BE.Controllers.Common.Dtos;

public record PagedResult<T> where T : class
{
    [JsonPropertyName("rows")]
    public required T[] Rows { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }
}

public static class PagedResult
{
    public static async Task<PagedResult<TFinal>> FromQuery<TFinal>(IQueryable<TFinal> rows, PagingRequest pagingRequest, CancellationToken cancellationToken) where TFinal : class
    {
        return new PagedResult<TFinal>
        {
            Rows = await rows
                .Skip(pagingRequest.Skip)
                .Take(pagingRequest.PageSize)
                .ToArrayAsync(cancellationToken),
            Count = await rows.CountAsync(cancellationToken)
        };
    }

    public static async Task<PagedResult<TFinal>> FromTempQuery<TTemp, TFinal>(IQueryable<TTemp> rows, PagingRequest pagingRequest, Converter<TTemp, TFinal> tempConverter, CancellationToken cancellationToken) where TFinal : class where TTemp : class
    {
        TTemp[] tempData = await rows
            .Skip(pagingRequest.Skip)
            .Take(pagingRequest.PageSize)
            .ToArrayAsync(cancellationToken);
        TFinal[] finalData = Array.ConvertAll(tempData, tempConverter);
        return new PagedResult<TFinal>
        {
            Rows = finalData,
            Count = await rows.CountAsync(cancellationToken)
        };
    }
}