using System.Globalization;
using System.Text.RegularExpressions;

namespace PolFileEditor.Models;

/// <summary>Why one rule takes precedence over another in a collision.</summary>
public enum CollisionReason
{
    /// <summary>The rules disagree on the verdict; Allow beats Block.</summary>
    AllowTrumpsBlock,

    /// <summary>The rules share a verdict but one is more specific, so it wins.</summary>
    Specificity,
}

/// <summary>A reference to one side of a collision: the other rule's number and summary,
/// plus a human-readable reason for the override (e.g. "(Allow trumps Block)").</summary>
public readonly record struct RuleReference(string RuleNumber, string Summary, string Reason);

/// <summary>
/// A detected collision between two rules whose match sets overlap. The <see cref="Winner"/>
/// takes precedence over the <see cref="Loser"/>; <see cref="Reason"/> says why, and
/// <see cref="WinnerSpecificity"/> is the winning rule's specificity (meaningful when the
/// reason is <see cref="CollisionReason.Specificity"/>).
/// </summary>
/// <typeparam name="T">Caller's rule handle (e.g. the view-model), carried through untouched.</typeparam>
public sealed record Collision<T>(T Winner, T Loser, CollisionReason Reason, int WinnerSpecificity);

/// <summary>
/// Detects when two firewall rules can both apply to the same packet yet produce
/// different verdicts.
///
/// Two rules <em>overlap</em> when, for every match field, their specified values are
/// compatible (an unused field matches anything; two specified values must intersect).
/// An overlap is only a <em>collision</em> when the two actions differ — two Allows (or
/// two Blocks) reach the same verdict, so nothing is overridden.
///
/// Precedence: "Allow overrides Block" is absolute here, so in any collision the Allow
/// rule wins and the Block rule is overridden. (Specificity orders rules of the same
/// action and therefore never changes a collision's outcome.)
/// </summary>
public static partial class RuleCollisionDetector
{
    [GeneratedRegex(@"^(\d{1,3})\.(\d{1,3})\.(\d{1,3})\.(\d{1,3})/(\d{1,2})$")]
    private static partial Regex CidrRegex();

    /// <summary>
    /// Finds every colliding pair within <paramref name="rules"/> and returns them as
    /// winner/loser pairs. Pairs are examined in list order, so results are deterministic.
    /// Rules from every task are compared against each other (collisions are document-wide).
    /// </summary>
    public static IReadOnlyList<Collision<T>> Detect<T>(
        IReadOnlyList<(T Handle, PolRule Rule)> rules)
    {
        var collisions = new List<Collision<T>>();
        for (var i = 0; i < rules.Count; i++)
        for (var j = i + 1; j < rules.Count; j++)
        {
            var (aHandle, a) = rules[i];
            var (bHandle, b) = rules[j];

            if (!Overlaps(a, b))
                continue;

            if (IsAllow(a) != IsAllow(b))
            {
                // Different verdicts: Allow always wins, whatever the specificity.
                var (winner, loser, winnerRule) = IsAllow(a) ? (aHandle, bHandle, a) : (bHandle, aHandle, b);
                collisions.Add(new Collision<T>(winner, loser, CollisionReason.AllowTrumpsBlock, Specificity(winnerRule)));
            }
            else
            {
                // Same verdict: the more specific rule wins. Equal specificity is a true
                // duplicate/tie with no precedence, so it isn't reported as an override.
                var sa = Specificity(a);
                var sb = Specificity(b);
                if (sa == sb)
                    continue;

                var (winner, loser, winnerSpecificity) = sa > sb ? (aHandle, bHandle, sa) : (bHandle, aHandle, sb);
                collisions.Add(new Collision<T>(winner, loser, CollisionReason.Specificity, winnerSpecificity));
            }
        }
        return collisions;
    }

    /// <summary>True when two overlapping rules establish a precedence: either they disagree
    /// on the verdict (Allow beats Block) or they agree but differ in specificity.</summary>
    public static bool Collides(PolRule a, PolRule b)
    {
        if (!Overlaps(a, b))
            return false;
        return IsAllow(a) != IsAllow(b) || Specificity(a) != Specificity(b);
    }

    /// <summary>Number of specified (non-unused) match criteria — the rule's specificity.
    /// Mirrors <c>RuleViewModel.Specificity</c>.</summary>
    public static int Specificity(PolRule rule)
    {
        var count = 0;
        if (!Validators.IsUnused(rule.MacSource)) count++;
        if (!Validators.IsUnused(rule.MacDest)) count++;
        if (!Validators.IsUnused(rule.IpSource)) count++;
        if (!Validators.IsUnused(rule.IpDest)) count++;
        if (!Validators.IsUnused(rule.IpProtocol)) count++;
        if (!Validators.IsUnused(rule.PortSource)) count++;
        if (!Validators.IsUnused(rule.PortDest)) count++;
        return count;
    }

    /// <summary>
    /// True when some packet could match both rules — every match field is either unused on
    /// at least one side, or specified on both with intersecting values. Ignores the action.
    /// </summary>
    public static bool Overlaps(PolRule a, PolRule b)
        => ExactMatch(a.MacSource, b.MacSource)
        && ExactMatch(a.MacDest, b.MacDest)
        && ExactMatch(a.IpProtocol, b.IpProtocol)
        && PortMatch(a.PortSource, b.PortSource)
        && PortMatch(a.PortDest, b.PortDest)
        && CidrMatch(a.IpSource, b.IpSource)
        && CidrMatch(a.IpDest, b.IpDest);

    private static bool IsAllow(PolRule rule)
        => string.Equals(rule.Action?.Trim(), "Allow", StringComparison.OrdinalIgnoreCase);

    /// <summary>Two fields intersect when either is unused or both hold the same value.</summary>
    private static bool ExactMatch(string? x, string? y)
    {
        if (Validators.IsUnused(x) || Validators.IsUnused(y))
            return true;
        return string.Equals(x!.Trim(), y!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Ports intersect when either is unused or both parse to the same number
    /// (falls back to text comparison for values that don't parse).</summary>
    private static bool PortMatch(string? x, string? y)
    {
        if (Validators.IsUnused(x) || Validators.IsUnused(y))
            return true;
        if (int.TryParse(x!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var px)
            && int.TryParse(y!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var py))
            return px == py;
        return string.Equals(x!.Trim(), y!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// CIDR blocks intersect when either is unused, or the two ranges overlap. Aligned CIDR
    /// blocks are either disjoint or nested, so they overlap iff their network portions agree
    /// under the shorter (broader) mask. A value that can't be parsed is treated as
    /// non-overlapping to avoid raising a collision we can't actually reason about.
    /// </summary>
    private static bool CidrMatch(string? x, string? y)
    {
        if (Validators.IsUnused(x) || Validators.IsUnused(y))
            return true;
        if (!TryParse(x, out var addrX, out var prefixX) || !TryParse(y, out var addrY, out var prefixY))
            return false;

        var mask = MaskFor(Math.Min(prefixX, prefixY));
        return (addrX & mask) == (addrY & mask);
    }

    private static bool TryParse(string? cidr, out uint address, out int prefix)
    {
        address = 0;
        prefix = 0;
        if (string.IsNullOrWhiteSpace(cidr))
            return false;

        var match = CidrRegex().Match(cidr.Trim());
        if (!match.Success)
            return false;

        uint result = 0;
        for (var i = 0; i < 4; i++)
        {
            var octet = uint.Parse(match.Groups[i + 1].Value, CultureInfo.InvariantCulture);
            if (octet > 255)
                return false;
            result = (result << 8) | octet;
        }

        prefix = int.Parse(match.Groups[5].Value, CultureInfo.InvariantCulture);
        if (prefix > 32)
            return false;

        address = result;
        return true;
    }

    private static uint MaskFor(int prefix)
        => prefix == 0 ? 0u : uint.MaxValue << (32 - prefix);
}
