namespace WorldOfRa.Server.World;

public sealed record NpcState(
    string Id,
    string Name,
    string ZoneId,
    Vector3Dto Position,
    float RotationY);
