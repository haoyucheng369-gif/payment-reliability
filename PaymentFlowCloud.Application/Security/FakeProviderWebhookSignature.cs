using System.Security.Cryptography;
using System.Text;

namespace PaymentFlowCloud.Application.Security;

public static class FakeProviderWebhookSignature
{
    public const string SignatureHeaderName = "X-Provider-Signature";
    public const string TimestampHeaderName = "X-Provider-Timestamp";

    public static string Create(string secret, long unixTimestampSeconds, string rawBody)
    {
        var payload = $"{unixTimestampSeconds}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hashBytes = HMACSHA256.HashData(keyBytes, payloadBytes);

        return $"sha256={Convert.ToHexString(hashBytes).ToLowerInvariant()}";
    }

    public static bool Verify(string secret, long unixTimestampSeconds, string rawBody, string receivedSignature)
    {
        var expectedSignature = Create(secret, unixTimestampSeconds, rawBody);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedSignature);
        var receivedBytes = Encoding.UTF8.GetBytes(receivedSignature);

        return expectedBytes.Length == receivedBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }
}
