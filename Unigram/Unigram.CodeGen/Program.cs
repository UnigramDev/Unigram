using System;
using System.Linq;
using System.Text;

namespace Unigram.CodeGen
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            TypeContainerGenerator.Generate();
            //TypeCrosserGenerator.Generate();
            Console.ReadLine();
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
            return string.Join("", split.Select(x => x[..1].ToUpperInvariant() + x[1..]));
        }

        public static string CamelCase(this string str)
        {
            return str[..1].ToLowerInvariant() + str[1..];
        }
    }
}
