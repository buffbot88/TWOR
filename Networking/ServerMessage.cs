using WorldOfRa.Server.World;

namespace WorldOfRa.Server.Networking;

public static class ServerMessage
{
    public static object Error(string code, string detail) => new
    {
        type = "error",
        code,
        detail
    };

    public static object Welcome(PlayerState player, DateTimeOffset serverTimeUtc) => new
    {
        type = "welcome",
        player,
        serverTimeUtc
    };

    public static object PlayerJoined(PlayerState player) => new
    {
        type = "playerJoined",
        player
    };

    public static object PlayerLeft(string playerId, string zoneId) => new
    {
        type = "playerLeft",
        playerId,
        zoneId
    };

    public static object PlayerMove(PlayerState player) => new
    {
        type = "playerMove",
        playerId = player.Id,
        zoneId = player.ZoneId,
        position = player.Position,
        rotationY = player.RotationY
    };

    public static object WorldSnapshot(IReadOnlyCollection<PlayerState> players) => new
    {
        type = "worldSnapshot",
        players
    };

    public static object NpcSnapshot(string zoneId, IReadOnlyCollection<NpcState> npcs) => new
    {
        type = "npcSnapshot",
        zoneId,
        npcs
    };
}
