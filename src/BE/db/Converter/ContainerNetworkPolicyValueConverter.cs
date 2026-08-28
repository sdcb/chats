using Chats.DB.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Chats.BE.DB.Converter;

/// <summary>
/// Maps <see cref="DBContainerNetworkPolicy"/> to the lower-case strings stored in
/// the NetworkPolicy columns. The database CHECK constraints use a binary
/// collation, so the casing produced here must stay lower-case.
/// </summary>
public class ContainerNetworkPolicyValueConverter() : ValueConverter<DBContainerNetworkPolicy, string>(
    v => ToDbValue(v), v => FromDbValue(v))
{
    public static string ToDbValue(DBContainerNetworkPolicy value) => value switch
    {
        DBContainerNetworkPolicy.None => "none",
        DBContainerNetworkPolicy.Egress => "egress",
        DBContainerNetworkPolicy.Public => "public",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown container network policy."),
    };

    public static DBContainerNetworkPolicy FromDbValue(string value) => value switch
    {
        "none" => DBContainerNetworkPolicy.None,
        "egress" => DBContainerNetworkPolicy.Egress,
        "public" => DBContainerNetworkPolicy.Public,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown container network policy."),
    };
}
