# World of Ra Server

Astraelum multiplayer presence prototype for Unity clients.

## Run

```powershell
dotnet run --project .\WorldOfRa.Server.csproj
```

The WebSocket endpoint is:

```text
ws://localhost:5000/ws?token=change-me-dev-token
```

The exact port can vary if ASP.NET Core launch settings or environment variables override it.
Set `WorldSocket:DevToken` per environment and pass the same token in the Unity client WebSocket URL.

## Client Messages

Send `hello` first:

```json
{
  "type": "hello",
  "name": "Player",
  "zoneId": "astraelum",
  "position": { "x": 0, "y": 0, "z": 0 },
  "rotationY": 180
}
```

Send movement updates:

```json
{
  "type": "playerMove",
  "position": { "x": 1.25, "y": 0, "z": -3.5 },
  "rotationY": 90
}
```

Future zone transitions can use:

```json
{
  "type": "zoneChangeRequest",
  "zoneId": "astraelum",
  "position": { "x": 0, "y": 0, "z": 0 },
  "rotationY": 180
}
```

## Server Messages

The server sends:

- `welcome` with the assigned `player`
- `worldSnapshot` with players in the current zone
- `npcSnapshot` with static NPC spawns for the current zone
- `playerJoined` when another player enters the zone
- `playerLeft` when another player leaves or disconnects
- `playerMove` when another player moves in the same zone

All messages are JSON with camel-case fields.
