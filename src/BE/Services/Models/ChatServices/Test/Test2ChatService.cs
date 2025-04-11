using Chats.BE.DB;
using Chats.BE.Services.Models.Dtos;
using OpenAI.Chat;
using System.Runtime.CompilerServices;
using System.Text;

namespace Chats.BE.Services.Models.ChatServices.Test;

public class Test2ChatService(Model model) : ChatService(model)
{
    string[] outputs =
    [
        $$"""
        欢迎使用Chats！🎉 我们很高兴您选择了我们的平台。在您开始探索之前，建议您首先点击左下角的用户名（admin），然后点击🔑“修改密码”按钮来确保您的账户安全。
        Welcome to Chats! 🎉 We’re thrilled that you've chosen our platform. Before you start exploring, we recommend that you click on your username (admin) in the bottom left corner and then click the 🔑 "Change Password" button to ensure your account’s security.
        """,

        $$"""
        如果您计划添加新的模型，请前往左下角的“后台管理”->“模型密钥”部分，添加相应的模型密钥。接下来，您可以在“模型配置”中使用这个密钥来添加模型。💡
        If you plan to add new models, navigate to the “Admin Panel” -> “Model Keys” section in the bottom left, and add the appropriate model key. Then, you can use this key in the "Model Configuration" to add models. 💡
        """,

        $$"""
        我们致力于不断改善您的体验，欢迎您在我们的GitHub页面：https://github.com/sdcb/chats 上提出宝贵的建议。您也可以加入我们的QQ群：498452653，与其他用户交流，获取帮助。感谢您的支持与信赖！🙏
        We are committed to continuously improving your experience. Feel free to provide valuable feedback on our GitHub page: https://github.com/sdcb/chats. You can also join our QQ group: 498452653 to interact with other users and get help. Thank you for your support and trust! 🙏
        """
    ];
    string[] urls =
    [
        "https://io.starworks.cc:88/cv-public/2025/welcome1.jpg",
        "https://io.starworks.cc:88/cv-public/2025/welcome2.jpg",
        "https://io.starworks.cc:88/cv-public/2025/welcome3.jpg",
    ];

    public override IAsyncEnumerable<ChatSegment> ChatStreamed(
        IReadOnlyList<ChatMessage> messages, 
        ChatCompletionOptions options, 
        CancellationToken cancellationToken)
    {
        string messageText = messages[messages.Count - 1].Content[0].Text;
        if (messageText.Contains('1'))
        {
            return UrlOnly(messages, options, cancellationToken);
        }
        else if (messageText.Contains('2'))
        {
            return TextAndUrl(messages, options, cancellationToken);
        }
        else
        {
            return TextOnly(messages, options, cancellationToken);
        }
    }

    async IAsyncEnumerable<ChatSegment> UrlOnly(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int inputTokens = GetPromptTokenCount(messages);

        StringBuilder outputed = new();
        for (int i = 0; i < outputs.Length; i++)
        {
            string url = urls[i];
            yield return new ChatSegment()
            {
                Items = [ChatSegmentItem.FromUrlImage(url)],
                FinishReason = null,
                Usage = new Dtos.ChatTokenUsage()
                {
                    InputTokens = inputTokens,
                    OutputTokens = Tokenizer.CountTokens(outputed.ToString()),
                }
            };
            await Task.Delay(1, cancellationToken);
        }
    }

    async IAsyncEnumerable<ChatSegment> TextAndUrl(
        IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int inputTokens = GetPromptTokenCount(messages);

        StringBuilder outputed = new();
        for (int i = 0; i < outputs.Length; i++)
        {
            string output = outputs[i];
            string url = urls[i];
            foreach (string[] c in UnicodeCharacterSplit(output).Chunk(3))
            {
                string combined = string.Concat(c);
                outputed.Append(combined);
                int outputTokens = Tokenizer.CountTokens(outputed.ToString());
                yield return new ChatSegment()
                {
                    Items = ChatSegmentItem.FromTextAndThink(combined, null),
                    Usage = new Dtos.ChatTokenUsage()
                    {
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                    },
                    FinishReason = null,
                };
                await Task.Delay(1, cancellationToken);
            }
            await Task.Delay(1, cancellationToken);
            yield return new ChatSegment()
            {
                Items = [ChatSegmentItem.FromUrlImage(url)],
                FinishReason = null,
                Usage = new Dtos.ChatTokenUsage()
                {
                    InputTokens = inputTokens,
                    OutputTokens = Tokenizer.CountTokens(outputed.ToString()),
                }
            };
        }
    }

    async IAsyncEnumerable<ChatSegment> TextOnly(IReadOnlyList<ChatMessage> messages,
        ChatCompletionOptions options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        int inputTokens = GetPromptTokenCount(messages);

        StringBuilder outputed = new();
        for (int i = 0; i < outputs.Length; i++)
        {
            string output = outputs[i];
            foreach (string[] c in UnicodeCharacterSplit(output).Chunk(3))
            {
                string combined = string.Concat(c);
                outputed.Append(combined);
                int outputTokens = Tokenizer.CountTokens(outputed.ToString());
                yield return new ChatSegment()
                {
                    Items = ChatSegmentItem.FromTextAndThink(combined, null),
                    Usage = new Dtos.ChatTokenUsage()
                    {
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                    },
                    FinishReason = null,
                };
                await Task.Delay(1, cancellationToken);
            }
            {
                outputed.Append('\n');
                int outputTokens = Tokenizer.CountTokens(outputed.ToString());
                yield return new ChatSegment()
                {
                    Items = ChatSegmentItem.FromTextAndThink("\n", null),
                    Usage = new Dtos.ChatTokenUsage()
                    {
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                    },
                    FinishReason = null,
                };
            }
        }
    }

    internal static IEnumerable<string> UnicodeCharacterSplit(string input)
    {
        for (var i = 0; i < input.Length; ++i)
        {
            if (char.IsHighSurrogate(input[i]))
            {
                int length = 0;
                while (true)
                {
                    length += 2;
                    if (i + length < input.Length && input[i + length] == 0x200D)
                    {
                        length += 1;
                    }
                    else
                    {
                        break;
                    }
                }
                yield return input.Substring(i, length);
                i += length - 1;
            }
            else
            {
                yield return input[i].ToString();
            }
        }
    }
}
