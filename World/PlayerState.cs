namespace WorldOfRa.Server.World;

public sealed record PlayerState(
    string Id,
    string Name,
    string ZoneId,
    Vector3Dto Position,
    float RotationY);
