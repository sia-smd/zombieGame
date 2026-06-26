namespace ZombieGame.Api;

using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZombieGame.Application;
using ZombieGame.Application.Options;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure;
using ZombieGame.Infrastructure.Persistence;
using ZombieGame.Infrastructure.Security;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<ZombieGame.Application.Interfaces.IGameRealtimeNotifier, ZombieGame.Api.Services.GameHubNotifier>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.GameLoopHostedService>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.ActiveMatchBootstrapService>();

        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt settings are missing.");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/game"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        var sid = context.Principal?.FindFirstValue(JwtTokenService.SessionIdClaim);
                        if (string.IsNullOrEmpty(sid) || !Guid.TryParse(sid, out var sessionId))
                            return;

                        var sessionRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IPlayerSessionRepository>();
                        var session = await sessionRepository.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
                        if (session is null || !session.IsActive || session.ExpireDate < DateTime.UtcNow)
                            context.Fail("Session is no longer active.");
                    }
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddSignalR();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "ZombieGame API", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("UnityClient", policy =>
            {
                policy.AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials()
                    .SetIsOriginAllowed(_ => true);
            });
        });

        var app = builder.Build();

        await DatabaseInitializer.InitializeAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("UnityClient");
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<Hubs.GameHub>("/hubs/game");

        await app.RunAsync();
    }
}
