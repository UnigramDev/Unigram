//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Telegram.Native;

namespace Unigram.Services
{
    public interface ITranslateService
    {
        bool CanTranslate(string text);

        Task<object> TranslateAsync(long chatId, long messageId, string toLanguage);
        Task<object> TranslateAsync(string text, string toLanguage);
        Task<object> TranslateAsync(FormattedText text, string toLanguage);
    }

    public class TranslateService : ITranslateService
    {
        private readonly IClientService _clientService;
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

        public TranslateService(IClientService clientService, ISettingsService settings)
        {
            _clientService = clientService;
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

        public Task<object> TranslateAsync(string text, string toLanguage)
        {
            return TranslateAsync(new FormattedText(text, null), toLanguage);
        }

        public async Task<object> TranslateAsync(FormattedText text, string toLanguage)
        {
            return await _clientService.SendAsync(new TranslateText(text, toLanguage));
        }

        public async Task<object> TranslateAsync(long chatId, long messageId, string toLanguage)
        {
            return await _clientService.SendAsync(new TranslateMessageText(chatId, messageId, toLanguage));
        }
    }
}
