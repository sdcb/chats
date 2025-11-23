using System.Net.Http;
using System.Threading.Tasks;

namespace Chats.BE.ApiTest;

public static class HttpResponseMessageExtensions
{
    public static async Task EnsureSuccessStatusCodeWithDetailsAsync(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). Content: {content}");
        }
    }
}
