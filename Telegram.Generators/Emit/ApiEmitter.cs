using System.Collections.Generic;
using System.Text;
using Telegram.Generators.Schema;

namespace Telegram.Generators.Emit
{
    /// <summary>
    /// Writes the C# for one schema type: the class, its ToJson when it can be sent, and its
    /// FromJson when it can be received.
    ///
    /// The output of this file is compiled by the app, so every change here should be checked by
    /// regenerating and diffing against the previous output - see Telegram.Benchmarks/README.md.
    /// </summary>
    internal static class ApiEmitter
    {
        /// <summary>
        /// updateFile and file are re-entered through the handler so the app can dedupe them:
        /// they arrive constantly during downloads and are the one place object identity pays.
        /// </summary>
        private static bool IsHandledByClient(string className)
        {
            return className == "UpdateFile" || className == "File";
        }

        public static void WriteClass(StringBuilder builder, SchemaClass type, bool serializable)
        {
            var className = Naming.ToPascalCase(type.Name);
            var baseName = Naming.ToPascalCase(type.IsFunction ? "Function" : type.ReturnType ?? "Object");

            builder.AppendLine("/// <summary>");
            builder.AppendLine("/// " + type.Description);

            if (type.IsFunction)
            {
                builder.AppendLine("/// <para>Returns <see cref=\"T:Telegram.Td.Api." + Naming.ToPascalCase(type.ReturnType) + "\"/>.</para>");
            }

            builder.AppendLine("/// </summary>");

            if (type.IsProxy)
            {
                builder.AppendLine("public abstract partial class " + className + " : " + baseName);
                builder.AppendLine("{");
                builder.AppendLine("}");
                return;
            }

            builder.AppendLine("public partial class " + className + " : " + baseName);
            builder.AppendLine("{");

            foreach (var prop in type.Properties)
            {
                var fieldName = Naming.PropertyName(prop, className);

                builder.AppendLine("    /// <summary>");
                builder.AppendLine("    /// " + prop.Description);
                builder.AppendLine("    /// </summary>");

                // Strings default to empty rather than null, which is what lets TDLib omit them
                // from the wire without every consumer having to null-check.
                if (prop.Type == "string" && !prop.IsVector)
                {
                    builder.AppendLine("    public " + Naming.PropertyType(prop) + " " + fieldName + " { get; set; } = string.Empty;");
                }
                else
                {
                    builder.AppendLine("    public " + Naming.PropertyType(prop) + " " + fieldName + " { get; set; }");
                }
            }

            if (!type.IsFunction || type.Properties.Count == 0)
            {
                builder.AppendLine();
                builder.AppendLine("    /// <summary>");
                builder.AppendLine("    /// " + type.Description);

                if (type.IsFunction)
                {
                    builder.AppendLine("    /// <para>Returns <see cref=\"T:Telegram.Td.Api." + Naming.ToPascalCase(type.ReturnType) + "\"/>.</para>");
                }

                builder.AppendLine("    /// </summary>");
                builder.AppendLine("    public " + className + "()");
                builder.AppendLine("    {");
                builder.AppendLine("    }");
            }

            if (type.Properties.Count > 0)
            {
                WriteConstructor(builder, type, className);
            }

            if (serializable)
            {
                WriteToJson(builder, type, className);
            }

            builder.AppendLine("}");
        }

        private static void WriteConstructor(StringBuilder builder, SchemaClass type, string className)
        {
            builder.AppendLine();

            builder.AppendLine("    /// <summary>");
            builder.AppendLine("    /// " + type.Description);

            if (type.IsFunction)
            {
                builder.AppendLine("    /// <para>Returns <see cref=\"T:Telegram.Td.Api." + Naming.ToPascalCase(type.ReturnType) + "\"/>.</para>");
            }

            builder.AppendLine("    /// </summary>");

            foreach (var prop in type.Properties)
            {
                builder.AppendLine("    /// <param name=\"" + Naming.ParameterName(prop) + "\">" + prop.Description + "</param>");
            }

            builder.Append("    public " + className + "(");

            for (int i = 0; i < type.Properties.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                var property = type.Properties[i];
                builder.Append(Naming.PropertyType(property) + " " + Naming.ParameterName(property));
            }

            builder.AppendLine(")");
            builder.AppendLine("    {");

            foreach (var property in type.Properties)
            {
                builder.AppendLine("        " + Naming.PropertyName(property, className) + " = " + Naming.ParameterName(property) + ";");
            }

            builder.AppendLine("    }");
        }

        private static void WriteToJson(StringBuilder builder, SchemaClass type, string className)
        {
            builder.AppendLine();
            builder.AppendLine("    public override void ToJson(Utf8JsonWriter writer)");
            builder.AppendLine("    {");
            builder.AppendLine("        writer.WriteString(\"@type\"u8, \"" + type.Name + "\"u8);");

            foreach (var prop in type.Properties)
            {
                var fieldName = Naming.PropertyName(prop, className);

                if (prop.IsVector)
                {
                    builder.AppendLine("        writer.WriteArray(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else if (prop.Type == "Bool")
                {
                    builder.AppendLine("        writer.WriteBoolean(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else if (prop.Type == "int32" || prop.Type == "int53" || prop.Type == "double")
                {
                    builder.AppendLine("        writer.WriteNumber(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else if (prop.Type == "int64")
                {
                    // int64 goes over the wire quoted: it exceeds what JSON numbers represent
                    // exactly, and JavaScript clients would round it.
                    builder.AppendLine("        writer.WriteNumberString(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else if (prop.Type == "string")
                {
                    builder.AppendLine("        writer.WriteString(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else if (prop.Type == "bytes")
                {
                    builder.AppendLine("        writer.WriteBase64String(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
                else
                {
                    builder.AppendLine("        writer.WriteObject(\"" + prop.Name + "\"u8, " + fieldName + ");");
                }
            }

            builder.AppendLine("    }");
        }

        /// <summary>
        /// The dispatcher for an abstract type: reads @type and hands off to the constructor's own
        /// parser.
        /// </summary>
        public static void WriteAbstractDispatcher(StringBuilder builder, string name, List<SchemaClass> classes)
        {
            var className = Naming.ToPascalCase(name);

            builder.AppendLine("private static " + className + " FromJson_" + className + "(ref Utf8JsonReader reader, ClientResultHandler handler)");
            builder.AppendLine("{");
            builder.AppendLine("    return FromJson(ref reader, handler, Handler);");
            builder.AppendLine();
            builder.AppendLine("    static " + className + "? Handler(ref Utf8JsonReader reader, ClientResultHandler handler, uint hash)");
            builder.AppendLine("    {");
            builder.AppendLine("        switch(hash)");
            builder.AppendLine("        {");

            foreach (var clazz in classes)
            {
                WriteDispatchCase(builder, clazz);
            }

            builder.AppendLine("            default: return null;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
        }

        /// <summary>One case of a @type switch, keyed by the CRC32 of the constructor name.</summary>
        public static void WriteDispatchCase(StringBuilder builder, SchemaClass type)
        {
            if (type.IsFunction || type.IsProxy)
            {
                return;
            }

            var crc32 = Crc32.Compute(type.Name);
            builder.AppendLine("        case " + crc32 + ":");
            builder.AppendLine("            return FromJson_" + Naming.ToPascalCase(type.Name) + "(ref reader, handler);");
        }

        /// <summary>The parser for one concrete type: a loop over its fields, keyed by CRC32.</summary>
        public static void WriteFromJson(StringBuilder builder, SchemaClass type)
        {
            var className = Naming.ToPascalCase(type.Name);

            builder.AppendLine("private static " + className + " FromJson_" + className + "(ref Utf8JsonReader reader, ClientResultHandler handler)");
            builder.AppendLine("{");

            if (IsHandledByClient(className))
            {
                builder.AppendLine("    return handler.Parse" + className + "(ref reader);");
            }
            else if (type.IsProxy)
            {
                builder.AppendLine("    return FromJson<" + className + ">(ref reader, handler);");
            }
            else if (type.Properties.Count > 0)
            {
                builder.AppendLine("    return ParseObject(ref reader, new " + className + "(), handler, Handler);");
                builder.AppendLine();
                builder.AppendLine("    static bool Handler(ref Utf8JsonReader reader, ClientResultHandler handler, " + className + " obj, uint hash)");
                builder.AppendLine("    {");
                builder.AppendLine("        switch (hash)");
                builder.AppendLine("        {");

                foreach (var prop in type.Properties)
                {
                    builder.AppendLine("            case " + Crc32.Compute(prop.Name) + ":");
                    builder.AppendLine("                obj." + Naming.PropertyName(prop, className) + " = " + ReadExpression(prop) + ";");
                    builder.AppendLine("                return true;");
                }

                builder.AppendLine("            default: return false;");
                builder.AppendLine("        }");
                builder.AppendLine("    }");
            }
            else
            {
                builder.AppendLine("    reader.ReadStartObject();");
                builder.AppendLine("    return new " + className + "();");
            }

            builder.AppendLine("}");
        }

        /// <summary>
        /// The pointer-reader dispatcher for an abstract type. Same CRC32 cases as the
        /// Utf8JsonReader version - TdJsonReader.ValueCrc32 computes the identical value.
        /// </summary>
        public static void WritePtrAbstractDispatcher(StringBuilder builder, string name, List<SchemaClass> classes)
        {
            var className = Naming.ToPascalCase(name);

            builder.AppendLine("private static " + className + " FromPtr_" + className + "(ref TdJsonReader reader, ClientResultHandler handler)");
            builder.AppendLine("{");
            builder.AppendLine("    return FromPtr(ref reader, handler, Handler);");
            builder.AppendLine();
            builder.AppendLine("    static " + className + "? Handler(ref TdJsonReader reader, ClientResultHandler handler, uint hash)");
            builder.AppendLine("    {");
            builder.AppendLine("        switch(hash)");
            builder.AppendLine("        {");

            foreach (var clazz in classes)
            {
                if (clazz.IsFunction || clazz.IsProxy)
                {
                    continue;
                }

                builder.AppendLine("        case " + Crc32.Compute(clazz.Name) + ":");
                builder.AppendLine("            return FromPtr_" + Naming.ToPascalCase(clazz.Name) + "(ref reader, handler);");
            }

            builder.AppendLine("            default: return null;");
            builder.AppendLine("        }");
            builder.AppendLine("    }");
            builder.AppendLine("}");
        }

        public static void WritePtrDispatchCase(StringBuilder builder, SchemaClass type)
        {
            if (type.IsFunction || type.IsProxy)
            {
                return;
            }

            builder.AppendLine("        case " + Crc32.Compute(type.Name) + ":");
            builder.AppendLine("            return FromPtr_" + Naming.ToPascalCase(type.Name) + "(ref reader, handler);");
        }

        /// <summary>
        /// The pointer parser for one concrete type. Fields dispatch on name length then an exact
        /// compare rather than a hash: there are few enough per class for that to be cheap, it
        /// cannot collide with an unknown field, and it keeps the comparison inside a library call
        /// instead of a hand-written loop over a span.
        /// </summary>
        public static void WriteFromPtr(StringBuilder builder, SchemaClass type)
        {
            var className = Naming.ToPascalCase(type.Name);

            builder.AppendLine("private static " + className + " FromPtr_" + className + "(ref TdJsonReader reader, ClientResultHandler handler)");
            builder.AppendLine("{");

            // TODO: the Utf8JsonReader path routes these back through ClientResultHandler so the
            // app can dedupe them. Doing the same here needs the interface to take a TdJsonReader,
            // which is part of wiring this into the app rather than into the benchmark.
            if (type.IsProxy)
            {
                builder.AppendLine("    return FromPtr<" + className + ">(ref reader, handler, null);");
            }
            else if (type.Properties.Count > 0)
            {
                builder.AppendLine("    var obj = new " + className + "();");
                builder.AppendLine("    ReadStartObjectPtr(ref reader);");
                builder.AppendLine();
                builder.AppendLine("    while (reader.TokenType == JsonTokenType.PropertyName)");
                builder.AppendLine("    {");
                builder.AppendLine("        var name = reader.ValueSpan;");
                builder.AppendLine("        reader.Read();");
                builder.AppendLine();
                builder.AppendLine("        switch (name.Length)");
                builder.AppendLine("        {");

                foreach (var group in GroupByNameLength(type.Properties))
                {
                    builder.AppendLine("            case " + group.Key + ":");

                    var first = true;
                    foreach (var prop in group.Value)
                    {
                        builder.AppendLine("                " + (first ? "if" : "else if") +
                            " (name.SequenceEqual(\"" + prop.Name + "\"u8)) obj." + Naming.PropertyName(prop, className) +
                            " = " + ReadPtrExpression(prop) + ";");
                        first = false;
                    }

                    builder.AppendLine("                break;");
                }

                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine("        if (reader.TokenType == JsonTokenType.StartObject || reader.TokenType == JsonTokenType.StartArray)");
                builder.AppendLine("        {");
                builder.AppendLine("            reader.Skip();");
                builder.AppendLine("        }");
                builder.AppendLine();
                builder.AppendLine("        reader.Read();");
                builder.AppendLine("    }");
                builder.AppendLine();
                builder.AppendLine("    return obj;");
            }
            else
            {
                builder.AppendLine("    ReadStartObjectPtr(ref reader);");
                builder.AppendLine("    return new " + className + "();");
            }

            builder.AppendLine("}");
        }

        /// <summary>Fields bucketed by the byte length of their name, in ascending order.</summary>
        private static List<KeyValuePair<int, List<SchemaProperty>>> GroupByNameLength(List<SchemaProperty> properties)
        {
            var groups = new SortedDictionary<int, List<SchemaProperty>>();

            foreach (var property in properties)
            {
                // Field names are ASCII throughout the scheme, so length in bytes is length in
                // chars - which is what the reader compares against.
                if (!groups.TryGetValue(property.Name.Length, out var group))
                {
                    groups[property.Name.Length] = group = new List<SchemaProperty>();
                }

                group.Add(property);
            }

            return new List<KeyValuePair<int, List<SchemaProperty>>>(groups);
        }

        private static string ReadPtrExpression(SchemaProperty prop)
        {
            if (prop.IsVector)
            {
                switch (prop.Type)
                {
                    case "Bool":
                        return "GetBooleanArrayPtr(ref reader)";
                    case "int32":
                        return "GetInt32ArrayPtr(ref reader)";
                    case "int53":
                        return "GetInt64ArrayPtr(ref reader)";
                    case "int64":
                        return "GetInt64StringArrayPtr(ref reader)";
                    case "double":
                        return "GetDoubleArrayPtr(ref reader)";
                    case "string":
                        return "GetStringArrayPtr(ref reader)";
                    case "bytes":
                        return "GetBase64StringArrayPtr(ref reader)";
                    default:
                        return prop.IsVectorOfVectors
                            ? "GetObjectArrayArrayPtr(ref reader, handler, FromPtr_" + Naming.ToPascalCase(prop.Type) + ")"
                            : "GetObjectArrayPtr(ref reader, handler, FromPtr_" + Naming.ToPascalCase(prop.Type) + ")";
                }
            }

            switch (prop.Type)
            {
                case "Bool":
                    return "reader.GetBoolean()";
                case "int32":
                    return "reader.GetInt32()";
                case "int53":
                    return "reader.GetInt64()";
                case "int64":
                    return "reader.GetInt64String()";
                case "double":
                    return "reader.GetDouble()";
                case "string":
                    return "reader.GetString()";
                case "bytes":
                    return "reader.GetBytesFromBase64()";
                default:
                    return "FromPtr_" + Naming.ToPascalCase(prop.Type) + "(ref reader, handler)";
            }
        }

        /// <summary>The reader call that produces one field's value.</summary>
        private static string ReadExpression(SchemaProperty prop)
        {
            if (prop.IsVector)
            {
                switch (prop.Type)
                {
                    case "Bool":
                        return "reader.GetBooleanArray()";
                    case "int32":
                        return "reader.GetInt32Array()";
                    case "int53":
                        return "reader.GetInt64Array()";
                    case "int64":
                        return "reader.GetInt64StringArray()";
                    case "double":
                        return "reader.GetDoubleArray()";
                    case "string":
                        return "reader.GetStringArray()";
                    case "bytes":
                        return "reader.GetBase64StringArray()";
                    default:
                        return prop.IsVectorOfVectors
                            ? "reader.GetObjectArrayArray(handler, FromJson_" + Naming.ToPascalCase(prop.Type) + ")"
                            : "reader.GetObjectArray(handler, FromJson_" + Naming.ToPascalCase(prop.Type) + ")";
                }
            }

            switch (prop.Type)
            {
                case "Bool":
                    return "reader.GetBoolean()";
                case "int32":
                    return "reader.GetInt32()";
                case "int53":
                    return "reader.GetInt64()";
                case "int64":
                    return "reader.GetInt64String()";
                case "double":
                    return "reader.GetDouble()";
                case "string":
                    return "reader.GetString()";
                case "bytes":
                    return "reader.GetBytesFromBase64()";
                default:
                    return "FromJson_" + Naming.ToPascalCase(prop.Type) + "(ref reader, handler)";
            }
        }
    }
}
