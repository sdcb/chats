using System.ClientModel.Primitives;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.PipelinePolicies;

public class AddHeaderPolicy(string headerName, string headerValue) : PipelinePolicy
{
    public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        // 添加自定义请求头
        message.Request.Headers.Add(headerName, headerValue);

        // 继续处理下一个 Policy
        ProcessNext(message, pipeline, currentIndex);
    }

    public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
    {
        // 添加自定义请求头
        message.Request.Headers.Add(headerName, headerValue);

        // 继续处理下一个 Policy
        await ProcessNextAsync(message, pipeline, currentIndex);
    }
}