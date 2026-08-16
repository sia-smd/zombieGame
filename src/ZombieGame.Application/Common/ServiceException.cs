namespace ZombieGame.Application.Common;

public class ServiceException : Exception
{
    public string? Code { get; }

    public ServiceException(string message, string? code = null) : base(message)
    {
        Code = code;
    }
}
