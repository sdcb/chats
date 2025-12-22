using System.ComponentModel.DataAnnotations;
using Chats.Web.Services.UrlEncryption;

namespace Chats.Web.Controllers.Common.Dtos;

/// <summary>
/// 通用的加密重排序请求，可以解密为不同类型的ReorderRequest
/// </summary>
public class EncryptedReorderRequest
{
    [Required]
    public required string SourceId { get; init; }
    public string? PreviousId { get; init; } // 新位置的前一个元素
    public string? NextId { get; init; }     // 新位置的后一个元素

    /// <summary>
    /// 解密为ChatPreset的ReorderRequest
    /// </summary>
    /// <param name="idEncryption">ID加密服务</param>
    /// <returns>解密后的ReorderRequest&lt;int&gt;</returns>
    public ReorderRequest<int> DecryptAsChatPreset(IUrlEncryptionService idEncryption)
    {
        return Decrypt(idEncryption.DecryptChatPresetId);
    }

    /// <summary>
    /// 通用解密方法，使用自定义的解密函数
    /// </summary>
    /// <typeparam name="T">目标ID类型</typeparam>
    /// <param name="decryptFunc">解密函数</param>
    /// <returns>解密后的ReorderRequest&lt;T&gt;</returns>
    public ReorderRequest<T> Decrypt<T>(Func<string, T> decryptFunc) where T : struct
    {
        return new ReorderRequest<T>
        {
            SourceId = decryptFunc(SourceId),
            PreviousId = !string.IsNullOrEmpty(PreviousId) ? decryptFunc(PreviousId) : null,
            NextId = !string.IsNullOrEmpty(NextId) ? decryptFunc(NextId) : null
        };
    }
}