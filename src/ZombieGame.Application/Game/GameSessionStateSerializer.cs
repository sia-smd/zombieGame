namespace ZombieGame.Application.Game;

using System.Text.Json;
using System.Text.Json.Serialization;
using ZombieGame.Domain.Models;

public static class GameSessionStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(GameSessionState state) =>
        JsonSerializer.Serialize(state, Options);

    public static GameSessionState Deserialize(string json)
    {
        var state = JsonSerializer.Deserialize<GameSessionState>(json, Options)
            ?? throw new InvalidOperationException("Failed to deserialize game session state.");

        NormalizeMetadata(state);
        return state;
    }

    private static void NormalizeMetadata(GameSessionState state)
    {
        if (state.Metadata.Count == 0)
            return;

        var normalized = new Dictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in state.Metadata)
        {
            normalized[key] = value switch
            {
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonElement element => element.ToString(),
                _ => value?.ToString() ?? string.Empty
            };
        }

        state.Metadata = normalized;
    }
}
