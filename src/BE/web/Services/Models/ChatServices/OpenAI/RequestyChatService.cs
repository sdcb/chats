using Chats.DB;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

public class RequestyChatService(IHttpClientFactory httpClientFactory, HostUrlService hostUrlService) : ChatCompletionService(httpClientFactory)
{
    protected override void AddAuthorizationHeader(HttpRequestMessage request, ModelKeySnapshot modelKey)
    {
        base.AddAuthorizationHeader(request, modelKey);
        request.Headers.Add("X-Title", "Sdcb Chats");
        request.Headers.Add("HTTP-Referer", hostUrlService.GetFEUrl());
    }
}
