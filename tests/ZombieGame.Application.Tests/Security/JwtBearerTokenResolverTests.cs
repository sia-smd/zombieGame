namespace ZombieGame.Application.Tests.Security;

using ZombieGame.Application.Security;

public class JwtBearerTokenResolverTests
{
    [Fact]
    public void Prefers_httpOnly_cookie_over_query_string()
    {
        var token = JwtBearerTokenResolver.Resolve("cookie-jwt", "query-jwt", "/hubs/room");
        Assert.Equal("cookie-jwt", token);
    }

    [Fact]
    public void Allows_Unity_query_token_only_on_hub_paths()
    {
        Assert.Equal("hub-jwt", JwtBearerTokenResolver.Resolve(null, "hub-jwt", "/hubs/game"));
        Assert.Equal("hub-jwt", JwtBearerTokenResolver.Resolve(null, "hub-jwt", "/hubs/room/negotiate"));
        Assert.Null(JwtBearerTokenResolver.Resolve(null, "hub-jwt", "/api/account/login"));
    }

    [Fact]
    public void Returns_null_when_no_cookie_or_hub_query()
    {
        Assert.Null(JwtBearerTokenResolver.Resolve(null, null, "/hubs/room"));
        Assert.Null(JwtBearerTokenResolver.Resolve("  ", "", "/api/profile"));
    }
}
