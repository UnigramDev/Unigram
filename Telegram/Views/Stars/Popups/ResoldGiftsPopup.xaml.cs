//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Stars.Popups
{
    public interface IResoldGiftsPopup
    {
        void UpdateAttributes();
        void UpdateItems(GiftsForResale gifts, bool updateFilters);

        ResourceDictionary Resources { get; }
    }

    public partial class ResoldGiftFilter
    {
        public ResoldGiftFilter(UpgradedGiftModelCount model)
        {
            Name = model.Model.Name;
            TotalCount = model.TotalCount;
            Id = model.Model.Sticker.Id;
            Sticker = model.Model.Sticker;
            Attribute = new UpgradedGiftAttributeIdModel(model.Model.Sticker.Id);
        }

        public ResoldGiftFilter(UpgradedGiftBackdropCount backdrop)
        {
            Name = backdrop.Backdrop.Name;
            TotalCount = backdrop.TotalCount;
            Id = backdrop.Backdrop.Id;
            Colors = backdrop.Backdrop.Colors;
            Attribute = new UpgradedGiftAttributeIdBackdrop(backdrop.Backdrop.Id);
        }

        public ResoldGiftFilter(UpgradedGiftSymbolCount symbol)
        {
            Name = symbol.Symbol.Name;
            TotalCount = symbol.TotalCount;
            Id = symbol.Symbol.Sticker.Id;
            Sticker = symbol.Symbol.Sticker;
            Attribute = new UpgradedGiftAttributeIdModel(symbol.Symbol.Sticker.Id);
        }

        public string Name { get; }

        public int TotalCount { get; }

        public long Id { get; }

        public Sticker Sticker { get; }

        public UpgradedGiftBackdropColors Colors { get; }

        public UpgradedGiftAttributeId Attribute { get; }
    }

    public partial class ResoldGiftFilterManager
    {
        private readonly UpgradedGiftAttributeId _attributeType;

        private readonly Dictionary<long, ResoldGiftFilter> _map;

        private readonly List<ResoldGiftFilter> _items;

        private readonly HashSet<long> _selected = new();

        private readonly IClientService _clientService;
        private readonly IResoldGiftsPopup _popup;
        private readonly Microsoft.UI.Xaml.Controls.DropDownButton _sender;

        private MenuFlyout _flyout;
        private IList<long> _prev;

        public ResoldGiftFilterManager(IClientService clientService, IResoldGiftsPopup popup, Microsoft.UI.Xaml.Controls.DropDownButton sender, IList<UpgradedGiftModelCount> models)
            : this(clientService, popup, sender, models.Select(x => new ResoldGiftFilter(x)))
        {
            _attributeType = new UpgradedGiftAttributeIdModel(-1);
        }

        public ResoldGiftFilterManager(IClientService clientService, IResoldGiftsPopup popup, Microsoft.UI.Xaml.Controls.DropDownButton sender, IList<UpgradedGiftBackdropCount> backdrops)
            : this(clientService, popup, sender, backdrops.Select(x => new ResoldGiftFilter(x)))
        {
            _attributeType = new UpgradedGiftAttributeIdBackdrop(-1);
        }

        public ResoldGiftFilterManager(IClientService clientService, IResoldGiftsPopup popup, Microsoft.UI.Xaml.Controls.DropDownButton sender, IList<UpgradedGiftSymbolCount> symbols)
            : this(clientService, popup, sender, symbols.Select(x => new ResoldGiftFilter(x)))
        {
            _attributeType = new UpgradedGiftAttributeIdSymbol(-1);
        }

        public ResoldGiftFilterManager(IClientService clientService, IResoldGiftsPopup popup, Microsoft.UI.Xaml.Controls.DropDownButton sender, IEnumerable<ResoldGiftFilter> items)
        {
            _clientService = clientService;
            _popup = popup;
            _sender = sender;

            _items = items.ToList();

            _map = _items.ToDictionary(x => x.Id);
            _selected = _map.Keys.ToHashSet();
        }

        public void ShowAt()
        {
            // TODO: MenuFlyout isn't a great choice because it's not virtualized
            // Replace by using a Flyout with a normal ListView inside, as it happens for interactions

            var flyout = new MenuFlyout();
            flyout.MenuFlyoutPresenterStyle = _popup.Resources["ResaleMenuFlyoutPresenterStyle"] as Style;

            MenuFlyoutItemBase hook = PopulateFlyout(flyout, null, false);

            void handler(object sender, RoutedEventArgs e)
            {
                hook.Loaded -= handler;

                var presenter = hook.GetParent<MenuFlyoutPresenter>();
                var search = presenter?.GetChild<TextBox>();

                if (_items.Count > 8)
                {
                    search.TextChanged += Search_TextChanged;
                }
                else
                {
                    search.Visibility = Visibility.Collapsed;
                }
            }

            if (hook != null)
            {
                hook.Loaded += handler;
            }

            _flyout = flyout;

            flyout.Closed += Flyout_Closed;
            flyout.ShowAt(_sender, FlyoutPlacementMode.Bottom);
        }

        public IEnumerable<UpgradedGiftAttributeId> GetAttributes()
        {
            if (_selected.Count < _items.Count)
            {
                foreach (var id in _selected)
                {
                    if (_map.TryGetValue(id, out var item))
                    {
                        yield return item.Attribute;
                    }
                }
            }
        }

        private MenuFlyoutItemBase PopulateFlyout(MenuFlyout flyout, string query, bool update)
        {
            MenuFlyoutItemBase hook = null;

            var next = new List<long>();

            if (_selected.Count < _items.Count)
            {
                next.Add(-1);
            }

            foreach (var item in _items)
            {
                if (query != null && !ClientEx.SearchByPrefix(item.Name, query))
                {
                    continue;
                }

                next.Add(item.Id);
            }

            var prev = (update ? _prev : null) ?? Array.Empty<long>();
            var diff = DiffCalculator.Create(prev, next);

            while (diff.Next())
            {
                if (diff.State == DiffState.Add)
                {
                    if (diff.NewValue == -1)
                    {
                        var toggle = new ToggleMenuFlyoutItem();
                        toggle.Text = Strings.SelectAll;
                        toggle.Icon = MenuFlyoutHelper.CreateIcon(Icons.CheckmarkCircle);
                        toggle.Command = new RelayCommand(SelectAll);

                        hook ??= toggle;
                        flyout.Items.Insert(diff.NewIndex, toggle);

                    }
                    else if (_map.TryGetValue(diff.NewValue, out ResoldGiftFilter filter))
                    {
                        var toggle = new ToggleMenuFlyoutItem();
                        toggle.Text = string.Format("{0} **{1}**", filter.Name, filter.TotalCount);
                        toggle.IsChecked = _selected.Contains(filter.Id);
                        toggle.CommandParameter = filter.Id;
                        toggle.Command = new RelayCommand<long>(SelectItem);
                        toggle.Style = _popup.Resources["StickerToggleMenuFlyoutItemStyle"] as Style;
                        toggle.Icon = MenuFlyoutHelper.CreateIcon(Icons.CheckmarkCircle);

                        if (filter.Sticker != null)
                        {
                            toggle.Tag = new CustomEmojiIcon
                            {
                                Source = DelayedFileSource.FromSticker(_clientService, filter.Sticker)
                            };
                        }
                        else if (filter.Colors != null)
                        {
                            toggle.Tag = new Border
                            {
                                Background = new SolidColorBrush(filter.Colors.CenterColor.ToColor()),
                                Width = 20,
                                Height = 20,
                                CornerRadius = new CornerRadius(10)
                            };
                        }

                        hook ??= toggle;
                        flyout.Items.Insert(diff.NewIndex, toggle);
                    }
                }
                else if (diff.State == DiffState.Move && diff.OldIndex < flyout.Items.Count && diff.NewIndex < flyout.Items.Count)
                {
                    var item = flyout.Items[diff.OldIndex];
                    flyout.Items.RemoveAt(diff.OldIndex);
                    flyout.Items.Insert(diff.NewIndex, item);
                }
                else if (diff.State == DiffState.Remove && diff.OldIndex < flyout.Items.Count)
                {
                    flyout.Items.RemoveAt(diff.OldIndex);
                }
            }

            _prev = next;
            return hook;
        }

        private void Flyout_Closed(object sender, object e)
        {
            _flyout = null;
        }

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox search)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(search.Text))
            {
                PopulateFlyout(_flyout, null, true);
            }
            else
            {
                PopulateFlyout(_flyout, search.Text, true);
            }
        }

        private void SelectItem(long id)
        {
            if (_selected.Count == _items.Count)
            {
                _selected.Clear();
                _selected.Add(id);
            }

            else if (_selected.Contains(id))
            {
                if (_selected.Count == 1)
                {
                    SelectAll();
                    return;
                }

                _selected.Remove(id);
            }
            else
            {
                _selected.Add(id);
            }

            _popup.UpdateAttributes();
            _sender.Content = _selected.Count < _items.Count
                ? Locale.Declension(PluralStringKey(), _selected.Count)
                : DefaultString();
        }

        private void SelectAll()
        {
            foreach (var model in _items)
            {
                _selected.Add(model.Id);
            }

            _popup.UpdateAttributes();
            _sender.Content = DefaultString();
        }

        private string PluralStringKey()
        {
            return _attributeType switch
            {
                UpgradedGiftAttributeIdModel => Strings.R.Gift2ResaleFilterModels,
                UpgradedGiftAttributeIdBackdrop => Strings.R.Gift2ResaleFilterBackdrops,
                UpgradedGiftAttributeIdSymbol => Strings.R.Gift2ResaleFilterSymbols,
                _ => null
            };
        }

        private string DefaultString()
        {
            return _attributeType switch
            {
                UpgradedGiftAttributeIdModel => Strings.Gift2ResaleFilterModel,
                UpgradedGiftAttributeIdBackdrop => Strings.Gift2ResaleFilterBackdrop,
                UpgradedGiftAttributeIdSymbol => Strings.Gift2ResaleFilterSymbol,
                _ => null
            };
        }
    }

    public partial class ResoldGiftsCollection : IncrementalCollection<GiftForResale>
    {
        private readonly IClientService _clientService;
        private readonly IResoldGiftsPopup _popup;
        private readonly long _giftId;

        private readonly GiftForResaleOrder _order = new GiftForResaleOrderPrice();
        private readonly bool _forCrafting;
        private readonly bool _forStars;
        private readonly IList<UpgradedGiftAttributeId> _attributes = Array.Empty<UpgradedGiftAttributeId>();

        private string _nextOffset = string.Empty;

        public ResoldGiftsCollection(IClientService clientService, IResoldGiftsPopup popup, long giftId, GiftForResaleOrder order, IList<UpgradedGiftAttributeId> attributes)
        {
            _clientService = clientService;
            _popup = popup;
            _giftId = giftId;
            _order = order;
            _attributes = attributes;
        }

        public ResoldGiftsCollection(IClientService clientService, IResoldGiftsPopup popup, long giftId)
        {
            _clientService = clientService;
            _popup = popup;
            _giftId = giftId;
            _order = new GiftForResaleOrderPrice();
            _attributes = Array.Empty<UpgradedGiftAttributeId>();
        }

        protected override async Task<IncrementalLoadResult> OnLoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;
            var hasMoreItems = false;

            var response = await _clientService.SendAsync(new SearchGiftsForResale(_giftId, _order, _forCrafting, _forStars, _attributes, _nextOffset, 24));
            if (response is GiftsForResale gifts)
            {
                foreach (var gift in gifts.Gifts)
                {
                    Add(gift);
                    totalCount++;
                }

                _popup.UpdateItems(gifts, _attributes.Empty());

                _nextOffset = gifts.NextOffset;
                hasMoreItems = gifts.NextOffset.Length > 0;
            }
            else
            {
                _nextOffset = string.Empty;
                hasMoreItems = false;
            }

            return new IncrementalLoadResult(totalCount, hasMoreItems);
        }

        public GiftForResaleOrder Order => _order;
    }

    public sealed partial class ResoldGiftsPopup : ContentPopup, IResoldGiftsPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly long _giftId;
        private readonly int _resaleCount;
        private readonly MessageSender _receiverId;

        private ResoldGiftsCollection _gifts;

        private ResoldGiftFilterManager _models;
        private ResoldGiftFilterManager _backdrops;
        private ResoldGiftFilterManager _symbols;

        public ResoldGiftsPopup(IClientService clientService, INavigationService navigationService, AvailableGift gift, MessageSender receiverId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _giftId = gift.Gift.Id;
            _resaleCount = gift.ResaleCount;
            _receiverId = receiverId;

            Title = gift.Title;
            Subtitle.Text = Locale.Declension(Strings.R.Gift2ResaleCount, gift.ResaleCount);

            _gifts = new ResoldGiftsCollection(clientService, this, gift.Gift.Id);
            ScrollingHost.ItemsSource = _gifts;

            if (_resaleCount >= 18)
            {
                OrderIcon.Text = Icons.DollarArrowUp16;
                OrderText.Text = Strings.ResellGiftFilterSortPriceShort;
                ModelButton.Content = Strings.Gift2ResaleFilterModel;
                BackdropButton.Content = Strings.Gift2ResaleFilterBackdrop;
                SymbolButton.Content = Strings.Gift2ResaleFilterSymbol;
            }
            else
            {
                FiltersRoot.Visibility = Visibility.Collapsed;
            }

            Opened += OnOpened;
        }

        public ResoldGiftsPopup(IClientService clientService, INavigationService navigationService, UpgradedGift gift, UpgradedGiftValueInfo valueInfo, MessageSender receiverId)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            _giftId = gift.RegularGiftId;
            _resaleCount = valueInfo.TelegramListedGiftCount;
            _receiverId = receiverId;

            Title = gift.Title;
            Subtitle.Text = Locale.Declension(Strings.R.Gift2ResaleCount, valueInfo.TelegramListedGiftCount);

            _gifts = new ResoldGiftsCollection(clientService, this, gift.RegularGiftId);
            ScrollingHost.ItemsSource = _gifts;

            if (valueInfo.TelegramListedGiftCount >= 18)
            {
                OrderIcon.Text = Icons.DollarArrowUp16;
                OrderText.Text = Strings.ResellGiftFilterSortPriceShort;
                ModelButton.Content = Strings.Gift2ResaleFilterModel;
                BackdropButton.Content = Strings.Gift2ResaleFilterBackdrop;
                SymbolButton.Content = Strings.Gift2ResaleFilterSymbol;
            }
            else
            {
                FiltersRoot.Visibility = Visibility.Collapsed;
            }

            Opened += OnOpened;
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            ShowHideSkeleton();
        }

        public void UpdateItems(GiftsForResale gifts, bool updateFilters)
        {
            if (updateFilters)
            {
                if (gifts.Models.Count > 0)
                {
                    _models = new ResoldGiftFilterManager(_clientService, this, ModelButton, gifts.Models);
                }

                if (gifts.Backdrops.Count > 0)
                {
                    _backdrops = new ResoldGiftFilterManager(_clientService, this, BackdropButton, gifts.Backdrops);
                }

                if (gifts.Symbols.Count > 0)
                {
                    _symbols = new ResoldGiftFilterManager(_clientService, this, SymbolButton, gifts.Symbols);
                }
            }

            Subtitle.Text = Locale.Declension(Strings.R.Gift2ResaleCount, gifts.TotalCount);
            ShowHideSkeleton();
        }

        private bool _skeletonCollapsed = true;

        private void ShowHideSkeleton()
        {
            if (_skeletonCollapsed && _gifts.Count == 0)
            {
                _skeletonCollapsed = false;
                ShowSkeleton();
            }
            else if (_skeletonCollapsed is false && _gifts.Count > 0)
            {
                _skeletonCollapsed = true;

                var visual = ElementCompositionPreview.GetElementChildVisual(ScrollingHost);
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);

                visual.StartAnimation("Opacity", animation);
            }
        }

        private void ShowSkeleton()
        {
            var size = ScrollingHost.ActualSize;
            var itemHeight = 136 + 4;
            var itemWidth = (size.X - 4) / 3;

            var rows = Math.Min(_resaleCount / 3, Math.Ceiling(size.Y / itemHeight));
            var shapes = new List<CanvasGeometry>();

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 4 + j * itemWidth, 4 + itemHeight * i, itemWidth - 4, itemHeight - 4, 4, 4));
                }
            }

            VisualUtilities.SetSkeleton(ScrollingHost, size, shapes.ToArray());
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GiftForResale gift)
            {
                Hide(ContentDialogResult.Primary);

                var receivedGift = new ReceivedGift(gift.ReceivedGiftId, null, null, 0, false, false, false, false, false, false, 0, new SentGiftUpgraded(gift.Gift), Array.Empty<int>(), 0, 0, false, 0, 0, 0, 0, 0, string.Empty, 0);

                var confirm = await _navigationService.ShowPopupAsync(new ReceivedGiftPopup(_clientService, _navigationService, receivedGift, gift.Gift.OwnerId, _receiverId));
                if (confirm == ContentDialogResult.Primary && _receiverId == null)
                {
                    _gifts.Remove(gift);
                    await this.ShowQueuedAsync(XamlRoot);
                }
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell receivedGiftCell && args.Item is GiftForResale gift)
            {
                receivedGiftCell.UpdateGift(_clientService, gift);
            }

            args.Handled = true;
        }

        private void Order_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.CreateFlyoutItem(UpdateOrder, new GiftForResaleOrderPrice(), Strings.ResellGiftFilterSortPrice, Icons.DollarArrowUp);
            flyout.CreateFlyoutItem(UpdateOrder, new GiftForResaleOrderPriceChangeDate(), Strings.ResellGiftFilterSortDate, Icons.CalendarArrowUp);
            flyout.CreateFlyoutItem(UpdateOrder, new GiftForResaleOrderNumber(), Strings.ResellGiftFilterSortNumber, Icons.NumberSymbolArrowUp);

            flyout.ShowAt(OrderButton, FlyoutPlacementMode.Bottom);
        }

        private void UpdateOrder(GiftForResaleOrder order)
        {
            UpdateItems(order, GetAttributes());

            OrderText.Text = order switch
            {
                GiftForResaleOrderNumber => Strings.ResellGiftFilterSortNumberShort,
                GiftForResaleOrderPriceChangeDate => Strings.ResellGiftFilterSortDateShort,
                _ => Strings.ResellGiftFilterSortPriceShort
            };

            OrderIcon.Text = order switch
            {
                GiftForResaleOrderNumber => Icons.NumberSymbolArrowUp16,
                GiftForResaleOrderPriceChangeDate => Icons.CalendarArrowUp16,
                _ => Icons.DollarArrowUp16
            };
        }

        private void Model_Click(object sender, RoutedEventArgs e)
        {
            _models?.ShowAt();
        }

        private void Backdrop_Click(object sender, RoutedEventArgs e)
        {
            _backdrops?.ShowAt();
        }

        private void Symbol_Click(object sender, RoutedEventArgs e)
        {
            _symbols?.ShowAt();
        }

        public void UpdateAttributes()
        {
            UpdateItems(_gifts.Order, GetAttributes());
        }

        public void UpdateItems(GiftForResaleOrder order, IList<UpgradedGiftAttributeId> attributes)
        {
            _gifts = new ResoldGiftsCollection(_clientService, this, _giftId, order, attributes);
            ScrollingHost.ItemsSource = _gifts;
            ShowHideSkeleton();
        }

        private IList<UpgradedGiftAttributeId> GetAttributes()
        {
            if (_models == null || _backdrops == null || _symbols == null)
            {
                return Array.Empty<UpgradedGiftAttributeId>();
            }

            var attributes = new List<UpgradedGiftAttributeId>(_models.GetAttributes());
            attributes.AddRange(_backdrops.GetAttributes());
            attributes.AddRange(_symbols.GetAttributes());

            return attributes;
        }
    }
}
