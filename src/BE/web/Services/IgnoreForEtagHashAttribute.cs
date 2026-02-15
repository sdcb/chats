namespace Chats.BE.Services;

[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreForEtagHashAttribute : Attribute
{
}