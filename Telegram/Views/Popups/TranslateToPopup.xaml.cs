//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Rg.DiffUtils;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Controls;
using Telegram.Td;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Popups
{
    public class TranslateToLanguage
    {
        public TranslateToLanguage(string id, string name, string nativeName)
        {
            Id = id;
            Name = name;
            NativeName = nativeName;
        }

        public string Id { get; }

        public string Name { get; }

        public string NativeName { get; }
    }

    public sealed partial class TranslateToPopup : ContentPopup
    {
        private readonly List<TranslateToLanguage> _languages;
        private readonly DiffObservableCollection<TranslateToLanguage> _diff;

        public TranslateToPopup()
        {
            InitializeComponent();

            Title = Strings.TranslateTo;
            SecondaryButtonText = Strings.Cancel;

            var languages = new[]
            {
                // Same languages as in TDesktop
                "en", // English
                "ar", // Arabic
                "be", // Belarusian
                "ca", // Catalan
                "zh", // Chinese
                "nl", // Dutch
                "fr", // French
                "de", // German
                "id", // Indonesian
                "it", // Italian
                "ja", // Japanese
                "ko", // Korean
                "pl", // Polish
                "pt", // Portuguese
                "ru", // Russian
                "es", // Spanish
                "uk", // Ukrainian

                "af", // Afrikaans
                "sq", // Albanian
                "am", // Amharic
                "hy", // Armenian
                "az", // Azerbaijani
                "eu", // Basque
                "bs", // Bosnian
                "bg", // Bulgarian
                "my", // Burmese
                "hr", // Croatian
                "cs", // Czech
                "da", // Danish
                "eo", // Esperanto
                "et", // Estonian
                "fi", // Finnish
                "gd", // Gaelic
                "gl", // Galician
                "ka", // Georgian
                "el", // Greek
                "gu", // Gujarati
                "ha", // Hausa
                "he", // Hebrew
                "hu", // Hungarian
                "is", // Icelandic
                "ig", // Igbo
                "ga", // Irish
                "kk", // Kazakh
                "rw", // Kinyarwanda
                "ku", // Kurdish
                "lo", // Lao
                "lv", // Latvian
                "lt", // Lithuanian
                "lb", // Luxembourgish
                "mk", // Macedonian
                "mg", // Malagasy
                "ms", // Malay
                "mt", // Maltese
                "mi", // Maori
                "mn", // Mongolian
                "ne", // Nepali
                "ps", // Pashto
                "fa", // Persian
                "ro", // Romanian
                "sr", // Serbian
                "sn", // Shona
                "sd", // Sindhi
                "si", // Sinhala
                "sk", // Slovak
                "sl", // Slovenian
                "so", // Somali
                "su", // Sundanese
                "sw", // Swahili
                "sv", // Swedish
                "tg", // Tajik
                "tt", // Tatar
                "te", // Telugu
                "th", // Thai
                "tr", // Turkish
                "tk", // Turkmen
                "ur", // Urdu
                "uz", // Uzbek
                "vi", // Vietnamese
                "cy", // Welsh
                "fy", // Western Frisian
                "xh", // Xhosa
                "yi"  // Yiddish
            };

            var items = new List<TranslateToLanguage>(languages.Length);

            foreach (var lang in languages)
            {
                var culture = new CultureInfo(lang);
                items.Add(new TranslateToLanguage(lang, culture.DisplayName, culture.NativeName));
            }

            var handler = new DiffHandler<TranslateToLanguage>((x, y) =>
            {
                return x.Id == y.Id;
            });

            _languages = items;
            _diff = new DiffObservableCollection<TranslateToLanguage>(items, handler, Constants.DiffOptions);

            ScrollingHost.ItemsSource = _diff;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchField.Text))
            {
                _diff.ReplaceDiff(_languages);
            }
            else
            {
                _diff.ReplaceDiff(_languages.Where(FilterByQuery));
            }

            ShowHideNoResult(_diff.Count == 0);
        }

        private bool _noResultCollapsed = true;

        private void ShowHideNoResult(bool show)
        {
            if (_noResultCollapsed != show)
            {
                return;
            }

            _noResultCollapsed = !show;
            NoResult.Visibility = Visibility.Visible;

            var visual = ElementComposition.GetElementVisual(NoResult);
            var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
            animation.InsertKeyFrame(0, show ? 0 : 1);
            animation.InsertKeyFrame(1, show ? 1 : 0);

            visual.StartAnimation("Opacity", animation);
        }

        private bool FilterByQuery(TranslateToLanguage language)
        {
            if (ClientEx.SearchByPrefix(language.Name, SearchField.Text))
            {
                return true;
            }

            return ClientEx.SearchByPrefix(language.NativeName, SearchField.Text);
        }

        private void ScrollingHost_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is TranslateToLanguage language)
            {
                SelectedItem = language.Id;
                Hide(ContentDialogResult.Primary);
            }
        }

        public string SelectedItem { get; private set; }
    }
}
