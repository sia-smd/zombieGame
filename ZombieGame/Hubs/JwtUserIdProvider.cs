namespace ZombieGame.Api.Hubs;

using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

public sealed class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) =>
        connection.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? connection.User?.FindFirstValue("sub");
}
