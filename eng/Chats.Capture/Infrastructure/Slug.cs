using System.Text;

namespace Chats.Capture.Infrastructure;

public static class Slug
{
  public static string Create(string value)
  {
    if (string.IsNullOrWhiteSpace(value))
    {
      return "unnamed";
    }

    StringBuilder builder = new(value.Length);
    bool lastWasDash = false;

    foreach (char ch in value.Trim().ToLowerInvariant())
    {
      if (char.IsLetterOrDigit(ch))
      {
        builder.Append(ch);
        lastWasDash = false;
        continue;
      }

      if (lastWasDash)
      {
        continue;
      }

      builder.Append('-');
      lastWasDash = true;
    }

    string result = builder.ToString().Trim('-');
    return result.Length == 0 ? "unnamed" : result;
  }
}