using System.Net.Http.Json;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Contracts.Nodes;
using VpnControlPlane.Domain.Entities;

namespace VpnControlPlane.Infrastructure.Services;

internal sealed class NodeAgentClient(HttpClient httpClient, IOptions<AgentClientOptions> options) : INodeAgentClient
{
    public static HttpMessageHandler CreateHandler(IServiceProvider serviceProvider)
    {
        var settings = serviceProvider.GetRequiredService<IOptions<AgentClientOptions>>().Value;
        var handler = new HttpClientHandler();

        if (!string.IsNullOrWhiteSpace(settings.ClientCertificatePath))
        {
            var certificate = new X509Certificate2(settings.ClientCertificatePath, settings.ClientCertificatePassword);
            handler.ClientCertificates.Add(certificate);
        }

        if (settings.AllowInvalidServerCertificate)
        {
            handler.ServerCertificateCustomValidationCallback = static (_, _, _, _) => true;
        }

        return handler;
    }

    public async Task<NodeSnapshotResponse> GetSnapshotAsync(Node node, CancellationToken cancellationToken)
    {
        var endpoint = $"{node.AgentBaseAddress}{options.Value.SnapshotPath}";
        var response = await httpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<NodeSnapshotResponse>(cancellationToken: cancellationToken);
        return snapshot ?? throw new InvalidOperationException($"Node agent '{node.AgentIdentifier}' returned an empty snapshot.");
    }
}
