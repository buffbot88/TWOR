using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using WorldOfRa.Server.Core;
using WorldOfRa.Server.World;

namespace WorldOfRa.Server.Services;

public sealed class PlayerSessionService
{
    private readonly WorldState _world;
    private readonly WorldOfRaOptions _options;
    private readonly WorldSocketOptions _socketOptions;
    private readonly ConcurrentDictionary<string, MovementAnchor> _movementAnchors = new();

    public PlayerSessionService(
        WorldState world,
        IOptions<WorldOfRaOptions> options,
        IOptions<WorldSocketOptions> socketOptions)
    {
        _world = world;
        _options = options.Value;
        _socketOptions = socketOptions.Value;
    }

    public PlayerConnectResult Connect(string connectionId, string? requestedName, string? requestedZoneId, Vector3Dto? position, float? rotationY)
    {
        var existing = _world.GetPlayer(connectionId);

        if (existing is not null)
        {
            return new PlayerConnectResult(existing, WasConnected: true);
        }

        var zoneId = NormalizeZoneId(requestedZoneId);
        if (!IsAllowedZoneId(zoneId))
        {
            zoneId = NormalizeZoneId(_options.DefaultZoneId);
        }

        var safePosition = ClampPosition(position ?? Vector3Dto.Zero);
        var player = new PlayerState(
            ServerIds.NewPlayerId(),
            NormalizeName(requestedName),
            zoneId,
            safePosition,
            NormalizeRotation(rotationY, 0));

        _movementAnchors[connectionId] = new MovementAnchor(safePosition, DateTimeOffset.UtcNow);
        return new PlayerConnectResult(_world.UpsertPlayer(connectionId, player), WasConnected: false);
    }

    public PlayerMovementResult UpdateMovement(string connectionId, Vector3Dto? position, float? rotationY)
    {
        var existing = _world.GetPlayer(connectionId);

        if (existing is null)
        {
            return PlayerMovementResult.Failed("notWelcomed", "Send hello before playerMove.");
        }

        var safePosition = position is null ? existing.Position : ClampPosition(position);

        if (position is not null && !IsMovementPossible(connectionId, existing.Position, safePosition))
        {
            return PlayerMovementResult.Failed("invalidMovement", "Movement rejected.");
        }

        var updated = existing with
        {
            Position = safePosition,
            RotationY = NormalizeRotation(rotationY, existing.RotationY)
        };

        _movementAnchors[connectionId] = new MovementAnchor(updated.Position, DateTimeOffset.UtcNow);
        return PlayerMovementResult.Success(_world.UpsertPlayer(connectionId, updated));
    }

    public ZoneChangeResult ChangeZone(string connectionId, string requestedZoneId, Vector3Dto? position, float? rotationY)
    {
        var existing = _world.GetPlayer(connectionId);

        if (existing is null)
        {
            return ZoneChangeResult.Failed("notWelcomed", "Send hello before zoneChangeRequest.");
        }

        var zoneId = NormalizeZoneId(requestedZoneId);
        if (!IsAllowedZoneId(zoneId))
        {
            return ZoneChangeResult.Failed("invalidZone", "Requested zone is not available.");
        }

        var safePosition = ClampPosition(position ?? Vector3Dto.Zero);

        var updated = existing with
        {
            ZoneId = zoneId,
            Position = safePosition,
            RotationY = NormalizeRotation(rotationY, existing.RotationY)
        };

        _world.UpsertPlayer(connectionId, updated);
        _movementAnchors[connectionId] = new MovementAnchor(safePosition, DateTimeOffset.UtcNow);
        return ZoneChangeResult.Success(new ZoneTransition(existing.ZoneId, updated));
    }

    public PlayerState? Disconnect(string connectionId)
    {
        _movementAnchors.TryRemove(connectionId, out _);
        return _world.RemovePlayer(connectionId);
    }

    public PlayerState? GetPlayer(string connectionId) => _world.GetPlayer(connectionId);

    public IReadOnlyCollection<PlayerState> GetPlayersInZone(string zoneId) => _world.GetPlayersInZone(zoneId);

    private string NormalizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return _options.DefaultPlayerName;
        }

        var trimmed = name.Trim();
        return trimmed[..Math.Min(trimmed.Length, 32)];
    }

    private string NormalizeZoneId(string? zoneId)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
        {
            return _options.DefaultZoneId;
        }

        var trimmed = zoneId.Trim().ToLowerInvariant();
        return trimmed[..Math.Min(trimmed.Length, 64)];
    }

    private bool IsAllowedZoneId(string zoneId)
    {
        if (string.Equals(zoneId, NormalizeZoneId(_options.DefaultZoneId), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (_socketOptions.AllowedZoneIds ?? []).Any(
            allowedZoneId => string.Equals(NormalizeZoneId(allowedZoneId), zoneId, StringComparison.OrdinalIgnoreCase));
    }

    private Vector3Dto ClampPosition(Vector3Dto position) => new(
        ClampCoordinate(position.X),
        ClampCoordinate(position.Y),
        ClampCoordinate(position.Z));

    private float ClampCoordinate(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        var maxCoordinate = Math.Max(1, _socketOptions.MaxPositionCoordinate);
        return Math.Clamp(value, -maxCoordinate, maxCoordinate);
    }

    private static float NormalizeRotation(float? rotationY, float fallback)
    {
        var value = rotationY ?? fallback;

        if (!float.IsFinite(value))
        {
            return fallback;
        }

        return ((value % 360) + 360) % 360;
    }

    private bool IsMovementPossible(string connectionId, Vector3Dto currentPosition, Vector3Dto requestedPosition)
    {
        var now = DateTimeOffset.UtcNow;
        var anchor = _movementAnchors.GetOrAdd(connectionId, _ => new MovementAnchor(currentPosition, now));
        var elapsedSeconds = Math.Max(0.05, (now - anchor.UpdatedAtUtc).TotalSeconds);
        var maxDistance = Math.Max(1, _socketOptions.MaxMovementUnitsPerSecond) * elapsedSeconds;
        var requestedDistance = Distance(anchor.Position, requestedPosition);

        return requestedDistance <= maxDistance + 0.5;
    }

    private static double Distance(Vector3Dto left, Vector3Dto right)
    {
        var x = left.X - right.X;
        var y = left.Y - right.Y;
        var z = left.Z - right.Z;

        return Math.Sqrt((x * x) + (y * y) + (z * z));
    }
}

public sealed record PlayerConnectResult(PlayerState Player, bool WasConnected);

public sealed record PlayerMovementResult(PlayerState? Player, string? ErrorCode, string? ErrorDetail)
{
    public static PlayerMovementResult Success(PlayerState player) => new(player, null, null);

    public static PlayerMovementResult Failed(string errorCode, string errorDetail) => new(null, errorCode, errorDetail);
}

public sealed record ZoneChangeResult(ZoneTransition? Transition, string? ErrorCode, string? ErrorDetail)
{
    public static ZoneChangeResult Success(ZoneTransition transition) => new(transition, null, null);

    public static ZoneChangeResult Failed(string errorCode, string errorDetail) => new(null, errorCode, errorDetail);
}

public sealed record ZoneTransition(string OldZoneId, PlayerState Player);

public sealed record MovementAnchor(Vector3Dto Position, DateTimeOffset UpdatedAtUtc);

public sealed class WorldOfRaOptions
{
    public string DefaultZoneId { get; set; } = "astraelum";

    public string DefaultPlayerName { get; set; } = "Player";
}
