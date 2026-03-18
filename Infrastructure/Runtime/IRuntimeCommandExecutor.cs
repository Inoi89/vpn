namespace VpnClient.Infrastructure.Runtime;

public sealed record RuntimeCommandResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);

public interface IRuntimeCommandExecutor
{
    Task<RuntimeCommandResult> ExecuteAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default);
}
