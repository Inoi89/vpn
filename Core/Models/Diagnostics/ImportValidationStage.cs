namespace VpnClient.Core.Models.Diagnostics;

public enum ImportValidationStage
{
    Unknown,
    Path,
    File,
    Format,
    Decode,
    Parse,
    Apply
}
