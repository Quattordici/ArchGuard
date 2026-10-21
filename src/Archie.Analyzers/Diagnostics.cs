using Archie.Core.Config;
using Archie.Core.Conventions;
using Microsoft.CodeAnalysis;

namespace Archie.Analyzers;

internal static class Diagnostics
{
    public const string Category = "Archie";

    /// <summary>
    /// Default severity of ARCH001 and ARCH002, the rules themselves. Error, so a project with the package and an archie.json fails the build,
    /// and so CI, on a new violation without further setup (plan §14). Changing it also needs AnalyzerReleases.Unshipped.md and the README.
    /// </summary>
    public const DiagnosticSeverity RuleSeverity = DiagnosticSeverity.Error;

    private const string HelpRoot = "https://github.com/Quattordici/Archie/blob/main/docs/diagnostics/";

    public static readonly DiagnosticDescriptor LayerDependency = new(
        id: "ARCH001",
        title: "Layer dependency not allowed",
        messageFormat: "{0} must not depend on {1}: '{2}' uses '{3}' ({4}). {0} may depend on: {5}.",
        category: Category,
        defaultSeverity: RuleSeverity,
        isEnabledByDefault: true,
        description: "Code in one layer uses a type from a layer that is not in its mayDependOn (or, in the list form, not below it in archie.json).",
        helpLinkUri: HelpRoot + "ARCH001.md");

    public static readonly DiagnosticDescriptor ForbiddenNamespace = new(
        id: "ARCH002",
        title: "Forbidden namespace used",
        messageFormat: "{0} must not depend on '{1}': '{2}' uses '{3}'. Forbidden by mustNotDependOn in archie.json.",
        category: Category,
        defaultSeverity: RuleSeverity,
        isEnabledByDefault: true,
        description: "Code in a layer uses a type from a namespace that the layer's mustNotDependOn in archie.json names.",
        helpLinkUri: HelpRoot + "ARCH002.md");

    public static readonly DiagnosticDescriptor Naming = Convention(ConventionIds.Naming, "Type name breaks a convention",
        "A type's name does not match mustBeNamed, or matches mustNotBeNamed, of a convention in archie.json that applies to it.");

    public static readonly DiagnosticDescriptor Placement = Convention(ConventionIds.Placement, "Type is in the wrong place",
        "A type is not in a layer or namespace of mustBeIn, or is in one of mustNotBeIn, of a convention in archie.json that applies to it.");

    public static readonly DiagnosticDescriptor Inheritance = Convention(ConventionIds.Inheritance, "Type's base types break a convention",
        "A type does not inherit what mustInherit names, or inherits what mustNotInherit names (base classes and interfaces), of a convention in archie.json.");

    public static readonly DiagnosticDescriptor Attribute = Convention(ConventionIds.Attribute, "Type's attributes break a convention",
        "A type does not have an attribute that mustHaveAttribute names, or has one that mustNotHaveAttribute names, of a convention in archie.json.");

    public static readonly DiagnosticDescriptor KindAndModifiers = Convention(ConventionIds.KindAndModifiers, "Type's kind or modifiers break a convention",
        "A type is not what mustBe says (class, sealed, internal, ...), or is what mustNotBe says, in a convention in archie.json that applies to it.");

    public static readonly DiagnosticDescriptor Unreadable = Config(ConfigParser.Unreadable, "archie.json could not be read");

    public static readonly DiagnosticDescriptor UnknownKey = Config(ConfigParser.UnknownKey, "Unknown or unsupported key in archie.json");

    public static readonly DiagnosticDescriptor InvalidValue = Config(ConfigParser.InvalidValue, "Invalid value in archie.json");

    public static readonly DiagnosticDescriptor NoConfig = new(
        id: "ARCH909",
        title: "No archie.json found",
        messageFormat: "Archie is installed but found no archie.json; add one next to your .sln",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpRoot + "ARCH909.md",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    public static DiagnosticDescriptor ForConvention(string id) => id switch
    {
        ConventionIds.Naming => Naming,
        ConventionIds.Placement => Placement,
        ConventionIds.Inheritance => Inheritance,
        ConventionIds.Attribute => Attribute,
        _ => KindAndModifiers,
    };

    public static DiagnosticDescriptor ForConfigError(string code) => code switch
    {
        ConfigParser.UnknownKey => UnknownKey,
        ConfigParser.InvalidValue => InvalidValue,
        _ => Unreadable,
    };

    // Conventions default to error like ARCH001/ARCH002 (decided 2026-10-21); the message is built by Core so every front-end says the same.
    private static DiagnosticDescriptor Convention(string id, string title, string description) => new(
        id: id,
        title: title,
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: RuleSeverity,
        isEnabledByDefault: true,
        description: description,
        helpLinkUri: HelpRoot + id + ".md");

    // Config errors are always errors: a config that is not understood checks nothing, which must not pass quietly.
    private static DiagnosticDescriptor Config(string id, string title) => new(
        id: id,
        title: title,
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpRoot + id + ".md");
}
