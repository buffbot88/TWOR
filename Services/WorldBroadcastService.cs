using WorldOfRa.Server.Networking;

namespace WorldOfRa.Server.Services;

public sealed class WorldBroadcastService
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, WebSocketConnection> _connections = new();
    private readonly PlayerSessionService _sessions;
    private readonly ILogger<WorldBroadcastService> _logger;
    private readonly TimeSpan _sendTimeout;

    public WorldBroadcastService(
        PlayerSessionService sessions,
        Microsoft.Extensions.Options.IOptions<WorldSocketOptions> options,
        ILogger<WorldBroadcastService> logger)
    {
        _sessions = sessions;
        _logger = logger;
        _sendTimeout = TimeSpan.FromSeconds(Math.Max(1, options.Value.SendTimeoutSeconds));
    }

    public void RegisterConnection(WebSocketConnection connection) => _connections[connection.Id] = connection;

    public void UnregisterConnection(string connectionId) => _connections.TryRemove(connectionId, out _);

    public Task BroadcastToZoneAsync(string zoneId, object message, string? exceptConnectionId, CancellationToken cancellationToken)
    {
        foreach (var connection in _connections.Values)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (connection.Id == exceptConnectionId)
            {
                continue;
            }

            var player = _sessions.GetPlayer(connection.Id);

            if (player is null || !string.Equals(player.ZoneId, zoneId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            _ = SendSafelyAsync(connection, message);
        }

        return Task.CompletedTask;
    }

    private async Task SendSafelyAsync(WebSocketConnection connection, object message)
    {
        try
        {
            var sent = await connection.SendJsonAsync(message, _sendTimeout, CancellationToken.None);

            if (!sent)
            {
                CleanupFailedConnection(connection.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send WebSocket message to {ConnectionId}", connection.Id);
            CleanupFailedConnection(connection.Id);
        }
    }

    private void CleanupFailedConnection(string connectionId)
    {
        UnregisterConnection(connectionId);
        var removed = _sessions.Disconnect(connectionId);

        if (removed is not null)
        {
            _ = BroadcastToZoneAsync(
                removed.ZoneId,
                ServerMessage.PlayerLeft(removed.Id, removed.ZoneId),
                exceptConnectionId: connectionId,
                CancellationToken.None);
        }
    }
}
