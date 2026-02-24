using Chats.BE.Services.RequestTracing;

namespace Chats.BE.UnitTest.Services.RequestTracing;

public class RequestTraceHelperTests
{
    [Fact]
    public void ResolveRawCaptureLimit_ShouldUseDefaultWhenMissingOrInvalid()
    {
        Assert.Equal(RequestTraceHelper.DefaultRawCaptureMaxBytes, RequestTraceHelper.ResolveRawCaptureLimit(null));
        Assert.Equal(RequestTraceHelper.DefaultRawCaptureMaxBytes, RequestTraceHelper.ResolveRawCaptureLimit(0));
        Assert.Equal(RequestTraceHelper.DefaultRawCaptureMaxBytes, RequestTraceHelper.ResolveRawCaptureLimit(-1));
    }

    [Fact]
    public void ResolveRawCaptureLimit_ShouldUseConfiguredWhenPositive()
    {
        Assert.Equal(12345, RequestTraceHelper.ResolveRawCaptureLimit(12345));
    }

    [Fact]
    public void IsSmallKnownLength_ShouldRespectKnownLengthAndFloorCap()
    {
        Assert.False(RequestTraceHelper.IsSmallKnownLength(null, 1024));
        Assert.False(RequestTraceHelper.IsSmallKnownLength(-1, 1024));

        Assert.True(RequestTraceHelper.IsSmallKnownLength(200 * 1024, 100));
        Assert.False(RequestTraceHelper.IsSmallKnownLength(300 * 1024, 100));

        Assert.True(RequestTraceHelper.IsSmallKnownLength(2 * 1024 * 1024, 3 * 1024 * 1024));
        Assert.False(RequestTraceHelper.IsSmallKnownLength(4 * 1024 * 1024, 3 * 1024 * 1024));
    }
}
