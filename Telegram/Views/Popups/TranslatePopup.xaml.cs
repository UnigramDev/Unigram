//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Views.Popups
{
    public sealed partial class TranslatePopup : ContentPopup
    {
        private readonly ITranslateService _translateService;
        private readonly string _toLanguage;

        private readonly long _chatId;
        private readonly long _messageId;

        private bool _loadingMore;

        public TranslatePopup(ITranslateService translateService, string text, string fromLanguage, string toLanguage, bool contentProtected)
            : this(translateService, 0, 0, text, fromLanguage, toLanguage, contentProtected)
        {

        }

        public TranslatePopup(ITranslateService translateService, long chatId, long messageId, string text, string fromLanguage, string toLanguage, bool contentProtected)
        {
            InitializeComponent();

            _translateService = translateService;
            _toLanguage = toLanguage;

            _chatId = chatId;
            _messageId = messageId;

            Title = Strings.AutomaticTranslation;
            PrimaryButtonText = Strings.Close;
            //SecondaryButtonText = Strings.Language;

            var fromName = TranslateService.LanguageName(fromLanguage, out bool rtl);
            var toName = TranslateService.LanguageName(toLanguage);

            if (string.IsNullOrEmpty(fromName))
            {
                Subtitle.Text = string.Format("Auto \u2192 {0}", toName);
            }
            else
            {
                Subtitle.Text = string.Format("{0} \u2192 {1}", fromName, toName);
            }

            var block = new LoadingTextBlock
            {
                PlaceholderText = text,
                IsPlaceholderRightToLeft = rtl,
                IsTextSelectionEnabled = !contentProtected,
                Margin = new Thickness(0, Presenter.Children.Count > 0 ? -8 : 0, 0, 0)
            };

            Presenter.Children.Add(block);
            Opened += OnOpened;
        }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (Presenter.Children.Count > 0)
            {
                await TranslateTokenAsync(Presenter.Children[0] as LoadingTextBlock);
            }
        }

        private async Task TranslateTokenAsync(LoadingTextBlock block)
        {
            if (_loadingMore || block.PlaceholderText == null || block.Tag != null)
            {
                return;
            }

            _loadingMore = true;

            var ticks = Logger.TickCount;

            Task<object> task;
            if (_chatId != 0 && _messageId != 0)
            {
                task = _translateService.TranslateAsync(_chatId, _messageId, _toLanguage);
            }
            else
            {
                task = _translateService.TranslateAsync(block.PlaceholderText, _toLanguage);
            }

            var response = await task;
            if (response is FormattedText translation)
            {
                var diff = (int)(Logger.TickCount - ticks);
                if (diff < 1000)
                {
                    await Task.Delay(1000 - diff);
                }

                block.Text = translation.Text;
            }
            else if (response is Error error)
            {
                if (error.Code == 429)
                {
                    block.Text = Strings.TranslationFailedAlert1;
                }
                else
                {
                    block.Text = Strings.TranslationFailedAlert2;
                }
            }

            _loadingMore = false;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
