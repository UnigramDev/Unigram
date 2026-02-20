//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Runtime.InteropServices;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class SuggestPostPopup : ContentPopup
    {
        public long Minimum { get; set; } = 0;
        public long Maximum { get; set; } = long.MaxValue;

        public InputScopeNameValue InputScope { get; set; }
        public INumberFormatter2 Formatter { get; set; }

        private readonly IClientService _clientService;

        public SuggestPostPopup(IClientService clientService, InputSuggestedPostInfo suggestedPostInfo)
        {
            InitializeComponent();
            Formatter = GetRegionalSettingsAwareDecimalFormatter();

            _clientService = clientService;

            Title = Strings.PostSuggestionsOfferTitle;

            SendDateFooter.Text = string.Format("{0} {1}", Strings.PostSuggestionsAddTimeHint, string.Format(Strings.PostSuggestionsAddTimeHint2, (clientService.Options.SuggestedPostLifetimeMin / 3600.0).ToString("N0")));

            var scope = new InputScope();
            var name = new InputScopeName();

            name.NameValue = InputScopeNameValue.Number;

            scope.Names.Add(name);
            Label.InputScope = scope;

            if (suggestedPostInfo?.Price is SuggestedPostPriceStar priceStar)
            {
                Navigation.SelectedIndex = 0;
                Label.Text = Formatter.FormatInt(priceStar.StarCount);
            }
            else if (suggestedPostInfo?.Price is SuggestedPostPriceTon priceTon)
            {
                Navigation.SelectedIndex = 1;
                Label.Text = Formatter.FormatDouble(priceTon.ToncoinCentCount / 100d);
            }
            else
            {
                Navigation.SelectedIndex = 0;
                Label.Text = Formatter.FormatInt(0);
            }

            Label.Focus(FocusState.Keyboard);
            Label.SelectionStart = Label.Text.Length;
            Label.Padding = new Thickness(36, Label.Padding.Top, Label.Padding.Right, Label.Padding.Bottom);

            if (suggestedPostInfo != null && suggestedPostInfo.SendDate != 0)
            {
                SendDate.Content = Telegram.Converters.Formatter.DateAt(suggestedPostInfo.SendDate);
            }

            SuggestedPostInfo = suggestedPostInfo;
        }

        public InputSuggestedPostInfo SuggestedPostInfo { get; private set; }

        internal const int LOCALE_NAME_MAX_LENGTH = 85;

#if NET9_0_OR_GREATER
        [LibraryImport("kernel32.dll")]
        private static partial int GetUserDefaultLocaleName(Span<char> buf, int bufferLength);

        private static string GetUserDefaultLocaleName()
        {
            Span<char> buffer = stackalloc char[LOCALE_NAME_MAX_LENGTH];

            int result = GetUserDefaultLocaleName(buffer, buffer.Length);
            if (result != 0)
            {
                int length = buffer.IndexOf('\0');
                if (length == -1)
                    length = result - 1;

                return new string(buffer.Slice(0, length));
            }
            return null;
        }
#else
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetUserDefaultLocaleName(StringBuilder buf, int bufferLength);

        private static string GetUserDefaultLocaleName()
        {
            var sb = new StringBuilder(LOCALE_NAME_MAX_LENGTH);
            if (GetUserDefaultLocaleName(sb, LOCALE_NAME_MAX_LENGTH) != 0)
            {
                return sb.ToString();
            }

            return null;
        }
#endif

        // This was largely copied from Calculator's GetRegionalSettingsAwareDecimalFormatter()
        private DecimalFormatter GetRegionalSettingsAwareDecimalFormatter()
        {
            DecimalFormatter formatter = null;

            var currentLocale = GetUserDefaultLocaleName();
            if (currentLocale != null)
            {
                // GetUserDefaultLocaleName may return an invalid bcp47 language tag with trailing non-BCP47 friendly characters,
                // which if present would start with an underscore, for example sort order
                // (see https://msdn.microsoft.com/en-us/library/windows/desktop/dd373814(v=vs.85).aspx).
                // Therefore, if there is an underscore in the locale name, trim all characters from the underscore onwards.

                var underscore = currentLocale.IndexOf('_');
                if (underscore != -1)
                {
                    currentLocale.Substring(0, underscore);
                }

                if (Windows.Globalization.Language.IsWellFormed(currentLocale))
                {
                    formatter = new DecimalFormatter(new[] { currentLocale }, GlobalizationPreferences.HomeGeographicRegion);
                }
            }

            if (formatter == null)
            {
                formatter = new DecimalFormatter();
            }

            formatter.IntegerDigits = 1;
            formatter.FractionDigits = 0;

            return formatter;
        }

        public event EventHandler<InputPopupValidatingEventArgs> Validating;

        private void OnBeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
        {
            var newValue = ParseInt(args.NewText);
            if (newValue >= Minimum && newValue <= Maximum)
            {
                OnValueChanged(newValue.Value);
            }
            else
            {
                OnValueChanged(0);
                SuggestedPostInfo = new InputSuggestedPostInfo(null, SuggestedPostInfo?.SendDate ?? 0);

                if (newValue > Maximum)
                {
                    VisualUtilities.QueueCallbackForCompositionRendered(InvalidateToMaximum);
                }

                args.Cancel = args.NewText.Length > 0;
            }
        }

        private long? ParseInt(string newValue)
        {
            var parser = Formatter as INumberParser;

            if (Navigation.SelectedIndex == 1)
            {
                var value = parser?.ParseDouble(newValue);
                if (value.HasValue)
                {
                    var cent = value * 100;
                    if (cent > 0 && cent < 1)
                    {
                        return null;
                    }

                    var rounded = (long)cent;
                    if (rounded != cent)
                    {
                        return null;
                    }

                    return rounded;
                }
            }
            else
            {
                return parser?.ParseInt(newValue);
            }

            return null;
        }

        private void InvalidateToMaximum()
        {
            var selectionStart = Label.SelectionStart;

            Label.Text = Formatter.FormatInt(Maximum);
            Label.SelectionStart = selectionStart + 1;

            VisualUtilities.ShakeView(InputRoot);
        }

        private void OnValueChanged(long value)
        {
            if (Navigation.SelectedIndex == 1)
            {
                var xtr = value / 1000000d;
                var usd = xtr * _clientService.Options.MillionToncoinToUsdRate;

                SuggestedPostInfo = new InputSuggestedPostInfo(value > 0 ? new SuggestedPostPriceTon(value) : null, SuggestedPostInfo?.SendDate ?? 0);
                Price.Text = "~" + Telegram.Converters.Formatter.FormatAmount((long)usd, "USD");

                IsPrimaryButtonEnabled = value == 0 || (value >= _clientService.Options.SuggestedPostToncoinCentCountMin && value <= _clientService.Options.SuggestedPostToncoinCentCountMax);
                PrimaryButtonText = string.Format(Strings.PostSuggestionsOfferTON, (value / 100d).ToString("0.##"));
            }
            else
            {
                var xtr = value / 1000d;
                var usd = xtr * _clientService.Options.ThousandStarToUsdRate;

                SuggestedPostInfo = new InputSuggestedPostInfo(value > 0 ? new SuggestedPostPriceStar(value) : null, SuggestedPostInfo?.SendDate ?? 0);
                Price.Text = "~" + Telegram.Converters.Formatter.FormatAmount((long)usd, "USD");

                IsPrimaryButtonEnabled = value == 0 || (value >= _clientService.Options.SuggestedPostStarCountMin && value <= _clientService.Options.SuggestedPostStarCountMax);
                PrimaryButtonText = string.Format(Strings.PostSuggestionsOfferStars.ReplaceStar(Icons.Premium), value);
            }
        }

        private void Label_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            Hide(ContentDialogResult.Primary);
        }

        private void Label_TextChanged(object sender, TextChangedEventArgs e)
        {
            //IsPrimaryButtonEnabled = Label.Text.Length >= MinLength;
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HeaderText.Text = Navigation.SelectedIndex switch
            {
                1 => Strings.PostSuggestionsOfferTitlePriceTON,
                _ => Strings.PostSuggestionsOfferTitlePriceStars
            };

            FooterText.Text = Navigation.SelectedIndex switch
            {
                1 => Strings.PostSuggestionsOfferSubtitleTON,
                _ => Strings.PostSuggestionsOfferSubtitleStars
            };

            StarCount.Visibility = Navigation.SelectedIndex == 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            TonCount.Visibility = Navigation.SelectedIndex == 1
                ? Visibility.Visible
                : Visibility.Collapsed;

            Label.Text = Navigation.SelectedIndex switch
            {
                1 => Formatter.FormatDouble(0),
                _ => Formatter.FormatInt(0)
            };

            Label.Focus(FocusState.Keyboard);
            Label.SelectAll();
        }

        private async void SendDate_Click(object sender, RoutedEventArgs e)
        {
            var popup = new ChooseDateTimeToast
            {
                Title = Strings.PostSuggestionsAddTime,
                Subtitle = Strings.PostSuggestionsAddTimeHint,
                ActionButtonContent = Strings.OK,
                ActionButtonStyle = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                CloseButtonContent = Strings.Cancel,
                PreferredPlacement = TeachingTipPlacementMode.Center,
                IsLightDismissEnabled = true,
                ShouldConstrainToRootBounds = true,
            };

            var confirm = await popup.ShowAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                SuggestedPostInfo = new InputSuggestedPostInfo(SuggestedPostInfo?.Price, (int)popup.Value.ToTimestamp());
                SendDate.Content = Telegram.Converters.Formatter.DateAt(popup.Value);
            }
        }
    }
}
