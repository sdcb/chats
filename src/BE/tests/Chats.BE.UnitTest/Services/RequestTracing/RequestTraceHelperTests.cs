using Chats.BE.Services.Configs;
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

    [Fact]
    public void MatchRequestStageFilters_ShouldIgnoreStatusCodeOnlyRulesUntilResponseStage()
    {
        RequestTraceFilters filters = new()
        {
            Include = new RequestTraceFilterRuleSet
            {
                StatusCodes = ["2xx"]
            },
            Exclude = new RequestTraceFilterRuleSet
            {
                StatusCodes = ["5xx"]
            }
        };

        Assert.True(RequestTraceHelper.MatchRequestStageFilters(filters, "source-a", "GET", "/v1/chat/completions"));
        Assert.True(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/chat/completions", 200, 10));
        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/chat/completions", 500, 10));
    }

    [Fact]
    public void MatchRequestStageFilters_ShouldApplyIncludeAndExcludeRules()
    {
        RequestTraceFilters filters = new()
        {
            Include = new RequestTraceFilterRuleSet
            {
                SourcePatterns = ["chatservice.*"],
                UrlPatterns = ["/v1/*"],
                Methods = ["POST"]
            },
            Exclude = new RequestTraceFilterRuleSet
            {
                UrlPatterns = ["/v1/internal/*"]
            }
        };

        Assert.True(RequestTraceHelper.MatchRequestStageFilters(filters, "ChatService.Gemini", "post", "/V1/chat/completions"));
        Assert.False(RequestTraceHelper.MatchRequestStageFilters(filters, "ChatService.Gemini", "GET", "/V1/chat/completions"));
        Assert.False(RequestTraceHelper.MatchRequestStageFilters(filters, "ChatService.Gemini", "POST", "/v1/internal/health"));
        Assert.False(RequestTraceHelper.MatchRequestStageFilters(filters, "OtherService", "POST", "/v1/chat/completions"));
    }

    [Fact]
    public void MatchResponseStageFilters_ShouldRespectIncludedStatusAndDuration()
    {
        RequestTraceFilters filters = new()
        {
            Include = new RequestTraceFilterRuleSet
            {
                UrlPatterns = ["/v1/*"],
                StatusCodes = ["2xx", "304"]
            },
            MinDurationMs = 100
        };

        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", null, 120));
        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 500, 120));
        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 200, 99));
        Assert.True(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 200, 120));
        Assert.True(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 304, 120));
    }

    [Fact]
    public void MatchResponseStageFilters_ShouldLetExcludeOverrideInclude()
    {
        RequestTraceFilters filters = new()
        {
            Include = new RequestTraceFilterRuleSet
            {
                UrlPatterns = ["/v1/*"],
                Methods = ["POST"],
                StatusCodes = ["2xx"]
            },
            Exclude = new RequestTraceFilterRuleSet
            {
                UrlPatterns = ["/v1/internal/*"],
                StatusCodes = ["2xx"]
            }
        };

        Assert.True(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "POST", "/v1/chat/completions", 200, 20));
        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "POST", "/v1/internal/cache", 200, 20));
    }
}
