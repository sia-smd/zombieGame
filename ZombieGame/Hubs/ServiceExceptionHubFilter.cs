namespace ZombieGame.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using ZombieGame.Application.Common;

/// <summary>Surfaces application errors to SignalR clients instead of a generic hub failure.</summary>
public sealed class ServiceExceptionHubFilter : IHubFilter
{
    private readonly ILogger<ServiceExceptionHubFilter> _logger;

    public ServiceExceptionHubFilter(ILogger<ServiceExceptionHubFilter> logger) =>
        _logger = logger;

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
            _logger.LogWarning(
                ex,
                "Hub {Hub}.{Method} service error",
                invocationContext.Hub.GetType().Name,
                invocationContext.HubMethodName);
            throw new HubException(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Hub {Hub}.{Method} unhandled error",
                invocationContext.Hub.GetType().Name,
                invocationContext.HubMethodName);
            throw;
        }
    }
}
