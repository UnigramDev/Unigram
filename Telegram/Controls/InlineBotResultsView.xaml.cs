//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace Telegram.Controls
{
    public sealed partial class InlineBotResultsView : UserControl
    {
        public DialogViewModel ViewModel => DataContext as DialogViewModel;

        private readonly AnimatedListHandler _handler;
        private readonly ZoomableListHandler _zoomer;

        public InlineBotResultsView()
        {
            InitializeComponent();

            _handler = new AnimatedListHandler(ScrollingHost, AnimatedListType.Other);

            _zoomer = new ZoomableListHandler(ScrollingHost);
            _zoomer.Opening = _handler.UnloadVisibleItems;
            _zoomer.Closing = _handler.ThrottleVisibleItems;
            _zoomer.DownloadFile = fileId => ViewModel.ClientService.DownloadFile(fileId, 32);
            _zoomer.SessionId = () => ViewModel.ClientService.SessionId;
        }

        public void UpdateCornerRadius(double radius)
        {
            var min = Math.Max(4, radius - 2);

            Root.Padding = new Thickness(0, 0, 0, radius);
            SwitchPm.CornerRadius = new CornerRadius(min, min, 4, 4);

            CornerRadius = new CornerRadius(radius, radius, 0, 0);
        }

        private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
        {
            if (ViewModel != null)
            {
                Bindings.Update();
            }

            if (ViewModel == null)
            {
                Bindings.StopTracking();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Bindings.StopTracking();
        }

        public event ItemClickEventHandler ItemClick;

        public void UpdateChatPermissions(Chat chat)
        {
            var rights = ViewModel.VerifyRights(chat, x => x.CanSendOtherMessages, Strings.GlobalAttachInlineRestricted, Strings.AttachInlineRestrictedForever, Strings.AttachInlineRestricted, out string label);

            LayoutRoot.Visibility = rights ? Visibility.Collapsed : Visibility.Visible;
            PermissionsPanel.Visibility = rights ? Visibility.Visible : Visibility.Collapsed;
            PermissionsLabel.Text = label ?? string.Empty;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem is InlineQueryResult result)
            {
                var collection = ViewModel.InlineBotResults;
                if (collection == null)
                {
                    return;
                }

                ViewModel.SendBotInlineResult(result, collection.GetQueryId(result));
            }
        }

        private object ConvertSource(BotResultsCollection collection)
        {
            if (collection == null)
            {
                return null;
            }
            else if (collection.All(x => x is InlineQueryResultSticker) || collection.All(x => x.IsMedia()))
            {
                if (ScrollingHost.ItemsPanel != VerticalGrid)
                {
                    ScrollingHost.ItemsPanel = VerticalGrid;
                    ScrollingHost.ItemTemplate = null;
                    ScrollingHost.ItemTemplateSelector = MediaTemplateSelector;

                    FluidGridView.Update(ScrollingHost);
                }
            }
            else if (ScrollingHost.ItemsPanel != VerticalStack)
            {
                ScrollingHost.ItemsPanel = VerticalStack;
                ScrollingHost.ItemTemplate = ResultTemplate;
                ScrollingHost.ItemTemplateSelector = null;
            }

            return new object();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                if (sender.ItemsPanel == VerticalStack)
                {
                    args.ItemContainer = new TextListViewItem();
                }
                else
                {
                    args.ItemContainer = new TextGridViewItem
                    {
                        Margin = new Thickness(2)
                    };
                }

                if (sender.ItemTemplateSelector != null)
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplateSelector.SelectTemplate(args.Item, args.ItemContainer);
                }
                else
                {
                    args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                }

                args.ItemContainer.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                args.ItemContainer.VerticalContentAlignment = VerticalAlignment.Stretch;

                _zoomer.ElementPrepared(args.ItemContainer);
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var result = args.Item as InlineQueryResult;

            if (args.ItemContainer.ContentTemplateRoot is InlineResultMediaCell mediaCell)
            {
                mediaCell.UpdateResult(ViewModel.ClientService, result);
            }
            else if (args.ItemContainer.ContentTemplateRoot is InlineResultArticleCell articleCell)
            {
                articleCell.UpdateResult(ViewModel.ClientService, result);
            }
            else if (content.Children[0] is AnimatedImage animated)
            {
                if (result is InlineQueryResultSticker sticker)
                {
                    var file = sticker.Sticker.StickerValue;
                    if (file == null)
                    {
                        return;
                    }

                    animated.Source = new DelayedFileSource(ViewModel.ClientService, file);
                }
                else if (result is InlineQueryResultAnimation animation)
                {
                    var file = animation.Animation.AnimationValue;
                    if (file == null)
                    {
                        return;
                    }

                    animated.Source = new DelayedFileSource(ViewModel.ClientService, file);
                }
            }

            args.Handled = true;
        }

        private async void ItemsWrapGrid_Loading(FrameworkElement sender, object args)
        {
            await sender.UpdateLayoutAsync();
            FluidGridView.Update(ScrollingHost);
        }

        private void Player_Ready(object sender, EventArgs e)
        {
            _handler.ThrottleVisibleItems();
        }
    }

    public class InlineQueryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate StickerTemplate { get; set; }
        public DataTemplate AnimationTemplate { get; set; }
        public DataTemplate MediaTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is InlineQueryResultSticker)
            {
                return StickerTemplate;
            }
            else if (item is InlineQueryResultAnimation)
            {
                return AnimationTemplate;
            }

            return MediaTemplate;
        }
    }
}
