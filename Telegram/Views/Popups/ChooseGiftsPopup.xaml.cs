//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Profile;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseGiftsPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly ProfileGiftsTabViewModel _viewModel;

        public ChooseGiftsPopup(ProfileGiftsTabViewModel viewModel)
        {
            InitializeComponent();

            _clientService = viewModel.ClientService;
            _viewModel = viewModel;

            Title = Strings.GiftsCollectionMenuAddGifts;
            PrimaryButtonText = Strings.GiftsCollectionMenuAddGifts;
            SecondaryButtonText = Strings.Cancel;

            ScrollingHost.ItemsSource = viewModel.Collections[0].Items;
        }

        public IEnumerable<ReceivedGift> SelectedItems => ScrollingHost.SelectedItems
            .Cast<ReceivedGift>()
            .OrderByDescending(x => x.Date);

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell cell && args.Item is ReceivedGift gift)
            {
                cell.UpdateGift(_clientService, gift);
                args.Handled = true;
            }
        }

        private void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var sort = new MenuFlyoutItem
            {
                Text = _viewModel.SortByPrice
                    ? Strings.Gift2FilterSortByValue
                    : Strings.Gift2FilterSortByDate,
                Icon = MenuFlyoutHelper.CreateIcon(_viewModel.SortByPrice ? Icons.DollarArrowUp : Icons.CalendarArrowUp)
            };

            var unlimited = new MenuFlyoutItem
            {
                Text = Strings.Gift2FilterUnlimited,
                Icon = _viewModel.ExcludeUnlimited ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
            };

            var limited = new MenuFlyoutItem
            {
                Text = Strings.Gift2FilterLimited,
                Icon = _viewModel.ExcludeNonUpgradable ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
            };

            var upgradable = new MenuFlyoutItem
            {
                Text = Strings.Gift2FilterUpgradable,
                Icon = _viewModel.ExcludeUpgradable ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
            };

            var unique = new MenuFlyoutItem
            {
                Text = Strings.Gift2FilterUnique,
                Icon = _viewModel.ExcludeUpgraded ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
            };

            void UpdateFilters(Action action)
            {
                //_hasBeenScrolled = false;
                //RootGrid.Update(-1, false);

                action();
            }

            sort.Click += (s, args) => UpdateFilters(() => _viewModel.SortByPrice = !_viewModel.SortByPrice);
            unlimited.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeUnlimited = !_viewModel.ExcludeUnlimited);
            limited.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeNonUpgradable = !_viewModel.ExcludeNonUpgradable);
            upgradable.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeUpgradable = !_viewModel.ExcludeUpgradable);
            unique.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeUpgraded = !_viewModel.ExcludeUpgraded);

            var flyout = new MenuFlyout();

            flyout.Items.Add(sort);
            flyout.CreateFlyoutSeparator();
            flyout.Items.Add(unlimited);
            flyout.Items.Add(limited);
            flyout.Items.Add(upgradable);
            flyout.Items.Add(unique);

            if (_viewModel.IsOwned)
            {
                var displayed = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterDisplayed,
                    Icon = _viewModel.ExcludeSaved ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                var hidden = new MenuFlyoutItem
                {
                    Text = Strings.Gift2FilterHidden,
                    Icon = _viewModel.ExcludeUnsaved ? null : MenuFlyoutHelper.CreateIcon(Icons.Checkmark)
                };

                displayed.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeSaved = !_viewModel.ExcludeSaved);
                hidden.Click += (s, args) => UpdateFilters(() => _viewModel.ExcludeUnsaved = !_viewModel.ExcludeUnsaved);

                flyout.CreateFlyoutSeparator();
                flyout.Items.Add(displayed);
                flyout.Items.Add(hidden);
            }

            flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedRight);
        }
    }
}
