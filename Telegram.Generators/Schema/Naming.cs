using System.Text;

namespace Telegram.Generators.Schema
{
    /// <summary>
    /// The TL scheme is snake_case; the C# API is PascalCase. Every name the emitter writes goes
    /// through here, including the two collisions the schema produces on its own.
    /// </summary>
    internal static class Naming
    {
        /// <summary>message_id -> MessageId. Also used for type names: chatPhoto -> ChatPhoto.</summary>
        public static string ToPascalCase(string name)
        {
            return Convert(name, upperFirst: true);
        }

        /// <summary>message_id -> messageId, for constructor parameters.</summary>
        public static string ToCamelCase(string name)
        {
            return Convert(name, upperFirst: false);
        }

        private static string Convert(string name, bool upperFirst)
        {
            var builder = new StringBuilder(name.Length);
            var upper = upperFirst;

            for (int i = 0; i < name.Length; i++)
            {
                var c = name[i];

                // Separators are dropped and capitalise whatever follows.
                if (!char.IsLetterOrDigit(c))
                {
                    upper = true;
                    continue;
                }

                builder.Append(upper ? char.ToUpper(c) : c);
                upper = false;
            }

            return builder.ToString();
        }

        /// <summary>
        /// The property name on the generated class. A field whose name matches its own type would
        /// produce `class Messages { Messages Messages { get; } }`, which C# rejects - those get a
        /// Value suffix (Messages.MessagesValue).
        /// </summary>
        public static string PropertyName(SchemaProperty property, string className)
        {
            var name = ToPascalCase(property.Name);
            return name == className ? name + "Value" : name;
        }

        /// <summary>
        /// The constructor parameter name. `event` is the one schema field name that is a C#
        /// keyword, so it is verbatim-escaped.
        /// </summary>
        public static string ParameterName(SchemaProperty property)
        {
            var name = ToCamelCase(property.Name);
            return name == "event" ? "@" + name : name;
        }

        /// <summary>The C# type for a field, including vector nesting and nullability.</summary>
        /// <remarks>
        /// A vector is a List on an object and an IList on a function. Objects are read - bound to
        /// ItemsSource, iterated per render - and List is what makes the concrete type visible to
        /// the analyzer, keeps foreach from boxing an enumerator, and lets the indexer inline.
        /// Functions are only ever written and serialised, so none of that applies and IList is the
        /// friendlier parameter type.
        /// </remarks>
        public static string PropertyType(SchemaProperty property, bool function = true)
        {
            var name = ScalarType(property.Type);
            var list = function ? "IList" : "List";

            if (property.IsVectorOfVectors)
            {
                return list + "<" + list + "<" + name + ">>";
            }

            if (property.IsVector)
            {
                return list + "<" + name + ">";
            }

            // The schema states nullability in prose rather than in the type, so this reads it back
            // out of the documentation.
            if (property.Description.Contains("may be null") || property.Description.Contains("pass null"))
            {
                name += "?";
            }

            return name;
        }

        /// <summary>
        /// The constructor parameter type. Always the interface, so that `new[] { x }` and
        /// `[x]` both still bind - the property converts, and for a parsed response that is a cast
        /// rather than a copy, both parsers building a List already.
        /// </summary>
        public static string ParameterType(SchemaProperty property)
        {
            if (property.IsVectorOfVectors)
            {
                return "IList<List<" + ScalarType(property.Type) + ">>";
            }

            return PropertyType(property, true);
        }

        public static string ScalarType(string name)
        {
            switch (name)
            {
                case "Bool":
                    return "bool";
                case "int32":
                    return "int";
                case "int53":
                case "int64":
                    return "long";
                case "double":
                    return "double";
                case "string":
                    return "string";
                case "bytes":
                    return "byte[]";
                default:
                    return ToPascalCase(name);
            }
        }
    }
}
