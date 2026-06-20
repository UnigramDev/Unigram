//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Gifts.Popups
{
    public sealed partial class GiftVariantsPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly DispatcherTimer _timer;
        private readonly Random _random = new();

        private readonly GiftUpgradeVariants _variants;
        private readonly MvxObservableCollection<object> _itemsSource;

        private UpgradedGiftModel _selectedModel;
        private UpgradedGiftBackdrop _selectedBackdrop;
        private UpgradedGiftSymbol _selectedSymbol;

        public GiftVariantsPopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, GiftUpgradeVariants variants)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _variants = variants;
            _itemsSource = new MvxObservableCollection<object>(variants.Models.Cast<object>());

            OnTick(null, null);

            if (gift.Gift is SentGiftUpgraded upgraded)
            {
                UpgradedTitle.Text = upgraded.Gift.Title;
            }

            ScrollingHost.ItemsSource = _itemsSource;

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };

            _timer.Tick += OnTick;
            _timer.Start();

            UpgradedSubtitle.Text = Strings.Gift2PreviewRandomTraits;

            TextBlockHelper.SetMarkdown(Info, Locale.Declension(Strings.R.GiftPreviewCountModels, _variants.Models.Count));

            Navigation.SelectedIndex = 0;
            Navigation.SelectionChanged += Navigation_SelectionChanged;
        }

        private void OnTick(object sender, object e)
        {
            _selectedModel = _variants.Models[_random.Next(0, _variants.Models.Count)];
            _selectedBackdrop = _variants.Backdrops[_random.Next(0, _variants.Backdrops.Count)];
            _selectedSymbol = _variants.Symbols[_random.Next(0, _variants.Symbols.Count)];

            UpdateComposition();
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is GiftVariantCell content)
            {
                Color color = default;

                if (args.Item is UpgradedGiftModel model)
                {
                    color = content.ActualTheme == ElementTheme.Light ? Theme.AccentLight.Default : Theme.AccentDark.Default;
                    content.UpdateModel(_clientService, model);
                }
                else if (args.Item is UpgradedGiftBackdrop backdrop)
                {
                    color = backdrop.Colors.EdgeColor.ToColor();
                    content.UpdateBackdrop(_clientService, _selectedModel, backdrop, _selectedSymbol);
                }
                else if (args.Item is UpgradedGiftSymbol symbol)
                {
                    color = _selectedBackdrop.Colors.EdgeColor.ToColor();
                    content.UpdateSymbol(_clientService, _selectedBackdrop, symbol);
                }

                var presenter = VisualTreeHelper.GetChild(args.ItemContainer, 0) as ListViewItemPresenter;
                if (presenter != null)
                {
                    var brush = new SolidColorBrush(color);

                    presenter.SelectedBorderBrush = brush;
                    presenter.SelectedPointerOverBorderBrush = brush;
                    presenter.SelectedPressedBorderBrush = brush;
                }
            }

            args.Handled = true;
        }

        private void Navigation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StopRandomization(false);

            if (Navigation.SelectedIndex == 0)
            {
                //ScrollingHost.ItemsSource = _variants.Models;
                _itemsSource.ReplaceWithT(_variants.Models);
                ScrollingHost.SelectedItem = _selectedModel;

                TextBlockHelper.SetMarkdown(Info, Locale.Declension(Strings.R.GiftPreviewCountModels, _variants.Models.Count));
            }
            else if (Navigation.SelectedIndex == 1)
            {
                //ScrollingHost.ItemsSource = _variants.Backdrops;
                _itemsSource.ReplaceWithT(_variants.Backdrops);
                ScrollingHost.SelectedItem = _selectedBackdrop;

                TextBlockHelper.SetMarkdown(Info, Locale.Declension(Strings.R.GiftPreviewCountBackdrops, _variants.Backdrops.Count));
            }
            else if (Navigation.SelectedIndex == 2)
            {
                //ScrollingHost.ItemsSource = _variants.Symbols;
                _itemsSource.ReplaceWithT(_variants.Symbols);
                ScrollingHost.SelectedItem = _selectedSymbol;

                TextBlockHelper.SetMarkdown(Info, Locale.Declension(Strings.R.GiftPreviewCountSymbols, _variants.Symbols.Count));
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScrollingHost.SelectedItem != null)
            {
                StopRandomization(false);
            }

            if (ScrollingHost.SelectedItem is UpgradedGiftModel model)
            {
                _selectedModel = model;
                UpdateComposition();
            }
            else if (ScrollingHost.SelectedItem is UpgradedGiftBackdrop backdrop)
            {
                _selectedBackdrop = backdrop;
                UpdateComposition();
            }
            else if (ScrollingHost.SelectedItem is UpgradedGiftSymbol symbol)
            {
                _selectedSymbol = symbol;
                UpdateComposition();
            }
        }

        private void StopRandomization(bool updateSelection)
        {
            _timer.Stop();
            Randomize.IsChecked = false;
            UpgradedSubtitle.Text = Strings.Gift2PreviewSelectedTraits;

            if (updateSelection)
            {
                if (Navigation.SelectedIndex == 0)
                {
                    ScrollingHost.SelectedItem = _selectedModel;
                }
                else if (Navigation.SelectedIndex == 1)
                {
                    ScrollingHost.SelectedItem = _selectedBackdrop;
                }
                else if (Navigation.SelectedIndex == 2)
                {
                    ScrollingHost.SelectedItem = _selectedSymbol;
                }
            }
        }

        private void UpdateComposition()
        {
            if (_selectedModel == null || _selectedBackdrop == null || _selectedSymbol == null)
            {
                return;
            }

            UpgradedHeader.Update(_clientService, _selectedBackdrop, _selectedSymbol);
            UpgradedAnimatedPhoto.Source = DelayedFileSource.FromSticker(_clientService, _selectedModel.Sticker);

            ModelName.Text = _selectedModel.Name;
            ModelRarity.Text = _selectedModel.Rarity.ToText();

            BackdropName.Text = _selectedBackdrop.Name;
            BackdropRarity.Text = _selectedBackdrop.Rarity.ToText();

            SymbolName.Text = _selectedSymbol.Name;
            SymbolRarity.Text = _selectedSymbol.Rarity.ToText();

            var badgeColor = _selectedBackdrop.Colors.CenterColor.ToColor();
            var badgeBrush = new SolidColorBrush(badgeColor.Lighten());

            ModelRarity.Background = badgeBrush;
            BackdropRarity.Background = badgeBrush;
            SymbolRarity.Background = badgeBrush;
        }

        private void Randomize_Click(object sender, RoutedEventArgs e)
        {
            if (_timer.IsEnabled)
            {
                StopRandomization(true);
            }
            else
            {
                _timer.Start();
                OnTick(null, null);

                UpgradedSubtitle.Text = Strings.Gift2PreviewRandomTraits;
                ScrollingHost.SelectedItem = null;
            }
        }
    }
}
