using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Chats.BE.DB.Enums;
using Chats.BE.Services.Models.Dtos;
using Chats.BE.Services.Models.Neutral;

namespace Chats.BE.Services.Models.ChatServices.OpenAI;

/// <summary>
/// Xiaomi Mimo OpenAI-compatible chat completion service.
/// Enables interleaved thinking with tool calls by sending back reasoning_content.
/// </summary>
public class MimoChatService(IHttpClientFactory httpClientFactory) : ChatCompletionService(httpClientFactory)
{
    public override async IAsyncEnumerable<ChatSegment> ChatStreamed(ChatRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        MimoInterleavedToolCallTransformer transformer = new();
        await foreach (ChatSegment segment in base.ChatStreamed(request, cancellationToken).WithCancellation(cancellationToken))
        {
            foreach (ChatSegment transformed in transformer.Transform(segment))
            {
                yield return transformed;
            }
        }
    }

    protected override bool TryBuildThinkingContentForRequest(
        NeutralMessage message,
        IReadOnlyList<NeutralThinkContent> thinkingContents,
        IReadOnlyList<NeutralToolCallContent> toolCalls,
        out string? thinkingContent)
    {
        // Mimo thinking mode tool calls require reasoning_content to be passed back.
        // Only attach it for assistant messages that contain tool calls.
        if (message.Role != NeutralChatRole.Assistant || toolCalls.Count == 0 || thinkingContents.Count == 0)
        {
            thinkingContent = null;
            return false;
        }

        thinkingContent = string.Join("", thinkingContents.Select(t => t.Content));
        return !string.IsNullOrEmpty(thinkingContent);
    }

    protected override JsonObject BuildRequestBody(ChatRequest request, bool stream)
    {
        JsonObject body = base.BuildRequestBody(request, stream);

        // Mimo enables thinking mode via `thinking: { type: "enabled" }` when ThinkingBudget is set.
        // Unlike other providers, Mimo doesn't support budget_tokens parameter.
        if (request.ChatConfig.ThinkingBudget.HasValue)
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "enabled"
            };
        }
        else
        {
            body["thinking"] = new JsonObject
            {
                ["type"] = "disabled"
            };
        }

        return body;
    }

    private sealed class MimoInterleavedToolCallTransformer
    {
        private bool _isToolCallMode = false;
        private bool _hasStarted = false;
        private readonly StringBuilder _buffer = new();
        private string? _currentFunctionName;
        private string? _currentParameterName;
        private string? _currentToolCallId;
        private bool _isFirstParameter = true;
        private bool _toolCallFinished = false;
        private int _toolCallIndex = 0;

        public IEnumerable<ChatSegment> Transform(ChatSegment segment)
        {
            if (segment is not ThinkChatSegment think)
            {
                if (segment is FinishReasonChatSegment fr && _toolCallFinished)
                {
                    yield return new FinishReasonChatSegment { FinishReason = DBFinishReason.ToolCalls };
                    yield break;
                }
                yield return segment;
                yield break;
            }

            string content = think.Think;
            if (!_hasStarted)
            {
                if (content.StartsWith("<tool_call>"))
                {
                    _isToolCallMode = true;
                    content = content.Substring("<tool_call>".Length);
                }
                _hasStarted = true;
            }

            if (_toolCallFinished)
            {
                throw new InvalidOperationException("Unexpected reasoning_content after tool call finished");
            }

            if (!_isToolCallMode)
            {
                yield return segment;
                yield break;
            }

            _buffer.Append(content);

            while (_buffer.Length > 0)
            {
                string fullBuffer = _buffer.ToString();
                
                // Skip leading whitespace/newlines if we are looking for a new tag
                if (_currentParameterName == null)
                {
                    string trimmed = fullBuffer.TrimStart();
                    if (trimmed.Length < fullBuffer.Length)
                    {
                        _buffer.Remove(0, fullBuffer.Length - trimmed.Length);
                        fullBuffer = trimmed;
                        if (fullBuffer.Length == 0) break;
                    }
                }

                if (_currentFunctionName == null)
                {
                    int start = fullBuffer.IndexOf("<function=");
                    if (start != -1)
                    {
                        int end = fullBuffer.IndexOf(">", start);
                        if (end != -1)
                        {
                            _currentFunctionName = fullBuffer.Substring(start + 10, end - (start + 10)).Trim();
                            _currentToolCallId = "call_" + Guid.NewGuid().ToString("n").Substring(0, 24);
                            yield return new ToolCallSegment
                            {
                                Index = _toolCallIndex,
                                Id = _currentToolCallId,
                                Name = _currentFunctionName,
                                Arguments = "{"
                            };
                            _isFirstParameter = true;
                            _buffer.Remove(0, end + 1);
                            continue;
                        }
                    }
                    break;
                }
                else if (_currentParameterName == null)
                {
                    int paramStart = fullBuffer.IndexOf("<parameter=");
                    int funcEnd = fullBuffer.IndexOf("</function>");

                    if (paramStart != -1 && (funcEnd == -1 || paramStart < funcEnd))
                    {
                        int end = fullBuffer.IndexOf(">", paramStart);
                        if (end != -1)
                        {
                            _currentParameterName = fullBuffer.Substring(paramStart + 11, end - (paramStart + 11)).Trim();
                            string prefix = _isFirstParameter ? "" : ", ";
                            _isFirstParameter = false;
                            yield return new ToolCallSegment
                            {
                                Index = _toolCallIndex,
                                Id = _currentToolCallId,
                                Arguments = $"{prefix}\"{_currentParameterName}\": "
                            };
                            _buffer.Remove(0, end + 1);
                            continue;
                        }
                    }
                    else if (funcEnd != -1)
                    {
                        yield return new ToolCallSegment
                        {
                            Index = _toolCallIndex,
                            Id = _currentToolCallId,
                            Arguments = "}"
                        };
                        _currentFunctionName = null;
                        _currentToolCallId = null;
                        _toolCallIndex++;
                        _buffer.Remove(0, funcEnd + "</function>".Length);
                        continue;
                    }
                    break;
                }
                else
                {
                    int paramEnd = fullBuffer.IndexOf("</parameter>");
                    if (paramEnd != -1)
                    {
                        string value = fullBuffer.Substring(0, paramEnd);
                        string jsonValue = long.TryParse(value, out _) || double.TryParse(value, out _) || (bool.TryParse(value, out bool b) && b.ToString().ToLower() == value.ToLower())
                            ? value.ToLower()
                            : JsonSerializer.Serialize(value);

                        yield return new ToolCallSegment
                        {
                            Index = _toolCallIndex,
                            Id = _currentToolCallId,
                            Arguments = jsonValue
                        };
                        _currentParameterName = null;
                        _buffer.Remove(0, paramEnd + "</parameter>".Length);
                        continue;
                    }
                    break;
                }
            }

            string currentBuffer = _buffer.ToString();
            if (currentBuffer.Contains("</tool_call>"))
            {
                _isToolCallMode = false;
                _toolCallFinished = true;
                int endIdx = currentBuffer.IndexOf("</tool_call>");
                string remaining = currentBuffer.Substring(endIdx + "</tool_call>".Length).Trim();
                if (!string.IsNullOrEmpty(remaining))
                {
                    throw new InvalidOperationException("Unexpected content after </tool_call>: " + remaining);
                }
                _buffer.Clear();
            }
        }
    }
}
