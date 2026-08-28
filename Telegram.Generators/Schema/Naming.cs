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
        public static string PropertyType(SchemaProperty property)
        {
            var name = ScalarType(property.Type);

            if (property.IsVectorOfVectors)
            {
                return "Vector<Vector<" + name + ">>";
            }

            if (property.IsVector)
            {
                return "Vector<" + name + ">";
            }

            // The schema states nullability in prose rather than in the type, so this reads it back
            // out of the documentation.
            if (property.Description.Contains("may be null") || property.Description.Contains("pass null"))
            {
                name += "?";
            }

            return name;
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
