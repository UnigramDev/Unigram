//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Text;

namespace Telegram.Services.Wallet
{
    /// <summary>
    /// A TON Connect request, as it reaches the app through a Telegram deep link.
    /// </summary>
    /// <remarks>
    /// **TEMPORARY.** TDLib will report these as an <c>InternalLinkType</c> of its own, and this
    /// whole class goes when it does, along with the two cases in
    /// <see cref="Common.MessageHelper"/> that call it. Nothing else should learn this encoding:
    /// what comes out of <see cref="Parse"/> is what the rest of the wallet is written against.
    ///
    /// The wallet's TON Connect address is a t.me link, so the TON Connect SDK treats it as a
    /// mini app and folds the whole request into one <c>startapp</c> parameter - which may only
    /// hold letters, digits, <c>_</c> and <c>-</c>. What it does to fit is:
    ///
    /// <code>
    ///   %  ->  --      =  ->  __      &amp;  ->  -
    /// </code>
    ///
    /// so the parameter reads <c>tonconnect-v__2-id__&lt;hex&gt;-r__--7B--22manifestUrl--22...</c>
    /// and unfolds into an ordinary query string. TDLib does not know this shape yet, so it arrives
    /// as a mini app link and is recognised here instead - by the <c>tonconnect</c> prefix rather
    /// than by which bot it names, since that is what identifies it.
    /// </remarks>
    public partial class TonConnectLink
    {
        /// <summary>
        /// What every one of these starts with, and the only thing that tells it apart from a real
        /// mini app parameter.
        /// </summary>
        public const string Prefix = "tonconnect";

        private TonConnectLink(Dictionary<string, string> parameters)
        {
            Parameters = parameters;
        }

        public IReadOnlyDictionary<string, string> Parameters { get; }

        /// <summary>The dApp's client id, hex encoded: half of the pair the session is keyed by.</summary>
        public string ClientId => Get("id");

        /// <summary>What the dApp is asking for, as the JSON the manifest URL arrives inside.</summary>
        public string Request => Get("r");

        /// <summary>The protocol version, which is 2 at the time of writing.</summary>
        public string Version => Get("v");

        /// <summary>Where the dApp wants the user sent afterwards, or "none".</summary>
        public string ReturnStrategy => Get("ret");

        /// <summary>Carried through untouched, for the account to correlate the two sides.</summary>
        public string TraceId => Get("trace_id");

        private string Get(string name)
        {
            return Parameters.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Reads a <c>startapp</c> parameter, or answers null when it is not one of these.
        /// </summary>
        public static TonConnectLink Parse(string startParameter)
        {
            if (string.IsNullOrEmpty(startParameter) || !startParameter.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            // Two characters before one, or the pairs would be read as two separators: a "--" that
            // became "%" must not then be seen as two "&".
            var builder = new StringBuilder(startParameter.Length);

            for (int i = 0; i < startParameter.Length; i++)
            {
                if (i + 1 < startParameter.Length && startParameter[i] == '-' && startParameter[i + 1] == '-')
                {
                    builder.Append('%');
                    i++;
                }
                else if (i + 1 < startParameter.Length && startParameter[i] == '_' && startParameter[i + 1] == '_')
                {
                    builder.Append('=');
                    i++;
                }
                else if (startParameter[i] == '-')
                {
                    builder.Append('&');
                }
                else
                {
                    builder.Append(startParameter[i]);
                }
            }

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var query = builder.ToString().Split('&');

            // The first part is the prefix itself and carries no value.
            for (int i = 1; i < query.Length; i++)
            {
                var separator = query[i].IndexOf('=');
                if (separator < 1)
                {
                    continue;
                }

                var name = query[i].Substring(0, separator);
                var value = query[i].Substring(separator + 1);

                try
                {
                    // Only now, and only the value: percent escapes are what the folding above was
                    // hiding, and unescaping any earlier would put "&" and "=" back into it.
                    parameters[Uri.UnescapeDataString(name)] = Uri.UnescapeDataString(value);
                }
                catch
                {
                    // A parameter that is not valid escaping is one the rest can do without - the
                    // request either carries what it needs or is refused for lacking it.
                }
            }

            return parameters.Count > 0
                ? new TonConnectLink(parameters)
                : null;
        }
    }
}
