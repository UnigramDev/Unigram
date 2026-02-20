//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Runtime.InteropServices;
using System.Text;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Globalization.NumberFormatting;
using Windows.System.UserProfile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class StakeDicePopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly MessageViewModel _message;

        public INumberFormatter2 Formatter { get; set; }

        public long Maximum { get; set; }
        public long Minimum { get; set; }

        public StakeDicePopup(MessageViewModel message)
        {
            InitializeComponent();
            Formatter = GetRegionalSettingsAwareDecimalFormatter();

            _clientService = message.ClientService;
            _message = message;

            Maximum = _clientService.Options.StakeDiceStakeAmountMax;
            Minimum = _clientService.Options.StakeDiceStakeAmountMin;

            var state = message.ClientService.StakeDiceState;
            if (state.PrizePerMille.Count > 0)
            {
                ApplyPrizeValue(state.PrizePerMille[0], Returns1);
                ApplyPrizeValue(state.PrizePerMille[1], Returns2);
                ApplyPrizeValue(state.PrizePerMille[2], Returns3);
                ApplyPrizeValue(state.PrizePerMille[3], Returns4);
                ApplyPrizeValue(state.PrizePerMille[4], Returns5);
                ApplyPrizeValue(state.PrizePerMille[5], Returns6);
            }

            ApplyPrizeValue(state.StreakPrizePerMille, ReturnsStreak);

            int width = 3;

            for (int i = 0; i < state.SuggestedStakeToncoinAmounts.Count; i++)
            {
                int x = i % width;
                int y = i / width;

                if (x == 0)
                {
                    Suggested.RowDefinitions.Add(new RowDefinition());
                }

                var button = new Button
                {
                    Content = string.Format("{0:0.#} \U0001F48E", state.SuggestedStakeToncoinAmounts[i] / Constants.ToncoinMin),
                    Style = BootStrapper.Current.Resources["AccentButtonStyle"] as Style,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Tag = state.SuggestedStakeToncoinAmounts[i]
                };

                button.Click += Button_Click;

                Grid.SetColumn(button, x);
                Grid.SetRow(button, y);

                Suggested.Children.Add(button);
            }

            Label.Padding = new Thickness(36, Label.Padding.Top, Label.Padding.Right, Label.Padding.Bottom);
            Label.Text = Formatter.FormatDouble(state.StakeToncoinAmount / Constants.ToncoinMin);
            Label.SelectionStart = Label.Text.Length;

            var index = Strings.StakeDiceReturnsInfo.IndexOf("\U0001F3B2");
            if (index != -1)
            {
                InfoPrefix.Text = Strings.StakeDiceReturnsInfo.Substring(0, index);
                InfoSuffix.Text = Strings.StakeDiceReturnsInfo.Substring(index + 2);
            }

            PrimaryButtonText = Strings.StakeDiceButton;

            static void ApplyPrizeValue(int prize, TextBlock textBlock)
            {
                double result = prize / 1000.0;
                textBlock.Text = $"x{result:0.#}";
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: long toncoinAmount })
            {
                Label.Text = Formatter.FormatDouble(toncoinAmount / Constants.ToncoinMin);
                Label.SelectionStart = Label.Text.Length;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            _message.Delegate.SendMessage(new InputMessageStakeDice(_message.ClientService.StakeDiceState.StateHash, _toncoinAmount, false));
        }

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

                //SuggestedPostInfo = new InputSuggestedPostInfo(null, SuggestedPostInfo?.SendDate ?? 0);

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

            var value = parser?.ParseDouble(newValue);
            if (value.HasValue)
            {
                var cent = value * Constants.ToncoinMin;
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

            return null;
        }

        private void InvalidateToMaximum()
        {
            var selectionStart = Label.SelectionStart;

            Label.Text = Formatter.FormatDouble(Maximum / Constants.ToncoinMin);
            Label.SelectionStart = selectionStart + 1;

            VisualUtilities.ShakeView(InputRoot);
        }

        private long _toncoinAmount;

        private void OnValueChanged(long value)
        {
            var xtr = value / Constants.ToncoinMin / 10000.0;
            var usd = xtr * _clientService.Options.MillionToncoinToUsdRate;

            _toncoinAmount = value;
            Price.Text = "~" + Telegram.Converters.Formatter.FormatAmount((long)usd, "USD");

            IsPrimaryButtonEnabled = value == 0 || (value >= _clientService.Options.StakeDiceStakeAmountMin && value <= _clientService.Options.StakeDiceStakeAmountMax);
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

    }
}
