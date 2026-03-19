using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VpnProductPlatform.Infrastructure;
using VpnProductPlatform.Infrastructure.Persistence;
using VpnProductPlatform.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProductPlatformInfrastructure(builder.Configuration);

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
    });

builder.Services.AddAuthorization();

var app = builder.Build();

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
