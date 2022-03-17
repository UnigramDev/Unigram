using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Native;
using Windows.Data.Json;

namespace Unigram.Services
{
    public class Translation
    {
        public Translation(string translated, string sourceLanguage)
        {
            Text = translated;
            SourceLanguage = sourceLanguage;
        }

        public string Text { get; private set; }

        public string SourceLanguage { get; private set; }
    }

    public interface ITranslateService
    {
        IList<string> Tokenize(string full, int maxBlockSize);

        bool CanTranslate(string text);

        Task<object> TranslateAsync(string text, string fromLanguage, string toLanguage);
    }

    public class TranslateService : ITranslateService
    {
        private readonly IProtoService _protoService;
        private readonly ISettingsService _settings;

        private readonly string[] _userAgents = new string[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.45 Safari/537.36", // 13.5%
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.110 Safari/537.36", // 6.6%
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:94.0) Gecko/20100101 Firefox/94.0", // 6.4%
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:95.0) Gecko/20100101 Firefox/95.0", // 6.2%
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.93 Safari/537.36", // 5.2%
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/96.0.4664.55 Safari/537.36" // 4.8%
        };

        public TranslateService(IProtoService protoService, ISettingsService settings)
        {
            _protoService = protoService;
            _settings = settings;
        }

        public bool CanTranslate(string text)
        {
            if (string.IsNullOrEmpty(text) || !_settings.IsTranslateEnabled)
            {
                return false;
            }

            var language = LanguageIdentification.IdentifyLanguage(text);
            var split = language.Split('-');

            var exclude = _settings.DoNotTranslate;
            if (exclude == null)
            {
                exclude = new[] { _settings.LanguagePackId };
            }

            foreach (var item in exclude)
            {
                var args = item.Split('_');
                if (string.Equals(args[0], split[0], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        public IList<string> Tokenize(string full, int maxBlockSize)
        {
            var blocks = new List<string>();
            if (full == null)
                return blocks;

            while (full.Length > maxBlockSize)
            {
                string maxBlockStr = full.Substring(0, maxBlockSize);
                int n = -1;
                if (n == -1) n = maxBlockStr.LastIndexOf("\n\n");
                if (n == -1) n = maxBlockStr.LastIndexOf("\n");
                if (n == -1) n = maxBlockStr.LastIndexOf(". ");
                blocks.Add(full.Substring(0, n + 1));
                full = full.Substring(n + 1);
            }

            if (full.Length > 0)
                blocks.Add(full);

            return blocks;
        }

        public async Task<object> TranslateAsync(string text, string fromLanguage, string toLanguage)
        {
            //var test = await _protoService.SendAsync(new TranslateText(text, fromLanguage, toLanguage));

            Random random = new Random();
            try
            {
                var uri = "https://translate.goo";
                uri += "gleapis.com/transl";
                uri += "ate_a";
                uri += "/singl";
                uri += "e?client=gtx&sl=" + Uri.EscapeDataString(fromLanguage) + "&tl=" + Uri.EscapeDataString(toLanguage) + "&dt=t" + "&ie=UTF-8&oe=UTF-8&otf=1&ssel=0&tsel=0&kc=7&dt=at&dt=bd&dt=ex&dt=ld&dt=md&dt=qca&dt=rw&dt=rm&dt=ss&q=";
                uri += Uri.EscapeDataString(text);

                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.TryAddWithoutValidation("User-Agent", _userAgents[random.Next(0, _userAgents.Length)]);
                request.Headers.TryAddWithoutValidation("Content-Type", "application/json");

                using var client = new HttpClient();
                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (JsonArray.TryParse(content, out JsonArray tokener))
                    {
                        var array = tokener.GetArrayAt(0);

                        string sourceLanguage = null;
                        try
                        {
                            sourceLanguage = tokener.GetStringAt(2);
                        }
                        catch { }

                        if (sourceLanguage != null && sourceLanguage.Contains("-"))
                        {
                            sourceLanguage = sourceLanguage.Substring(0, sourceLanguage.IndexOf("-"));
                        }

                        string result = "";
                        for (uint i = 0; i < array.Count; ++i)
                        {
                            var block = array.GetArrayAt(i)[0];
                            if (block.ValueType != JsonValueType.String)
                            {
                                continue;
                            }

                            var blockText = block.GetString();
                            if (blockText != null && !blockText.Equals("null"))
                                result += /*(i > 0 ? "\n" : "") +*/ blockText;
                        }

                        if (text.Length > 0 && text[0] == '\n')
                            result = "\n" + result;

                        return new Translation(result, sourceLanguage);
                    }
                    else
                    {
                        return new Error(500, "WHATEVER");
                    }
                }
                else
                {
                    var status = response != null ? (int)response.StatusCode : 500;
                    var rateLimit = status == 429;

                    return new Error(status, rateLimit ? "FLOOD_WAIT" : "WHATEVER");
                }
            }
            catch
            {
                return new Error(500, "WHATEVER");
            }
        }
    }
}
