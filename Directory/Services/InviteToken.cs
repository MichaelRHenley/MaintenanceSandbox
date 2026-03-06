namespace MaintenanceSandbox.Directory.Services;

using System.Security.Cryptography;
using System.Text;

public static class InviteToken
{
    public static string GenerateToken(int bytes = 32)
    {
        var data = RandomNumberGenerator.GetBytes(bytes);
        return Base64UrlEncode(data);
    }

    public static string HashToken(string token)
    {
        token ??= "";
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hash);
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input).Replace("+", "-").Replace("/", "_").TrimEnd('=');
}
