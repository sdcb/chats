using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Chats.BE.DB;

public partial class ChatConfig
{
    public ChatConfig Clone()
    {
        return new ChatConfig
        {
            Id = Id,
            HashCode = HashCode,
            ModelId = ModelId,
            SystemPrompt = SystemPrompt,
            Temperature = Temperature,
            WebSearchEnabled = WebSearchEnabled,
            MaxOutputTokens = MaxOutputTokens,
            ReasoningEffort = ReasoningEffort,
            ImageSizeId = ImageSizeId,
        };
    }

    public bool ContentEquals(ChatConfig other)
    {
        return ModelId == other.ModelId
            && (SystemPrompt ?? "") == (other.SystemPrompt ?? "")
            && Temperature == other.Temperature
            && WebSearchEnabled == other.WebSearchEnabled
            && MaxOutputTokens == other.MaxOutputTokens
            && ReasoningEffort == other.ReasoningEffort
            && ImageSizeId == other.ImageSizeId
            && GetMcpServerIds().SetEquals(other.GetMcpServerIds());
    }

    private HashSet<int> GetMcpServerIds()
    {
        return [.. ChatConfigMcps.Select(x => x.McpServerId)];
    }

    /// <summary>
    /// 计算当前实例的哈希值，采用 SHA256 算法：每个字段的原始内存表示内部使用分隔符分隔，
    /// 其中 SystemPrompt 的字符数据直接使用其底层 UTF‑16 编码，不额外生成中间字符串。
    /// </summary>
    public long GenerateDBHashCode()
    {
        using IncrementalHash incrementalHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        // 辅助：向哈希器追加数据，再追加分隔符
        void AppendField(ReadOnlySpan<byte> data, bool withSeparator = true)
        {
            incrementalHash.AppendData(data);
            if (withSeparator)
            {
                incrementalHash.AppendData("|"u8);
            }
        }

        // 1. ModelId: short 型 2 字节
        Span<byte> shortBuffer = stackalloc byte[2];
        BitConverter.TryWriteBytes(shortBuffer, ModelId);
        AppendField(shortBuffer);

        // 2. SystemPrompt：先写入字符个数（int 4字节）
        Span<byte> intBuffer = stackalloc byte[4];
        int promptLength = SystemPrompt is null ? 0 : SystemPrompt.Length;
        BitConverter.TryWriteBytes(intBuffer, promptLength);
        AppendField(intBuffer);

        // 如果 SystemPrompt 非空，则直接追加其内存中 UTF‑16 的字节数据
        if (SystemPrompt is not null && promptLength > 0)
        {
            ReadOnlySpan<char> charSpan = SystemPrompt.AsSpan();
            ReadOnlySpan<byte> charBytes = MemoryMarshal.AsBytes(charSpan);
            AppendField(charBytes);
        }

        // 3. Temperature (float?): 先写入存在标志（1字节）
        Span<byte> flagBuffer = [(byte)(Temperature.HasValue ? 1 : 0)];
        AppendField(flagBuffer, withSeparator: false);
        // 如有值，则写入 4 字节的 float
        if (Temperature.HasValue)
        {
            Span<byte> floatBuffer = stackalloc byte[4];
            BitConverter.TryWriteBytes(floatBuffer, Temperature.Value);
            AppendField(floatBuffer);
        }

        // 4. WebSearchEnabled (bool)：用 1 字节表示
        flagBuffer[0] = (byte)(WebSearchEnabled ? 1 : 0);
        AppendField(flagBuffer);

        // 5. MaxOutputTokens (int?): 先写存在标志，再写 4 字节 int（如有值）
        flagBuffer[0] = (byte)(MaxOutputTokens.HasValue ? 1 : 0);
        AppendField(flagBuffer, withSeparator: false);
        if (MaxOutputTokens.HasValue)
        {
            BitConverter.TryWriteBytes(intBuffer, MaxOutputTokens.Value);
            AppendField(intBuffer);
        }

        // 6. ReasoningEffort (byte): 用 1 字节表示
        flagBuffer[0] = ReasoningEffort;
        AppendField(flagBuffer);

        // 7. ImageSizeId (short): 仅当非默认值(0)时才包含以保持向后兼容
        if (ImageSizeId != 0)
        {
            BitConverter.TryWriteBytes(shortBuffer, ImageSizeId);
            AppendField(shortBuffer);
        }

        // 8. McpServerIds: 仅当存在关联时才包含以保持向后兼容
        int[] mcpServerIds = ChatConfigMcps.Select(x => x.McpServerId).OrderBy(x => x).ToArray();
        if (mcpServerIds.Length > 0)
        {
            // 先写入MCP服务器数量
            BitConverter.TryWriteBytes(intBuffer, mcpServerIds.Length);
            AppendField(intBuffer);
            
            // 然后写入每个MCP服务器ID（已排序确保一致性）
            foreach (int mcpId in mcpServerIds)
            {
                BitConverter.TryWriteBytes(intBuffer, mcpId);
                AppendField(intBuffer);
            }
        }

        // 计算 SHA256 哈希，取前 8 字节转换为 long 类型
        byte[] fullHash = incrementalHash.GetHashAndReset();
        long hashCode = BinaryPrimitives.ReadInt64LittleEndian(fullHash);
        return hashCode;
    }
}