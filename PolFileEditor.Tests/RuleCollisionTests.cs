using PolFileEditor.Models;
using Xunit;

namespace PolFileEditor.Tests;

public class RuleCollisionTests
{
    private static PolRule Rule(
        string action = "Block",
        string macSrc = "-", string macDst = "-",
        string ipSrc = "-", string ipDst = "-",
        string proto = "-", string portSrc = "-", string portDst = "-",
        string comment = "")
        => new()
        {
            Action = action,
            MacSource = macSrc, MacDest = macDst,
            IpSource = ipSrc, IpDest = ipDst,
            IpProtocol = proto, PortSource = portSrc, PortDest = portDst,
            Comment = comment,
        };

    // ---- Overlaps -----------------------------------------------------------

    [Fact]
    public void Two_match_all_rules_overlap()
        => Assert.True(RuleCollisionDetector.Overlaps(Rule(), Rule()));

    [Fact]
    public void Rules_disagreeing_on_a_specified_field_do_not_overlap()
    {
        var a = Rule(ipDst: "10.0.1.0/24", proto: "6", portDst: "443");
        var b = Rule(ipDst: "10.0.1.0/24", proto: "6", portDst: "80");
        Assert.False(RuleCollisionDetector.Overlaps(a, b));
    }

    [Fact]
    public void Unused_field_matches_a_specified_one()
    {
        // b constrains the protocol; a leaves it unused -> still overlaps.
        var a = Rule(ipDst: "10.0.1.0/24");
        var b = Rule(ipDst: "10.0.1.0/24", proto: "6");
        Assert.True(RuleCollisionDetector.Overlaps(a, b));
    }

    [Fact]
    public void Nested_cidrs_overlap_but_disjoint_ones_do_not()
    {
        var broad = Rule(ipDst: "10.0.0.0/16");
        var inside = Rule(ipDst: "10.0.1.0/24");
        var elsewhere = Rule(ipDst: "10.9.0.0/24");

        Assert.True(RuleCollisionDetector.Overlaps(broad, inside));
        Assert.False(RuleCollisionDetector.Overlaps(inside, elsewhere));
    }

    [Fact]
    public void Ports_compare_numerically()
    {
        var a = Rule(proto: "6", portDst: "443");
        var b = Rule(proto: "6", portDst: "0443");
        Assert.True(RuleCollisionDetector.Overlaps(a, b));
    }

    // ---- Specificity helper -------------------------------------------------

    [Fact]
    public void Specificity_counts_specified_match_fields()
    {
        Assert.Equal(0, RuleCollisionDetector.Specificity(Rule()));
        Assert.Equal(3, RuleCollisionDetector.Specificity(
            Rule(ipDst: "10.0.2.0/24", proto: "6", portDst: "443")));
    }

    // ---- Collides -----------------------------------------------------------

    [Fact]
    public void Same_action_same_specificity_overlap_is_not_a_collision()
    {
        // Identical verdict AND specificity -> a duplicate, not an override.
        var a = Rule("Block", ipDst: "10.0.1.0/24");
        var b = Rule("Block", ipDst: "10.0.1.0/24");
        Assert.False(RuleCollisionDetector.Collides(a, b));
    }

    [Fact]
    public void Same_action_different_specificity_overlap_is_a_collision()
    {
        // Same verdict, but one is more specific -> the specific one overrides.
        var broad = Rule("Block", ipDst: "10.0.0.0/16");
        var narrow = Rule("Block", ipDst: "10.0.1.0/24", proto: "6");
        Assert.True(RuleCollisionDetector.Collides(broad, narrow));
    }

    [Fact]
    public void Opposite_actions_that_overlap_collide()
    {
        var allow = Rule("Allow", ipDst: "10.0.1.0/24");
        var block = Rule("Block", ipDst: "10.0.1.0/24");
        Assert.True(RuleCollisionDetector.Collides(allow, block));
    }

    [Fact]
    public void Opposite_actions_that_do_not_overlap_do_not_collide()
    {
        var allow = Rule("Allow", ipDst: "10.0.1.0/24");
        var block = Rule("Block", ipDst: "10.9.0.0/24");
        Assert.False(RuleCollisionDetector.Collides(allow, block));
    }

    // ---- Detect: precedence and reasons -------------------------------------

    [Fact]
    public void Allow_wins_even_when_the_block_is_more_specific()
    {
        // Broad Allow (specificity 1) vs narrow Block (specificity 3) -> Allow still wins.
        var allow = Rule("Allow", ipDst: "10.0.1.0/24", comment: "permit subnet");
        var block = Rule("Block", ipDst: "10.0.1.0/24", proto: "6", portDst: "443", comment: "deny https");

        var rules = new[] { ("A", allow), ("B", block) };
        var collisions = RuleCollisionDetector.Detect(rules);

        var c = Assert.Single(collisions);
        Assert.Equal("A", c.Winner);
        Assert.Equal("B", c.Loser);
        Assert.Equal(CollisionReason.AllowTrumpsBlock, c.Reason);
    }

    [Fact]
    public void Same_action_more_specific_rule_wins_on_specificity()
    {
        var broad = Rule("Block", ipDst: "10.0.0.0/16");                 // specificity 1
        var narrow = Rule("Block", ipDst: "10.0.1.0/24", proto: "6");    // specificity 2

        var rules = new[] { ("broad", broad), ("narrow", narrow) };
        var c = Assert.Single(RuleCollisionDetector.Detect(rules));

        Assert.Equal("narrow", c.Winner);
        Assert.Equal("broad", c.Loser);
        Assert.Equal(CollisionReason.Specificity, c.Reason);
        Assert.Equal(2, c.WinnerSpecificity);
    }

    [Fact]
    public void Official_example_allow_overrides_block_despite_equal_specificity()
    {
        // The two example rules from the project description (Part 4):
        //   1,Block,-,-,10.0.0.1/32,10.0.1.0/24,6,-,80,...
        //   2,Allow,-,-,10.0.0.1/32,10.0.1.125/32,6,-,80,... "overriding rule"
        // Both match 4 items (equal specificity) and overlap (.125/32 is inside .1.0/24).
        // Per "Allow will override a Block rule", rule 2 must win regardless of specificity.
        var block = Rule("Block", ipSrc: "10.0.0.1/32", ipDst: "10.0.1.0/24", proto: "6", portDst: "80");
        var allow = Rule("Allow", ipSrc: "10.0.0.1/32", ipDst: "10.0.1.125/32", proto: "6", portDst: "80");

        Assert.Equal(4, RuleCollisionDetector.Specificity(block));
        Assert.Equal(4, RuleCollisionDetector.Specificity(allow));

        var rules = new[] { ("1", block), ("2", allow) };
        var c = Assert.Single(RuleCollisionDetector.Detect(rules));

        Assert.Equal("2", c.Winner);
        Assert.Equal("1", c.Loser);
        Assert.Equal(CollisionReason.AllowTrumpsBlock, c.Reason);
    }

    // ---- Containment: an override needs one match set to bound the other ----

    [Fact]
    public void Contains_requires_every_outer_field_to_bound_the_inner()
    {
        var outer = Rule("Block", ipDst: "10.0.0.0/16");
        var inner = Rule("Block", ipDst: "10.0.1.0/24", proto: "6");

        // inner's packets are a strict subset of outer's -> outer contains inner.
        Assert.True(RuleCollisionDetector.Contains(outer, inner));
        Assert.False(RuleCollisionDetector.Contains(inner, outer));
    }

    [Fact]
    public void Contains_is_false_when_neither_set_bounds_the_other()
    {
        // Task 1: block outgoing TCP from cn4.   Task 2: isolate cn5 (block traffic to it).
        var fromCn4 = Rule("Block", ipSrc: "10.0.30.4/32", proto: "6");
        var toCn5   = Rule("Block", ipDst: "10.0.30.5/32");

        Assert.False(RuleCollisionDetector.Contains(fromCn4, toCn5));
        Assert.False(RuleCollisionDetector.Contains(toCn5, fromCn4));
    }

    // ---- The reported bug: unrelated same-action rules must not override ----

    [Fact]
    public void Same_action_partial_overlap_without_containment_is_not_an_override()
    {
        // "Block outgoing TCP from cn4" (source + proto) and "Block incoming to cn5" (dest)
        // only co-apply to the single cn4 -> cn5 TCP flow. Neither rule's match set contains
        // the other's and both Block, so there is no override between them.
        var fromCn4 = Rule("Block", ipSrc: "10.0.30.4/32", proto: "6");   // Task 1
        var toCn5   = Rule("Block", ipDst: "10.0.30.5/32");               // Task 2

        // They do share packets (a cn4 -> cn5 TCP packet matches both)...
        Assert.True(RuleCollisionDetector.Overlaps(fromCn4, toCn5));
        // ...but that partial overlap is not a collision.
        Assert.False(RuleCollisionDetector.Collides(fromCn4, toCn5));

        var rules = new[] { ("1.1", fromCn4), ("2.1", toCn5) };
        Assert.Empty(RuleCollisionDetector.Detect(rules));
    }

    [Fact]
    public void Opposite_action_partial_overlap_is_not_an_override()
    {
        // "Allow ICMP to HQ" and "Block outgoing from cn5" only co-apply to cn5 -> HQ ICMP.
        // The Allow is NOT an exception carved out of that Block (it isn't contained in it),
        // so per "Allow rules override a previously more BROAD Block rule" there is no override.
        var allowIcmpToHq = Rule("Allow", ipDst: "10.0.0.0/24", proto: "1");  // Task 3
        var blockFromCn5  = Rule("Block", ipSrc: "10.0.30.5/32");             // Task 2

        Assert.True(RuleCollisionDetector.Overlaps(allowIcmpToHq, blockFromCn5)); // share packets
        Assert.False(RuleCollisionDetector.Collides(allowIcmpToHq, blockFromCn5)); // but no override

        var rules = new[] { ("1.0", allowIcmpToHq), ("2.0", blockFromCn5) };
        Assert.Empty(RuleCollisionDetector.Detect(rules));
    }

    [Fact]
    public void Allow_carved_out_of_a_broader_block_is_a_real_override()
    {
        // "Allow ICMP to HQ" IS contained in "Block all ICMP" -> it carves HQ out of the
        // blanket block, exactly the intended Allow-overrides-broad-Block relationship.
        var allowIcmpToHq = Rule("Allow", ipDst: "10.0.0.0/24", proto: "1");  // Task 3
        var blockAllIcmp  = Rule("Block", proto: "1");                        // broad ICMP block

        Assert.True(RuleCollisionDetector.Collides(allowIcmpToHq, blockAllIcmp));

        var rules = new[] { ("1.0", allowIcmpToHq), ("3.5", blockAllIcmp) };
        var c = Assert.Single(RuleCollisionDetector.Detect(rules));
        Assert.Equal("1.0", c.Winner);
        Assert.Equal("3.5", c.Loser);
        Assert.Equal(CollisionReason.AllowTrumpsBlock, c.Reason);
    }

    [Fact]
    public void Nested_cidrs_of_equal_field_count_still_override()
    {
        // Both specify exactly one field, so the crude field-count "specificity" calls them
        // equal -- yet /24 is strictly inside /16, a real shadow that must be reported.
        var broad  = Rule("Block", ipDst: "10.0.0.0/16");    // specificity 1
        var narrow = Rule("Block", ipDst: "10.0.1.0/24");    // specificity 1

        Assert.True(RuleCollisionDetector.Collides(broad, narrow));

        var c = Assert.Single(RuleCollisionDetector.Detect(new[] { ("broad", broad), ("narrow", narrow) }));
        Assert.Equal("narrow", c.Winner);
        Assert.Equal("broad", c.Loser);
        Assert.Equal(CollisionReason.Specificity, c.Reason);
    }

    [Fact]
    public void Independent_rules_produce_no_collisions()
    {
        var rules = new[]
        {
            ("A", Rule("Allow", ipDst: "10.0.1.0/24")),
            ("B", Rule("Block", ipDst: "10.9.0.0/24")),
        };
        Assert.Empty(RuleCollisionDetector.Detect(rules));
    }

    [Fact]
    public void One_rule_can_override_several_others()
    {
        // A broad Allow overrides two narrower, overlapping Blocks.
        var rules = new[]
        {
            ("1", Rule("Allow", ipDst: "10.0.0.0/16", comment: "allow all")),
            ("2", Rule("Block", ipDst: "10.0.1.0/24")),
            ("3", Rule("Block", ipDst: "10.0.2.0/24")),
        };

        var collisions = RuleCollisionDetector.Detect(rules);
        Assert.Equal(2, collisions.Count);
        Assert.All(collisions, c => Assert.Equal("1", c.Winner));
        Assert.Contains(collisions, c => c.Loser == "2");
        Assert.Contains(collisions, c => c.Loser == "3");
    }
}
