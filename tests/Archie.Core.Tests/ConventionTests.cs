using Archie.Core.Config;
using Archie.Core.Conventions;
using Archie.Core.Rules;

namespace Archie.Core.Tests;

public class ConventionTests
{
    [Test]
    [Arguments("*Handler", "OrderHandler", true)]
    [Arguments("*Handler", "Handler", true)]
    [Arguments("*Handler", "HandlerBase", false)]
    [Arguments("I*", "IOrder", true)]
    [Arguments("I*", "Order", false)]
    [Arguments("*Dto*", "OrderDtoV2", true)]
    [Arguments("*Dto*", "OrderDot", false)]
    [Arguments("Order*Handler", "OrderPlacedHandler", true)]
    [Arguments("Order*Handler", "OrderHandler", true)]
    [Arguments("A*A", "A", false)]
    [Arguments("*", "Anything", true)]
    [Arguments("Exact", "Exact", true)]
    [Arguments("Exact", "exact", false)]
    public void Name_patterns_match_the_whole_name(string pattern, string name, bool expected) =>
        NamePattern.TryParse(pattern, out _).ShouldNotBeNull().Matches(name).ShouldBe(expected);

    [Test]
    [Arguments("IRequestHandler<,>", null, "IRequestHandler", 2)]
    [Arguments("IRequestHandler<TRequest, TResponse>", null, "IRequestHandler", 2)]
    [Arguments("IHandler<>", null, "IHandler", 1)]
    [Arguments("MediatR.IRequestHandler`2", "MediatR", "IRequestHandler", 2)]
    [Arguments("EntityBase", null, "EntityBase", -1)]
    [Arguments("My.App.EntityBase", "My.App", "EntityBase", -1)]
    public void Type_references_keep_namespace_name_and_arity(string text, string? ns, string name, int arity)
    {
        var type = TypeReference.TryParse(text, isAttribute: false, out _).ShouldNotBeNull();

        type.Namespace.ShouldBe(ns);
        type.Name.ShouldBe(name);
        type.Arity.ShouldBe(arity);
    }

    [Test]
    public void A_type_reference_without_namespace_matches_any_namespace_and_a_full_name_only_its_own()
    {
        var shortName = TypeReference.TryParse("DbContext", isAttribute: false, out _)!;
        var fullName = TypeReference.TryParse("Vendor.Orm.DbContext", isAttribute: false, out _)!;

        shortName.Matches("DbContext", 0, "Anything").ShouldBeTrue();
        fullName.Matches("DbContext", 0, "Vendor.Orm").ShouldBeTrue();
        fullName.Matches("DbContext", 0, "App.Domain").ShouldBeFalse();
    }

    [Test]
    public void An_attribute_name_may_leave_off_Attribute()
    {
        var attribute = TypeReference.TryParse("ApiController", isAttribute: true, out _)!;

        attribute.Matches("ApiControllerAttribute", 0, "X").ShouldBeTrue();
        attribute.Matches("ApiController", 0, "X").ShouldBeTrue();
        attribute.Matches("ApiControllerAttributes", 0, "X").ShouldBeFalse();
        TypeReference.TryParse("ApiController", isAttribute: false, out _)!.Matches("ApiControllerAttribute", 0, "X").ShouldBeFalse();
    }

    [Test]
    public void The_model_keeps_conventions_in_order_with_their_requirements()
    {
        var model = Model("""
            {
              "rootNamespace": "App",
              "layers": ["Application", "Domain"],
              "conventions": [
                { "name": "Handlers", "types": { "inherit": "IRequestHandler<,>" }, "mustBeNamed": "*Handler", "mustBeIn": "Application" },
                { "types": { "in": ["Domain", "App.Shared"], "areNot": "abstract" }, "mustBe": ["sealed", "public"] },
              ],
            }
            """);

        model.Conventions.Count.ShouldBe(2);
        model.Conventions[0].Reference.ShouldBe("Convention 'Handlers'");
        model.Conventions[0].Requirements.Select(r => r.RuleKey).ShouldBe(["mustBeNamed=*Handler", "mustBeIn=Application"]);
        model.Conventions[0].Types.Description.ShouldBe("types that inherit 'IRequestHandler<,>'");
        model.Conventions[1].Reference.ShouldBe("conventions[1]");
        model.Conventions[1].Types.Description.ShouldBe("types in Domain or 'App.Shared' that are not abstract");
        model.Conventions[1].Requirements.Single().Describe().ShouldBe("must be public and sealed");
    }

    [Test]
    public void Without_types_a_convention_applies_to_every_checked_type() =>
        Model("""{ "conventions": [{ "mustNotBeNamed": "*Manager" }] }""").Conventions.Single().Types.Description.ShouldBe("all types");

    [Test]
    public void The_evaluator_reports_each_broken_requirement_with_a_message()
    {
        var evaluator = Evaluator("""
            {
              "rootNamespace": "App",
              "layers": ["Application", "Domain"],
              "conventions": [{ "name": "Events", "types": { "inherit": "IEvent" }, "mustBe": ["sealed", "record"], "mustBeIn": "Domain" }],
            }
            """);

        var violations = evaluator.Check(new Subject("OrderPlaced", "App.Application.Orders", TypeTraits.Class | TypeTraits.Public, inherits: ["IEvent"]));

        violations.Select(v => v.Id).ShouldBe(["ARCH105", "ARCH102"]);
        violations[0].Message.ShouldBe(
            "'OrderPlaced' must be sealed and record (it is not sealed or record). Convention 'Events' in archie.json applies to types that inherit 'IEvent'.");
        violations[1].Message.ShouldBe(
            "'OrderPlaced' must be in Domain, but is in Application (App.Application.Orders). Convention 'Events' in archie.json applies to types that inherit 'IEvent'.");
    }

    [Test]
    public void Types_that_are_not_selected_or_are_excluded_are_not_checked()
    {
        var evaluator = Evaluator("""
            {
              "exclude": ["App.Legacy"],
              "conventions": [{ "types": { "named": "*Repository", "are": "interface" }, "mustBeIn": "App.Domain.**" }],
            }
            """);

        evaluator.Check(new Subject("OrderRepository", "App.Data", TypeTraits.Class)).ShouldBeEmpty();
        evaluator.Check(new Subject("IOrderRepository", "App.Legacy.Data", TypeTraits.Interface)).ShouldBeEmpty();
        evaluator.Check(new Subject("IOrderRepository", "App.Domain.Orders", TypeTraits.Interface)).ShouldBeEmpty();
        evaluator.Check(new Subject("IOrderRepository", "", TypeTraits.Interface)).Single().Message.ShouldStartWith(
            "'IOrderRepository' must be in 'App.Domain.**', but is in the global namespace.");
    }

    [Test]
    public void MustNotInherit_names_what_the_type_does_inherit_when_there_are_several()
    {
        var evaluator = Evaluator("""{ "conventions": [{ "mustNotInherit": ["DbContext", "ControllerBase", "HttpClient"] }] }""");

        var violation = evaluator.Check(new Subject("Mixed", "App", TypeTraits.Class, inherits: ["DbContext", "HttpClient"])).Single();

        violation.Message.ShouldStartWith("'Mixed' must not inherit 'DbContext' or 'ControllerBase' or 'HttpClient' (it does: 'DbContext', 'HttpClient').");
        violation.Requirement.RuleKey.ShouldBe("mustNotInherit=DbContext,ControllerBase,HttpClient");
    }

    private static ArchModel Model(string text)
    {
        var result = ConfigParser.Parse(text);
        result.Errors.ShouldBeEmpty();
        return result.Model.ShouldNotBeNull();
    }

    private static ConventionEvaluator Evaluator(string text) => new(new ArchEvaluator(Model(text)));

    private sealed class Subject(string name, string ns, TypeTraits traits, string[]? inherits = null, string[]? attributes = null) : IConventionSubject
    {
        public string Name => name;

        public string DisplayName => name;

        public string Namespace => ns;

        public TypeTraits Traits => traits;

        public bool Inherits(TypeReference type) => (inherits ?? []).Any(i => type.Matches(i, 0, "Lib"));

        public bool HasAttribute(TypeReference attribute) => (attributes ?? []).Any(a => attribute.Matches(a, 0, "Lib"));
    }
}
