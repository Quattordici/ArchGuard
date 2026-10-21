namespace Archie.Core.Rules;

/// <summary>Which rule, if any, a dependency breaks.</summary>
public enum VerdictKind
{
    /// <summary>Allowed, or not checked (no layer, excluded).</summary>
    None,

    /// <summary>The target is in a layer the source's layer may not depend on (ARCH001).</summary>
    LayerNotAllowed,

    /// <summary>The target is in a namespace the source's layer must not depend on (ARCH002).</summary>
    ForbiddenNamespace,
}

/// <summary>The outcome of checking one dependency.</summary>
public readonly struct Verdict(VerdictKind kind, int sourceLayer, int targetLayer)
{
    /// <summary>Allowed.</summary>
    public static Verdict None { get; } = new(VerdictKind.None, -1, -1);

    /// <summary>Which rule is broken, if any.</summary>
    public VerdictKind Kind { get; } = kind;

    /// <summary>Whether a rule is broken.</summary>
    public bool IsViolation => Kind != VerdictKind.None;

    /// <summary>Index of the source's layer, or -1.</summary>
    public int SourceLayer { get; } = sourceLayer;

    /// <summary>Index of the target's layer, or -1 when it is in no layer.</summary>
    public int TargetLayer { get; } = targetLayer;
}
