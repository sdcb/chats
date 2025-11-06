using OpenAI.Chat;
using DBFile = Chats.BE.DB.File;

namespace Chats.BE.Services.Models.ChatServices;

public class StepContentFilePart(DBFile file) : ChatMessageContentPart()
{
    public DBFile File { get; } = file;
}