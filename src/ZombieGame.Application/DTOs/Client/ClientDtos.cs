namespace ZombieGame.Application.DTOs.Client;

public record ClientBootResponse(
    string ServerVersion,
    string MinClientVersion,
    bool EngineHealthy,
    bool DatabaseHealthy,
    bool? RedisHealthy);
