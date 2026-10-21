using System.Collections.Generic;
using Archie.Core.Rules;

namespace Archie.Core.Config;

/// <summary>The outcome of reading archie.json: a model when the file is valid, otherwise every error found.</summary>
public sealed class ConfigResult
{
    internal ConfigResult(ArchModel? model, IReadOnlyList<ConfigError> errors)
    {
        Model = model;
        Errors = errors;
    }

    /// <summary>The rules, or <see langword="null"/> when there are <see cref="Errors"/>: a partly understood config checks nothing.</summary>
    public ArchModel? Model { get; }

    /// <summary>Every problem found, in file order per section; empty when <see cref="Model"/> is set.</summary>
    public IReadOnlyList<ConfigError> Errors { get; }
}
