//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Collections.Generic;
using System.Linq;
using Telegram.Collections;
using Telegram.Controls;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseCountriesPopup : ContentPopup
    {
        private List<CountryInfo> _countries;
        private DiffObservableCollection<CountryInfo> _diff;

        private List<CountryInfo> _selection;

        public ChooseCountriesPopup(IClientService clientService, IEnumerable<string> selected)
        {
            InitializeComponent();

            Title = Strings.BoostingSelectCountry;
            PrimaryButtonText = Strings.OK;
            SecondaryButtonText = Strings.Cancel;

            InitializeCountries(clientService, selected);
        }

        private async void InitializeCountries(IClientService clientService, IEnumerable<string> selected)
        {
            var items = new List<CountryInfo>();
            var selection = new List<CountryInfo>();
            var countries = await clientService.SendAsync(new GetCountries()) as Countries;

            foreach (var item in countries.CountriesValue)
            {
                if (selected.Contains(item.CountryCode))
                {
                    items.Add(item);
                    selection.Add(item);
                }
            }

            foreach (var item in countries.CountriesValue)
            {
                if (item.IsHidden || selected.Contains(item.CountryCode))
                {
                    continue;
                }

                items.Add(item);
            }

            var handler = new DiffHandler<CountryInfo>((x, y) =>
            {
                return x.CountryCode == y.CountryCode;
            });

            _countries = items;
            _selection = selection;
            _diff = new DiffObservableCollection<CountryInfo>(items, handler);

            ScrollingHost.ItemsSource = _diff;

            foreach (var item in countries.CountriesValue)
            {
                if (selected.Contains(item.CountryCode))
                {
                    ScrollingHost.SelectedItems.Add(item);
                }
            }
        }

        public IList<CountryInfo> SelectedItems { get; private set; }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            SelectedItems = _selection.ToList();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void SearchField_TextChanged(object sender, TextChangedEventArgs e)
        {
            ScrollingHost.SelectionChanged -= OnSelectionChanged;

            if (string.IsNullOrWhiteSpace(SearchField.Text))
            {
                _diff.ReplaceDiff(_countries);
            }
            else
            {
                _diff.ReplaceDiff(_countries.Where(FilterByQuery));
            }

            ScrollingHost.SelectionChanged += OnSelectionChanged;

            foreach (var selection in _selection)
            {
                if (ScrollingHost.Items.Contains(selection))
                {
                    ScrollingHost.SelectedItems.Add(selection);
                }
            }

            ShowHideNoResult(_diff.Count == 0);
        }

        private void SearchField_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            //if (e.Key == VirtualKey.Enter && _diff.Count > 0)
            //{
            //    SelectedItem = _diff[0].Id;
            //    Hide(ContentDialogResult.Primary);
            //}
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

        private bool FilterByQuery(CountryInfo language)
        {
            if (ClientEx.SearchByPrefix(language.Name, SearchField.Text))
            {
                return true;
            }

            return ClientEx.SearchByPrefix(language.EnglishName, SearchField.Text);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selection = ScrollingHost.SelectedItems.Cast<CountryInfo>().ToList();
        }
    }
}
