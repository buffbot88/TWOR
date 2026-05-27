using System.Text.Json;

namespace WorldOfRa.Server.Networking;

public static class JsonDefaults
{
    public static readonly JsonNamingPolicy PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    public static readonly JsonNamingPolicy DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;

    public static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = PropertyNamingPolicy,
        DictionaryKeyPolicy = DictionaryKeyPolicy,
        WriteIndented = false
    };
}
