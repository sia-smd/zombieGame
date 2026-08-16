using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ZombieGame.UnityClient.Networking
{
  internal static class HttpJson
  {
    public static StringContent ToJsonContent<T>(T value) =>
      new(JsonSerializer.Serialize(value, NetworkingJson.Options), Encoding.UTF8, "application/json");

    public static async Task<T?> ReadAsync<T>(HttpContent content, CancellationToken ct)
    {
      var json = await content.ReadAsStringAsync();
      if (string.IsNullOrWhiteSpace(json))
        return default;

      return JsonSerializer.Deserialize<T>(json, NetworkingJson.Options);
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(
      HttpClient http,
      string url,
      T body,
      CancellationToken ct) =>
      http.PostAsync(url, ToJsonContent(body), ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(
      HttpClient http,
      string url,
      T body,
      CancellationToken ct) =>
      http.PutAsync(url, ToJsonContent(body), ct);
  }
}
