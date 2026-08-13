// Builds a synthetic-but-faithful corpus from td_api.tl.
// Faithful because TDLib's generated to_json (td_api_json_*.cpp) writes *every* scalar field,
// including false booleans and empty strings, and omits only null object pointers.
// Run: dotnet run GenCorpus.cs

using System.Text;

var tl = @"C:\Source\Telegram\Libraries\tdjson\td_api.tl";
var outDir = @"C:\Source\Telegram\Telegram.Benchmarks\Corpus";
Directory.CreateDirectory(outDir);

var defs = new Dictionary<string, List<(string Name, string Type, bool IsVector)>>();

foreach (var raw in File.ReadAllLines(tl))
{
    var line = raw.Trim();
    if (line.Length == 0 || line.StartsWith("//") || line.StartsWith("---") || !line.EndsWith(";")) continue;
    var eq = line.LastIndexOf(" = ");
    if (eq < 0) continue;

    var head = line[..eq];
    var space = head.IndexOf(' ');
    var name = space < 0 ? head : head[..space];

    var props = new List<(string, string, bool)>();
    if (space > 0)
    {
        foreach (var arg in head[(space + 1)..].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var colon = arg.IndexOf(':');
            if (colon < 0) continue;
            var pt = arg[(colon + 1)..];
            var vec = false;
            while (pt.StartsWith("vector<") && pt.EndsWith(">")) { vec = true; pt = pt[7..^1]; }
            props.Add((arg[..colon], pt, vec));
        }
    }
    defs[name] = props;
}

// Object-valued fields we want present. Anything not listed is omitted, exactly as TDLib
// omits a null pointer.
var snippets = new Dictionary<string, string>
{
    ["sender_id"] = """{"@type":"messageSenderUser","user_id":1234567}""",
    ["content"] = """{"@type":"messageText","text":{"@type":"formattedText","text":"Hey, are we still on for tomorrow? I can move things around if not","entities":[{"@type":"textEntity","offset":0,"length":3,"type":{"@type":"textEntityTypeBold"}}]},"link_preview":null,"link_preview_options":null}""",
    ["interaction_info"] = """{"@type":"messageInteractionInfo","view_count":0,"forward_count":0,"reply_info":null,"reactions":null}""",
    ["reply_to"] = """{"@type":"messageReplyToMessage","chat_id":-1001234567890,"message_id":91234304,"quote":null,"origin":null,"origin_send_date":0,"content":null}""",
};

string Value(string type, bool vector, int seed)
{
    if (vector) return "[]";
    return type switch
    {
        "Bool" => (seed % 7 == 0) ? "true" : "false",
        "int32" => (1700000000 + seed).ToString(),
        "int53" => (91234304L + seed).ToString(),
        "int64" => "\"" + (7146138731234567890L + seed) + "\"",
        "double" => "0.0",
        "string" => "\"\"",
        "bytes" => "\"\"",
        _ => "" // object: only emitted when a snippet was supplied
    };
}

string Build(string typeName)
{
    var sb = new StringBuilder();
    sb.Append("{\"@type\":\"").Append(typeName).Append('"');

    var i = 0;
    foreach (var p in defs[typeName])
    {
        i++;
        var primitive = p.Type is "Bool" or "int32" or "int53" or "int64" or "double" or "string" or "bytes";
        string value;

        if (snippets.TryGetValue(p.Name, out var snippet) && !primitive)
        {
            value = snippet;
        }
        else if (primitive || p.IsVector)
        {
            value = Value(p.Type, p.IsVector, i);
        }
        else
        {
            continue; // null object pointer, omitted by TDLib
        }

        sb.Append(",\"").Append(p.Name).Append("\":").Append(value);
    }

    return sb.Append('}').ToString();
}

var message = Build("message");
var file = Build("file").Replace("\"local\":", "\"local\":").Replace("}}", "}}"); // file's local/remote are objects
// file.local / file.remote are non-null in practice, so splice them in.
var localFile = Build("localFile");
var remoteFile = Build("remoteFile");
file = file[..^1] + ",\"local\":" + localFile + ",\"remote\":" + remoteFile + "}";

var lines = new List<string>
{
    """{"@type":"updateNewMessage","message":""" + message + "}",
    """{"@type":"updateFile","file":""" + file + "}",
    """{"@type":"updateUserStatus","user_id":1234567,"status":{"@type":"userStatusOnline","expires":1700000900}}""",
    """{"@type":"messages","total_count":50,"messages":[""" + string.Join(",", Enumerable.Repeat(message, 50)) + "]}",
};

File.WriteAllLines(Path.Combine(outDir, "synthetic.jsonl"), lines);

// Bare objects for the head-to-head dispatch benchmarks. Kept out of Corpus\ so they don't
// turn up in the end-to-end numbers.
var fixtures = Path.Combine(Path.GetDirectoryName(outDir)!, "Fixtures");
Directory.CreateDirectory(fixtures);
File.WriteAllText(Path.Combine(fixtures, "message.json"), message);
File.WriteAllText(Path.Combine(fixtures, "localFile.json"), localFile);
Console.WriteLine($"wrote {fixtures}");

Console.WriteLine($"wrote {Path.Combine(outDir, "synthetic.jsonl")}");
foreach (var l in lines)
{
    var type = l[(l.IndexOf("\"@type\":\"") + 9)..];
    Console.WriteLine($"  {type[..type.IndexOf('"')],-20} {l.Length,8:N0} bytes");
}
