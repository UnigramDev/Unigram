//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Telegram
{
    public partial class TypeCrosserGenerator
    {
        public static void Generate()
        {
            var types = typeof(Telegram.Td.Api.File).Assembly.GetTypes();

            var typesToCross = new List<Type>();
            var typesToCrossMap = new Dictionary<Type, Dictionary<string, Type>>();
            var addedSomething = true;

            while (addedSomething)
            {
                addedSomething = false;

                foreach (var type in types)
                {
                    if (type.IsInterface)
                    {
                        continue;
                    }

                    var properties = type.GetProperties();
                    var targets = new Dictionary<string, Type>();

                    foreach (var item in properties)
                    {
                        var property = item.PropertyType;
                        while (property.IsGenericType)
                        {
                            property = property.GenericTypeArguments[0];
                        }

                        if (property == typeof(Telegram.Td.Api.File) || typesToCross.Contains(property))
                        {
                            targets[item.Name] = item.PropertyType;
                        }
                    }

                    if (targets.Count > 0)
                    {
                        if (typesToCrossMap.TryGetValue(type, out var existing))
                        {
                            if (existing.Count < targets.Count)
                            {
                                addedSomething = true;
                            }
                        }
                        else
                        {
                            typesToCross.Add(type);
                            addedSomething = true;
                        }

                        typesToCrossMap[type] = targets;

                        foreach (var baseType in type.GetInterfaces())
                        {
                            if (baseType.IsPublic && baseType.IsVisible && !typesToCross.Contains(baseType))
                            {
                                typesToCross.Add(baseType);
                                addedSomething = true;
                            }
                        }
                    }
                }
            }

            var builder = new FormattedBuilder();
            builder.AppendLine("public void ProcessFiles(object target)");
            builder.AppendLine("{");
            builder.AppendLine("switch (target)");
            builder.AppendLine("{");

            foreach (var type in typesToCrossMap.OrderBy(x => x.Key.Name))
            {
                var key = type.Key.Name;
                var name = type.Key.Name.CamelCase();

                builder.AppendLine($"case {key} {name}:");
                builder.Increment();

                foreach (var property in type.Value.OrderBy(x => x.Key))
                {
                    var propertyKey = property.Key;

                    if (property.Value.IsGenericType)
                    {
                        var argument = property.Value.GenericTypeArguments[0];
                        if (argument.IsGenericType)
                        {
                            builder.AppendLine($"foreach (var item in {name}.{propertyKey}.SelectMany(x => x))");
                        }
                        else
                        {
                            builder.AppendLine($"foreach (var item in {name}.{propertyKey})");
                        }

                        builder.AppendLine("{");

                        builder.AppendLine($"ProcessFiles(item);");
                    }
                    else
                    {
                        builder.AppendLine($"if ({name}.{propertyKey} != null)");
                        builder.AppendLine("{");

                        if (property.Value == typeof(Telegram.Td.Api.File))
                        {
                            builder.AppendLine($"{name}.{propertyKey} = ProcessFile({name}.{propertyKey});");
                        }
                        else
                        {
                            builder.AppendLine($"ProcessFiles({name}.{propertyKey});");
                        }
                    }

                    builder.AppendLine("}");
                }

                builder.AppendLine("break;");
                builder.Decrement();
            }

            builder.AppendLine("}");
            var c = builder.ToString();

            var b = string.Join(", ", typesToCross);
            var a = 2 + 3;
        }
    }

    public partial class FormattedBuilder
    {
        private readonly StringBuilder _builder;
        private int _indent;

        public FormattedBuilder(string text, int indent)
        {
            _builder = new StringBuilder(text);
            _indent = indent;
        }

        public FormattedBuilder()
        {
            _builder = new StringBuilder();
        }

        public void Append(string text)
        {
            _builder.Append(text);
        }

        public void Increment()
        {
            _indent++;
        }

        public void Decrement()
        {
            _indent--;
        }

        public void AppendLine(string text)
        {
            if (text == "}")
            {
                _indent--;
            }

            AppendIndent();
            _builder.Append(text);
            _builder.AppendLine();

            if (text == "{")
            {
                _indent++;
            }
        }

        public void AppendIndent(string text)
        {
            if (text == "}")
            {
                _indent--;
            }

            AppendIndent();
            _builder.Append(text);

            if (text == "{")
            {
                _indent++;
            }
        }

        public void AppendLine()
        {
            _builder.AppendLine();
        }

        private void AppendIndent()
        {
            for (int i = 0; i < _indent; i++)
            {
                _builder.Append("    ");
            }
        }

        public override string ToString()
        {
            return _builder.ToString();
        }
    }

    public static class Extensions2
    {
        public static string TitleCase(this string str)
        {
            var split = str.Split('_');
            return string.Join("", split.Select(x => x[..1].ToUpperInvariant() + x[1..]));
        }

        public static string CamelCase(this string str)
        {
            return str[..1].ToLowerInvariant() + str[1..];
        }
    }

}
