using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.ComponentModel.DataAnnotations;
using Chats.BE.Infrastructure.Functional;
using Chats.BE.Controllers.Chats.Chats.Dtos;

namespace Chats.BE.Services.CodeInterpreter;

[AttributeUsage(AttributeTargets.Method)]
internal sealed class ToolFunctionAttribute(string description)
    : Attribute
{
    public string Description { get; } = description;
}

[AttributeUsage(AttributeTargets.Parameter)]
internal sealed class ToolParamAttribute(string description)
    : Attribute
{
    public string Description { get; } = description;
}

internal sealed class AttributedToolRegistry
{
    private static readonly JsonSerializerOptions ToolJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal sealed record ToolDescriptor(
        string ToolName,
        string Description,
        MethodInfo Method,
        IReadOnlyList<ParameterInfo> ToolParameters,
        string SchemaJson);

    private readonly Dictionary<string, ToolDescriptor> _byName;

    public IReadOnlyList<string> ToolNames { get; }
    public IReadOnlyList<ToolDescriptor> Tools { get; }

    public AttributedToolRegistry(Type hostType)
    {
        List<ToolDescriptor> tools = Discover(hostType);
        Tools = tools;
        ToolNames = tools.Select(x => x.ToolName).ToArray();
        _byName = tools.ToDictionary(x => x.ToolName, StringComparer.Ordinal);
    }

    public bool Contains(string toolName) => _byName.ContainsKey(toolName);

    public string GetSchema(string toolName) => _byName[toolName].SchemaJson;

    public string GetDescription(string toolName) => _byName[toolName].Description;

    internal abstract record ToolInvokeResult;
    internal sealed record ToolInvokeTask(Task<Result<string>> Task) : ToolInvokeResult;
    internal sealed record ToolInvokeStream(IAsyncEnumerable<ToolProgressDelta> Stream) : ToolInvokeResult;

    public ToolInvokeResult Invoke(object hostInstance, CodeInterpreterExecutor.TurnContext ctx, string toolName, string rawJsonArgs, CancellationToken cancellationToken)
    {
        if (!_byName.TryGetValue(toolName, out ToolDescriptor? desc))
        {
            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>($"Unknown code interpreter tool: {toolName}")));
        }

        JsonObject argsObj;
        try
        {
            if (string.IsNullOrWhiteSpace(rawJsonArgs))
            {
                argsObj = new JsonObject();
            }
            else
            {
                JsonNode? node = JsonNode.Parse(rawJsonArgs);
                if (node is not JsonObject o)
                {
                    return new ToolInvokeTask(Task.FromResult(Result.Fail<string>("Tool args must be a JSON object")));
                }
                argsObj = o;
            }
        }
        catch (Exception ex)
        {
            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>($"Invalid JSON args: {ex.Message}")));
        }

        Result<object?[]> bound = BindArguments(desc.ToolParameters, argsObj);
        if (bound.IsFailure)
        {
            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>(bound.Error!)));
        }

        object?[] invokeArgs = new object?[bound.Value.Length + 2];
        invokeArgs[0] = ctx;
        for (int i = 0; i < bound.Value.Length; i++)
        {
            invokeArgs[i + 1] = bound.Value[i];
        }
        invokeArgs[bound.Value.Length + 1] = cancellationToken;

        try
        {
            object? result = desc.Method.Invoke(hostInstance, invokeArgs);
            if (result is Task<Result<string>> task)
            {
                return new ToolInvokeTask(task);
            }

            if (result is IAsyncEnumerable<ToolProgressDelta> stream)
            {
                return new ToolInvokeStream(stream);
            }

            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>(
                $"Tool method '{desc.Method.Name}' must return Task<Result<string>> or IAsyncEnumerable<ToolProgressDelta>")));
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>(tie.InnerException.Message)));
        }
        catch (Exception ex)
        {
            return new ToolInvokeTask(Task.FromResult(Result.Fail<string>(ex.Message)));
        }
    }

    private static Result<object?[]> BindArguments(IReadOnlyList<ParameterInfo> toolParams, JsonObject args)
    {
        object?[] values = new object?[toolParams.Count];

        for (int i = 0; i < toolParams.Count; i++)
        {
            ParameterInfo p = toolParams[i];
            string name = p.Name ?? throw new InvalidOperationException("Tool parameter missing name");

            bool isNullable = IsNullable(p);
            bool hasDefault = p.HasDefaultValue;
            bool hasRequiredAttr = p.GetCustomAttribute<RequiredAttribute>() != null;
            bool isRequired = hasRequiredAttr || (!isNullable && !hasDefault);

            JsonNode? node = args.TryGetPropertyValue(name, out JsonNode? v) ? v : null;

            if (node == null || node.GetValueKind() == JsonValueKind.Null)
            {
                if (isRequired)
                {
                    return Result.Fail<object?[]>($"Missing required parameter: {name}");
                }

                values[i] = hasDefault ? p.DefaultValue : null;
                continue;
            }

            Type targetType = p.ParameterType;
            try
            {
                object? converted = ConvertJsonNode(node, targetType);

                if (converted == null)
                {
                    if (isRequired)
                    {
                        return Result.Fail<object?[]>($"Parameter '{name}' cannot be null");
                    }
                    values[i] = null;
                    continue;
                }

                // Attributes validation
                if (hasRequiredAttr)
                {
                    if (converted is string s && string.IsNullOrWhiteSpace(s))
                    {
                        return Result.Fail<object?[]>($"Parameter '{name}' cannot be empty");
                    }
                }

                if (p.GetCustomAttribute<MinLengthAttribute>() is MinLengthAttribute mla)
                {
                    if (converted is string s && s.Length < mla.Length)
                    {
                        return Result.Fail<object?[]>($"Parameter '{name}' must have at least {mla.Length} characters");
                    }

                    if (converted is Array a && a.Length < mla.Length)
                    {
                        return Result.Fail<object?[]>($"Parameter '{name}' must have at least {mla.Length} items");
                    }
                }

                if (p.GetCustomAttribute<EnumDataTypeAttribute>() is EnumDataTypeAttribute eda)
                {
                    Type enumType = eda.EnumType;
                    if (!enumType.IsEnum)
                    {
                        return Result.Fail<object?[]>($"Parameter '{name}' EnumDataType must reference an enum type");
                    }

                    if (converted is string sv)
                    {
                        string[] names = Enum.GetNames(enumType);
                        if (!names.Any(n => string.Equals(n, sv, StringComparison.OrdinalIgnoreCase)))
                        {
                            string allowed = string.Join(", ", names.Select(n => n.ToLowerInvariant()));
                            return Result.Fail<object?[]>($"Parameter '{name}' must be one of: {allowed}");
                        }
                    }
                    else if (converted != null && converted.GetType().IsEnum)
                    {
                        if (!Enum.IsDefined(enumType, converted))
                        {
                            string allowed = string.Join(", ", Enum.GetNames(enumType).Select(n => n.ToLowerInvariant()));
                            return Result.Fail<object?[]>($"Parameter '{name}' must be one of: {allowed}");
                        }
                    }
                }

                values[i] = converted;
            }
            catch (Exception ex)
            {
                return Result.Fail<object?[]>($"Invalid parameter '{name}': {ex.Message}");
            }
        }

        return Result.Ok(values);
    }

    private static object? ConvertJsonNode(JsonNode node, Type targetType)
    {
        if (targetType == typeof(JsonObject) && node is JsonObject jo)
        {
            return jo;
        }

        Type? underlying = Nullable.GetUnderlyingType(targetType);
        if (underlying != null)
        {
            targetType = underlying;
        }

        string json = node.ToJsonString();
        return JsonSerializer.Deserialize(json, targetType, ToolJsonOptions);
    }

    private static bool IsNullable(ParameterInfo p)
    {
        if (!p.ParameterType.IsValueType)
        {
            // reference type: use nullability context if available; default to nullable when default is null
            if (p.HasDefaultValue && p.DefaultValue == null) return true;

            NullabilityInfoContext nic = new();
            NullabilityInfo ni = nic.Create(p);
            return ni.ReadState == NullabilityState.Nullable;
        }

        return Nullable.GetUnderlyingType(p.ParameterType) != null;
    }

    private static List<ToolDescriptor> Discover(Type hostType)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        List<ToolDescriptor> list = [];

        foreach (MethodInfo m in hostType.GetMethods(flags))
        {
            ToolFunctionAttribute? fn = m.GetCustomAttribute<ToolFunctionAttribute>();
            if (fn == null) continue;

            bool isCompat = typeof(Task<Result<string>>).IsAssignableFrom(m.ReturnType);
            bool isStream = m.ReturnType == typeof(IAsyncEnumerable<ToolProgressDelta>);
            if (!isCompat && !isStream)
            {
                throw new InvalidOperationException(
                    $"Tool method {hostType.Name}.{m.Name} must return Task<Result<string>> or IAsyncEnumerable<ToolProgressDelta>");
            }

            ParameterInfo[] ps = m.GetParameters();
            if (ps.Length < 2)
            {
                throw new InvalidOperationException($"Tool method {hostType.Name}.{m.Name} must have at least (TurnContext ctx, CancellationToken ct)");
            }

            if (ps[0].ParameterType != typeof(CodeInterpreterExecutor.TurnContext))
            {
                throw new InvalidOperationException($"Tool method {hostType.Name}.{m.Name} first parameter must be TurnContext");
            }

            if (ps[^1].ParameterType != typeof(CancellationToken))
            {
                throw new InvalidOperationException($"Tool method {hostType.Name}.{m.Name} last parameter must be CancellationToken");
            }

            List<ParameterInfo> toolParams = ps.Skip(1).SkipLast(1).ToList();
            string toolName = ToSnakeCase(m.Name);

            string schema = BuildSchemaJson(toolParams);

            list.Add(new ToolDescriptor(toolName, fn.Description, m, toolParams, schema));
        }

        list.Sort((a, b) => StringComparer.Ordinal.Compare(a.ToolName, b.ToolName));
        return list;
    }

    private static string BuildSchemaJson(IReadOnlyList<ParameterInfo> ps)
    {
        JsonObject root = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
        };

        JsonObject props = (JsonObject)root["properties"]!;
        JsonArray required = new();

        foreach (ParameterInfo p in ps)
        {
            string name = p.Name ?? throw new InvalidOperationException("Parameter missing name");
            bool hasRequiredAttr = p.GetCustomAttribute<RequiredAttribute>() != null;
            bool isNullable = (IsNullable(p) || (p.HasDefaultValue && p.DefaultValue == null)) && !hasRequiredAttr;
            bool isRequired = hasRequiredAttr || (!isNullable && !p.HasDefaultValue);

            ToolParamAttribute? pd = p.GetCustomAttribute<ToolParamAttribute>();
            EnumDataTypeAttribute? eda = p.GetCustomAttribute<EnumDataTypeAttribute>();
            MinLengthAttribute? mla = p.GetCustomAttribute<MinLengthAttribute>();

            JsonObject schema = BuildTypeSchema(p.ParameterType, isNullable);
            if (pd != null) schema["description"] = pd.Description;

            if (eda != null)
            {
                Type enumType = eda.EnumType;
                if (!enumType.IsEnum)
                {
                    throw new InvalidOperationException($"EnumDataType on parameter {name} must reference an enum type");
                }
                schema["enum"] = new JsonArray(Enum.GetNames(enumType).Select(v => (JsonNode)v.ToLowerInvariant()).ToArray());
            }

            if (mla != null)
            {
                if (p.ParameterType == typeof(string))
                {
                    schema["minLength"] = mla.Length;
                }
                else if (p.ParameterType.IsArray)
                {
                    schema["minItems"] = mla.Length;
                }
            }

            props[name] = schema;
            if (isRequired) required.Add(name);
        }

        if (required.Count > 0) root["required"] = required;

        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    private static JsonObject BuildTypeSchema(Type t, bool isNullable)
    {
        if (t.IsEnum)
        {
            JsonObject s = new()
            {
                ["type"] = "string",
                ["enum"] = new JsonArray(Enum.GetNames(t).Select(v => (JsonNode)v.ToLowerInvariant()).ToArray())
            };
            return WithNullability(s, isNullable);
        }

        if (t == typeof(string))
        {
            return WithNullability(new JsonObject { ["type"] = "string" }, isNullable);
        }
        if (t == typeof(int) || t == typeof(int?) || t == typeof(long) || t == typeof(long?) || t == typeof(short) || t == typeof(short?))
        {
            return WithNullability(new JsonObject { ["type"] = "integer" }, isNullable);
        }
        if (t == typeof(double) || t == typeof(double?) || t == typeof(float) || t == typeof(float?))
        {
            return WithNullability(new JsonObject { ["type"] = "number" }, isNullable);
        }
        if (t == typeof(bool) || t == typeof(bool?))
        {
            return WithNullability(new JsonObject { ["type"] = "boolean" }, isNullable);
        }
        if (t == typeof(string[]) || t == typeof(List<string>))
        {
            JsonObject arr = new()
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" }
            };
            return WithNullability(arr, isNullable);
        }
        if (t == typeof(JsonObject))
        {
            return WithNullability(new JsonObject { ["type"] = "object" }, isNullable);
        }

        // Treat unknown POCO/record as object with its public properties.
        JsonObject obj = new()
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
        };

        JsonObject props = (JsonObject)obj["properties"]!;
        JsonArray required = new();

        foreach (PropertyInfo p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (!p.CanRead) continue;
            string name = ToCamelCase(p.Name);

            NullabilityInfoContext nic = new();
            NullabilityInfo ni = nic.Create(p);
            bool propNullable = ni.ReadState == NullabilityState.Nullable || Nullable.GetUnderlyingType(p.PropertyType) != null;

            props[name] = BuildTypeSchema(p.PropertyType, propNullable);
            if (!propNullable) required.Add(name);
        }

        if (required.Count > 0) obj["required"] = required;
        return WithNullability(obj, isNullable);
    }

    private static JsonObject WithNullability(JsonObject schema, bool isNullable)
    {
        if (!isNullable) return schema;

        if (schema["type"] is JsonValue tv && tv.TryGetValue(out string? t))
        {
            schema["type"] = new JsonArray(t, "null");
            return schema;
        }

        return schema;
    }

    internal static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        StringBuilder sb = new();
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]))) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        if (char.IsLower(name[0])) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
