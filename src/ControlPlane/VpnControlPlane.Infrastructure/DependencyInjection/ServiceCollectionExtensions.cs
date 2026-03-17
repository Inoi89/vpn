using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Infrastructure.BackgroundJobs;
using VpnControlPlane.Infrastructure.Persistence;
using VpnControlPlane.Infrastructure.Services;

namespace VpnControlPlane.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' was not found.");

        services.AddDbContext<ControlPlaneDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(provider => provider.GetRequiredService<ControlPlaneDbContext>());

        services.AddScoped<INodeRepository, EfNodeRepository>();
        services.AddScoped<IUserRepository, EfUserRepository>();
        services.AddScoped<IAccessRepository, EfAccessRepository>();
        services.AddScoped<IDashboardReadService, DashboardReadService>();
        services.AddScoped<INodeSnapshotWriter, EfNodeSnapshotWriter>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<NodePollingJob>();

        services.Configure<AgentClientOptions>(configuration.GetSection(AgentClientOptions.SectionName));
        services.AddHttpClient<INodeAgentClient, NodeAgentClient>()
            .ConfigurePrimaryHttpMessageHandler(NodeAgentClient.CreateHandler)
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        services.AddHangfire(hangfire => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString)));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
            options.Queues = ["default", "polling"];
        });

        return services;
    }
}
