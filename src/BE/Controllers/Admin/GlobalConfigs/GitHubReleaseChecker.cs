using System.Net.Http.Headers;
using System.Text.Json;

namespace Chats.BE.Controllers.Admin.GlobalConfigs;

public class GitHubReleaseChecker
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repo;

    public static GitHubReleaseChecker SdcbChats => new("sdcb", "chats");

    public GitHubReleaseChecker(string owner, string repo)
    {
        _owner = owner;
        _repo = repo;

        _httpClient = new HttpClient { BaseAddress = new Uri("https://api.github.com") };
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SdcbChatsVersionChecker", "1.0"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
    }

    public async Task<string> GetLatestReleaseTagNameAsync(CancellationToken cancellationToken)
    {
        HttpResponseMessage response = await _httpClient.GetAsync($"/repos/{_owner}/{_repo}/releases/latest", cancellationToken);
        response.EnsureSuccessStatusCode(); // 如果请求失败，抛出异常

        using Stream responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using JsonDocument jsonDocument = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        return jsonDocument.RootElement.GetProperty("tag_name").GetString()!;
    }
}