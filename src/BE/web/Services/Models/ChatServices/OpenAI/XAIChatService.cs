using System.Text.Json.Nodes;

namespace Chats.Web.Services.Models.ChatServices.OpenAI;

public class XAIChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        if (request.ChatConfig.Model.AllowSearch)
        {
            body["search_parameters"] = new JsonObject
            {
                ["mode"] = request.ChatConfig.WebSearchEnabled ? "on" : "off"
                // return_citations, from_date, to_date, max_search_results, sources is also supported but not used
                // https://docs.x.ai/docs/guides/live-search
            };
        }

        return body;
    }
}
