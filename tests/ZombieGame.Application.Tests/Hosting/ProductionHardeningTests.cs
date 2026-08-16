namespace ZombieGame.Application.Tests.Hosting;

using ZombieGame.Application.Hosting;
using ZombieGame.Application.Options;
using ZombieGame.Application.Sms;

public class ProductionHardeningTests
{
    [Fact]
    public void JwtSecret_RejectsPlaceholderInProduction()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.ValidateJwtSecret("short", isProduction: false));
        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.ValidateJwtSecret(
                "DEVELOPMENT_ONLY_ZOMBIE_GAME_SECRET_32_CHARS_MINIMUM",
                isProduction: true));
        ProductionConfiguration.ValidateJwtSecret(
            "DEVELOPMENT_ONLY_ZOMBIE_GAME_SECRET_32_CHARS_MINIMUM",
            isProduction: false);
        ProductionConfiguration.ValidateJwtSecret(
            "a-long-enough-production-secret-value-32",
            isProduction: true);
    }

    [Fact]
    public void Cors_AllowsEmptyOriginsForSameSiteHosting()
    {
        var development = ProductionConfiguration.ResolveAllowedOrigins([], isDevelopment: true);
        Assert.Contains("http://localhost:5173", development);
        Assert.Empty(ProductionConfiguration.ResolveAllowedOrigins([], isDevelopment: false));
        Assert.Equal(
            ["https://game.example"],
            ProductionConfiguration.ResolveAllowedOrigins(["https://game.example"], isDevelopment: false));
    }

    [Fact]
    public void Sms_RequiresKavenegarKeyOnlyWhenThatProviderIsSelected()
    {
        ProductionConfiguration.ValidateSmsSettings(new SmsSettings(), isProduction: true);
        Assert.Throws<InvalidOperationException>(() =>
            ProductionConfiguration.ValidateSmsSettings(
                new SmsSettings { Provider = SmsSettings.KavenegarProvider },
                isProduction: true));
        ProductionConfiguration.ValidateSmsSettings(
            new SmsSettings
            {
                Provider = SmsSettings.KavenegarProvider,
                Kavenegar = new KavenegarSmsSettings { ApiKey = "key" }
            },
            isProduction: true);
    }

    [Fact]
    public void KavenegarContract_UsesVerifyLookupAndLocalReceptor()
    {
        var settings = new KavenegarSmsSettings
        {
            ApiKey = "abc",
            Template = "verify",
            BaseUrl = "https://api.kavenegar.com/v1"
        };

        Assert.Equal("09121234567", KavenegarSmsContract.ToReceptor("+989121234567"));
        Assert.Contains("verify/lookup.json", KavenegarSmsContract.BuildRequestPath(settings));
        var form = KavenegarSmsContract.BuildForm(settings, "+989121234567", "123456");
        Assert.Equal("09121234567", form["receptor"]);
        Assert.Equal("123456", form["token"]);
        Assert.Equal("verify", form["template"]);
    }
}
