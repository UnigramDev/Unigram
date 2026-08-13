//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using BenchmarkDotNet.Running;
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Telegram.Benchmarks;
using Telegram.Td;
using Telegram.Td.Api;

// A faster parser that returns different objects is not a faster parser. Nothing runs until
// the corpus round-trips and the two dispatch strategies agree field for field.
if (!Validate())
{
    return 1;
}

if (args.Contains("--validate-only"))
{
    return 0;
}

// The same suite the UWP host runs, so the JIT and .NET Native numbers line up row for row.
if (args.Contains("--plain"))
{
    var harness = new Harness();
    Suite.Run(harness, includeRoundTrips: true);
    Console.WriteLine(harness.Report());
    return 0;
}

BenchmarkSwitcher.FromAssembly(typeof(ParseBenchmarks).Assembly).Run(args);
return 0;

static bool Validate()
{
    var ok = Validation.Run(Console.Error.WriteLine);

    // Reflection over every property is free here and not on .NET Native, so the dispatch
    // comparison stays in the desktop host.
    ok &= SameAsCurrent("message.json", ClientJson.FromJson_Message_Current, ClientJson.FromJson_Message_Alt);
    ok &= SameAsCurrent("localFile.json", ClientJson.FromJson_LocalFile_Current, ClientJson.FromJson_LocalFile_Alt);

    Console.WriteLine(ok ? "corpus ok" : "corpus FAILED");
    return ok;
}

static bool SameAsCurrent<T>(string fixture, Parser<T> current, Parser<T> candidate) where T : class
{
    var bytes = Fixtures.Load(fixture);

    var a = Run(current);
    var b = Run(candidate);
    var ok = true;

    foreach (var property in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
    {
        if (!property.PropertyType.IsPrimitive && property.PropertyType != typeof(string))
        {
            continue;
        }

        var left = property.GetValue(a);
        var right = property.GetValue(b);

        if (!Equals(left, right))
        {
            Console.Error.WriteLine($"FAIL {fixture}: {property.Name} {left} != {right}");
            ok = false;
        }
    }

    return ok;

    T Run(Parser<T> parser)
    {
        var reader = new Utf8JsonReader(bytes);
        reader.Read();
        reader.Read();
        reader.Read();
        reader.Read();
        return parser(ref reader, BenchmarkResultHandler.Instance);
    }
}



