//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;

namespace Telegram.Views.Popups
{
    public sealed partial class TranslatePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly ITranslateService _translateService;
        private readonly string _toLanguage;
        private readonly string _tone;

        private readonly long _chatId;
        private readonly long _messageId;

        private readonly FormattedText _text;

        private readonly string _fromLanguage;
        private readonly bool _contentProtected;

        private readonly TextSelectionManager _textSelectionManager;

        private bool _loadingMore;

        public TranslatePopup(ITranslateService translateService, string text, string fromLanguage, string toLanguage, bool contentProtected)
            : this(translateService, 0, 0, text.AsFormattedText(), fromLanguage, toLanguage, contentProtected)
        {

        }

        public TranslatePopup(ITranslateService translateService, FormattedText text, string fromLanguage, string toLanguage, bool contentProtected)
            : this(translateService, 0, 0, text, fromLanguage, toLanguage, contentProtected)
        {

        }

        public TranslatePopup(ITranslateService translateService, long chatId, long messageId, FormattedText text, string fromLanguage, string toLanguage, bool contentProtected)
        {
            InitializeComponent();

            _clientService = translateService.ClientService;
            _translateService = translateService;
            _toLanguage = toLanguage;

            _chatId = chatId;
            _messageId = messageId;

            _text = text;

            _fromLanguage = fromLanguage;
            _contentProtected = contentProtected;

            _textSelectionManager = new TextSelectionManager(this, Block, handleContextMenu: true);

            Title = Strings.AutomaticTranslation;
            PrimaryButtonText = Strings.Close;
            //SecondaryButtonText = Strings.Language;

            var fromName = TranslateService.LanguageName(fromLanguage, out bool rtl);
            var toName = TranslateService.LanguageName(toLanguage);

            if (string.IsNullOrEmpty(fromName))
            {
                FromLanguage.Text = "Auto \u2192";
            }
            else
            {
                FromLanguage.Text = string.Format("{0} \u2192", fromName);
            }

            ToLanguage.Text = toName;

            Block.ShowHideSkeleton(true);
            Block.SetText(_clientService, text);

            Opened += OnOpened;
        }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (_loadingMore)
            {
                return;
            }

            _loadingMore = true;

            var ticks = Logger.TickCount;

            Task<object> task;
            if (_chatId != 0 && _messageId != 0)
            {
                task = _translateService.TranslateAsync(_chatId, _messageId, _toLanguage, _tone);
            }
            else
            {
                task = _translateService.TranslateAsync(_text, _toLanguage, _tone);
            }

            var response = await task;
            if (response is FormattedText translation)
            {
                var diff = (int)(Logger.TickCount - ticks);
                if (diff < 1000)
                {
                    await Task.Delay(1000 - diff);
                }

                Block.ShowHideSkeleton(false);
                Block.SetText(_clientService, translation);
            }
            else if (response is Error error)
            {
                Block.ShowHideSkeleton(false);

                if (error.Code == 429)
                {
                    Block.SetText(_clientService, Strings.TranslationFailedAlert1.AsFormattedText());
                }
                else
                {
                    Block.SetText(_clientService, Strings.TranslationFailedAlert2.AsFormattedText());
                }
            }

            _loadingMore = false;
        }

        private async void ToLanguage_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();

            var popup = new TranslateToPopup();

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary && popup.SelectedItem != null)
            {
                var translate = new TranslatePopup(_translateService, _chatId, _messageId, _text, _fromLanguage, popup.SelectedItem, _contentProtected);
                _ = translate.ShowQueuedAsync(XamlRoot);
            }
        }
    }
}
