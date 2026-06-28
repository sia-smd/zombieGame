using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ZombieGame.UnityClient.Networking
{
    public sealed class ApiException : Exception
    {
        public int StatusCode { get; }

        public ApiException(int statusCode, string message) : base(message) => StatusCode = statusCode;

        public static async Task ThrowIfFailedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var body = await response.Content.ReadAsStringAsync();
            var message = string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? "Request failed" : body;
            throw new ApiException((int)response.StatusCode, message);
        }
    }
}
