using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Archie.Core.Json;

/// <summary>
/// Reads JSON with comments (<c>//</c>, <c>/* */</c>) and trailing commas into <see cref="JsonNode"/>s that keep their position.
/// Hand-written instead of System.Text.Json: an analyzer cannot rely on the System.Text.Json version its host loads, and config errors need
/// the span of every key and value.
/// </summary>
internal sealed class JsoncReader
{
    // Deep enough for any archie.json, shallow enough that hostile input can't overflow the compiler's stack.
    private const int MaxDepth = 64;

    private readonly string _text;
    private int _position;
    private int _depth;

    private JsoncReader(string text)
    {
        _text = text;
    }

    /// <summary>Parses <paramref name="text"/>; throws <see cref="JsonSyntaxException"/> with the position of the first error.</summary>
    public static JsonNode Parse(string text)
    {
        var reader = new JsoncReader(text);
        if (text.Length > 0 && text[0] == '﻿')
        {
            reader._position = 1;
        }

        reader.SkipTrivia();
        if (reader.AtEnd)
        {
            throw new JsonSyntaxException("archie.json is empty. It needs at least { \"layers\": [...] }.", 0, 0);
        }

        var value = reader.ReadValue();
        reader.SkipTrivia();
        if (!reader.AtEnd)
        {
            throw reader.Error("Unexpected text after the end of the JSON value.");
        }

        return value;
    }

    private bool AtEnd => _position >= _text.Length;

    private char Current => _text[_position];

    private JsonNode ReadValue()
    {
        if (AtEnd)
        {
            throw Error("Unexpected end of file; a value was expected.");
        }

        switch (Current)
        {
            case '{':
                return Nested(ReadObject);
            case '[':
                return Nested(ReadArray);
            case '"':
                var start = _position;
                var value = ReadString();
                return new JsonScalar(JsonKind.String, start, _position - start, value);
            case 't':
                return ReadLiteral("true", JsonKind.True);
            case 'f':
                return ReadLiteral("false", JsonKind.False);
            case 'n':
                return ReadLiteral("null", JsonKind.Null);
            case '-':
            case >= '0' and <= '9':
                return ReadNumber();
            case '\'':
                throw Error("Strings use double quotes in JSON.");
            default:
                throw Error($"Unexpected character '{Current}'; a value was expected.");
        }
    }

    private JsonNode Nested(Func<JsonNode> read)
    {
        if (++_depth > MaxDepth)
        {
            throw Error($"Nested more than {MaxDepth} levels deep.");
        }

        var node = read();
        _depth--;
        return node;
    }

    private JsonObject ReadObject()
    {
        var start = _position++;
        var properties = new List<JsonProperty>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            SkipTrivia();
            if (AtEnd)
            {
                throw new JsonSyntaxException("Unexpected end of file; '}' was expected.", start, 1);
            }

            if (Current == '}')
            {
                _position++;
                return new JsonObject(start, _position - start, properties);
            }

            if (Current != '"')
            {
                throw Current == '\'' ? Error("Keys use double quotes in JSON.") : Error("A key in double quotes or '}' was expected.");
            }

            var nameStart = _position;
            var name = ReadString();
            var nameLength = _position - nameStart;
            if (!names.Add(name))
            {
                throw new JsonSyntaxException($"Duplicate key '{name}'.", nameStart, nameLength);
            }

            SkipTrivia();
            Expect(':', "':' was expected after the key.");
            SkipTrivia();
            properties.Add(new JsonProperty(name, nameStart, nameLength, ReadValue()));
            SkipTrivia();
            if (!AtEnd && Current == ',')
            {
                _position++;
            }
            else if (!AtEnd && Current != '}')
            {
                throw Error("',' or '}' was expected.");
            }
        }
    }

    private JsonArray ReadArray()
    {
        var start = _position++;
        var items = new List<JsonNode>();
        while (true)
        {
            SkipTrivia();
            if (AtEnd)
            {
                throw new JsonSyntaxException("Unexpected end of file; ']' was expected.", start, 1);
            }

            if (Current == ']')
            {
                _position++;
                return new JsonArray(start, _position - start, items);
            }

            items.Add(ReadValue());
            SkipTrivia();
            if (!AtEnd && Current == ',')
            {
                _position++;
            }
            else if (!AtEnd && Current != ']')
            {
                throw Error("',' or ']' was expected.");
            }
        }
    }

    private string ReadString()
    {
        var start = _position++;
        StringBuilder? builder = null;
        var runStart = _position;
        while (true)
        {
            if (AtEnd || Current is '\r' or '\n')
            {
                throw new JsonSyntaxException("The string is not closed with '\"'.", start, _position - start);
            }

            var c = Current;
            if (c == '"')
            {
                var tail = _text.Substring(runStart, _position - runStart);
                _position++;
                return builder is null ? tail : builder.Append(tail).ToString();
            }

            if (c < ' ')
            {
                throw Error("Control characters must be escaped in a string.");
            }

            if (c != '\\')
            {
                _position++;
                continue;
            }

            builder ??= new StringBuilder();
            builder.Append(_text, runStart, _position - runStart);
            _position++;
            if (AtEnd)
            {
                continue; // reported as an unclosed string
            }

            var escape = Current;
            _position++;
            switch (escape)
            {
                case '"': builder.Append('"'); break;
                case '\\': builder.Append('\\'); break;
                case '/': builder.Append('/'); break;
                case 'b': builder.Append('\b'); break;
                case 'f': builder.Append('\f'); break;
                case 'n': builder.Append('\n'); break;
                case 'r': builder.Append('\r'); break;
                case 't': builder.Append('\t'); break;
                case 'u':
                    if (_position + 4 > _text.Length
                        || !int.TryParse(_text.Substring(_position, 4), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                    {
                        throw new JsonSyntaxException("\\u must be followed by four hex digits.", _position - 2, 2);
                    }

                    builder.Append((char)code);
                    _position += 4;
                    break;
                default:
                    throw new JsonSyntaxException($"Unknown escape '\\{escape}'.", _position - 2, 2);
            }

            runStart = _position;
        }
    }

    private JsonScalar ReadNumber()
    {
        var start = _position;
        if (Current == '-')
        {
            _position++;
        }

        if (!SkipDigits())
        {
            throw Error("A digit was expected.");
        }

        if (!AtEnd && Current == '.')
        {
            _position++;
            if (!SkipDigits())
            {
                throw Error("A digit was expected after '.'.");
            }
        }

        if (!AtEnd && Current is 'e' or 'E')
        {
            _position++;
            if (!AtEnd && Current is '+' or '-')
            {
                _position++;
            }

            if (!SkipDigits())
            {
                throw Error("A digit was expected in the exponent.");
            }
        }

        return new JsonScalar(JsonKind.Number, start, _position - start, _text.Substring(start, _position - start));
    }

    private bool SkipDigits()
    {
        var start = _position;
        while (!AtEnd && Current is >= '0' and <= '9')
        {
            _position++;
        }

        return _position > start;
    }

    private JsonScalar ReadLiteral(string literal, JsonKind kind)
    {
        if (string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
        {
            throw Error($"Unexpected character '{Current}'; a value was expected.");
        }

        var start = _position;
        _position += literal.Length;
        return new JsonScalar(kind, start, literal.Length, literal);
    }

    private void SkipTrivia()
    {
        while (!AtEnd)
        {
            var c = Current;
            if (c is ' ' or '\t' or '\r' or '\n')
            {
                _position++;
            }
            else if (c == '/' && _position + 1 < _text.Length && _text[_position + 1] == '/')
            {
                while (!AtEnd && Current is not ('\r' or '\n'))
                {
                    _position++;
                }
            }
            else if (c == '/' && _position + 1 < _text.Length && _text[_position + 1] == '*')
            {
                var end = _text.IndexOf("*/", _position + 2, StringComparison.Ordinal);
                if (end < 0)
                {
                    throw new JsonSyntaxException("The comment is not closed with '*/'.", _position, 2);
                }

                _position = end + 2;
            }
            else
            {
                return;
            }
        }
    }

    private void Expect(char expected, string message)
    {
        if (AtEnd || Current != expected)
        {
            throw Error(message);
        }

        _position++;
    }

    private JsonSyntaxException Error(string message) => new(message, _position, AtEnd ? 0 : 1);
}

/// <summary>A syntax error at a position in the text.</summary>
internal sealed class JsonSyntaxException(string message, int start, int length) : Exception(message)
{
    public int Start { get; } = start;

    public int Length { get; } = length;
}
