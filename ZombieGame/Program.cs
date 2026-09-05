namespace ZombieGame.Api;

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using ZombieGame.Application;
using ZombieGame.Application.Hosting;
using ZombieGame.Application.Interfaces;
using ZombieGame.Application.Options;
using ZombieGame.Application.Security;
using ZombieGame.Application.Services;
using ZombieGame.Domain.Interfaces;
using ZombieGame.Infrastructure;
using ZombieGame.Infrastructure.Health;
using ZombieGame.Infrastructure.Logging;
using ZombieGame.Infrastructure.Persistence;
using ZombieGame.Infrastructure.Sms;
using ZombieGame.Api.Middleware;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.Configure<DailyExceptionLogOptions>(
            builder.Configuration.GetSection(DailyExceptionLogOptions.SectionName));

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddProvider(new DailyExceptionLoggerProvider(
            Microsoft.Extensions.Options.Options.Create(
                builder.Configuration.GetSection(DailyExceptionLogOptions.SectionName)
                    .Get<DailyExceptionLogOptions>() ?? new DailyExceptionLogOptions()),
            builder.Environment.ContentRootPath));

        builder.Services.AddApplication(builder.Configuration);
        builder.Services.AddInfrastructure(builder.Configuration);

        builder.Services.AddSingleton<ZombieGame.Application.Interfaces.IGameRealtimeNotifier, ZombieGame.Api.Services.GameHubNotifier>();
        builder.Services.AddSingleton<ZombieGame.Application.Interfaces.IRoomRealtimeNotifier, ZombieGame.Api.Services.RoomHubNotifier>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.GameLoopHostedService>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.RoomLoopHostedService>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.MatchmakingTimeoutHostedService>();
        builder.Services.AddHostedService<ZombieGame.Api.Services.ActiveMatchBootstrapService>();

        var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt settings are missing.");
        ProductionConfiguration.ValidateJwtSecret(jwtSettings.Secret, builder.Environment.IsProduction());

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
                        // Authorization: Bearer is handled by the handler when Token is left unset.
                        if (!string.IsNullOrEmpty(context.Request.Headers.Authorization))
                            return Task.CompletedTask;

                        context.Token = JwtBearerTokenResolver.Resolve(
                            context.Request.Cookies[AuthCookieNames.AccessToken],
                            context.Request.Query["access_token"],
                            context.HttpContext.Request.Path);
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        if (!JwtSessionValidator.TryReadSessionClaims(
                                context.Principal,
                                out var sessionId,
                                out var playerId,
                                out var jti))
                        {
                            context.Fail("Required session claims are missing or invalid.");
                            return;
                        }

                        var sessionRepository = context.HttpContext.RequestServices
                            .GetRequiredService<IPlayerSessionRepository>();
                        var session = await sessionRepository.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
                        if (!JwtSessionValidator.IsActiveSession(session, playerId, jti, DateTime.UtcNow))
                            context.Fail("Session is no longer active.");
                    }
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddControllers();
        builder.Services.AddSingleton<Microsoft.AspNetCore.SignalR.IUserIdProvider, Hubs.JwtUserIdProvider>();
        builder.Services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = builder.Environment.IsDevelopment();
            options.AddFilter<Hubs.ServiceExceptionHubFilter>();
        });
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
                var allowedOrigins = ProductionConfiguration.ResolveAllowedOrigins(
                    builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>(),
                    builder.Environment.IsDevelopment());

                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
                else
                {
                    policy.AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        var smsSettings = builder.Configuration.GetSection(SmsSettings.SectionName).Get<SmsSettings>() ?? new SmsSettings();
        ProductionConfiguration.ValidateSmsSettings(smsSettings, builder.Environment.IsProduction());
        if (smsSettings.UsesKavenegar)
            builder.Services.AddHttpClient<ISmsService, KavenegarSmsService>();
        else
            builder.Services.AddSingleton<ISmsService, MockSmsService>();

        var rateLimits = builder.Configuration.GetSection(RateLimitSettings.SectionName).Get<RateLimitSettings>()
            ?? new RateLimitSettings();
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new { message = "Too many requests. Try again later." },
                    cancellationToken);
            };

            options.AddPolicy(RateLimitSettings.AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimits.AuthPermitLimit,
                        Window = TimeSpan.FromMinutes(Math.Max(1, rateLimits.WindowMinutes)),
                        QueueLimit = 0
                    }));

            options.AddPolicy(RateLimitSettings.SmsPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    "sms:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimits.SmsPermitLimit,
                        Window = TimeSpan.FromMinutes(Math.Max(1, rateLimits.WindowMinutes)),
                        QueueLimit = 0
                    }));
        });

        var healthChecks = builder.Services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>("sql");
        if (builder.Configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()?.Enabled == true)
            healthChecks.AddCheck<RedisHealthCheck>("redis");

        var app = builder.Build();

        if (app.Environment.IsProduction() && !smsSettings.UsesKavenegar)
            app.Logger.LogWarning("SMS provider is Mock. Set Sms:Provider to Kavenegar and Sms:Kavenegar:ApiKey before sending real verification codes.");

        await DatabaseInitializer.InitializeAsync(app.Services);

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseMiddleware<ExceptionLoggingMiddleware>();
        app.UseHttpsRedirection();
        var webRoot = app.Environment.WebRootPath
            ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
        var spaIndex = Path.Combine(webRoot, "index.html");
        var serveSpa = File.Exists(spaIndex);
        if (serveSpa)
        {
            app.UseDefaultFiles();
            app.UseStaticFiles();
        }

        app.UseCors("UnityClient");
        app.UseRateLimiter();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHub<Hubs.GameHub>("/hubs/game");
        app.MapHub<Hubs.RoomHub>("/hubs/room");
        app.MapHealthChecks("/health");
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });
        if (serveSpa)
            app.MapFallbackToFile("index.html");

        await app.RunAsync();
    }
}
