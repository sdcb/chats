namespace Chats.DB.Enums;

public enum DBTransactionType : byte
{
    AdminCharge = 1,
    WebChatCost = 2,
    Initial = 3,
    ApiCost = 4,
    ValidationCost = 5,
}
