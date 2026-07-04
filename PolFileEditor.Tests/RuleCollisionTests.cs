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
