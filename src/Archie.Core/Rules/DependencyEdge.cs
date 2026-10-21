namespace Archie.Core.Rules;

/// <summary>
/// One use of a type by a member or type: the only thing a front-end hands to the rules. The analyzer builds edges from symbols and
/// operations; the metadata backend (Phase 4) builds the same edges from IL. <see cref="SourceId"/> and <see cref="TargetTypeId"/> are
/// documentation comment IDs, which both can produce, so together with the diagnostic ID they are the stable key of a violation (baseline,
/// <c>verify</c>, conformance tests).
/// </summary>
public readonly struct DependencyEdge(string sourceId, string sourceNamespace, string targetTypeId, string targetNamespace, DependencyKind kind)
{
    /// <summary>Doc-comment ID of the member that has the dependency (<c>M:</c>, <c>F:</c>, <c>P:</c>, <c>E:</c>), or of the type for declaration-level uses (<c>T:</c>).</summary>
    public string SourceId { get; } = sourceId;

    /// <summary>Namespace of the source's outermost type; empty for the global namespace.</summary>
    public string SourceNamespace { get; } = sourceNamespace;

    /// <summary>Doc-comment ID of the used type's definition (<c>T:App.Infrastructure.Repository`1</c>).</summary>
    public string TargetTypeId { get; } = targetTypeId;

    /// <summary>Namespace of the used type; empty for the global namespace.</summary>
    public string TargetNamespace { get; } = targetNamespace;

    /// <summary>How the target is used.</summary>
    public DependencyKind Kind { get; } = kind;

    /// <inheritdoc />
    public override string ToString() => $"{SourceId} -> {TargetTypeId} ({Kind})";
}
