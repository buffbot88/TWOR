using WorldOfRa.Server.World;

namespace WorldOfRa.Server.Services;

public sealed class NpcSpawnService
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<NpcState>> NpcsByZone =
        new Dictionary<string, IReadOnlyCollection<NpcState>>(StringComparer.OrdinalIgnoreCase)
        {
            ["astraelum"] =
            [
                new NpcState("npc_astraelum_guide", "Astraelum Guide", "astraelum", new Vector3Dto(2, 0, 4), 180),
                new NpcState("npc_sunwatcher", "Sunwatcher", "astraelum", new Vector3Dto(-6, 0, 8), 90)
            ]
        };

    public IReadOnlyCollection<NpcState> GetNpcsForZone(string zoneId)
    {
        return NpcsByZone.TryGetValue(zoneId, out var npcs) ? npcs : Array.Empty<NpcState>();
    }
}
