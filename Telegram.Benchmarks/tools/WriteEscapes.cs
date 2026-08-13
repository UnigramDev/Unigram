// Writes the escape-exercising corpus line. Done from code so the JSON escape sequences reach the
// file as backslash sequences rather than as literal control characters.
// Run: dotnet run WriteEscapes.cs

var path = @"C:\Source\Telegram\Telegram.Benchmarks\Corpus\synthetic-escapes.jsonl";

var bs = "\\";
var name = "quote" + bs + "\"back" + bs + bs + "slash" + bs + "/newline" + bs + "ntab" + bs + "tend";

// \u escapes: a control char, a non-ASCII BMP char, and a surrogate pair for an astral one.
var value = "bel[" + bs + "u0007] acute[" + bs + "u00e9] astral[" + bs + "ud83d" + bs + "ude00] done";

var lines = new[]
{
    "{\"@type\":\"updateOption\",\"name\":\"" + name + "\",\"value\":{\"@type\":\"optionValueString\",\"value\":\"" + value + "\"}}",
    "{\"@type\":\"updateOption\",\"name\":\"literal-utf8\",\"value\":{\"@type\":\"optionValueString\",\"value\":\"caf\u00e9 \u00fcber na\u00efve \u2014 \u00e9\u00e8\u00ea, \ud83c\udf89 party, \u65e5\u672c\u8a9e\"}}"
};

File.WriteAllLines(path, lines, new System.Text.UTF8Encoding(false));
Console.WriteLine("wrote " + path);
foreach (var l in lines) Console.WriteLine("  " + l.Length + " chars");
