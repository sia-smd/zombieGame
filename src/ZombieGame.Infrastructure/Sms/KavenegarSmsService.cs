namespace ZombieGame.Infrastructure.Sms;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZombieGame.Application.Common;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Sms;

public sealed class KavenegarSmsService : ISmsService
{
    private readonly HttpClient _http;
    private readonly SmsSettings _settings;
    private readonly ILogger<KavenegarSmsService> _logger;

    public KavenegarSmsService(
        HttpClient http,
        IOptions<SmsSettings> settings,
        ILogger<KavenegarSmsService> logger)
    {
        _http = http;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendVerificationCodeAsync(
        string mobileNumber,
        string code,
        CancellationToken cancellationToken = default)
    {
        var kavenegar = _settings.Kavenegar;
        if (string.IsNullOrWhiteSpace(kavenegar.ApiKey))
            throw new ServiceException("Kavenegar API key is not configured.");

        using var content = new FormUrlEncodedContent(
            KavenegarSmsContract.BuildForm(kavenegar, mobileNumber, code));
        using var response = await _http.PostAsync(
            KavenegarSmsContract.BuildRequestPath(kavenegar),
            content,
            cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<KavenegarResponse>(
            cancellationToken: cancellationToken);
        var status = payload?.Return?.Status ?? (int)response.StatusCode;
        if (!response.IsSuccessStatusCode || !KavenegarSmsContract.IsSuccessStatus(status))
        {
            _logger.LogWarning("Kavenegar rejected SMS with status {Status}.", status);
            throw new ServiceException("Verification SMS could not be sent.");
        }
    }

    private sealed class KavenegarResponse
    {
        [JsonPropertyName("return")]
        public KavenegarReturn? Return { get; set; }
    }

    private sealed class KavenegarReturn
    {
        public int Status { get; set; }
        public string? Message { get; set; }
    }
}
