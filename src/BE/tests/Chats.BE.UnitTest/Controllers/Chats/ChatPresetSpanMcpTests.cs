using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.UnitTest.Controllers.Chats;

public sealed class ChatPresetSpanMcpTests
{
    private const int McpServerId = 42;

    [Fact]
    public async Task ApplyTo_EmptyPreset_RemovesPersistedMcpAssociation()
    {
        DbContextOptions<ChatsDB> options = CreateOptions();
        (int targetConfigId, int presetConfigId) = await SeedConfigs(options, targetMcpCount: 1, presetMcpCount: 0);

        await ApplyPreset(options, targetConfigId, presetConfigId);

        await using ChatsDB verificationDb = new(options);
        ChatConfigMcp[] associations = await verificationDb.ChatConfigMcps
            .Where(x => x.ChatConfigId == targetConfigId)
            .ToArrayAsync();
        Assert.Empty(associations);
    }

    [Fact]
    public async Task ApplyTo_SamePresetTwice_DoesNotCreateDuplicateMcpAssociations()
    {
        DbContextOptions<ChatsDB> options = CreateOptions();
        (int targetConfigId, int presetConfigId) = await SeedConfigs(options, targetMcpCount: 0, presetMcpCount: 1);

        await ApplyPreset(options, targetConfigId, presetConfigId);
        await ApplyPreset(options, targetConfigId, presetConfigId);

        await using ChatsDB verificationDb = new(options);
        ChatConfigMcp[] associations = await verificationDb.ChatConfigMcps
            .Where(x => x.ChatConfigId == targetConfigId && x.McpServerId == McpServerId)
            .ToArrayAsync();
        Assert.Single(associations);
    }

    [Fact]
    public async Task ApplyTo_PresetContainingMcp_RemovesExistingDuplicateAssociations()
    {
        DbContextOptions<ChatsDB> options = CreateOptions();
        (int targetConfigId, int presetConfigId) = await SeedConfigs(options, targetMcpCount: 2, presetMcpCount: 1);

        await ApplyPreset(options, targetConfigId, presetConfigId);

        await using ChatsDB verificationDb = new(options);
        ChatConfigMcp[] associations = await verificationDb.ChatConfigMcps
            .Where(x => x.ChatConfigId == targetConfigId && x.McpServerId == McpServerId)
            .ToArrayAsync();
        Assert.Single(associations);
    }

    [Fact]
    public async Task ApplyTo_PresetContainingMcp_CopiesCustomHeaders()
    {
        const string customHeaders = "{\"X-Test\":\"preset-value\"}";
        DbContextOptions<ChatsDB> options = CreateOptions();
        (int targetConfigId, int presetConfigId) = await SeedConfigs(
            options,
            targetMcpCount: 0,
            presetMcpCount: 1,
            presetCustomHeaders: customHeaders);

        await ApplyPreset(options, targetConfigId, presetConfigId);

        await using ChatsDB verificationDb = new(options);
        ChatConfigMcp association = await verificationDb.ChatConfigMcps
            .SingleAsync(x => x.ChatConfigId == targetConfigId && x.McpServerId == McpServerId);
        Assert.Equal(customHeaders, association.CustomHeaders);
    }

    private static DbContextOptions<ChatsDB> CreateOptions()
    {
        return new DbContextOptionsBuilder<ChatsDB>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
    }

    private static async Task<(int targetConfigId, int presetConfigId)> SeedConfigs(
        DbContextOptions<ChatsDB> options,
        int targetMcpCount,
        int presetMcpCount,
        string? presetCustomHeaders = null)
    {
        await using ChatsDB db = new(options);
        ChatConfig targetConfig = Config(targetMcpCount);
        ChatConfig presetConfig = Config(presetMcpCount, presetCustomHeaders);
        db.ChatConfigs.AddRange(targetConfig, presetConfig);
        await db.SaveChangesAsync();
        return (targetConfig.Id, presetConfig.Id);
    }

    private static async Task ApplyPreset(
        DbContextOptions<ChatsDB> options,
        int targetConfigId,
        int presetConfigId)
    {
        await using ChatsDB db = new(options);

        ChatConfig targetConfig = await db.ChatConfigs
            .Include(x => x.ChatConfigMcps)
            .SingleAsync(x => x.Id == targetConfigId);
        ChatConfig presetConfig = await db.ChatConfigs
            .Include(x => x.ChatConfigMcps)
            .SingleAsync(x => x.Id == presetConfigId);
        ChatSpan targetSpan = new() { ChatConfig = targetConfig };
        ChatPresetSpan presetSpan = new() { ChatConfig = presetConfig };

        presetSpan.ApplyTo(targetSpan, new Model());
        await db.SaveChangesAsync();
    }

    private static ChatConfig Config(int mcpCount, string? customHeaders = null)
    {
        return new ChatConfig
        {
            ChatConfigMcps = [.. Enumerable.Range(0, mcpCount)
                .Select(_ => new ChatConfigMcp
                {
                    McpServerId = McpServerId,
                    CustomHeaders = customHeaders,
                })],
        };
    }
}
