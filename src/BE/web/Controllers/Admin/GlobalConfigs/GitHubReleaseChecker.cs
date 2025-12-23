using System.Net.Http.Headers;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

public class GitHubReleaseChecker(IHttpClientFactory httpClientFactory)
{
    public async Task<string> GetLatestReleaseTagNameAsync(CancellationToken cancellationToken)
    {
        using HttpClient httpClient = httpClientFactory.CreateClient();
        httpClient.BaseAddress = new Uri("https://api.github.com");
        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));

        HttpResponseMessage response = await httpClient.GetAsync($"/repos/sdcb/chats/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode(); // 如果请求失败，抛出异常

        using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument jsonDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        return jsonDocument.RootElement.GetProperty("tag_name").GetString()!;
    }
}