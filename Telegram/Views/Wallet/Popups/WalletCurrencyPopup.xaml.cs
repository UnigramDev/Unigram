//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Services.Wallet;
using Telegram.Td;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.System;

namespace Telegram.Views.Wallet.Popups
{
    public partial class WalletCurrency
    {
        public WalletCurrency(string code, string name)
        {
            Code = code;
            Name = name;
        }

        /// <summary>
        /// The ISO 4217 code, which is what everything else in the app takes.
        /// </summary>
        public string Code { get; }

        public string Name { get; }
    }

    /// <summary>
    /// Picks the currency wallet amounts are shown in.
    /// </summary>
    /// <remarks>
    /// The list is whatever TDLib quotes a rate for, so a currency that cannot be converted is
    /// never offered.
    /// </remarks>
    public sealed partial class WalletCurrencyPopup : ModalPopup
    {
        private readonly List<WalletCurrency> _currencies = new();
        private readonly DiffObservableCollection<WalletCurrency> _diff;

        public WalletCurrencyPopup(IWalletService wallet)
        {
            InitializeComponent();

            Title = "[Currency]";
            SecondaryButtonContent = Strings.Cancel;

            var handler = new DiffHandler<WalletCurrency>((x, y) =>
            {
                return x.Code == y.Code;
            });

            _diff = new DiffObservableCollection<WalletCurrency>(_currencies, handler);
            ScrollingHost.ItemsSource = _diff;

            InitializeCurrencies(wallet);
        }

        private async void InitializeCurrencies(IWalletService wallet)
        {
            var rates = await wallet.GetCurrencyRatesAsync();
            if (rates == null)
            {
                return;
            }

            var names = Names();

            foreach (var rate in rates)
            {
                _currencies.Add(new WalletCurrency(rate.Currency, names.TryGetValue(rate.Currency, out var name)
                    ? name
                    : rate.Currency));
            }

            _currencies.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase));
            _diff.ReplaceDiff(_currencies);

            ShowHideNoResult(_diff.Count == 0);
        }

        /// <summary>
        /// Currency code to name, the only way the framework offers it: every region knows its own
        /// currency, so the map is built by walking them once.
        /// </summary>
        private static IDictionary<string, string> Names()
        {
            var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
            {
                try
                {
                    var region = new RegionInfo(culture.Name);
                    names[region.ISOCurrencySymbol] = region.CurrencyEnglishName;
                }
                catch
                {
                    // A culture without a region of its own. There are a few, and they have no
                    // currency to contribute.
                }
            }

            return names;
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchField.Text))
            {
                _diff.ReplaceDiff(_currencies);
            }
            else
            {
                _diff.ReplaceDiff(_currencies.Where(FilterByQuery));
            }

            ShowHideNoResult(_diff.Count == 0);
        }

        private void SearchField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Enter && _diff.Count > 0)
            {
                SelectedItem = _diff[0].Code;
                Hide(ContentDialogResult.Primary);
            }
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

        private bool FilterByQuery(WalletCurrency currency)
        {
            if (ClientEx.SearchByPrefix(currency.Name, SearchField.Text))
            {
                return true;
            }

            // By code as well: somebody looking for CHF types CHF, not Swiss Franc.
            return ClientEx.SearchByPrefix(currency.Code, SearchField.Text);
        }

        private void ScrollingHost_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is WalletCurrency currency)
            {
                SelectedItem = currency.Code;
                Hide(ContentDialogResult.Primary);
            }
        }

        public string SelectedItem { get; private set; }
    }
}
