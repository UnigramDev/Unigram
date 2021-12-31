using System;
using System.Globalization;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Controls;
using Unigram.Services;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Unigram.Views.Popups
{
    public sealed partial class TranslatePopup : ContentPopup
    {
        private readonly ITranslateService _translateService;
        private readonly string _fromLanguage;
        private readonly string _toLanguage;

        private bool _loadingMore;

        public TranslatePopup(ITranslateService translateService, string text, string fromLanguage, string toLanguage, bool contentProtected)
        {
            InitializeComponent();

            _translateService = translateService;
            _fromLanguage = fromLanguage == "und" ? "auto" : fromLanguage;
            _toLanguage = toLanguage;

            Title = Strings.Resources.AutomaticTranslation;
            PrimaryButtonText = Strings.Resources.CloseTranslation;

            var tokenizedText = translateService.Tokenize(text, 1024);

            var fromName = LanguageName(fromLanguage, out bool rtl);
            var toName = LanguageName(toLanguage, out _);

            if (string.IsNullOrEmpty(fromName))
            {
                SubtitleFrom.PlaceholderBrush = new SolidColorBrush(Colors.Transparent);
                SubtitleFrom.PlaceholderText = toName;
            }
            else
            {
                SubtitleFrom.PlaceholderText = fromName;
            }

            Subtitle.Text = string.Format(" \u2192 {0}", toName);

            foreach (var token in tokenizedText)
            {
                var block = new LoadingTextBlock
                {
                    PlaceholderText = token,
                    IsPlaceholderRightToLeft = rtl,
                    IsTextSelectionEnabled = !contentProtected,
                    Margin = new Thickness(0, Presenter.Children.Count > 0 ? -8 : 0, 0, 0)
                };

                if (Presenter.Children.Count > 0)
                {
                    block.EffectiveViewportChanged += Block_EffectiveViewportChanged;
                }

                Presenter.Children.Add(block);
            }

            Opened += OnOpened;
        }

        private async void Block_EffectiveViewportChanged(FrameworkElement sender, EffectiveViewportChangedEventArgs args)
        {
            System.Diagnostics.Debug.WriteLine(args.EffectiveViewport.Top);

            if (args.EffectiveViewport.Y > -100)
            {
                await TranslateTokenAsync(sender as LoadingTextBlock);
            }
        }

        private async void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            await TranslateTokenAsync(Presenter.Children[0] as LoadingTextBlock);
        }

        private async Task TranslateTokenAsync(LoadingTextBlock block)
        {
            if (_loadingMore || block.PlaceholderText == null || block.Tag != null)
            {
                return;
            }

            _loadingMore = true;

            // Unsubscribe to disable load more
            block.EffectiveViewportChanged -= Block_EffectiveViewportChanged;
            block.Tag = new object();

            var ticks = Environment.TickCount;

            var response = await _translateService.TranslateAsync(block.PlaceholderText, _fromLanguage, _toLanguage);
            if (response is Translation translation)
            {
                var diff = Environment.TickCount - ticks;
                if (diff < 1000)
                {
                    await Task.Delay(1000 - diff);
                }

                block.Text = translation.Text;
                SubtitleFrom.Text = LanguageName(translation.SourceLanguage, out _);
            }
            else if (response is Error error)
            {
                if (error.Code == 429)
                {
                    block.Text = Strings.Resources.TranslationFailedAlert1;
                    SubtitleFrom.Text = LanguageName(_fromLanguage == "auto" ? _toLanguage : _fromLanguage, out _);
                }
                else
                {
                    block.Text = Strings.Resources.TranslationFailedAlert2;
                    SubtitleFrom.Text = LanguageName(_fromLanguage == "auto" ? _toLanguage : _fromLanguage, out _);
                }
            }
        }

        private string LanguageName(string locale, out bool rtl)
        {
            if (locale == null || locale.Equals("und") || locale.Equals("auto"))
            {
                rtl = false;
                return null;
            }

            var split = locale.Split('-');
            var latin = split.Length > 1 && string.Equals(split[1], "latn", StringComparison.OrdinalIgnoreCase);

            var culture = new CultureInfo(split[0]);
            rtl = culture.TextInfo.IsRightToLeft && !latin;
            return culture.DisplayName;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }
    }
}
