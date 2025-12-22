namespace Chats.Web.Services.Models.Neutral;

/// <summary>
/// Neutral chat role that is independent of any third-party SDK or database model.
/// </summary>
public enum NeutralChatRole
{
    User = 0,
    Assistant = 1,
    Tool = 2,
}
