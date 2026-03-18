namespace VpnClient.Core.Models;

public enum ConfigLineKind
{
    Blank,
    Comment,
    SectionHeader,
    KeyValue,
    Unknown
}
