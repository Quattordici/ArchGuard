using Archie.Core.Config;
using Archie.Core.Rules;

namespace Archie.Core.Tests;

public class ArchEvaluatorTests
{
    private static readonly ArchEvaluator Evaluator = new(ConfigParser.Parse("""
        {
          "rootNamespace": "MyApp",
          "layers": [
            "Api",
            "Infrastructure",
            "Application",
            "Domain",
            { "name": "Legacy", "namespaces": "MyApp.Domain.Legacy" },
          ],
          "mustNotDependOn": { "Domain": ["Microsoft.EntityFrameworkCore", "MyApp.Infrastructure.Ef"] },
          "exclude": ["MyApp.Domain.Generated"],
        }
        """).Model!);

    [Test]
    [Arguments("MyApp.Domain.Orders", "MyApp.Infrastructure.Persistence", VerdictKind.LayerNotAllowed)]
    [Arguments("MyApp.Domain", "MyApp.Application", VerdictKind.LayerNotAllowed)]
    [Arguments("MyApp.Application", "MyApp.Domain.Orders", VerdictKind.None)]
    [Arguments("MyApp.Api", "MyApp.Infrastructure", VerdictKind.None)]
    [Arguments("MyApp.Domain.Orders", "MyApp.Domain.Customers", VerdictKind.None)]
    [Arguments("MyApp.Domain", "Microsoft.EntityFrameworkCore.Metadata", VerdictKind.ForbiddenNamespace)]
    [Arguments("MyApp.Application", "Microsoft.EntityFrameworkCore", VerdictKind.None)]
    [Arguments("MyApp.Domain", "System.Collections.Generic", VerdictKind.None)]
    [Arguments("MyApp.Domain", "MyApp.Shared", VerdictKind.None)]
    [Arguments("MyApp.Domain", "", VerdictKind.None)]
    [Arguments("MyApp.Shared", "MyApp.Infrastructure", VerdictKind.None)]
    [Arguments("", "MyApp.Infrastructure", VerdictKind.None)]
    [Arguments("MyApp.Domain.Generated", "MyApp.Infrastructure", VerdictKind.None)]
    [Arguments("MyApp.Application", "MyApp.Domain.Generated", VerdictKind.None)]
    [Arguments("MyApp.Domain.Legacy", "MyApp.Domain", VerdictKind.LayerNotAllowed)]
    [Arguments("MyApp.Domain", "MyApp.Domain.Legacy.Orders", VerdictKind.None)]
    [Arguments("MyApp.Application", "MyApp.Domain.Legacy", VerdictKind.None)]
    public void Checks_a_dependency_edge(string source, string target, VerdictKind expected)
    {
        var edge = new DependencyEdge("M:Source.Member", source, "T:Target.Type", target, DependencyKind.Invocation);

        Evaluator.Check(edge).Kind.ShouldBe(expected);
    }

    [Test]
    public void A_layer_dependency_wins_over_a_forbidden_namespace_and_names_both_layers()
    {
        var verdict = Evaluator.Check(new DependencyEdge("M:S", "MyApp.Domain", "T:T", "MyApp.Infrastructure.Ef", DependencyKind.Field));

        verdict.Kind.ShouldBe(VerdictKind.LayerNotAllowed);
        Evaluator.Model.Layers[verdict.SourceLayer].Name.ShouldBe("Domain");
        Evaluator.Model.Layers[verdict.TargetLayer].Name.ShouldBe("Infrastructure");
    }

    [Test]
    public void The_most_specific_pattern_decides_the_layer()
    {
        Evaluator.Classify("MyApp.Domain.Legacy.Orders").Layer.ShouldBe(4);
        Evaluator.Classify("MyApp.Domain.LegacyOrders").Layer.ShouldBe(3);
    }

    [Test]
    public void Excluded_and_unlayered_code_is_not_a_checked_source()
    {
        Evaluator.Classify("MyApp.Domain.Generated").IsCheckedSource.ShouldBeFalse();
        Evaluator.Classify("MyApp.Shared").IsCheckedSource.ShouldBeFalse();
        Evaluator.Classify("MyApp.Domain").IsCheckedSource.ShouldBeTrue();
    }

    [Test]
    public void Describes_what_a_layer_may_depend_on()
    {
        Evaluator.DescribeAllowed(0).ShouldBe("Infrastructure, Application, Domain, Legacy");
        Evaluator.DescribeAllowed(4).ShouldBe("nothing");
    }
}
