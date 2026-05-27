using System.Security.Cryptography;

namespace WorldOfRa.Server.Core;

public static class ServerIds
{
    public static string NewConnectionId() => $"conn_{RandomNumberGenerator.GetHexString(12).ToLowerInvariant()}";

    public static string NewPlayerId() => $"player_{RandomNumberGenerator.GetHexString(8).ToLowerInvariant()}";
}
