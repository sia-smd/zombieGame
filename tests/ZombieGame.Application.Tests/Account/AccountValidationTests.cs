namespace ZombieGame.Application.Tests.Account;

using ZombieGame.Application.Services;
using ZombieGame.Application.Validation;

public class MobileNumberValidatorTests
{
    [Theory]
    [InlineData("09354214334")]
    [InlineData("+989354214334")]
    [InlineData("989354214334")]
    [InlineData("9354214334")]
    [InlineData("09121234567")]
    [InlineData("+989121234567")]
    public void IsValid_AcceptsIranianMobiles(string mobile) =>
        Assert.True(MobileNumberValidator.IsValid(mobile));

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("abc")]
    [InlineData("+0123456789")]
    [InlineData("+12025550123")]
    [InlineData("02112345678")]
    public void IsValid_RejectsInvalidNumbers(string mobile) =>
        Assert.False(MobileNumberValidator.IsValid(mobile));

    [Theory]
    [InlineData("+989354214334", "09354214334")]
    [InlineData("989354214334", "09354214334")]
    [InlineData("9354214334", "09354214334")]
    [InlineData("09354214334", "09354214334")]
    public void Normalize_UsesLocalZeroNineFormat(string input, string expected) =>
        Assert.Equal(expected, MobileNumberValidator.Normalize(input));
}

public class ForbiddenWordsServiceTests
{
    private readonly ForbiddenWordsService _service = new();

    [Fact]
    public void ContainsForbiddenWord_DetectsBlockedTerms() =>
        Assert.True(_service.ContainsForbiddenWord("hello admin"));

    [Fact]
    public void ContainsForbiddenWord_AllowsCleanNames() =>
        Assert.False(_service.ContainsForbiddenWord("Survivor42"));
}

public class AvatarCatalogServiceTests
{
    [Fact]
    public void IsAllowed_ReturnsTrueForConfiguredAvatars()
    {
        var service = new AvatarCatalogService(Microsoft.Extensions.Options.Options.Create(new Application.Options.AccountSettings()));
        Assert.True(service.IsAllowed("avatar_default_01"));
        Assert.False(service.IsAllowed("avatar_unknown"));
    }
}
