using System.Collections.Generic;

namespace Telegram.Generators.Schema
{
    internal sealed class SchemaProperty
    {
        public SchemaProperty(string name, string type, string description)
        {
            Name = name;
            Description = description;

            // vector<vector<x>> nests at most twice in this schema, and the emitter only has a
            // shape for those two cases.
            while (type.StartsWith("vector<") && type.EndsWith(">"))
            {
                IsVectorOfVectors = IsVector;
                IsVector = true;
                type = type.Substring(7, type.Length - 8);
            }

            Type = type;
        }

        public string Name { get; }
        public string Type { get; }
        public bool IsVector { get; }
        public bool IsVectorOfVectors { get; }
        public string Description { get; }
    }

    internal sealed class SchemaClass
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string ReturnType { get; set; }
        public List<SchemaProperty> Properties { get; set; } = new List<SchemaProperty>();

        /// <summary>An abstract base declared by //@class, with no fields of its own.</summary>
        public bool IsProxy { get; set; }

        /// <summary>Declared after ---functions---, so it is a request rather than a result.</summary>
        public bool IsFunction { get; set; }
    }

    internal sealed class SchemaError
    {
        public SchemaError(int line, string message)
        {
            Line = line;
            Message = message;
        }

        /// <summary>1-based, to match what an editor shows.</summary>
        public int Line { get; }

        public string Message { get; }
    }

    internal sealed class ParsedSchema
    {
        public List<SchemaClass> Classes { get; } = new List<SchemaClass>();
        public List<SchemaError> Errors { get; } = new List<SchemaError>();
    }

    /// <summary>
    /// Reads td_api.tl. Every malformed construct is recorded as an error against its line and the
    /// rest of the file still parses - a generator that throws produces no types at all, which
    /// surfaces as thousands of unrelated compile errors rather than one useful message.
    /// </summary>
    internal static class TlParser
    {
        /// <summary>
        /// Fields and types documented "; for bots only" are dropped: the app is not a bot, and
        /// carrying them would widen every generated class for nothing. TDLib still sends them, so
        /// the parser must tolerate unknown fields on the wire.
        /// </summary>
        private const string BotsOnly = "; for bots only";

        private const string FunctionsMarker = "---functions---";

        public static ParsedSchema Parse(string text)
        {
            var result = new ParsedSchema();
            var reader = new TlReader(text.Replace("\r\n", "\n"));
            var documentation = new Dictionary<string, string>();
            var forBotsOnly = new HashSet<string>();
            var functions = false;

            while (reader.Read())
            {
                switch (reader.TokenType)
                {
                    case TlTokenType.Functions:
                        functions = true;
                        break;

                    case TlTokenType.Comment:
                        documentation.Clear();
                        ReadDocumentation(documentation, reader.Value);

                        if (documentation.TryGetValue("class", out var className))
                        {
                            if (!documentation.TryGetValue("description", out var classDescription))
                            {
                                result.Errors.Add(new SchemaError(reader.Line, $"//@class {className} has no //@description"));
                                break;
                            }

                            if (classDescription.Contains(BotsOnly))
                            {
                                forBotsOnly.Add(className);
                                break;
                            }

                            result.Classes.Add(new SchemaClass
                            {
                                Name = className,
                                Description = classDescription,
                                IsProxy = true,
                                IsFunction = functions
                            });
                        }
                        break;

                    case TlTokenType.Object:
                        ReadConstructor(result, reader, documentation, forBotsOnly, functions);
                        break;
                }
            }

            return result;
        }

        private static void ReadConstructor(ParsedSchema result, TlReader reader,
            Dictionary<string, string> documentation, HashSet<string> forBotsOnly, bool functions)
        {
            // name arg:type arg:type = ReturnType;
            var equals = reader.Value.IndexOf('=');
            if (equals < 0)
            {
                result.Errors.Add(new SchemaError(reader.Line, $"'{reader.Value}' is not a constructor: no '='"));
                return;
            }

            var definition = reader.Value.Substring(0, equals).TrimEnd();
            var returnType = reader.Value.Substring(equals + 1).TrimStart().TrimEnd(';');

            var parts = definition.Split(' ');
            var name = parts[0];

            if (!documentation.TryGetValue("description", out var description))
            {
                result.Errors.Add(new SchemaError(reader.Line, $"'{name}' has no //@description"));
                return;
            }

            var properties = new List<SchemaProperty>(parts.Length - 1);

            for (int i = 1; i < parts.Length; i++)
            {
                var colon = parts[i].IndexOf(':');
                if (colon < 0)
                {
                    result.Errors.Add(new SchemaError(reader.Line, $"'{name}' has a field '{parts[i]}' with no type"));
                    return;
                }

                var fieldName = parts[i].Substring(0, colon);
                var fieldType = parts[i].Substring(colon + 1);

                if (!documentation.TryGetValue(fieldName, out var fieldDescription))
                {
                    result.Errors.Add(new SchemaError(reader.Line, $"'{name}' has no //@{fieldName} for its '{fieldName}' field"));
                    return;
                }

                if (fieldDescription.Contains(BotsOnly))
                {
                    continue;
                }

                properties.Add(new SchemaProperty(fieldName, fieldType, fieldDescription));
            }

            // A constructor that is the only member of its type doesn't need a separate base: the
            // emitter derives it from Object directly, signalled by a null ReturnType.
            if (Naming.ToPascalCase(name) == Naming.ToPascalCase(returnType))
            {
                returnType = null;
            }

            if (description.Contains(BotsOnly) || forBotsOnly.Contains(returnType))
            {
                return;
            }

            result.Classes.Add(new SchemaClass
            {
                Name = name,
                Description = description,
                ReturnType = returnType,
                Properties = properties,
                IsProxy = false,
                IsFunction = functions
            });
        }

        /// <summary>
        /// A documentation block is one or more //@key value pairs, run together across lines.
        /// </summary>
        private static void ReadDocumentation(Dictionary<string, string> items, string line)
        {
            foreach (var part in line.Split(new[] { '@' }, System.StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split(new[] { ' ' }, 2);
                if (pair.Length < 2)
                {
                    continue; // a bare @name with no text - nothing to record
                }

                items[pair[0].Trim()] = pair[1].Trim();
            }
        }
    }

    internal enum TlTokenType
    {
        None,
        Comment,
        Object,
        Functions
    }

    /// <summary>
    /// Line-oriented reader. Blank lines are skipped, runs of comment lines are joined into one
    /// token, and everything else is a constructor.
    /// </summary>
    internal sealed class TlReader
    {
        private readonly string[] _lines;
        private int _index;

        public TlReader(string text)
        {
            _lines = text.Split('\n');

            // Skip the TL built-ins at the top of the file (double ? = Double; and friends), which
            // carry no documentation and describe primitives the emitter hard-codes. The first
            // comment line is where the schema proper starts.
            while (_index < _lines.Length && !_lines[_index].StartsWith("//"))
            {
                _index++;
            }
        }

        public TlTokenType TokenType { get; private set; }

        public string Value { get; private set; } = string.Empty;

        /// <summary>1-based line of the current token.</summary>
        public int Line { get; private set; }

        public bool Read()
        {
            while (_index < _lines.Length && string.IsNullOrEmpty(_lines[_index]))
            {
                _index++;
            }

            if (_index >= _lines.Length)
            {
                TokenType = TlTokenType.None;
                Value = string.Empty;
                return false;
            }

            Line = _index + 1;
            var line = _lines[_index];

            if (line.StartsWith("//"))
            {
                var value = new System.Text.StringBuilder(line.Substring(2));

                // Continuation lines belong to the same block; the leading dashes some of them use
                // are decoration.
                var next = _index + 1;
                while (next < _lines.Length && _lines[next].StartsWith("//"))
                {
                    value.Append(' ').Append(_lines[next].Substring(2).TrimStart('-'));
                    next++;
                }

                _index = next;
                TokenType = TlTokenType.Comment;
                Value = value.ToString();
                return true;
            }

            _index++;

            if (line == FunctionsMarkerValue)
            {
                TokenType = TlTokenType.Functions;
                Value = string.Empty;
                return true;
            }

            TokenType = TlTokenType.Object;
            Value = line;
            return true;
        }

        private const string FunctionsMarkerValue = "---functions---";
    }
}
