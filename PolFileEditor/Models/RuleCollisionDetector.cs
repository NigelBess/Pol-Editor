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
///
/// A collision (override) requires one rule's match set to be <em>contained</em> in the
/// other's — a mere partial overlap where neither side bounds the other (e.g. "Allow ICMP to
/// HQ" vs "Block from cn5", which only share cn5→HQ ICMP) is two independent rules, not an
/// override. Given containment:
/// <list type="bullet">
/// <item>Opposite verdicts: the Allow overrides the broader (or equal) Block and wins —
/// this is "Allow rules override a previously more broad Block rule".</item>
/// <item>The same verdict: the narrower (contained) rule is a shadow/carve-out of the broader
/// one and wins on specificity. Two identical sets are a duplicate, not an override.</item>
/// </list>
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

            // An override needs one rule's match set to be contained in the other's. A mere
            // partial overlap where neither side bounds the other (e.g. "Allow ICMP to HQ" vs
            // "Block from cn5") is two independent rules that happen to share some packets —
            // the Allow is not an exception carved out of that Block, so it overrides nothing.
            var aInsideB = Contains(b, a); // a's packets ⊆ b's packets
            var bInsideA = Contains(a, b); // b's packets ⊆ a's packets
            if (!aInsideB && !bInsideA)
                continue;

            if (IsAllow(a) != IsAllow(b))
            {
                // Opposite verdicts with containment: the Allow overrides a broader (or equal)
                // Block, per "Allow rules override a previously more broad Block rule".
                var (winner, loser, winnerRule) = IsAllow(a) ? (aHandle, bHandle, a) : (bHandle, aHandle, b);
                collisions.Add(new Collision<T>(winner, loser, CollisionReason.AllowTrumpsBlock, Specificity(winnerRule)));
            }
            else
            {
                // Same verdict: the narrower (contained) rule is a carve-out of the broader one
                // and wins on specificity. Two identical sets are a duplicate, not an override.
                if (aInsideB == bInsideA)
                    continue;

                var (winner, loser, winnerRule) = aInsideB ? (aHandle, bHandle, a) : (bHandle, aHandle, b);
                collisions.Add(new Collision<T>(winner, loser, CollisionReason.Specificity, Specificity(winnerRule)));
            }
        }
        return collisions;
    }

    /// <summary>True when two rules establish a precedence. In every case one rule's match set
    /// must be contained in the other's — a mere partial overlap where neither bounds the other
    /// is two independent rules, not an override. With opposite verdicts any containment (the
    /// Allow carving out of, or shadowing, the Block) is a collision; with the same verdict only
    /// strict containment is (identical sets are a duplicate, not an override).</summary>
    public static bool Collides(PolRule a, PolRule b)
    {
        var aInsideB = Contains(b, a);
        var bInsideA = Contains(a, b);
        if (!aInsideB && !bInsideA)
            return false;

        return IsAllow(a) != IsAllow(b) || aInsideB != bInsideA;
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

    /// <summary>
    /// True when every packet matched by <paramref name="inner"/> is also matched by
    /// <paramref name="outer"/> — i.e. <paramref name="inner"/>'s match set is a subset of
    /// <paramref name="outer"/>'s. This holds when, for every field <paramref name="outer"/>
    /// constrains, <paramref name="inner"/> constrains the same field to a value inside it
    /// (an unused field on <paramref name="outer"/> imposes nothing). Containment — not mere
    /// overlap — is what makes a narrower rule a shadow/carve-out of a broader one.
    /// </summary>
    public static bool Contains(PolRule outer, PolRule inner)
        => ExactContains(outer.MacSource, inner.MacSource)
        && ExactContains(outer.MacDest, inner.MacDest)
        && ExactContains(outer.IpProtocol, inner.IpProtocol)
        && PortContains(outer.PortSource, inner.PortSource)
        && PortContains(outer.PortDest, inner.PortDest)
        && CidrContains(outer.IpSource, inner.IpSource)
        && CidrContains(outer.IpDest, inner.IpDest);

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

    /// <summary>An exact field bounds the inner one when the outer is unused (bounds nothing)
    /// or both are specified and equal.</summary>
    private static bool ExactContains(string? outer, string? inner)
    {
        if (Validators.IsUnused(outer))
            return true;
        if (Validators.IsUnused(inner))
            return false;
        return string.Equals(outer!.Trim(), inner!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A port bounds the inner one when the outer is unused, or both parse to the same
    /// number (falls back to text equality for values that don't parse).</summary>
    private static bool PortContains(string? outer, string? inner)
    {
        if (Validators.IsUnused(outer))
            return true;
        if (Validators.IsUnused(inner))
            return false;
        if (int.TryParse(outer!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var po)
            && int.TryParse(inner!.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var pi))
            return po == pi;
        return string.Equals(outer!.Trim(), inner!.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A CIDR block bounds the inner one when the outer is unused, or the inner block
    /// is nested within it — the inner mask is at least as long (narrower) and their network
    /// portions agree under the outer's (broader) mask. An unparseable value bounds nothing.</summary>
    private static bool CidrContains(string? outer, string? inner)
    {
        if (Validators.IsUnused(outer))
            return true;
        if (Validators.IsUnused(inner))
            return false;
        if (!TryParse(outer, out var addrOuter, out var prefixOuter)
            || !TryParse(inner, out var addrInner, out var prefixInner))
            return false;
        if (prefixInner < prefixOuter)
            return false; // inner is broader than outer -> not contained

        var mask = MaskFor(prefixOuter);
        return (addrOuter & mask) == (addrInner & mask);
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
