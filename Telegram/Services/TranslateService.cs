//
// Copyright Fela Ameghino 2015-2025
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Native;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels;

namespace Telegram.Services
{
    public interface ITranslateService
    {
        bool CanTranslateText(string text, bool entireChat = false);
        bool CanTranslateText(FormattedText text, bool entireChat = false);

        bool CanTranslate(string language, bool entireChat);

        Task<object> TranslateAsync(long chatId, long messageId, string toLanguage);
        Task<object> TranslateAsync(string text, string toLanguage);
        Task<object> TranslateAsync(FormattedText text, string toLanguage);

        bool Translate(MessageViewModel message, string toLanguage);
    }

    public partial class TranslateService : ITranslateService
    {
        private readonly IClientService _clientService;
        private readonly ISettingsService _settings;
        private readonly IEventAggregator _aggregator;

        private const string LANG_UND = "und";
        private const string LANG_AUTO = "auto";
        private const string LANG_LATN = "latn";

        public TranslateService(IClientService clientService, ISettingsService settings, IEventAggregator aggregator)
        {
            _clientService = clientService;
            _settings = settings;
            _aggregator = aggregator;
        }

        public static string LanguageName(string locale)
        {
            return LanguageName(locale, out _);
        }

        public static string LanguageName(string locale, out bool rtl)
        {
            if (locale == null || locale.Equals(LANG_UND) || locale.Equals(LANG_AUTO))
            {
                rtl = false;
                return null;
            }

            var split = locale.Split('-');
            var latin = split.Length > 1 && string.Equals(split[1], LANG_LATN, StringComparison.OrdinalIgnoreCase);

            var culture = new CultureInfo(split[0]);
            rtl = culture.TextInfo.IsRightToLeft && !latin;

            var displayName = LocaleService.Current.GetString("TranslateLanguage" + split[0].ToUpper());
            if (displayName.Length > 0)
            {
                return displayName;
            }

            return culture.DisplayName;
        }

        public bool CanTranslateText(FormattedText text, bool entireChat = false)
        {
            if (text == null)
            {
                return false;
            }

            return CanTranslateText(text.Text, entireChat);
        }

        public bool CanTranslateText(string text, bool entireChat = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var language = LanguageIdentification.IdentifyLanguage(text);
            return CanTranslate(language, entireChat);
        }

        public bool CanTranslate(string language, bool entireChat)
        {
            var allowed = entireChat
                ? _settings.Translate.Chats
                : _settings.Translate.Messages;

            if (string.IsNullOrEmpty(language) || !allowed)
            {
                return false;
            }

            var split = language.Split('-');
            var exclude = _settings.Translate.DoNot;

            if (entireChat)
            {
                exclude.Add(LANG_UND);
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

        private readonly ConcurrentDictionary<TranslatedKey, TranslatedMessage> _translations = new();

        public bool Translate(MessageViewModel message, string toLanguage)
        {
            if (message.IsOutgoing || message.Text == null)
            {
                return false;
            }

            var key = new TranslatedKey(message.ChatId, message.Id, toLanguage);
            var cached = message.Text.Text;

            if (_translations.TryGetValue(key, out var value))
            {
                if (string.Equals(cached, value.Text))
                {
                    if (value.Result != null)
                    {
                        message.TranslatedText = value.Result;
                    }

                    message.TranslatedText ??= new MessageTranslateResultPending();
                    return false;
                }
            }

            if (CanTranslateText(message.Text.Text, true))
            {
                message.TranslatedText = new MessageTranslateResultPending();

                _translations[key] = new TranslatedMessage(cached, null);
                _clientService.Send(new TranslateMessageText(message.ChatId, message.Id, toLanguage), handler =>
                {
                    if (handler is FormattedText text && string.Equals(message.Text?.Text, cached))
                    {
                        // Entities are lost!!!
                        text = ClientEx.MergeEntities(text, ClientEx.GetTextEntities(text.Text));

                        var styled = TextStyleRun.GetText(text);
                        var result = new MessageTranslateResultText(toLanguage, styled);

                        message.TranslatedText = result;

                        _translations[key] = new TranslatedMessage(cached, result);
                        _aggregator.Publish(new UpdateMessageTranslatedText(message.ChatId, message.Id, result));
                    }
                });

                return true;
            }

            message.TranslatedText = null;
            return false;
        }


        struct TranslatedKey
        {
            public TranslatedKey(long chatId, long messageId, string toLanguage)
            {
                ChatId = chatId;
                MessageId = messageId;
                ToLanguage = toLanguage;
            }

            public long ChatId;
            public long MessageId;
            public string ToLanguage;
        }

        struct TranslatedMessage
        {
            public TranslatedMessage(string text, MessageTranslateResultText result)
            {
                Text = text;
                Result = result;
            }

            public string Text;
            public MessageTranslateResultText Result;
        }
    }
}
