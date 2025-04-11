using OpenAI.Chat;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Chats.BE.Services.Models;

public class CcoWrapper(JsonObject json)
{
    public bool Stream
    {
        get => (bool?)json["stream"] ?? false;
        set => SetOrRemove("stream", value);
    }

    public string Model
    {
        get => (string)json["model"]!;
        set => SetOrRemove("model", value);
    }

    public IList<ChatMessage>? Messages
    {
        get => json["messages"]
            ?.AsArray()
            .Select(x => ModelReaderWriter.Read<ChatMessage>(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(x)))!)
            .ToArray();
        set => SetOrRemove("messages", value != null 
            ? new JsonArray(value.Select(x => JsonNode.Parse(ModelReaderWriter.Write(x).ToArray())).ToArray())
            : null);
    }

    public bool SeemsValid()
    {
        return Model != null && Messages != null;
    }

    public ChatCompletionOptions ToCleanCco()
    {
        JsonObject newOne = (JsonObject)json.DeepClone();
        newOne.Remove("stream");
        newOne.Remove("model");
        newOne.Remove("messages");
        return ModelReaderWriter.Read<ChatCompletionOptions>(BinaryData.FromBytes(JsonSerializer.SerializeToUtf8Bytes(newOne)))!;
    }

    private void SetOrRemove(string key, JsonNode? value)
    {
        if (value is null)
        {
            json.Remove(key);
        }
        else
        {
            json[key] = value;
        }
    }
}
