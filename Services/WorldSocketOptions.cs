namespace WorldOfRa.Server.Services;

public sealed class WorldSocketOptions
{
    public const string SectionName = "WorldSocket";

    public string DevToken { get; set; } = "change-me-dev-token";

    public string[] AllowedOrigins { get; set; } =
    [
        "http://localhost",
        "https://localhost",
        "http://127.0.0.1",
        "https://127.0.0.1"
    ];

    public int MaxConnections { get; set; } = 200;

    public int MaxConnectionsPerIp { get; set; } = 20;

    public int IdleTimeoutSeconds { get; set; } = 60;

    public int MaxMoveMessagesPerSecond { get; set; } = 20;

    public string[] AllowedZoneIds { get; set; } = ["astraelum"];

    public float MaxPositionCoordinate { get; set; } = 5000;

    public float MaxMovementUnitsPerSecond { get; set; } = 100;

    public int SendTimeoutSeconds { get; set; } = 2;
}
