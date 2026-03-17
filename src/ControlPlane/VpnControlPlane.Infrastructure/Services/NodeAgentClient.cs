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

    public async Task<IssueAccessResponse> IssueAccessAsync(Node node, IssueAccessRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{node.AgentBaseAddress}{options.Value.IssueAccessPath}";
        var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<IssueAccessResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException($"Node agent '{node.AgentIdentifier}' returned an empty issue-access payload.");
    }

    public async Task<SetAccessStateResponse> SetAccessStateAsync(Node node, SetAccessStateRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{node.AgentBaseAddress}{options.Value.SetAccessStatePath}";
        var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<SetAccessStateResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException($"Node agent '{node.AgentIdentifier}' returned an empty set-access-state payload.");
    }

    public async Task<DeleteAccessResponse> DeleteAccessAsync(Node node, DeleteAccessRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{node.AgentBaseAddress}{options.Value.DeleteAccessPath}";
        var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DeleteAccessResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException($"Node agent '{node.AgentIdentifier}' returned an empty delete-access payload.");
    }

    public async Task<GetAccessConfigResponse> GetAccessConfigAsync(Node node, GetAccessConfigRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"{node.AgentBaseAddress}{options.Value.GetAccessConfigPath}";
        var response = await httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GetAccessConfigResponse>(cancellationToken: cancellationToken);
        return payload ?? throw new InvalidOperationException($"Node agent '{node.AgentIdentifier}' returned an empty get-access-config payload.");
    }
}
