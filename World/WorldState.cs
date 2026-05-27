using System.Collections.Concurrent;

namespace WorldOfRa.Server.World;

public sealed class WorldState
{
    private readonly ConcurrentDictionary<string, PlayerState> _playersByConnectionId = new();

    public int PlayerCount => _playersByConnectionId.Count;

    public PlayerState? GetPlayer(string connectionId) =>
        _playersByConnectionId.TryGetValue(connectionId, out var player) ? player : null;

    public IReadOnlyCollection<PlayerState> GetPlayersInZone(string zoneId) =>
        _playersByConnectionId.Values
            .Where(player => string.Equals(player.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(player => player.Id, StringComparer.Ordinal)
            .ToArray();

    public PlayerState UpsertPlayer(string connectionId, PlayerState player)
    {
        _playersByConnectionId[connectionId] = player;
        return player;
    }

    public PlayerState? RemovePlayer(string connectionId)
    {
        return _playersByConnectionId.TryRemove(connectionId, out var player) ? player : null;
    }
}
