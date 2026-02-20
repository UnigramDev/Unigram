//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Native;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml.Controls;

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

        public bool IsLanguageDetected => _languageDetected != null && _languageDetected != "und";

        public bool CanTranslate => _chat.IsTranslatable && TranslateService.CanTranslate(_languageDetected, true);

        private bool _isTranslating;
        public bool IsTranslating
        {
            get => _isTranslating && CanTranslate && HasAutomaticTranslation();
            set => SetTranslating(value);
        }

        private bool? _hasAutomaticTranslation;

        private bool HasAutomaticTranslation()
        {
            if (ClientService.IsPremium || _hasAutomaticTranslation == true)
            {
                return true;
            }
            else if (_hasAutomaticTranslation == null && ClientService.TryGetSupergroup(Chat, out Supergroup supergroup))
            {
                _hasAutomaticTranslation = supergroup.HasAutomaticTranslation;
                return supergroup.HasAutomaticTranslation;
            }

            return false;
        }

        private void SetTranslating()
        {
            if (Settings.Chats.TryGet(ChatId, TopicId, ChatSetting.IsTranslating, out bool value))
            {
                Set(ref _isTranslating, value, nameof(IsTranslating));
            }
        }

        private bool SetTranslating(bool value)
        {
            if (Set(ref _isTranslating, value, nameof(IsTranslating)))
            {
                Settings.Chats[ChatId, TopicId, ChatSetting.IsTranslating] = value;

                UpdateChatIsTranslatable();
                return true;
            }

            return false;
        }

        private void UpdateLanguageStatistics(MessageViewModel message)
        {
            if (IsLanguageDetected || message.IsOutgoing || string.IsNullOrEmpty(message.Text?.Text) || !HasAutomaticTranslation())
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
                if (IsLanguageDetected || _languageBuilder == null || !HasAutomaticTranslation())
                {
                    return;
                }

                _languageSlices++;

                var enough = _languageSlices == 2 || _languageMessages >= 10;
                var complete = IsNewestSliceLoaded is true && IsOldestSliceLoaded is true;

                if (enough || complete)
                {
                    _languageDetected = LanguageIdentification.IdentifyLanguage(_languageBuilder.ToString());
                    _languageBuilder = null;

                    Logger.Info(_languageDetected);
                    Dispatcher.Dispatch(UpdateChatIsTranslatable);
                }
                else
                {
                    _languageDetected = "und";
                    Dispatcher.Dispatch(() => Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected));
                }
            }
        }

        public void SummarizeMessage(MessageViewModel message)
        {
            var changed = message.SummarizedText == null;
            if (changed)
            {
                _translateService.Summarize(message, Settings.Translate.To);
            }
            else
            {
                message.SummarizedText = null;
            }

            if (changed != (message.SummarizedText == null))
            {
                Delegate?.UpdateBubbleWithMessageId(message.Id, bubble =>
                {
                    bubble.UpdateMessageTextLayout(message);
                    Delegate?.UpdateMessageSummary(message);
                });
            }
        }

        private void UpdateChatIsTranslatable()
        {
            Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected);

            var translating = IsTranslating;
            var translateTo = Settings.Translate.To;

            void TranslateMessage(MessageViewModel message, bool reply)
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
                    if (reply)
                    {
                        Delegate?.UpdateBubbleWithReplyToMessageId(message.Id, (bubble, reply) => bubble.UpdateMessageReply(reply));
                    }
                    else
                    {
                        Delegate?.UpdateBubbleWithMessageId(message.Id, bubble => bubble.UpdateMessageText(message));
                    }
                }
            }

            foreach (var message in Items)
            {
                TranslateMessage(message, false);

                if (message.ReplyToItem is MessageViewModel reply)
                {
                    TranslateMessage(reply, true);
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
            var detected = DetectedLanguage;
            if (string.IsNullOrEmpty(detected))
            {
                return;
            }

            var languageName = Services.TranslateService.LanguageName(detected);
            var toast = string.Format(Strings.AddedToDoNotTranslate, languageName);

            // TODO: add undo button
            ShowToast(toast, ToastPopupIcon.Translate);

            var languages = Settings.Translate.DoNot;
            languages.Add(detected);

            Settings.Translate.DoNot = languages;

            UpdateChatIsTranslatable();
        }

        public bool TranslateChat()
        {
            if (HasAutomaticTranslation())
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
