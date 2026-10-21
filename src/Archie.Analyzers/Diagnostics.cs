using Archie.Core.Config;
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

    public static DiagnosticDescriptor ForConfigError(string code) => code switch
    {
        ConfigParser.UnknownKey => UnknownKey,
        ConfigParser.InvalidValue => InvalidValue,
        _ => Unreadable,
    };

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
