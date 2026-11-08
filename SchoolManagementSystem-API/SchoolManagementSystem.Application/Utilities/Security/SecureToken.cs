using System.Security.Cryptography;
using System.Text;

namespace SchoolManagementSystem.Application.Utilities.Security;

public static class SecureToken
{
    public static string Create() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public static string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
