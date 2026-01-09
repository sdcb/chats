using Chats.BE.Services.CodeInterpreter;
using Chats.DB;
using Chats.DockerInterface.Models;

namespace Chats.BE.DB.Extensions;

public static class ChatDockerSessionExtensions
{
    extension(ChatDockerSession dbSession)
    {
        public string AIReableDockerInfo
        {
            get
            {
                string basicInfo = $"sessionId: {dbSession.Label}, image: {dbSession.Image}\nshell: [{dbSession.ShellPrefix.Replace(',', ' ')}]";
                string resourceLimits = $"cpu cores={dbSession.CpuCores}, memory={HumanizeMemoryLimits(dbSession.MemoryBytes)}, max processes={dbSession.MaxProcesses}, network={(NetworkMode)dbSession.NetworkMode}";
                if (!string.IsNullOrWhiteSpace(dbSession.Ip)) resourceLimits += $", ip={dbSession.Ip}";
                return basicInfo + ", " + resourceLimits;

                static string HumanizeMemoryLimits(long? memoryLimits)
                {
                    if (memoryLimits == null) return "(unlimited)";
                    return CodeInterpreterExecutor.HumanizeFileSize(memoryLimits.Value);
                }
            }
        }
    }
}
