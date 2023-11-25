using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Native;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.ViewModels
{
    public partial class DialogViewModel
    {
        private StringBuilder _languageBuilder;
        private int _languageMessages;
        private int _languageSlices;
        private string _languageDetected;

        public string DetectedLanguage => _languageDetected;
        public bool IsTranslatable => _chat.IsTranslatable && (_languageDetected == null || TranslateService.CanTranslate(_languageDetected, true));

        private bool _isTranslating;
        public bool IsTranslating
        {
            get => _isTranslating && IsTranslatable;
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
                Delegate?.ForEach(UpdateMessageTranslatedText);
                return true;
            }

            return false;
        }

        private void UpdateMessageTranslatedText(MessageBubble bubble, MessageViewModel message)
        {
            _translateService.Translate(message, Settings.Translate.To);
            bubble.UpdateMessageText(message);
        }

        private void UpdateLanguageStatistics(MessageViewModel message)
        {
            if (_languageDetected != null || message.IsOutgoing || message.Text == null || !IsTranslatable)
            {
                return;
            }

            _languageBuilder ??= new();

            foreach (var paragraph in message.Text.Paragraphs)
            {
                if (paragraph.Type != Common.ParagraphStyle.Monospace)
                {
                    _languageBuilder.Prepend(message.Text.Text.Substring(paragraph.Offset, paragraph.Length), "\n");
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

            if (_languageDetected != null || _languageBuilder == null || !IsTranslatable)
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

        private void UpdateChatIsTranslatable()
        {
            Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected);
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
            Window.Current.ShowToast(toast, new LocalFileSource("ms-appx:///Assets/Toasts/Translate.tgs"));
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

            Delegate?.ForEach(UpdateMessageTranslatedText);
        }

        public async void EditTranslate()
        {
            var popup = new TranslateToPopup();

            var confirm = await ShowPopupAsync(popup);
            if (confirm == ContentDialogResult.Primary)
            {
                Settings.Translate.To = popup.SelectedItem;
                Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected);

                if (SetTranslating(true))
                {
                    return;
                }

                Delegate?.ForEach(UpdateMessageTranslatedText);
            }
        }

        public void StopTranslate()
        {
            var languageName = Services.TranslateService.LanguageName(DetectedLanguage);
            var toast = string.Format(Strings.AddedToDoNotTranslate, languageName);

            // TODO: add undo button
            Window.Current.ShowToast(toast, new LocalFileSource("ms-appx:///Assets/Toasts/Translate.tgs"));

            var languages = Settings.Translate.DoNot;
            languages.Add(DetectedLanguage);

            Settings.Translate.DoNot = languages;

            Delegate?.UpdateChatIsTranslatable(_chat, _languageDetected);
            Delegate?.ForEach(UpdateMessageTranslatedText);
        }
    }
}
