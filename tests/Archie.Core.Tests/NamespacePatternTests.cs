using Archie.Core.Rules;

namespace Archie.Core.Tests;

public class NamespacePatternTests
{
    [Test]
    [Arguments("MyApp.Domain", "MyApp.Domain", true)]
    [Arguments("MyApp.Domain", "MyApp.Domain.Orders.Events", true)]
    [Arguments("MyApp.Domain", "MyApp.DomainEvents", false)]
    [Arguments("MyApp.Domain", "MyApp", false)]
    [Arguments("MyApp.Domain.**", "MyApp.Domain", true)]
    [Arguments("MyApp.*.Domain", "MyApp.Orders.Domain", true)]
    [Arguments("MyApp.*.Domain", "MyApp.Orders.Domain.Events", true)]
    [Arguments("MyApp.*.Domain", "MyApp.Domain", false)]
    [Arguments("MyApp.**.Domain", "MyApp.Domain", true)]
    [Arguments("MyApp.**.Domain", "MyApp.A.B.Domain.C", true)]
    [Arguments("**.Domain", "Any.Where.Domain", true)]
    [Arguments("MyApp.Domain", "myapp.domain", false)]
    [Arguments("MyApp", "", false)]
    public void Matches_segment_by_segment_and_includes_the_subtree(string pattern, string @namespace, bool expected)
    {
        var segments = @namespace.Length == 0 ? [] : @namespace.Split('.');

        Parse(pattern).Matches(segments).ShouldBe(expected);
    }

    [Test]
    [Arguments("MyApp.Domain", 2)]
    [Arguments("MyApp.*.Domain.**", 2)]
    [Arguments("**", 0)]
    public void Specificity_is_the_number_of_literal_segments(string pattern, int specificity) =>
        Parse(pattern).Specificity.ShouldBe(specificity);

    [Test]
    [Arguments("MyApp.Domain", "MyApp.Infrastructure", false)]
    [Arguments("MyApp.Domain", "MyApp.Domain.Legacy", true)]
    [Arguments("MyApp.*.Domain", "MyApp.Orders.*", true)]
    [Arguments("MyApp.*.Domain", "MyApp.Orders.Api", false)]
    [Arguments("MyApp.**.Domain", "MyApp.Orders.Api", true)]
    [Arguments("A.*", "B.*", false)]
    public void Overlaps_when_some_namespace_matches_both(string first, string second, bool expected)
    {
        Parse(first).Overlaps(Parse(second)).ShouldBe(expected);
        Parse(second).Overlaps(Parse(first)).ShouldBe(expected);
    }

    [Test]
    [Arguments("", "it is empty")]
    [Arguments("MyApp..Domain", "it has an empty segment (two dots, or a dot at the start or end)")]
    [Arguments("MyApp.Domain.", "it has an empty segment (two dots, or a dot at the start or end)")]
    [Arguments("MyApp.Dom*", "'Dom*' mixes a name and '*'; a segment is either a name, '*' (one segment) or '**' (any number)")]
    [Arguments("MyApp.1Domain", "'1Domain' is not a valid namespace name")]
    public void Rejects_invalid_patterns_with_a_reason(string pattern, string reason)
    {
        NamespacePattern.TryParse(pattern, out var error).ShouldBeNull();
        error.ShouldBe(reason);
    }

    private static NamespacePattern Parse(string text) => NamespacePattern.TryParse(text, out _).ShouldNotBeNull();
}
