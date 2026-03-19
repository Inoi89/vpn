using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using VpnProductPlatform.Application.Abstractions;
using VpnProductPlatform.Infrastructure;
using VpnProductPlatform.Infrastructure.Persistence;
using VpnProductPlatform.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProductPlatformInfrastructure(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length == 0)
        {
            policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var jwtSection = builder.Configuration.GetSection(JwtOptions.SectionName);
var jwtOptions = new JwtOptions
{
    Issuer = jwtSection["Issuer"] ?? "VpnProductPlatform",
    Audience = jwtSection["Audience"] ?? "VpnProductPlatform.Client",
    SigningKey = jwtSection["SigningKey"] ?? "development-signing-key-change-me-please",
    LifetimeMinutes = int.TryParse(jwtSection["LifetimeMinutes"], out var lifetimeMinutes) ? lifetimeMinutes : 1440
};
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
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionIdValue = context.Principal?.FindFirst("sid")?.Value
                    ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Sid)?.Value;

                if (!Guid.TryParse(sessionIdValue, out var sessionId))
                {
                    context.Fail("Session id claim is missing.");
                    return;
                }

                var sessionRepository = context.HttpContext.RequestServices.GetRequiredService<IAccountSessionRepository>();
                var session = await sessionRepository.GetByIdAsync(sessionId, context.HttpContext.RequestAborted);
                if (session is null || !session.IsActiveAt(DateTimeOffset.UtcNow))
                {
                    context.Fail("Session is revoked or expired.");
                }
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
    ForwardLimit = 1
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseCors("frontend");

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<ProductPlatformDbSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
