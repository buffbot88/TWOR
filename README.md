World of Ra Server Spec — v0.13

Purpose:

Unity clients connect to a .NET 9 server so multiple players can walk around Astraelum together.

Core server responsibilities

player connect/disconnect
assign player id
track zone id
track player position
track player rotation
broadcast player movement
send NPC spawn list
support future zone transitions

Server stack

.NET 9
ASP.NET Core Minimal API
WebSockets
JSON messages
SQLite first, MySQL later

First milestone

2 Unity players connect
each sees the other walking around Astraelum
server tracks positions
server broadcasts movement

Message types

hello
welcome
playerJoined
playerLeft
playerMove
worldSnapshot
npcSnapshot
zoneChangeRequest

Player state

{
  "id": "player_123",
  "name": "Player",
  "zoneId": "astraelum",
  "position": { "x": 0, "y": 0, "z": 0 },
  "rotationY": 180
}

Folder plan

WorldOfRa.Server/
  Program.cs
  appsettings.json

  Core/
    GameClock.cs
    ServerIds.cs

  Networking/
    WebSocketConnection.cs
    WebSocketHub.cs
    ClientMessage.cs
    ServerMessage.cs

  World/
    PlayerState.cs
    NpcState.cs
    ZoneState.cs
    WorldState.cs

  Services/
    PlayerSessionService.cs
    WorldBroadcastService.cs
    NpcSpawnService.cs

Not yet

combat
inventory
quests
guilds
accounts
database persistence
anti-cheat
MMO scaling

First server is just:

Astraelum multiplayer presence prototype
