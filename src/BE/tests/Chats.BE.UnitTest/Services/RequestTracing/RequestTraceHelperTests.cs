using Chats.BE.Services.Configs;
using Chats.BE.Services.RequestTracing;
using System.Text;
using System.Text.Json;

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
    public void MatchRequestStageFilters_ShouldIgnoreMinDurationUntilResponseStage()
    {
        RequestTraceFilters filters = new()
        {
            Include = new RequestTraceFilterRuleSet
            {
                UrlPatterns = ["/v1/*"]
            },
            MinDurationMs = 100
        };

        Assert.True(RequestTraceHelper.MatchRequestStageFilters(filters, "source-a", "GET", "/v1/models"));
        Assert.False(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 200, 99));
        Assert.True(RequestTraceHelper.MatchResponseStageFilters(filters, "source-a", "GET", "/v1/models", 200, 100));
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

    [Fact]
    public void RedactUrlQueryParameters_ShouldReturnOriginalWhenNoQueryOrRules()
    {
        Assert.Equal("/v1/chat/completions", RequestTraceHelper.RedactUrlQueryParameters("/v1/chat/completions", ["token"]));
        Assert.Equal("/v1/chat/completions?token=abc", RequestTraceHelper.RedactUrlQueryParameters("/v1/chat/completions?token=abc", []));
        Assert.Equal("/v1/chat/completions?token=abc", RequestTraceHelper.RedactUrlQueryParameters("/v1/chat/completions?token=abc", null));
        Assert.Equal("/v1/chat/completions?#fragment", RequestTraceHelper.RedactUrlQueryParameters("/v1/chat/completions?#fragment", ["token"]));
    }

    [Fact]
    public void RedactUrlQueryParameters_ShouldRedactMatchedParametersCaseInsensitively()
    {
        string url = "https://api.example.com/v1/chat?keep=1&TOKEN=abc&token=def#fragment";

        string redacted = RequestTraceHelper.RedactUrlQueryParameters(url, ["token"]);

        Assert.Equal("https://api.example.com/v1/chat?keep=1&TOKEN=***&token=***#fragment", redacted);
    }

    [Fact]
    public void RedactUrlQueryParameters_ShouldPreserveRelativeUrlStructureAndUnmatchedSegments()
    {
        string url = "/v1/chat?token=abc&&empty=&token&name=test";

        string redacted = RequestTraceHelper.RedactUrlQueryParameters(url, ["token"]);

        Assert.Equal("/v1/chat?token=***&&empty=&token&name=test", redacted);
    }

        [Fact]
        public void DecodeTextBody_ShouldRedactConfiguredJsonFieldsCaseInsensitively()
        {
                string json =
                        """
                        {
                            "access_token": "eyJhbGciOiJSU...",
                            "id_token": "id-value",
                            "nested": {
                                "Refresh_Token": "refresh-value"
                            },
                            "items": [
                                {
                                    "token": "token-value"
                                }
                            ],
                            "keep": "visible"
                        }
                        """;

                (string? text, int? originalLength) = RequestTraceHelper.DecodeTextBody(
                        Encoding.UTF8.GetBytes(json),
                        4096,
                        null,
                        null,
                        "application/json; charset=utf-8",
                    ["access_token", "refresh_token", "token", "id_token"]);

                Assert.Equal(json.Length, originalLength);
                Assert.NotNull(text);

                using JsonDocument doc = JsonDocument.Parse(text);
                Assert.Equal("***", doc.RootElement.GetProperty("access_token").GetString());
                Assert.Equal("***", doc.RootElement.GetProperty("id_token").GetString());
                Assert.Equal("***", doc.RootElement.GetProperty("nested").GetProperty("Refresh_Token").GetString());
                Assert.Equal("***", doc.RootElement.GetProperty("items")[0].GetProperty("token").GetString());
                Assert.Equal("visible", doc.RootElement.GetProperty("keep").GetString());
        }
}
