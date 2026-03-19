using System.Text.Json;

namespace VpnClient.Infrastructure.Import;

internal static class AmneziaVpnConfigMaterializer
{
    public static string Materialize(string packageJson, string rawConfig)
    {
        if (string.IsNullOrWhiteSpace(packageJson))
        {
            return Normalize(rawConfig);
        }

        using var document = JsonDocument.Parse(packageJson);
        return Materialize(document.RootElement, rawConfig);
    }

    public static string Materialize(JsonElement root, string rawConfig)
    {
        var primaryDns = FindStringProperty(root, "dns1");
        var secondaryDns = FindStringProperty(root, "dns2");
        var mtu = FindStringProperty(root, "mtu");

        var lines = Normalize(rawConfig)
            .Split('\n', StringSplitOptions.None)
            .ToList();

        var interfaceSectionIndex = FindSectionIndex(lines, "Interface");
        if (interfaceSectionIndex >= 0)
        {
            ApplyDns(lines, interfaceSectionIndex, primaryDns, secondaryDns);
            ApplyMtu(lines, interfaceSectionIndex, mtu);
        }

        var joined = string.Join('\n', lines)
            .Replace("$PRIMARY_DNS", primaryDns ?? string.Empty, StringComparison.Ordinal)
            .Replace("$SECONDARY_DNS", secondaryDns ?? string.Empty, StringComparison.Ordinal);

        return Normalize(joined);
    }

    private static void ApplyDns(IList<string> lines, int interfaceSectionIndex, string? primaryDns, string? secondaryDns)
    {
        var dnsValues = new[] { primaryDns, secondaryDns }
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (dnsValues.Length == 0)
        {
            return;
        }

        var dnsLineIndex = FindKeyIndex(lines, interfaceSectionIndex, "DNS");
        var dnsLine = $"DNS = {string.Join(", ", dnsValues)}";
        if (dnsLineIndex >= 0)
        {
            lines[dnsLineIndex] = dnsLine;
            return;
        }

        InsertInterfaceSetting(lines, interfaceSectionIndex, dnsLine);
    }

    private static void ApplyMtu(IList<string> lines, int interfaceSectionIndex, string? mtu)
    {
        if (string.IsNullOrWhiteSpace(mtu))
        {
            return;
        }

        var mtuLineIndex = FindKeyIndex(lines, interfaceSectionIndex, "MTU");
        var mtuLine = $"MTU = {mtu.Trim()}";
        if (mtuLineIndex >= 0)
        {
            lines[mtuLineIndex] = mtuLine;
            return;
        }

        InsertInterfaceSetting(lines, interfaceSectionIndex, mtuLine);
    }

    private static void InsertInterfaceSetting(IList<string> lines, int interfaceSectionIndex, string line)
    {
        var insertIndex = interfaceSectionIndex + 1;
        while (insertIndex < lines.Count)
        {
            var trimmed = lines[insertIndex].Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith("PrivateKey", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            insertIndex++;
        }

        lines.Insert(insertIndex, line);
    }

    private static int FindSectionIndex(IList<string> lines, string sectionName)
    {
        for (var index = 0; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.Equals($"[{sectionName}]", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static int FindKeyIndex(IList<string> lines, int interfaceSectionIndex, string key)
    {
        for (var index = interfaceSectionIndex + 1; index < lines.Count; index++)
        {
            var trimmed = lines[index].Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                break;
            }

            if (trimmed.StartsWith($"{key} =", StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static string? FindStringProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals(propertyName))
                {
                    return property.Value.ValueKind switch
                    {
                        JsonValueKind.String => property.Value.GetString(),
                        JsonValueKind.Number => property.Value.ToString(),
                        _ => null
                    };
                }

                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    var candidate = property.Value.GetString();
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && LooksLikeJson(candidate)
                        && TryParseJsonDocument(candidate, out var nestedDocument))
                    {
                        using (nestedDocument)
                        {
                            var nestedValue = FindStringProperty(nestedDocument.RootElement, propertyName);
                            if (!string.IsNullOrWhiteSpace(nestedValue))
                            {
                                return nestedValue;
                            }
                        }
                    }
                }
                else
                {
                    var nestedValue = FindStringProperty(property.Value, propertyName);
                    if (!string.IsNullOrWhiteSpace(nestedValue))
                    {
                        return nestedValue;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nestedValue = FindStringProperty(item, propertyName);
                if (!string.IsNullOrWhiteSpace(nestedValue))
                {
                    return nestedValue;
                }
            }
        }

        return null;
    }

    private static bool LooksLikeJson(string value)
    {
        var trimmed = value.TrimStart();
        return trimmed.StartsWith("{", StringComparison.Ordinal)
               || trimmed.StartsWith("[", StringComparison.Ordinal);
    }

    private static bool TryParseJsonDocument(string value, out JsonDocument document)
    {
        try
        {
            document = JsonDocument.Parse(value);
            return true;
        }
        catch
        {
            document = null!;
            return false;
        }
    }

    private static string Normalize(string rawConfig)
    {
        var normalized = rawConfig
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        return normalized + Environment.NewLine;
    }
}
