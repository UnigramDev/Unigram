using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Windows.Storage;

namespace Unigram
{
    public class TypeCrosserGenerator
    {
        public static async void Generate()
        {
            var schemeInfo = new FileInfo(Path.Combine(ApplicationData.Current.LocalFolder.Path, "scheme.tl"));
            if (schemeInfo.Exists is false)
            {
                using var client = new HttpClient();
                var response = await client.GetStringAsync("https://raw.githubusercontent.com/tdlib/td/master/td/generate/scheme/td_api.tl");

                File.WriteAllText("scheme.tl", response);
            }

            var scheme = File.ReadAllLines(schemeInfo.FullName);
            var functions = false;

            //var types = new Dictionary<string, string>();
            var types = new List<KeyValuePair<string, string>>();

            foreach (var line in scheme)
            {
                if (string.IsNullOrEmpty(line) || line.StartsWith("//"))
                {
                    continue;
                }
                else if (line.Equals("---functions---"))
                {
                    functions = true;
                    continue;
                }

                var split = line.Split('=');
                var type = split[1].Trim(' ', ';');

                if (functions)
                {

                }
                else
                {
                }
                types.Add(new KeyValuePair<string, string>(type, split[0]));
            }

            var typesToCross = new List<string>();
            var typesToCrossMap = new List<KeyValuePair<string, Dictionary<string, string>>>();
            var addedSomething = true;

            var vectorRegex = new Regex("vector<(.*?)>", RegexOptions.Compiled);

            while (addedSomething)
            {
                addedSomething = false;

                foreach (var type in types)
                {
                    var split = type.Value.Split(' ');
                    if (split.Length <= 1)
                    {
                        continue;
                    }

                    var targets = new Dictionary<string, string>();

                    foreach (var item in split.Skip(1))
                    {
                        var pair = item.Split(':');
                        if (pair.Length < 2)
                        {
                            continue;
                        }

                        var match = vectorRegex.Match(pair[1]);
                        if (match.Success)
                        {
                            pair[1] = match.Groups[1].Value;
                        }

                        var pair1 = pair[1].CamelCase();
                        if (pair1 == "file" || typesToCross.Contains(pair1))
                        {
                            if (match.Success)
                            {
                                targets[pair[0]] = match.Value;
                            }
                            else
                            {
                                targets[pair[0]] = pair1;
                            }
                        }
                    }

                    if (targets.Count > 0)
                    {
                        var split0 = split[0].CamelCase();
                        if (!typesToCross.Contains(split0))
                        {
                            typesToCrossMap.Add(new KeyValuePair<string, Dictionary<string, string>>(split0, targets));

                            typesToCross.Add(split0);
                            addedSomething = true;
                        }

                        var key = type.Key.CamelCase();
                        if (!typesToCross.Contains(key))
                        {
                            typesToCross.Add(key);
                            addedSomething = true;
                        }
                    }
                }
            }

            var builder = new FormattedBuilder();
            builder.AppendLine("public void ProcessFiles(object target)");
            builder.AppendLine("{");

            var first = true;

            foreach (var type in typesToCrossMap)
            {
                var key = type.Key.TitleCase();
                var name = type.Key.CamelCase();

                if (first)
                {
                    builder.AppendLine($"if (target is {key} {name})");
                    first = false;
                }
                else
                {
                    builder.AppendLine($"else if (target is {key} {name})");
                }

                builder.AppendLine("{");

                foreach (var property in type.Value)
                {
                    var propertyKey = property.Key.TitleCase();
                    if (property.Key == name)
                    {
                        propertyKey += "Value";
                    }

                    var match = vectorRegex.Match(property.Value);
                    if (match.Success)
                    {
                        builder.AppendLine($"foreach (var item in {name}.{propertyKey})");
                        builder.AppendLine("{");

                        builder.AppendLine($"ProcessFiles(item);");
                    }
                    else
                    {
                        builder.AppendLine($"if ({name}.{propertyKey} != null)");
                        builder.AppendLine("{");

                        if (property.Value == "file")
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

                builder.AppendLine("}");
            }

            builder.AppendLine("}");
            var c = builder.ToString();

            var b = string.Join(", ", typesToCross);
            var a = 2+3;
        }
    }

    public class FormattedBuilder
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

    public static class Extensions
    {
        public static string TitleCase(this string str)
        {
            var split = str.Split('_');
            return string.Join("", split.Select(x => x.Substring(0, 1).ToUpperInvariant() + x.Substring(1)));
        }

        public static string CamelCase(this string str)
        {
            return str.Substring(0, 1).ToLowerInvariant() + str.Substring(1);
        }
    }

}
