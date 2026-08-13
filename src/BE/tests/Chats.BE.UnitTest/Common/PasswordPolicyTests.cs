using Chats.BE.Services.Common;

namespace Chats.BE.UnitTest.Common;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("Abcdef1!", true)]
    [InlineData("abcdefgh1!", true)]
    [InlineData("ABCDEFGH1!", true)]
    [InlineData("Abcdefgh", false)]
    [InlineData("Ab1!", false)]
    [InlineData("12345678", false)]
    public void IsStrongEnough_ValidatesLengthAndCharacterTypes(string password, bool expected)
    {
        Assert.Equal(expected, PasswordPolicy.IsStrongEnough(password));
    }
}
