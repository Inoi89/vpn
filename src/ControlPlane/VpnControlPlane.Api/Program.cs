using System.Text;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using VpnControlPlane.Api.Hubs;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.DependencyInjection;
using VpnControlPlane.Infrastructure.BackgroundJobs;
using VpnControlPlane.Infrastructure.DependencyInjection;
using VpnControlPlane.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VPN Control Plane API",
        Version = "v1"
    });

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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ISessionRealtimeNotifier, SignalRSessionRealtimeNotifier>();

var jwtSigningKey = builder.Configuration["Security:Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Security:Jwt:SigningKey is required.");
var jwtIssuer = builder.Configuration["Security:Jwt:Issuer"] ?? "vpn-control-plane";
var jwtAudience = builder.Configuration["Security:Jwt:Audience"] ?? "vpn-control-plane-ui";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(
        "ui",
        policy =>
        {
            if (allowedOrigins.Length == 0)
            {
                policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
                return;
            }

            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
        });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(accessToken)
                    && context.HttpContext.Request.Path.StartsWithSegments("/hubs/sessions"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("ui");
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapControllers();
app.MapHub<SessionUpdatesHub>("/hubs/sessions");

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ControlPlaneDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    await WaitForDatabaseAsync(dbContext, logger, app.Lifetime.ApplicationStopping);
    await EnsureSchemaUpgradesAsync(dbContext, app.Lifetime.ApplicationStopping);

    var recurringJobManager = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();
    recurringJobManager.AddOrUpdate<NodePollingJob>(
        "poll-nodes",
        job => job.PollAsync(),
        "*/15 * * * * *");
}

app.Run();

static async Task WaitForDatabaseAsync(
    ControlPlaneDbContext dbContext,
    ILogger logger,
    CancellationToken cancellationToken)
{
    const int maxAttempts = 20;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
            logger.LogInformation("Database is ready after {Attempt} attempt(s).", attempt);
            return;
        }
        catch (Exception exception) when (attempt < maxAttempts)
        {
            logger.LogWarning(
                exception,
                "Database is not ready yet. Retrying startup in 3 seconds. Attempt {Attempt}/{MaxAttempts}.",
                attempt,
                maxAttempts);

            await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        }
    }

    await dbContext.Database.EnsureCreatedAsync(cancellationToken);
}

static Task EnsureSchemaUpgradesAsync(ControlPlaneDbContext dbContext, CancellationToken cancellationToken)
{
    return dbContext.Database.ExecuteSqlRawAsync(
        """
        alter table if exists peer_configs
            add column if not exists is_enabled boolean not null default true;
        """,
        cancellationToken);
}
