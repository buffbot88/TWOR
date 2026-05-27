using System.Net.WebSockets;
using Microsoft.Extensions.Options;
using WorldOfRa.Server.Core;
using WorldOfRa.Server.Services;

namespace WorldOfRa.Server.Networking;

public sealed class WebSocketHub
{
    private const int ReceiveBufferSize = 4096;
    private const int MaxMessageBytes = 64 * 1024;

    private readonly PlayerSessionService _sessions;
    private readonly WorldBroadcastService _broadcasts;
    private readonly NpcSpawnService _npcs;
    private readonly GameClock _clock;
    private readonly MessageRateLimiter _rateLimiter;
    private readonly ILogger<WebSocketHub> _logger;
    private readonly TimeSpan _idleTimeout;

    public WebSocketHub(
        PlayerSessionService sessions,
        WorldBroadcastService broadcasts,
        NpcSpawnService npcs,
        GameClock clock,
        MessageRateLimiter rateLimiter,
        IOptions<WorldSocketOptions> options,
        ILogger<WebSocketHub> logger)
    {
        _sessions = sessions;
        _broadcasts = broadcasts;
        _npcs = npcs;
        _clock = clock;
        _rateLimiter = rateLimiter;
        _logger = logger;
        _idleTimeout = TimeSpan.FromSeconds(Math.Max(5, options.Value.IdleTimeoutSeconds));
    }

    public async Task AcceptAsync(WebSocket socket, CancellationToken cancellationToken)
    {
        var connection = new WebSocketConnection(ServerIds.NewConnectionId(), socket);
        _broadcasts.RegisterConnection(connection);

        _logger.LogInformation("WebSocket connected: {ConnectionId}", connection.Id);

        try
        {
            await ReceiveLoopAsync(connection, socket, cancellationToken);
        }
        finally
        {
            _broadcasts.UnregisterConnection(connection.Id);
            _rateLimiter.Remove(connection.Id);
            var removed = _sessions.Disconnect(connection.Id);

            if (removed is not null)
            {
                await _broadcasts.BroadcastToZoneAsync(
                    removed.ZoneId,
                    ServerMessage.PlayerLeft(removed.Id, removed.ZoneId),
                    exceptConnectionId: connection.Id,
                    CancellationToken.None);
            }

            _logger.LogInformation("WebSocket disconnected: {ConnectionId}", connection.Id);
        }
    }

    private async Task ReceiveLoopAsync(WebSocketConnection connection, WebSocket socket, CancellationToken cancellationToken)
    {
        var buffer = new byte[ReceiveBufferSize];

        while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
        {
            using var message = new MemoryStream();
            WebSocketReceiveResult result;

            do
            {
                var receiveResult = await ReceiveWithIdleTimeoutAsync(connection, socket, buffer, cancellationToken);

                if (receiveResult is null)
                {
                    return;
                }

                result = receiveResult;

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Client closed", cancellationToken);
                    return;
                }

                if (result.MessageType != WebSocketMessageType.Text)
                {
                    await connection.SendJsonAsync(ServerMessage.Error("unsupportedMessageType", "Only text JSON messages are supported."), cancellationToken);
                    continue;
                }

                message.Write(buffer, 0, result.Count);

                if (message.Length > MaxMessageBytes)
                {
                    await connection.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message exceeded maximum size.", cancellationToken);
                    return;
                }
            }
            while (!result.EndOfMessage);

            if (ClientMessage.TryParse(message.GetBuffer().AsSpan(0, (int)message.Length), out var clientMessage) && clientMessage is not null)
            {
                await HandleMessageAsync(connection, clientMessage, cancellationToken);
            }
            else
            {
                await connection.SendJsonAsync(ServerMessage.Error("invalidJson", "Message must include a valid type field."), cancellationToken);
            }
        }
    }

    private async Task HandleMessageAsync(WebSocketConnection connection, ClientMessage message, CancellationToken cancellationToken)
    {
        switch (message.Type)
        {
            case "hello":
                await HandleHelloAsync(connection, message, cancellationToken);
                break;

            case "playerMove":
                await HandlePlayerMoveAsync(connection, message, cancellationToken);
                break;

            case "zoneChangeRequest":
                await HandleZoneChangeRequestAsync(connection, message, cancellationToken);
                break;

            default:
                await connection.SendJsonAsync(ServerMessage.Error("unknownMessageType", "Unknown message type."), cancellationToken);
                break;
        }
    }

    private async Task HandleHelloAsync(WebSocketConnection connection, ClientMessage message, CancellationToken cancellationToken)
    {
        var connectResult = _sessions.Connect(
            connection.Id,
            message.Name,
            message.ZoneId,
            message.Position,
            message.RotationY);
        var player = connectResult.Player;

        await connection.SendJsonAsync(ServerMessage.Welcome(player, _clock.UtcNow), cancellationToken);
        await connection.SendJsonAsync(ServerMessage.WorldSnapshot(_sessions.GetPlayersInZone(player.ZoneId)), cancellationToken);
        await connection.SendJsonAsync(ServerMessage.NpcSnapshot(player.ZoneId, _npcs.GetNpcsForZone(player.ZoneId)), cancellationToken);

        if (!connectResult.WasConnected)
        {
            await _broadcasts.BroadcastToZoneAsync(
                player.ZoneId,
                ServerMessage.PlayerJoined(player),
                exceptConnectionId: connection.Id,
                cancellationToken);
        }
    }

    private async Task HandlePlayerMoveAsync(WebSocketConnection connection, ClientMessage message, CancellationToken cancellationToken)
    {
        if (!_rateLimiter.TryConsumeMove(connection.Id))
        {
            await connection.SendJsonAsync(ServerMessage.Error("rateLimited", "Too many movement messages."), cancellationToken);
            await connection.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Movement rate limit exceeded.", CancellationToken.None);
            return;
        }

        var result = _sessions.UpdateMovement(connection.Id, message.Position, message.RotationY);

        if (result.Player is null)
        {
            await connection.SendJsonAsync(
                ServerMessage.Error(result.ErrorCode ?? "invalidMovement", result.ErrorDetail ?? "Movement rejected."),
                cancellationToken);
            return;
        }

        await _broadcasts.BroadcastToZoneAsync(
            result.Player.ZoneId,
            ServerMessage.PlayerMove(result.Player),
            exceptConnectionId: connection.Id,
            cancellationToken);
    }

    private async Task HandleZoneChangeRequestAsync(WebSocketConnection connection, ClientMessage message, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message.ZoneId))
        {
            await connection.SendJsonAsync(ServerMessage.Error("invalidZone", "zoneChangeRequest requires zoneId."), cancellationToken);
            return;
        }

        var zoneChange = _sessions.ChangeZone(connection.Id, message.ZoneId, message.Position, message.RotationY);

        if (zoneChange.Transition is null)
        {
            await connection.SendJsonAsync(
                ServerMessage.Error(zoneChange.ErrorCode ?? "invalidZone", zoneChange.ErrorDetail ?? "Zone change rejected."),
                cancellationToken);
            return;
        }

        var transition = zoneChange.Transition;

        if (!string.Equals(transition.OldZoneId, transition.Player.ZoneId, StringComparison.OrdinalIgnoreCase))
        {
            await _broadcasts.BroadcastToZoneAsync(
                transition.OldZoneId,
                ServerMessage.PlayerLeft(transition.Player.Id, transition.OldZoneId),
                exceptConnectionId: connection.Id,
                cancellationToken);

            await _broadcasts.BroadcastToZoneAsync(
                transition.Player.ZoneId,
                ServerMessage.PlayerJoined(transition.Player),
                exceptConnectionId: connection.Id,
                cancellationToken);
        }

        await connection.SendJsonAsync(ServerMessage.WorldSnapshot(_sessions.GetPlayersInZone(transition.Player.ZoneId)), cancellationToken);
        await connection.SendJsonAsync(ServerMessage.NpcSnapshot(transition.Player.ZoneId, _npcs.GetNpcsForZone(transition.Player.ZoneId)), cancellationToken);
    }

    private async Task<WebSocketReceiveResult?> ReceiveWithIdleTimeoutAsync(
        WebSocketConnection connection,
        WebSocket socket,
        byte[] buffer,
        CancellationToken cancellationToken)
    {
        using var idleTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var receiveTask = socket.ReceiveAsync(new ArraySegment<byte>(buffer), idleTimeout.Token);
        var timeoutTask = Task.Delay(_idleTimeout, cancellationToken);

        if (await Task.WhenAny(receiveTask, timeoutTask) == receiveTask)
        {
            try
            {
                return await receiveTask;
            }
            catch (IOException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        idleTimeout.Cancel();
        await connection.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Idle timeout.", CancellationToken.None);

        try
        {
            await receiveTask;
        }
        catch (OperationCanceledException)
        {
        }
        catch (IOException)
        {
        }
        catch (ObjectDisposedException)
        {
        }

        return null;
    }
}
