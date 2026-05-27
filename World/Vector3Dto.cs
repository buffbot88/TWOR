namespace WorldOfRa.Server.World;

public sealed record Vector3Dto(float X, float Y, float Z)
{
    public static Vector3Dto Zero { get; } = new(0, 0, 0);
}
