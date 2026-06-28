using System.Text.Json;

namespace ZombieGame.UnityClient.Networking
{
    public static class NetworkingJson
    {
        /// <summary>Matches ASP.NET Core default JSON (camelCase, numeric enums).</summary>
        public static JsonSerializerOptions Options { get; } = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }
}
