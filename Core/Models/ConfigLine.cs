namespace VpnClient.Core.Models;

public sealed record ConfigLine(
    int Index,
    ConfigLineKind Kind,
    string RawText,
    string? SectionName = null,
    string? Key = null,
    string? Value = null);
