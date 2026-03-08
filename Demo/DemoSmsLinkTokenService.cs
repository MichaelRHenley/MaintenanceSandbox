using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace MaintenanceSandbox.Demo;

public sealed class DemoSmsLinkTokenService
{
    private readonly DemoOptions _opts;

    public DemoSmsLinkTokenService(IOptions<DemoOptions> opts)
    {
        _opts = opts.Value;
    }

    public string GenerateToken(Guid tenantId, string role)
    {
        var expiry = DateTimeOffset.UtcNow
            .AddMinutes(_opts.EmailLinkExpiryMinutes > 0 ? _opts.EmailLinkExpiryMinutes : 30)
            .ToUnixTimeSeconds();

        var payload = $"{tenantId:N}|{role}|{expiry}";
        var sig = Sign(payload);
        return $"{B64Encode(Encoding.UTF8.GetBytes(payload))}.{sig}";
    }

    public (Guid TenantId, string Role)? ValidateToken(string token)
    {
        try
        {
            var dot = token.IndexOf('.');
            if (dot < 0) return null;

            var payloadB64 = token[..dot];
            var sig = token[(dot + 1)..];

            var payload = Encoding.UTF8.GetString(B64Decode(payloadB64));

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(sig),
                    Encoding.UTF8.GetBytes(Sign(payload))))
                return null;

            var parts = payload.Split('|');
            if (parts.Length != 3) return null;
            if (!Guid.TryParse(parts[0], out var tenantId)) return null;
            if (!long.TryParse(parts[2], out var expiry)) return null;
            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiry) return null;

            return (tenantId, parts[1]);
        }
        catch
        {
            return null;
        }
    }

    private string Sign(string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(
            string.IsNullOrWhiteSpace(_opts.EmailLinkSecret) ? "dev-fallback-key" : _opts.EmailLinkSecret);
        using var hmac = new HMACSHA256(keyBytes);
        return B64Encode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static string B64Encode(byte[] data) =>
        Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');

    private static byte[] B64Decode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
