namespace ZombieGame.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using ZombieGame.Application.Common;

/// <summary>Surfaces application errors to SignalR clients instead of a generic hub failure.</summary>
public sealed class ServiceExceptionHubFilter : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(
        HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        try
        {
            return await next(invocationContext);
        }
        catch (ServiceException ex)
        {
            throw new HubException(ex.Message);
        }
    }
}
