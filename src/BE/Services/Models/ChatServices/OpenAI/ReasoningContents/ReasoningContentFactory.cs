using OpenAI.Chat;
using System.Collections;
using System.Reflection;

namespace Chats.BE.Services.Models.ChatServices.OpenAI.ReasoningContents;

public class ReasoningContentFactory
{
    /// <summary>
    /// 创建一个从 StreamingChatCompletionUpdate 对象中提取 reasoning_content 的委托，
    /// 如果未找到或无法解析，则返回 null。
    /// </summary>
    /// <returns>Func<StreamingChatCompletionUpdate, string?></returns>
    public static Func<StreamingChatCompletionUpdate, string?> CreateStreamingReasoningContentAccessor(string reasoningContentProp = "reasoning_content")
    {
        // 1. 获取 StreamingChatCompletionUpdate 类型
        Type streamingChatType = typeof(StreamingChatCompletionUpdate);

        // 2. 获取 internal 属性 "Choices"
        //    类型：IReadOnlyList<InternalCreateChatCompletionStreamResponseChoice>
        PropertyInfo? choicesProp = streamingChatType.GetProperty(
            "Choices",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        ) ?? throw new InvalidOperationException("Unable to reflect property 'Choices' in StreamingChatCompletionUpdate.");

        // 3. 拿到 Choices 的泛型参数 T = InternalCreateChatCompletionStreamResponseChoice
        Type? choicesPropType = choicesProp.PropertyType ?? throw new InvalidOperationException("Unable to determine the property type of 'Choices'."); // IReadOnlyList<T>

        if (!choicesPropType.IsGenericType || choicesPropType.GetGenericArguments().Length != 1)
        {
            throw new InvalidOperationException("Property 'Choices' is not the expected generic type IReadOnlyList<T>.");
        }

        // 取得 T
        Type choiceType = choicesPropType.GetGenericArguments()[0];

        // 4. 从 choiceType 中获取 internal 属性 "Delta"
        PropertyInfo? deltaProp = choiceType.GetProperty(
            "Delta",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        ) ?? throw new InvalidOperationException("Unable to reflect property 'Delta' in choice type.");

        // 5. 获取 Delta 对象的类型，然后从中获取 "SerializedAdditionalRawData"
        Type deltaType = deltaProp.PropertyType;
        PropertyInfo? rawDataProp = deltaType.GetProperty(
            "SerializedAdditionalRawData",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        ) ?? throw new InvalidOperationException("Unable to reflect property 'SerializedAdditionalRawData' in delta type.");

        // ---
        // 创建并返回委托，在委托中使用上述缓存的 PropertyInfo
        // ---
        return streamingChatObj =>
        {
            if (streamingChatObj == null)
            {
                return null;
            }

            // 拿到 choices 数据
            object? choicesObj = choicesProp.GetValue(streamingChatObj);
            if (choicesObj is not IEnumerable choicesEnumerable)
            {
                return null;
            }

            foreach (object? choice in choicesEnumerable)
            {
                if (choice == null) continue;

                // 获取 Delta 对象
                object? deltaObj = deltaProp.GetValue(choice);
                if (deltaObj == null) continue;

                // 获取字典 SerializedAdditionalRawData
                object? rawDataValue = rawDataProp.GetValue(deltaObj);
                if (rawDataValue is not IDictionary<string, BinaryData> dict) continue;

                // 从字典里查找 "reasoning_content"
                if (dict.TryGetValue(reasoningContentProp, out BinaryData? binaryData))
                {
                    return binaryData.ToObjectFromJson<string>();
                }
            }

            // 如果所有 Choice 中都没有找到则返回 null
            return null;
        };
    }

    /// <summary>
    /// Creates a function that, given a <see cref="ChatCompletion"/>, tries to retrieve the "reasoning_content"
    /// from its internal structure via reflection. Returns null if not found.
    /// </summary>
    /// <returns>A delegate that takes a <see cref="ChatCompletion"/> and returns a string or null.</returns>
    public static Func<ChatCompletion, string?> CreateReasoningContentAccessor(string reasoningContentProp = "reasoning_content")
    {
        // 1. Get ChatCompletion type
        Type chatCompletionType = typeof(ChatCompletion);

        // 2. Get the internal property "Choices"
        //    This is of type: IReadOnlyList<InternalCreateChatCompletionResponseChoice>
        PropertyInfo? choicesProp = chatCompletionType.GetProperty(
            "Choices",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );
        if (choicesProp == null)
        {
            throw new InvalidOperationException("Unable to reflect property 'Choices' in ChatCompletion.");
        }

        // 3. Retrieve the property type, which should be IReadOnlyList<T>
        Type choicesPropType = choicesProp.PropertyType;
        if (!choicesPropType.IsGenericType || choicesPropType.GetGenericArguments().Length != 1)
        {
            throw new InvalidOperationException("Property 'Choices' is not the expected generic type IReadOnlyList<T>.");
        }

        // T = InternalCreateChatCompletionResponseChoice
        Type choiceType = choicesPropType.GetGenericArguments()[0];

        // 4. From choiceType, get the internal property "Message"
        PropertyInfo? messageProp = choiceType.GetProperty(
            "Message",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        ) ?? throw new InvalidOperationException("Unable to reflect property 'Message' in choice type.");

        // 5. From the message type, get "SerializedAdditionalRawData"
        Type messageType = messageProp.PropertyType;
        PropertyInfo? rawDataProp = messageType.GetProperty(
            "SerializedAdditionalRawData",
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        ) ?? throw new InvalidOperationException("Unable to reflect property 'SerializedAdditionalRawData' in message type.");

        // ---
        // Create and return the delegate
        // ---
        return chatCompletionObj =>
        {
            if (chatCompletionObj == null)
            {
                return null;
            }

            // Get choices
            object? choicesObj = choicesProp.GetValue(chatCompletionObj);
            if (choicesObj is not IEnumerable choicesEnumerable)
            {
                return null;
            }

            // Iterate over choices
            foreach (object? choice in choicesEnumerable)
            {
                if (choice == null) continue;

                // Get Message object
                object? messageObj = messageProp.GetValue(choice);
                if (messageObj == null) continue;

                // Get SerializedAdditionalRawData
                object? rawDataValue = rawDataProp.GetValue(messageObj);
                if (rawDataValue is not IDictionary<string, BinaryData> dict) continue;

                // Check if "reasoning_content" exists
                if (dict.TryGetValue(reasoningContentProp, out BinaryData? binaryData))
                {
                    return binaryData.ToObjectFromJson<string>();
                }
            }

            // If not found in any choice
            return null;
        };
    }
}