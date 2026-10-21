using System.Collections.Generic;

namespace Archie.Core.Conventions;

/// <summary>One entry of <c>conventions</c>: which types it applies to, and what they must be.</summary>
public sealed class Convention
{
    internal Convention(int index, string? name, TypeSelector types, IReadOnlyList<Requirement> requirements)
    {
        Index = index;
        Name = name;
        Types = types;
        Requirements = requirements;
        Reference = name is null ? $"conventions[{index}]" : $"Convention '{name}'";
    }

    /// <summary>Position in <c>conventions</c>.</summary>
    public int Index { get; }

    /// <summary>The optional <c>name</c>.</summary>
    public string? Name { get; }

    /// <summary>Which types it applies to.</summary>
    public TypeSelector Types { get; }

    /// <summary>The <c>must…</c> keys, in config order; at least one.</summary>
    public IReadOnlyList<Requirement> Requirements { get; }

    /// <summary>For messages: "Convention 'Handlers'" or "conventions[2]".</summary>
    public string Reference { get; }
}

/// <summary>A type that breaks a requirement of a convention.</summary>
public sealed class ConventionViolation
{
    internal ConventionViolation(Convention convention, Requirement requirement, string message)
    {
        Convention = convention;
        Requirement = requirement;
        Message = message;
    }

    /// <summary>The convention.</summary>
    public Convention Convention { get; }

    /// <summary>The requirement that is not met.</summary>
    public Requirement Requirement { get; }

    /// <summary>The diagnostic ID.</summary>
    public string Id => Requirement.Id;

    /// <summary>
    /// The whole message, built here so every front-end says the same: "'OrderProcessor' must be named '*Handler'. Convention 'Handlers' in
    /// archie.json applies to types that inherit 'IRequestHandler&lt;,&gt;'."
    /// </summary>
    public string Message { get; }
}
