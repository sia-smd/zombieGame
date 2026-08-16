namespace ZombieGame.Application.Room;

using System.Text.Json;
using System.Text.Json.Serialization;
using ZombieGame.Domain.Models.Room;

public static class RoomStateSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string Serialize(RoomState state) =>
        JsonSerializer.Serialize(state, Options);

    public static RoomState Deserialize(string json) =>
        JsonSerializer.Deserialize<RoomState>(json, Options)
        ?? throw new InvalidOperationException("Failed to deserialize room state.");
}
