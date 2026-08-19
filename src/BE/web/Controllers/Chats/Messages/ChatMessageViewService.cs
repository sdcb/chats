using Chats.BE.Controllers.Chats.Messages.Dtos;
using Chats.BE.Services.FileServices;
using Chats.BE.Services.UrlEncryption;
using Chats.DB;
using Microsoft.EntityFrameworkCore;

namespace Chats.BE.Controllers.Chats.Messages;

public sealed class ChatMessageViewService(
    ChatsDB db,
    IUrlEncryptionService urlEncryption,
    FileUrlProvider fileUrlProvider)
{
    internal sealed record TurnMeta(long Id, long? ParentId, bool IsUser, byte? SpanId, DateTime CreatedAt);

    public async Task<ChatMessageViewDto?> GetInitialViewAsync(
        int chatId,
        DateTime? createdBeforeOrAt,
        CancellationToken cancellationToken)
    {
        long? configuredLeafId = await db.Chats
            .Where(x => x.Id == chatId)
            .Select(x => x.LeafTurnId)
            .FirstOrDefaultAsync(cancellationToken);

        TurnMeta[] turns = await LoadMetadata(chatId, createdBeforeOrAt, cancellationToken);
        if (turns.Length == 0)
        {
            return new ChatMessageViewDto { Messages = [], LeafMessageId = null };
        }

        IReadOnlyDictionary<long, TurnMeta> turnMap = turns.ToDictionary(x => x.Id);
        long leafId = configuredLeafId.HasValue && turnMap.ContainsKey(configuredLeafId.Value)
            ? configuredLeafId.Value
            : FindLatestDeepestLeaf(turns, turns.Select(x => x.Id).ToHashSet());

        List<List<TurnMeta>> levels = BuildVisibleLevels(turns, leafId);
        return await BuildResult(levels, leafId, turns, cancellationToken);
    }

    public async Task<ChatMessageViewDto?> GetSubtreeViewAsync(
        int chatId,
        long rootTurnId,
        DateTime? createdBeforeOrAt,
        CancellationToken cancellationToken)
    {
        TurnMeta[] turns = await LoadMetadata(chatId, createdBeforeOrAt, cancellationToken);
        TurnMeta? root = turns.FirstOrDefault(x => x.Id == rootTurnId);
        if (root == null)
        {
            return null;
        }

        HashSet<long> subtreeIds = CollectSubtreeIds(turns, rootTurnId);
        long leafId = FindLatestDeepestLeaf(turns, subtreeIds);
        List<List<TurnMeta>> allLevels = BuildVisibleLevels(turns, leafId);
        int rootLevel = allLevels.FindIndex(level => level.Any(x => x.Id == rootTurnId));
        if (rootLevel < 0)
        {
            return null;
        }

        List<List<TurnMeta>> subtreeLevels = allLevels.GetRange(rootLevel, allLevels.Count - rootLevel);
        // The target sibling is the only full turn returned at the requested level.
        // Its sibling IDs are still included as metadata, so the client can render
        // pagination controls without loading the other siblings' contents.
        subtreeLevels[0] = [.. subtreeLevels[0].Where(x => x.Id == rootTurnId)];
        return await BuildResult(subtreeLevels, leafId, turns, cancellationToken);
    }

    private async Task<TurnMeta[]> LoadMetadata(int chatId, DateTime? createdBeforeOrAt, CancellationToken cancellationToken)
    {
        IQueryable<ChatTurn> query = db.ChatTurns
            .AsNoTracking()
            .Where(x => x.ChatId == chatId && x.Steps.Any());
        if (createdBeforeOrAt.HasValue)
        {
            query = query.Where(x => x.Steps.Min(s => s.CreatedAt) <= createdBeforeOrAt.Value);
        }

        // Keep the relational query limited to scalar members. EF providers can
        // translate the correlated MIN and ordering here, but cannot translate
        // ordering by a custom record constructor that contains that subquery.
        var rows = await query
            .Select(x => new
            {
                x.Id,
                x.ParentId,
                x.IsUser,
                x.SpanId,
                CreatedAt = x.Steps.Min(s => s.CreatedAt),
            })
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .ToArrayAsync(cancellationToken);

        return [.. rows.Select(x => new TurnMeta(
            x.Id,
            x.ParentId,
            x.IsUser,
            x.SpanId,
            x.CreatedAt))];
    }

    private async Task<ChatMessageViewDto> BuildResult(
        IReadOnlyList<List<TurnMeta>> levels,
        long leafId,
        IReadOnlyList<TurnMeta> allTurns,
        CancellationToken cancellationToken)
    {
        long[] visibleIds = [.. levels.SelectMany(x => x).Select(x => x.Id).Distinct()];
        Dictionary<long, long[]> siblingIds = visibleIds.ToDictionary(
            id => id,
            id => GetSiblingIds(allTurns, allTurns.First(x => x.Id == id)));

        ChatTurn[] entities = await FullTurnQuery()
            .AsNoTracking()
            .AsSplitQuery()
            .Where(x => visibleIds.Contains(x.Id))
            .ToArrayAsync(cancellationToken);
        Dictionary<long, ChatTurn> entityMap = entities.ToDictionary(x => x.Id);

        TurnDto[] messages = [.. levels
            .SelectMany(level => level.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id))
            .Where(x => entityMap.ContainsKey(x.Id))
            .Select(x => ChatMessageTemp.FromDB(entityMap[x.Id]).ToDto(urlEncryption, fileUrlProvider, siblingIds[x.Id]))];

        return new ChatMessageViewDto
        {
            Messages = messages,
            LeafMessageId = urlEncryption.EncryptTurnId(leafId),
        };
    }

    private IQueryable<ChatTurn> FullTurnQuery() => db.ChatTurns
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentBlob)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileService)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentFile).ThenInclude(x => x!.File).ThenInclude(x => x.FileImageInfo)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentText)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentThink)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCall)
        .Include(x => x.Steps).ThenInclude(x => x.StepContents).ThenInclude(x => x.StepContentToolCallResponse)
        .Include(x => x.Steps).ThenInclude(x => x.Usage!).ThenInclude(x => x.ModelSnapshot).ThenInclude(x => x.ModelKeySnapshot)
        .Include(x => x.ChatConfigSnapshot!).ThenInclude(x => x.ModelSnapshot).ThenInclude(x => x.ModelKeySnapshot);

    internal static List<List<TurnMeta>> BuildVisibleLevels(IReadOnlyList<TurnMeta> turns, long leafId)
    {
        Dictionary<long, TurnMeta> turnMap = turns.ToDictionary(x => x.Id);
        List<List<TurnMeta>> levels = [];
        TurnMeta? current = turnMap.GetValueOrDefault(leafId);
        TurnMeta? previousUser = null;

        while (current != null)
        {
            long? parentId = current.ParentId;
            if (current.IsUser)
            {
                levels.Insert(0, [current]);
                previousUser = current;
            }
            else
            {
                TurnMeta[] assistantSiblings = [.. turns.Where(x => !x.IsUser && x.ParentId == parentId)];
                List<TurnMeta> group = [];
                foreach (IGrouping<byte?, TurnMeta> spanGroup in assistantSiblings.GroupBy(x => x.SpanId))
                {
                    TurnMeta? selected = spanGroup.FirstOrDefault(x => x.Id == current.Id)
                        ?? (previousUser == null ? null : spanGroup.FirstOrDefault(x => previousUser.ParentId == x.Id))
                        ?? spanGroup.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Last();
                    group.Add(selected);
                }
                levels.Insert(0, [.. group.OrderBy(x => x.SpanId).ThenBy(x => x.CreatedAt).ThenBy(x => x.Id)]);
            }

            current = parentId.HasValue ? turnMap.GetValueOrDefault(parentId.Value) : null;
        }

        return levels;
    }

    internal static HashSet<long> CollectSubtreeIds(IReadOnlyList<TurnMeta> turns, long rootId)
    {
        Dictionary<long, List<long>> children = turns
            .Where(x => x.ParentId.HasValue)
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Id).ToList());
        HashSet<long> result = [rootId];
        Queue<long> queue = new([rootId]);
        while (queue.TryDequeue(out long id))
        {
            if (!children.TryGetValue(id, out List<long>? childIds)) continue;
            foreach (long childId in childIds)
            {
                if (result.Add(childId)) queue.Enqueue(childId);
            }
        }
        return result;
    }

    internal static long FindLatestDeepestLeaf(IReadOnlyList<TurnMeta> turns, HashSet<long> allowedIds)
    {
        Dictionary<long, List<TurnMeta>> children = turns
            .Where(x => x.ParentId.HasValue && allowedIds.Contains(x.Id) && allowedIds.Contains(x.ParentId.Value))
            .GroupBy(x => x.ParentId!.Value)
            .ToDictionary(x => x.Key, x => x.ToList());
        TurnMeta[] roots = [.. turns.Where(x => allowedIds.Contains(x.Id) && (!x.ParentId.HasValue || !allowedIds.Contains(x.ParentId.Value)))];

        (TurnMeta Turn, int Depth) best = (roots.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Last(), 0);
        Queue<(TurnMeta Turn, int Depth)> queue = new(roots.Select(x => (x, 0)));
        while (queue.TryDequeue(out (TurnMeta Turn, int Depth) item))
        {
            if (!children.TryGetValue(item.Turn.Id, out List<TurnMeta>? next) || next.Count == 0)
            {
                if (item.Depth > best.Depth || item.Depth == best.Depth &&
                    (item.Turn.CreatedAt > best.Turn.CreatedAt || item.Turn.CreatedAt == best.Turn.CreatedAt && item.Turn.Id > best.Turn.Id))
                {
                    best = item;
                }
                continue;
            }
            foreach (TurnMeta child in next) queue.Enqueue((child, item.Depth + 1));
        }
        return best.Turn.Id;
    }

    internal static long[] GetSiblingIds(IReadOnlyList<TurnMeta> turns, TurnMeta turn) =>
        [.. turns
            .Where(x => x.ParentId == turn.ParentId && x.IsUser == turn.IsUser && (turn.IsUser || x.SpanId == turn.SpanId))
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Id)];
}
