using System.Net.Http;

namespace Chats.BE.Services.RequestTracing;

public static class HttpClientTracingContext
{
    public static readonly HttpRequestOptionsKey<string> ClientNameOptionKey = new("request-trace.http-client-name");

    public const string UnspecifiedClientName = "HttpClient.Unspecified";

    public static string GetClientName(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(ClientNameOptionKey, out string? clientName) && !string.IsNullOrWhiteSpace(clientName))
        {
            return clientName;
        }

        return UnspecifiedClientName;
    }
}

public sealed class HttpClientNameStampHandler(string clientName) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Options.Set(HttpClientTracingContext.ClientNameOptionKey, clientName);
        return base.SendAsync(request, cancellationToken);
    }
}