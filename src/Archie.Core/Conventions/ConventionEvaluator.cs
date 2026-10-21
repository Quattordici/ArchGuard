using System;
using System.Collections.Generic;
using Archie.Core.Rules;

namespace Archie.Core.Conventions;

/// <summary>Checks types against the conventions of a config. All convention logic lives here, like <see cref="ArchEvaluator"/> for dependencies. Thread-safe.</summary>
public sealed class ConventionEvaluator
{
    private static readonly IReadOnlyList<ConventionViolation> None = Array.Empty<ConventionViolation>();

    private readonly ArchEvaluator _layers;

    /// <summary>Creates an evaluator for the conventions of <paramref name="layers"/>' model, sharing its namespace cache.</summary>
    public ConventionEvaluator(ArchEvaluator layers)
    {
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    /// <summary>The conventions being applied.</summary>
    public IReadOnlyList<Convention> Conventions => _layers.Model.Conventions;

    /// <summary>Every requirement <paramref name="subject"/> breaks, in config order; empty for excluded code.</summary>
    public IReadOnlyList<ConventionViolation> Check(IConventionSubject subject)
    {
        if (subject is null)
        {
            throw new ArgumentNullException(nameof(subject));
        }

        var ns = _layers.Classify(subject.Namespace);
        if (ns.IsExcluded)
        {
            return None;
        }

        List<ConventionViolation>? violations = null;
        foreach (var convention in Conventions)
        {
            if (!convention.Types.Matches(subject, ns))
            {
                continue;
            }

            foreach (var requirement in convention.Requirements)
            {
                if (!requirement.IsMet(subject, ns, out var detail))
                {
                    var message = $"'{subject.DisplayName}' {requirement.Describe()}{detail}. {convention.Reference} in archie.json applies to {convention.Types.Description}.";
                    (violations ??= []).Add(new ConventionViolation(convention, requirement, message));
                }
            }
        }

        return violations ?? None;
    }
}
