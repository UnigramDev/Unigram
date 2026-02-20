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
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Views.Gifts.Popups
{
    public partial class GiftsForCraftingCollection : ObservableCollection<ReceivedGift>, ISupportIncrementalLoading
    {
        private readonly IClientService _clientService;
        private readonly long _giftId;

        private readonly GiftForResaleOrder _order = new GiftForResaleOrderPrice();
        private readonly bool _forCrafting;
        private readonly IList<UpgradedGiftAttributeId> _attributes = Array.Empty<UpgradedGiftAttributeId>();

        private string _nextOffset = string.Empty;
        private bool _hasMoreItems = true;

        public GiftsForCraftingCollection(IClientService clientService, long giftId, GiftsForCrafting crafting)
            : base(crafting.Gifts.Where(x => x.Gift is SentGiftUpgraded upgraded && upgraded.Gift.OwnerId.IsUser(clientService.Options.MyId)))
        {
            _clientService = clientService;
            _giftId = giftId;
            _nextOffset = crafting.NextOffset;
        }

        public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            return AsyncInfo.Run(LoadMoreItemsAsync);
        }

        private async Task<LoadMoreItemsResult> LoadMoreItemsAsync(CancellationToken token)
        {
            var totalCount = 0u;

            var response = await _clientService.SendAsync(new GetGiftsForCrafting(_giftId, _nextOffset, 24));
            if (response is GiftsForCrafting gifts)
            {
                foreach (var gift in gifts.Gifts)
                {
                    if (gift.Gift is SentGiftUpgraded upgraded && upgraded.Gift.OwnerId.IsUser(_clientService.Options.MyId))
                    {
                        Add(gift);
                        totalCount++;
                    }
                }

                _nextOffset = gifts.NextOffset;
                _hasMoreItems = gifts.NextOffset.Length > 0;
            }
            else
            {
                _nextOffset = string.Empty;
                _hasMoreItems = false;
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems => _hasMoreItems;

        public GiftForResaleOrder Order => _order;
    }

    public sealed partial class GiftCraftChoosePopup : ContentPopup, IResoldGiftsPopup
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

        public GiftCraftChoosePopup(IClientService clientService, INavigationService navigationService, ReceivedGift gift, GiftsForCrafting crafting)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            if (gift.Gift is not SentGiftUpgraded upgraded)
            {
                return;
            }

            _giftId = upgraded.Gift.RegularGiftId;
            //_resaleCount = valueInfo.TelegramListedGiftCount;
            //_receiverId = receiverId;

            Title = upgraded.Gift.Title;
            //Subtitle.Text = Locale.Declension(Strings.R.Gift2ResaleCount, valueInfo.TelegramListedGiftCount);

            _gifts = new ResoldGiftsCollection(clientService, this, upgraded.Gift.RegularGiftId);
            ScrollingHost.ItemsSource = _gifts;

            ScrollingHost2.ItemsSource = new GiftsForCraftingCollection(clientService, upgraded.Gift.RegularGiftId, crafting);

            //if (valueInfo.TelegramListedGiftCount >= 18)
            {
                OrderIcon.Text = Icons.DollarArrowUp16;
                OrderText.Text = Strings.ResellGiftFilterSortPriceShort;
                ModelButton.Content = Strings.Gift2ResaleFilterModel;
                BackdropButton.Content = Strings.Gift2ResaleFilterBackdrop;
                SymbolButton.Content = Strings.Gift2ResaleFilterSymbol;
            }
            //else
            //{
            //    FiltersRoot.Visibility = Visibility.Collapsed;
            //}

            Opened += OnOpened;
        }

        public ReceivedGift SelectedGift { get; private set; }

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

            var compositor = BootStrapper.Current.Compositor;

            var geometries = shapes.ToArray();
            var path = compositor.CreatePathGeometry(new CompositionPath(CanvasGeometry.CreateGroup(null, geometries, CanvasFilledRegionDetermination.Winding)));

            var transparent = Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF);
            var foregroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
            var backgroundColor = Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);

            var lookup = ThemeService.GetLookup(ActualTheme);
            if (lookup.TryGet("MenuFlyoutItemBackgroundPointerOver", out Color color))
            {
                foregroundColor = color;
                backgroundColor = color;
            }

            var gradient = compositor.CreateLinearGradientBrush();
            gradient.StartPoint = new Vector2(0, 0);
            gradient.EndPoint = new Vector2(1, 0);
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, transparent));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, foregroundColor));
            gradient.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, transparent));

            var background = compositor.CreateRectangleGeometry();
            background.Size = size;
            var backgroundShape = compositor.CreateSpriteShape(background);
            backgroundShape.FillBrush = compositor.CreateColorBrush(backgroundColor);

            var foreground = compositor.CreateRectangleGeometry();
            foreground.Size = size;
            var foregroundShape = compositor.CreateSpriteShape(foreground);
            foregroundShape.FillBrush = gradient;

            var clip = compositor.CreateGeometricClip(path);
            var visual = compositor.CreateShapeVisual();
            visual.Clip = clip;
            visual.Shapes.Add(backgroundShape);
            visual.Shapes.Add(foregroundShape);
            visual.RelativeSizeAdjustment = Vector2.One;

            var animation = compositor.CreateVector2KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector2(-size.X, 0));
            animation.InsertKeyFrame(1, new Vector2(size.X, 0));
            animation.IterationBehavior = AnimationIterationBehavior.Forever;
            animation.Duration = TimeSpan.FromSeconds(1);

            foregroundShape.StartAnimation("Offset", animation);

            ElementCompositionPreview.SetElementChildVisual(ScrollingHost, visual);
        }

        private async void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is GiftForResale giftForResale)
            {
                var chat = await _clientService.GetChatFromMessageSenderAsync(null);

                var confirm = await TransferGiftPopup.ShowAsync(XamlRoot, _clientService, giftForResale, chat);
                if (confirm == ContentDialogResult.Primary)
                {
                    GiftResalePrice price;
                    if (giftForResale.Gift.ResaleParameters.ToncoinOnly)
                    {
                        price = new GiftResalePriceTon(giftForResale.Gift.ResaleParameters.ToncoinCentCount);
                    }
                    else
                    {
                        price = new GiftResalePriceStar(giftForResale.Gift.ResaleParameters.StarCount);
                    }

                    var response = await _clientService.SendPaymentAsync(giftForResale.Gift.ResaleParameters.StarCount, new SendResoldGift(giftForResale.Gift.Name, _clientService.MyId, price));
                    if (response is GiftResaleResultOk ok)
                    {
                        //_aggregator.Publish(new UpdateGiftIsSold(_gift.ReceivedGiftId));
                        SelectedGift = new ReceivedGift(ok.ReceivedGiftId, null, null, 0, false, false, false, false, false, false, 0, new SentGiftUpgraded(giftForResale.Gift), Array.Empty<int>(), 0, 0, false, 0, 0, 0, 0, 0, string.Empty, 0); ;
                        Hide(ContentDialogResult.Primary);

                        //if (chat != null)
                        //{
                        //    _navigationService.NavigateToChat(chat.Id);
                        //    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftToTitle, string.Format(Strings.BoughtResoldGiftToText, chat.Title)), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                        //}
                        //else
                        //{
                        //    ToastPopup.Show(XamlRoot, string.Format("**{0}**\n{1}", Strings.BoughtResoldGiftTitle, string.Format(Strings.BoughtResoldGiftText, upgraded.Gift.ToName())), new DelayedFileSource(_clientService, upgraded.Gift.Model.Sticker));
                        //}
                    }
                    else if (response is Error error)
                    {
                        ToastPopup.ShowError(XamlRoot, error);
                    }
                    else if (response is ErrorStarsNeeded)
                    {
                        Hide();
                        await _navigationService.ShowPopupAsync(new BuyPopup(), BuyStarsArgs.ForChannel(giftForResale.Gift.ResaleParameters.StarCount, 0));
                    }
                }

                //Hide(ContentDialogResult.Primary);

                //var receivedGift = new ReceivedGift(giftForResale.ReceivedGiftId, null, null, 0, false, false, false, false, false, false, 0, new SentGiftUpgraded(giftForResale.Gift), Array.Empty<int>(), 0, 0, false, 0, 0, 0, 0, 0, string.Empty, 0);

                //var confirm = await _navigationService.ShowPopupAsync(new ReceivedGiftPopup(_clientService, _navigationService, receivedGift, giftForResale.Gift.OwnerId, _receiverId));
                //if (confirm == ContentDialogResult.Primary && _receiverId == null)
                //{
                //    _gifts.Remove(giftForResale);
                //    await this.ShowQueuedAsync(XamlRoot);
                //}
            }
            else if (e.ClickedItem is ReceivedGift receivedGift)
            {
                SelectedGift = receivedGift;
                Hide(ContentDialogResult.Primary);
            }
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is ReceivedGiftCell receivedGiftCell)
            {
                if (args.Item is GiftForResale giftForResale)
                {
                    receivedGiftCell.UpdateGift(_clientService, giftForResale);
                }
                else if (args.Item is ReceivedGift receivedGift)
                {
                    receivedGiftCell.UpdateGift(_clientService, receivedGift);
                }
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
