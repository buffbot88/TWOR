using WorldOfRa.Server.Core;
using WorldOfRa.Server.Networking;
using WorldOfRa.Server.Services;
using WorldOfRa.Server.World;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonDefaults.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = JsonDefaults.DictionaryKeyPolicy;
});
builder.Services.Configure<WorldOfRaOptions>(builder.Configuration.GetSection("WorldOfRa"));
builder.Services.Configure<WorldSocketOptions>(builder.Configuration.GetSection(WorldSocketOptions.SectionName));

builder.Services.AddSingleton<GameClock>();
builder.Services.AddSingleton<WorldState>();
builder.Services.AddSingleton<WebSocketSecurityService>();
builder.Services.AddSingleton<ConnectionLimitService>();
builder.Services.AddSingleton<MessageRateLimiter>();
builder.Services.AddSingleton<PlayerSessionService>();
builder.Services.AddSingleton<NpcSpawnService>();
builder.Services.AddSingleton<WorldBroadcastService>();
builder.Services.AddSingleton<WebSocketHub>();

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

app.MapGet("/", () => Results.Ok(new
{
    name = "World of Ra Server",
    milestone = "Astraelum multiplayer presence prototype",
    websocket = "/ws"
}));

app.MapGet("/health", (WorldState world, ConnectionLimitService limits) => Results.Ok(new
{
    status = "ok",
    connectedPlayers = world.PlayerCount,
    activeConnections = limits.ActiveConnectionCount
}));

app.Map("/ws", async context =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("Expected a WebSocket request.");
        return;
    }

    var security = context.RequestServices.GetRequiredService<WebSocketSecurityService>();
    var token = context.Request.Query["token"].FirstOrDefault();

    if (!security.IsAuthorized(token))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsync("Unauthorized.");
        return;
    }

    var origin = context.Request.Headers["Origin"].FirstOrDefault();

    if (!security.IsOriginAllowed(origin))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Forbidden origin.");
        return;
    }

    var limits = context.RequestServices.GetRequiredService<ConnectionLimitService>();
    var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    if (!limits.TryAcquire(remoteIp, out var connectionLease, out var limitReason) || connectionLease is null)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.Response.WriteAsync(limitReason);
        return;
    }

    using var lease = connectionLease;
    var hub = context.RequestServices.GetRequiredService<WebSocketHub>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await hub.AcceptAsync(socket, context.RequestAborted);
});

app.Run();

public partial class Program;
