using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.Certificate;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using VpnNodeAgent.Abstractions;
using VpnNodeAgent.Configuration;
using VpnNodeAgent.Endpoints;
using VpnNodeAgent.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(httpsOptions =>
    {
        httpsOptions.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
    });
});

builder.Services
    .AddAuthentication(CertificateAuthenticationDefaults.AuthenticationScheme)
    .AddCertificate(options =>
    {
        options.Events = new CertificateAuthenticationEvents
        {
            OnCertificateValidated = context =>
            {
                var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<AgentOptions>>().Value;
                var thumbprint = context.ClientCertificate?.Thumbprint;

                if (string.IsNullOrWhiteSpace(thumbprint))
                {
                    context.Fail("Client certificate thumbprint is required.");
                    return Task.CompletedTask;
                }

                if (settings.AllowedClientThumbprints.Count == 0
                    || settings.AllowedClientThumbprints.Contains(thumbprint, StringComparer.OrdinalIgnoreCase))
                {
                    context.Success();
                    return Task.CompletedTask;
                }

                context.Fail("Client certificate thumbprint is not authorized.");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VPN Node Agent",
        Version = "v1"
    });
});

builder.Services.AddSingleton<IWireGuardCommandRunner, WgCommandRunner>();
builder.Services.AddSingleton<IWireGuardDumpParser, WireGuardDumpParser>();
builder.Services.AddSingleton<IConfigFileCatalog, ConfigFileCatalog>();
builder.Services.AddSingleton<IWireGuardConfigParser, WireGuardConfigParser>();
builder.Services.AddSingleton<IAgentSnapshotService, AgentSnapshotService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/healthz", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous();

app.MapAgentEndpoints();

app.Run();
