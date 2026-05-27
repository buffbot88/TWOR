using System.Text.Json;
using WorldOfRa.Server.World;

namespace WorldOfRa.Server.Networking;

public sealed record ClientMessage(
    string Type,
    string? Name,
    string? ZoneId,
    Vector3Dto? Position,
    float? RotationY)
{
    public static bool TryParse(ReadOnlySpan<byte> json, out ClientMessage? message)
    {
        message = null;

        try
        {
            message = JsonSerializer.Deserialize<ClientMessage>(json, JsonDefaults.SerializerOptions);
            return !string.IsNullOrWhiteSpace(message?.Type);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
