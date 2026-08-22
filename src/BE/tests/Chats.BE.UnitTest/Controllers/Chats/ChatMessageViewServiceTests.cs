using Chats.BE.Controllers.Chats.Messages;
using Meta = Chats.BE.Controllers.Chats.Messages.ChatMessageViewService.TurnMeta;

namespace Chats.BE.UnitTest.Controllers.Chats;

public class ChatMessageViewServiceTests
{
    [Fact]
    public void BuildVisibleLevels_ReturnsCurrentPathAndOneAssistantPerSpan()
    {
        Meta[] turns =
        [
            M(1, null, user: true, 1),
            M(2, 1, user: false, 2, span: 0),
            M(3, 1, user: false, 3, span: 0),
            M(4, 1, user: false, 4, span: 1),
            M(5, 3, user: true, 5),
            M(6, 5, user: false, 6, span: 0),
        ];

        List<List<Meta>> levels = ChatMessageViewService.BuildVisibleLevels(turns, 6);

        Assert.Equal(4, levels.Count);
        Assert.Equal([1], levels[0].Select(x => x.Id));
        Assert.Equal([3, 4], levels[1].Select(x => x.Id));
        Assert.Equal([5], levels[2].Select(x => x.Id));
        Assert.Equal([6], levels[3].Select(x => x.Id));
    }

    [Fact]
    public void FindLatestDeepestLeaf_PrefersNewestLeafAtSameDepth()
    {
        Meta[] turns =
        [
            M(1, null, user: true, 1),
            M(2, 1, user: false, 2),
            M(3, 2, user: true, 3),
            M(4, 2, user: true, 4),
        ];

        long leaf = ChatMessageViewService.FindLatestDeepestLeaf(turns, turns.Select(x => x.Id).ToHashSet());

        Assert.Equal(4, leaf);
    }

    [Fact]
    public void SubtreeSelection_ExcludesAncestorsAndOtherSiblingSubtrees()
    {
        Meta[] turns =
        [
            M(1, null, user: true, 1),
            M(2, 1, user: false, 2),
            M(3, 1, user: false, 3),
            M(4, 2, user: true, 4),
            M(5, 3, user: true, 5),
            M(6, 3, user: true, 6),
        ];

        HashSet<long> subtree = ChatMessageViewService.CollectSubtreeIds(turns, 3);
        long leaf = ChatMessageViewService.FindLatestDeepestLeaf(turns, subtree);
        List<List<Meta>> levels = ChatMessageViewService.BuildVisibleLevels(turns, leaf);
        int rootLevel = levels.FindIndex(level => level.Any(x => x.Id == 3));
        long[] returned = [.. levels.Skip(rootLevel).SelectMany(x => x).Select(x => x.Id)];

        Assert.Equal(6, leaf);
        Assert.Equal([3, 6], returned);
        Assert.DoesNotContain(1, returned);
        Assert.DoesNotContain(2, returned);
        Assert.DoesNotContain(4, returned);
    }

    [Fact]
    public void SubtreeRootLevel_ContainsOnlyRequestedSibling()
    {
        Meta[] turns =
        [
            M(1, null, user: true, 1),
            M(2, 1, user: false, 2, span: 0),
            M(3, 1, user: false, 3, span: 0),
            M(4, 3, user: true, 4),
        ];

        HashSet<long> subtree = ChatMessageViewService.CollectSubtreeIds(turns, 3);
        long leaf = ChatMessageViewService.FindLatestDeepestLeaf(turns, subtree);
        List<List<Meta>> levels = ChatMessageViewService.BuildVisibleLevels(turns, leaf);
        int rootLevel = levels.FindIndex(level => level.Any(x => x.Id == 3));
        levels[rootLevel] = [.. levels[rootLevel].Where(x => x.Id == 3)];

        Assert.Equal([3], levels[rootLevel].Select(x => x.Id));
        Assert.Equal([2, 3], ChatMessageViewService.GetSiblingIds(turns, turns[2]));
    }

    [Fact]
    public void GetSiblingIds_GroupsAssistantBranchesBySpan()
    {
        Meta[] turns =
        [
            M(1, null, user: true, 1),
            M(2, 1, user: false, 2, span: 0),
            M(3, 1, user: false, 3, span: 0),
            M(4, 1, user: false, 4, span: 1),
        ];

        Assert.Equal([2, 3], ChatMessageViewService.GetSiblingIds(turns, turns[1]));
        Assert.Equal([4], ChatMessageViewService.GetSiblingIds(turns, turns[3]));
    }

    private static Meta M(long id, long? parentId, bool user, int minute, byte? span = null) =>
        new(id, parentId, user, span, new DateTime(2026, 1, 1, 0, minute, 0, DateTimeKind.Utc));
}
