using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.Services.CodeInterpreter;

public static class CodeInterpreterContextMessageBuilder
{
    public static IList<NeutralMessage> BuildMessages(
        IEnumerable<Step> historySteps,
        IEnumerable<Step> currentRoundSteps,
        bool codeExecutionEnabled,
        string? contextPrefix)
    {
        List<Step> history = [.. historySteps];
        List<Step> current = [.. currentRoundSteps];
        List<Step> allSteps = [.. history, .. current];

        if (!codeExecutionEnabled)
        {
            return allSteps.ToNeutral();
        }

        if (current.Count == 0)
        {
            return allSteps.ToNeutral();
        }

        if (string.IsNullOrWhiteSpace(contextPrefix))
        {
            return allSteps.ToNeutral();
        }

        HashSet<Step> injectTargets = [.. current];

        List<NeutralMessage> injected = new(allSteps.Count);
        foreach (Step step in allSteps)
        {
            NeutralMessage msg = step.ToNeutral();
            if (injectTargets.Contains(step) && (DBChatRole)step.ChatRoleId == DBChatRole.User)
            {
                List<NeutralContent> contents = [NeutralTextContent.Create(contextPrefix), .. msg.Contents];
                msg = msg with { Contents = contents };
            }
            injected.Add(msg);
        }

        return injected;
    }
}
