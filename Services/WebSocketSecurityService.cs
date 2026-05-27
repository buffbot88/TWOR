using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace WorldOfRa.Server.Services;

public sealed class WebSocketSecurityService
{
    private readonly WorldSocketOptions _options;

    public WebSocketSecurityService(IOptions<WorldSocketOptions> options)
    {
        _options = options.Value;
    }

    public bool IsAuthorized(string? suppliedToken)
    {
        if (string.IsNullOrWhiteSpace(_options.DevToken) || string.IsNullOrEmpty(suppliedToken))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(_options.DevToken);
        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedToken);

        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    public bool IsOriginAllowed(string? origin)
    {
        if (string.IsNullOrWhiteSpace(origin))
        {
            // Native Unity WebSocket clients commonly omit Origin. Token auth still applies.
            return true;
        }

        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri))
        {
            return false;
        }

        if (IsLocalDevelopmentOrigin(originUri))
        {
            return true;
        }

        var normalizedOrigin = NormalizeOrigin(originUri);

        return (_options.AllowedOrigins ?? [])
            .Select(NormalizeConfiguredOrigin)
            .OfType<string>()
            .Contains(normalizedOrigin, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsLocalDevelopmentOrigin(Uri originUri)
    {
        return string.Equals(originUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(originUri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(originUri.Host, "::1", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(originUri.Host, "[::1]", StringComparison.OrdinalIgnoreCase);
    }

    private static string? NormalizeConfiguredOrigin(string origin)
    {
        return Uri.TryCreate(origin, UriKind.Absolute, out var uri) ? NormalizeOrigin(uri) : null;
    }

    private static string NormalizeOrigin(Uri uri)
    {
        var builder = new UriBuilder(uri.Scheme, uri.Host, uri.IsDefaultPort ? -1 : uri.Port);
        return builder.Uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }
}
