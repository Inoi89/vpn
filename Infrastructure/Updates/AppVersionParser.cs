using System.Globalization;
using System.Reflection;

namespace VpnClient.Infrastructure.Updates;

public static class AppVersionParser
{
    public static string GetCurrentVersion(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return informationalVersion.Split('+', 2, StringSplitOptions.TrimEntries)[0];
        }

        return assembly.GetName().Version?.ToString() ?? "0.0.0";
    }

    public static bool IsNewerVersion(string candidate, string current)
    {
        return Compare(candidate, current) > 0;
    }

    public static int Compare(string left, string right)
    {
        var leftVersion = Parse(left);
        var rightVersion = Parse(right);
        return leftVersion.CompareTo(rightVersion);
    }

    private static ComparableVersion Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new ComparableVersion([0, 0, 0, 0], string.Empty);
        }

        var match = System.Text.RegularExpressions.Regex.Match(
            value.Trim(),
            "^(?<numbers>\\d+(?:\\.\\d+){0,3})(?<suffix>.*)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            return new ComparableVersion([0, 0, 0, 0], value.Trim());
        }

        var numericParts = match.Groups["numbers"].Value
            .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .ToList();

        while (numericParts.Count < 4)
        {
            numericParts.Add(0);
        }

        return new ComparableVersion(numericParts.ToArray(), match.Groups["suffix"].Value.Trim());
    }

    private readonly record struct ComparableVersion(int[] Parts, string Suffix) : IComparable<ComparableVersion>
    {
        public int CompareTo(ComparableVersion other)
        {
            for (var index = 0; index < Parts.Length; index++)
            {
                var comparison = Parts[index].CompareTo(other.Parts[index]);
                if (comparison != 0)
                {
                    return comparison;
                }
            }

            var hasSuffix = !string.IsNullOrWhiteSpace(Suffix);
            var otherHasSuffix = !string.IsNullOrWhiteSpace(other.Suffix);

            if (hasSuffix == otherHasSuffix)
            {
                return string.Compare(Suffix, other.Suffix, StringComparison.OrdinalIgnoreCase);
            }

            return hasSuffix ? -1 : 1;
        }
    }
}
