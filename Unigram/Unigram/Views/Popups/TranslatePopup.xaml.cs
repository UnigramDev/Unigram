//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Globalization;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Unigram.Views.Popups
{
    public sealed partial class TranslatePopup : ContentPopup
    {
        private const string LANG_UND = "und";
        private const string LANG_AUTO = "auto";
        private const string LANG_LATN = "latn";

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

            Title = Strings.Resources.AutomaticTranslation;
            PrimaryButtonText = Strings.Resources.Close;
            //SecondaryButtonText = Strings.Resources.Language;

            var fromName = LanguageName(fromLanguage, out bool rtl);
            var toName = LanguageName(toLanguage, out _);

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

            var ticks = Environment.TickCount;

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
                var diff = Environment.TickCount - ticks;
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
                    block.Text = Strings.Resources.TranslationFailedAlert1;
                }
                else
                {
                    block.Text = Strings.Resources.TranslationFailedAlert2;
                }
            }

            _loadingMore = false;
        }

        private string LanguageName(string locale, out bool rtl)
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
            return culture.DisplayName;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
