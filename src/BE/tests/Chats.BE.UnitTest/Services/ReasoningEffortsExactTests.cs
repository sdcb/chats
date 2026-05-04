using Chats.DB;

namespace Chats.BE.UnitTest.Services;

public class ReasoningEffortsExactTests
{
    [Fact]
    public void ThrowIfInvalid_ShouldRejectUppercase()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ReasoningEfforts.ThrowIfInvalid("Low"));

        Assert.Contains("Low", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ThrowIfInvalid_ShouldRejectPaddedWhitespace()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => ReasoningEfforts.ThrowIfInvalid(" low "));

        Assert.Contains(" low ", exception.Message, StringComparison.Ordinal);
    }
}