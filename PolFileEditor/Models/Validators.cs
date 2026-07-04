using System.Globalization;
using System.Text.RegularExpressions;

namespace PolFileEditor.Models;

/// <summary>Severity of a field validation result.</summary>
public enum Severity
{
    Ok,
    Warning,
    Error
}

/// <summary>The outcome of validating a single field value.</summary>
public readonly record struct ValidationResult(Severity Severity, string Message)
{
    public static readonly ValidationResult Valid = new(Severity.Ok, string.Empty);
}

/// <summary>
/// Field validators for a .pol firewall rule. Every field treats an empty string or
/// "-" as "unused" (always valid). All values are strings, matching the .pol format.
/// </summary>
public static partial class Validators
{
    [GeneratedRegex(@"^([0-9A-Fa-f]{2}:){5}[0-9A-Fa-f]{2}$")]
    private static partial Regex MacRegex();

    [GeneratedRegex(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})/(\d{1,2})$")]
    private static partial Regex CidrRegex();

    /// <summary>True when the field is unused ("-" or blank).</summary>
    public static bool IsUnused(string? value)
        => string.IsNullOrWhiteSpace(value) || value.Trim() == "-";

    public static ValidationResult Mac(string? value)
    {
        if (IsUnused(value)) return ValidationResult.Valid;

        return MacRegex().IsMatch(value!.Trim())
            ? ValidationResult.Valid
            : new ValidationResult(Severity.Error,
                "MAC address must be in the form xx:xx:xx:xx:xx:xx (hex), or '-' if unused.");
    }

    public static ValidationResult Cidr(string? value)
    {
        if (IsUnused(value)) return ValidationResult.Valid;

        var text = value!.Trim();
        var match = CidrRegex().Match(text);
        if (!match.Success)
        {
            return new ValidationResult(Severity.Error,
                "IP must be in CIDR notation a.b.c.d/nn (e.g. 10.0.1.0/24), or '-' if unused.");
        }

        var octets = new int[4];
        for (var i = 0; i < 4; i++)
        {
            octets[i] = int.Parse(match.Groups[i + 1].Value, CultureInfo.InvariantCulture);
            if (octets[i] > 255)
            {
                return new ValidationResult(Severity.Error,
                    $"IP octet '{octets[i]}' is out of range (each octet must be 0-255).");
            }
        }

        var prefix = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        if (prefix > 32)
        {
            return new ValidationResult(Severity.Error,
                $"Subnet mask /{prefix} is invalid (must be between /0 and /32).");
        }

        // Verify this is a proper NETWORK address: host bits (below the mask) must be zero.
        var address = (uint)((octets[0] << 24) | (octets[1] << 16) | (octets[2] << 8) | octets[3]);
        var mask = prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
        var network = address & mask;
        if (network != address)
        {
            var suggestion = $"{(network >> 24) & 0xFF}.{(network >> 16) & 0xFF}.{(network >> 8) & 0xFF}.{network & 0xFF}/{prefix}";
            return new ValidationResult(Severity.Error,
                $"'{text}' is not a valid network address for /{prefix}: host bits must be zero. Did you mean {suggestion}?");
        }

        if (prefix < 24)
        {
            return new ValidationResult(Severity.Warning,
                $"Subnet /{prefix} is broader than /24 and covers {(1L << (32 - prefix))} addresses. Verify this is intended.");
        }

        return ValidationResult.Valid;
    }

    public static ValidationResult Protocol(string? value)
    {
        if (IsUnused(value)) return ValidationResult.Valid;

        if (int.TryParse(value!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var proto)
            && proto is >= 0 and <= 254)
        {
            return ValidationResult.Valid;
        }

        return new ValidationResult(Severity.Error,
            "IP protocol must be an integer 0-254 (e.g. 1=ICMP, 6=TCP, 17=UDP), or '-' if unused.");
    }

    public static ValidationResult Port(string? value)
    {
        if (IsUnused(value)) return ValidationResult.Valid;

        if (int.TryParse(value!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var port)
            && port is >= 0 and <= 65535)
        {
            return ValidationResult.Valid;
        }

        return new ValidationResult(Severity.Error,
            "Port must be an integer 0-65535, or '-' if unused.");
    }
}
