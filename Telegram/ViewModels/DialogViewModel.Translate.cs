﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        private readonly object _languageLock = new();
        private StringBuilder _languageBuilder;
        private int _languageMessages;
        private int _languageSlices;
        private string _languageDetected;

        public string DetectedLanguage => _languageDetected;

        public bool CanTranslate => _chat.IsTranslatable && TranslateService.CanTranslate(_languageDetected, true);

        private bool _isTranslating;
        public bool IsTranslating
        {
            get => _isTranslating && CanTranslate && ClientService.IsPremium;
            set => SetTranslating(value);
        }

        private void SetTranslating()
        {
            if (Settings.Chats.TryGet(Chat.Id, ThreadId, ChatSetting.IsTranslating, out bool value))
            {
                Set(ref _isTranslating, value, nameof(IsTranslating));
            }
        }

        private bool SetTranslating(bool value)
        {
            if (Set(ref _isTranslating, value, nameof(IsTranslating)))
            {
                Settings.Chats[Chat.Id, ThreadId, ChatSetting.IsTranslating] = value;

                UpdateChatIsTranslatable();
                return true;
            }

            return false;
        }

        private void UpdateLanguageStatistics(MessageViewModel message)
        {
            if (_languageDetected != null || message.IsOutgoing || message.Text == null || !ClientService.IsPremium)
            {
                return;
            }

            lock (_languageLock)
            {
                _languageBuilder ??= new();

                foreach (var paragraph in message.Text.Paragraphs)
                {
                    if (paragraph.Type is not TextParagraphTypeMonospace)
                    {
                        _languageBuilder.Prepend(message.Text.Text.Substring(paragraph.Offset, paragraph.Length), "\n");
                    }
                }
            }

            _languageMessages++;

            if (IsTranslating)
            {
                _translateService.Translate(message, Settings.Translate.To);
            }
        }

        private void UpdateDetectedLanguage()
        {
            if (Dispatcher.HasThreadAccess)
            {
                Task.Run(UpdateDetectedLanguage);
                return;
            }

            lock (_languageLock)
            {
                if (_languageDetected != null || _languageBuilder == null || !ClientService.IsPremium)
                {
                    return;
                }

                _languageSlices++;

                var enough = _languageSlices == 2 || _languageMessages >= 10;
                var complete = IsFirstSliceLoaded is true && IsLastSliceLoaded is true;

                if (enough || complete)
                {
                    _languageDetected = LanguageIdentification.IdentifyLanguage(_languageBuilder.ToString());
                    _languageBuilder = null;

                    Logger.Info(_languageDetected);
                    Dispatcher.Dispatch(UpdateChatIsTranslatable);
                }
            }
        }

        private void UpdateChatIsTranslatable()
        {
            Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected);

            var translating = IsTranslating;
            var translateTo = Settings.Translate.To;

            foreach (var message in Items)
            {
                var changed = message.TranslatedText == null;
                if (translating)
                {
                    _translateService.Translate(message, translateTo);
                }
                else
                {
                    message.TranslatedText = null;
                }

                if (changed != (message.TranslatedText == null))
                {
                    Delegate?.UpdateBubbleWithMessageId(message.Id, bubble => bubble.UpdateMessageText(message));
                }
            }
        }

        public void HideTranslate()
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            ClientService.Send(new ToggleChatIsTranslatable(chat.Id, false));
            IsTranslating = false;

            var toast = chat.Type switch
            {
                ChatTypeSupergroup { IsChannel: true } => Strings.TranslationBarHiddenForChannel,
                ChatTypeSupergroup => Strings.TranslationBarHiddenForGroup,
                ChatTypeBasicGroup => Strings.TranslationBarHiddenForGroup,
                _ => Strings.TranslationBarHiddenForChat
            };

            // TODO: add undo button
            ShowToast(toast, ToastPopupIcon.Translate);
        }

        public async void ShowTranslate()
        {
            if (_chat is not Chat chat)
            {
                return;
            }

            await ClientService.SendAsync(new ToggleChatIsTranslatable(chat.Id, true));

            if (SetTranslating(true))
            {
                return;
            }

            UpdateChatIsTranslatable();
        }

        public async void EditTranslate()
        {
            var popup = new TranslateToPopup();

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                Settings.Translate.To = popup.SelectedItem;

                if (SetTranslating(true))
                {
                    return;
                }

                UpdateChatIsTranslatable();
            }
        }

        public void StopTranslate()
        {
            var languageName = Services.TranslateService.LanguageName(DetectedLanguage);
            var toast = string.Format(Strings.AddedToDoNotTranslate, languageName);

            // TODO: add undo button
            ShowToast(toast, ToastPopupIcon.Translate);

            var languages = Settings.Translate.DoNot;
            languages.Add(DetectedLanguage);

            Settings.Translate.DoNot = languages;

            UpdateChatIsTranslatable();
        }

        public bool TranslateChat()
        {
            if (ClientService.IsPremium)
            {
                IsTranslating = !_isTranslating;
            }
            else
            {
                IsTranslating = false;
                NavigationService.ShowPromo(new PremiumSourceFeature(new PremiumFeatureRealTimeChatTranslation()));
            }

            return IsTranslating;
        }
    }
}
