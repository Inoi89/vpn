namespace VpnClient.Core.Models.Diagnostics;

public sealed record ImportValidationError
{
    public DateTimeOffset TimestampUtc { get; init; }
    public string? SourcePath { get; init; }
    public string? FileName { get; init; }
    public ImportValidationStage Stage { get; init; } = ImportValidationStage.Unknown;
    public string Message { get; init; } = string.Empty;
    public string? ExceptionType { get; init; }
    public string? Details { get; init; }
}
