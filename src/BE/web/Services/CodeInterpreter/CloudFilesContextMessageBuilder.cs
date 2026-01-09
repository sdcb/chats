using Chats.BE.Services.Models.Neutral;
using Chats.BE.Services.Models.Neutral.Conversions;
using Chats.DB;
using Chats.DB.Enums;

namespace Chats.BE.Services.CodeInterpreter;

public static class CloudFilesContextMessageBuilder
{
    public static IList<NeutralMessage> BuildMessages(
        IEnumerable<Step> historySteps,
        IEnumerable<Step> currentRoundSteps,
        bool codeExecutionEnabled,
        Func<IEnumerable<Step>, string?> buildCloudFilesContextPrefix)
    {
        List<Step> current = [.. currentRoundSteps];
        List<Step> allSteps = [.. historySteps, .. current];

        if (!codeExecutionEnabled)
        {
            return allSteps.ToNeutral();
        }

        if (current.Count == 0)
        {
            // No "current round" user prompt to inject into.
            return allSteps.ToNeutral();
        }

        string? prefix = buildCloudFilesContextPrefix(allSteps);
        if (string.IsNullOrWhiteSpace(prefix))
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
                List<NeutralContent> contents = [NeutralTextContent.Create(prefix), .. msg.Contents];
                msg = msg with { Contents = contents };
            }
            injected.Add(msg);
        }

        return injected;
    }
}
