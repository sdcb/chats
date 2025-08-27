using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Chats.BE.Tests.Common;

internal class DictionaryConfiguration(Dictionary<string, string> data) : IConfiguration
{
    public string? this[string key]
    {
        get => data.TryGetValue(key, out string? value) ? value : null;
        set => data[key] = value ?? throw new ArgumentNullException(nameof(value));
    }

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        throw new NotImplementedException();
    }

    public IChangeToken GetReloadToken()
    {
        throw new NotImplementedException();
    }

    public IConfigurationSection GetSection(string key)
    {
        throw new NotImplementedException();
    }
}
