using Hangfire;
using VpnControlPlane.Application.Abstractions;
using VpnControlPlane.Application.Nodes.Commands;

namespace VpnControlPlane.Infrastructure.BackgroundJobs;

public sealed class NodePollingJob(
    INodeRepository nodeRepository,
    INodeAgentClient nodeAgentClient,
    ICommandDispatcher commandDispatcher,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    [DisableConcurrentExecution(timeoutInSeconds: 30)]
    public async Task PollAsync()
    {
        var nodes = await nodeRepository.ListAsync(enabledOnly: true, CancellationToken.None);

        foreach (var node in nodes)
        {
            try
            {
                var snapshot = await nodeAgentClient.GetSnapshotAsync(node, CancellationToken.None);
                await commandDispatcher.Send(new UpsertNodeSnapshotCommand(node.Id, snapshot), CancellationToken.None);
            }
            catch (Exception exception)
            {
                var trackedNode = await nodeRepository.GetByIdAsync(node.Id, includeRelated: false, CancellationToken.None);
                if (trackedNode is null)
                {
                    continue;
                }

                trackedNode.MarkUnreachable(exception.Message, clock.UtcNow);
                await unitOfWork.SaveChangesAsync(CancellationToken.None);
            }
        }
    }
}
