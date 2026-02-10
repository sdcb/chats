namespace Chats.BE.Services.OAuth;

public class OAuthProtocolException(string error, string description, int statusCode = 400) : Exception(description)
{
    public string Error { get; } = error;

    public string Description { get; } = description;

    public int StatusCode { get; } = statusCode;
}
