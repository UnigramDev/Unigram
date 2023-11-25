//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Native;
using Telegram.Td.Api;

namespace Telegram.Services
{
    public interface ITranslateService
    {
        bool CanTranslate(string text);
        bool CanTranslate(FormattedText text);

        Task<object> TranslateAsync(long chatId, long messageId, string toLanguage);
        Task<object> TranslateAsync(string text, string toLanguage);
        Task<object> TranslateAsync(FormattedText text, string toLanguage);
    }

    public class TranslateService : ITranslateService
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settings;

        public TranslateService(IClientService clientService, ISettingsService settings)
        {
            _clientService = clientService;
            _settings = settings;
        }

        public bool CanTranslate(FormattedText text)
        {
            if (text == null)
            {
                return false;
            }

            return CanTranslate(text.Text);
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
