namespace Archie.Core.Config;

/// <summary>A problem in archie.json, with the span it applies to. Never thrown: <see cref="ConfigParser.Parse"/> returns them.</summary>
public sealed class ConfigError
{
    internal ConfigError(string code, string message, int start, int length, int line, int column)
    {
        Code = code;
        Message = message;
        Start = start;
        Length = length;
        Line = line;
        Column = column;
    }

    /// <summary>The diagnostic ID: <see cref="ConfigParser.Unreadable"/>, <see cref="ConfigParser.UnknownKey"/> or <see cref="ConfigParser.InvalidValue"/>.</summary>
    public string Code { get; }

    /// <summary>What is wrong and, where it can be guessed, how to fix it.</summary>
    public string Message { get; }

    /// <summary>Offset of the first character in the text (UTF-16 code units).</summary>
    public int Start { get; }

    /// <summary>Length of the span in UTF-16 code units; 0 at the end of the file.</summary>
    public int Length { get; }

    /// <summary>1-based.</summary>
    public int Line { get; }

    /// <summary>1-based, in UTF-16 code units.</summary>
    public int Column { get; }

    /// <inheritdoc />
    public override string ToString() => $"({Line},{Column}): {Code}: {Message}";
}
