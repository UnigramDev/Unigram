using System;
using System.IO;
using System.Text;
using Telegram.Generators;

internal static class Program
{
    private static int Main(string[] args)
    {
        string schema = null;
        string output = null;
        string parsers = null;

        for (var i = 0; i < args.Length - 1; i++)
        {
            switch (args[i])
            {
                case "--schema":
                    schema = args[++i];
                    break;
                case "--out":
                    output = args[++i];
                    break;
                case "--parsers":
                    parsers = args[++i];
                    break;
            }
        }

        if (schema == null || output == null)
        {
            Console.Error.WriteLine("usage: --schema <td_api.tl> --out <directory> [--parsers Reader|Pointer|Both]");
            return 1;
        }

        try
        {
            var text = SchemaWriter.Write(File.ReadAllText(schema), parsers);

            Directory.CreateDirectory(output);

            var path = Path.Combine(output, "TdDotNetApi.g.cs");

            // Only when it differs: this is a 6 MB compiler input, and restamping it on a build
            // where the scheme did not change costs a full recompile.
            if (!File.Exists(path) || File.ReadAllText(path) != text)
            {
                // With the BOM, so the file is byte for byte what AddSource writes on the Roslyn
                // path and the two modes stay comparable.
                File.WriteAllText(path, text, new UTF8Encoding(true));
            }

            return 0;
        }
        catch (Exception ex)
        {
            // MSBuild reads this back as the Exec failure, so the message is the whole report.
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
