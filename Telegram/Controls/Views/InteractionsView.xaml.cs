//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Controls.Views
{
    public sealed partial class InteractionsView : UserControl, IIncrementalCollectionOwner
    {
        private readonly IClientService _clientService;

        private readonly MessageViewers _viewers;
        private bool _viewersLoaded;

        private readonly long _chatId;
        private readonly long _messageId;
        private readonly ReactionType _reactionType;

        private readonly IncrementalCollection<object> _items;
        private readonly HashSet<long> _users = new();

        private string _nextOffset;

        public InteractionsView(IClientService clientService, long chatId, long messageId, MessageViewers viewers)
            : this(clientService, chatId, messageId, null, viewers)
        {
        }

        public InteractionsView(IClientService clientService, long chatId, long messageId, ReactionType reactionType)
            : this(clientService, chatId, messageId, reactionType, null)
        {
        }

        private InteractionsView(IClientService clientService, long chatId, long messageId, ReactionType reactionType, MessageViewers viewers)
        {
            InitializeComponent();

            _clientService = clientService;
            _chatId = chatId;
            _messageId = messageId;
            _reactionType = reactionType;
            _viewers = viewers;

            _items = new IncrementalCollection<object>(this);
            _nextOffset = string.Empty;

            ScrollingHost.ItemsSource = _items;
            ScrollingHost.Loaded += (s, args) =>
            {
                ShowHideSkeleton();
            };
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
        }

        private bool _skeletonCollapsed = true;

        private void ShowHideSkeleton()
        {
            if (_skeletonCollapsed && _items.Count == 0 && ScrollingHost.ItemsPanelRoot != null)
            {
                _skeletonCollapsed = false;
                ShowSkeleton();
            }
            else if (_skeletonCollapsed is false && _items.Count > 0 && ScrollingHost.ItemsPanelRoot != null)
            {
                _skeletonCollapsed = true;

                var visual = ElementCompositionPreview.GetElementChildVisual(ScrollingHost.ItemsPanelRoot);
                var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                animation.InsertKeyFrame(0, 1);
                animation.InsertKeyFrame(1, 0);

                visual.StartAnimation("Opacity", animation);
            }
        }

        private void ShowSkeleton()
        {
            var size = ScrollingHost.ActualSize;
            var itemHeight = 6 + 36 + 6;

            var rows = Math.Min(10, Math.Ceiling(size.Y / itemHeight));
            var shapes = new List<CanvasGeometry>();

            var maxWidth = (int)Math.Clamp(size.X - 32 - 12 - 12 - 48 - 12, 80, 280);
            var random = new Random();

            for (int i = 0; i < rows; i++)
            {
                var y = itemHeight * i;

                shapes.Add(CanvasGeometry.CreateEllipse(null, 12 + 18, y + 6 + 18, 18, 18));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 36 + 8, y + 6, random.Next(80, maxWidth), 18, 4, 4));
                shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 12 + 36 + 8, y + 6 + 18 + 4, random.Next(80, maxWidth), 14, 4, 4));
            }

            VisualUtilities.SetSkeleton(ScrollingHost.ItemsPanelRoot, size, shapes.ToArray());
        }

        public event TypedEventHandler<InteractionsView, ItemClickEventArgs> ItemClick;

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content)
            {
                var cell = content.Children[0] as ProfileCell;
                var animated = content.Children[1] as CustomEmojiIcon;

                if (args.Item is AddedReaction addedReaction)
                {
                    cell.UpdateAddedReaction(_clientService, args, OnContainerContentChanging);

                    if (_reactionType == null)
                    {
                        using (animated.BeginBatchUpdate())
                        {
                            var custom = addedReaction.Type is ReactionTypeCustomEmoji;
                            var size = custom ? 20 : 40;

                            animated.Width = animated.Height = size;
                            animated.Margin = new Thickness(0, 0, custom ? 12 : 2, 0);
                            animated.FrameSize = new Size(size, size);
                            animated.LoopCount = custom ? 3 : 1;
                            animated.IsViewportAware = custom;

                            animated.Source = new ReactionFileSource(_clientService, addedReaction.Type)
                            {
                                UseCenterAnimation = true
                            };
                        }
                    }
                    else
                    {
                        animated.Source = null;
                    }
                }
                else if (args.Item is MessageViewer messageViewer)
                {
                    cell.UpdateMessageViewer(_clientService, args, OnContainerContentChanging);
                    animated.Source = null;
                }

                args.Handled = true;

                if (args.ItemIndex == 0 && args.Phase == 2)
                {
                    var element = FocusManagerEx.TryGetFocusedElement(XamlRoot);
                    if (element is MenuFlyoutContent flyout)
                    {
                        args.ItemContainer.Focus(flyout.FocusState);
                    }
                }
            }
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            ItemClick?.Invoke(this, e);
        }

        public async Task<IncrementalLoadResult> LoadMoreItemsAsync(uint count)
        {
            Logger.Info();

            var totalCount = 0u;

            if (_nextOffset != null)
            {
                var response = await _clientService.SendAsync(new GetMessageAddedReactions(_chatId, _messageId, _reactionType, _nextOffset, 50));
                if (response is AddedReactions addedReactions)
                {
                    _nextOffset = addedReactions.NextOffset.Length > 0 ? addedReactions.NextOffset : null;

                    foreach (var item in addedReactions.Reactions)
                    {
                        if (item.SenderId is MessageSenderUser senderUser)
                        {
                            _users.Add(senderUser.UserId);
                        }

                        totalCount++;
                        _items.Add(item);
                    }
                }
                else
                {
                    _nextOffset = null;
                }
            }
            else if (_viewers != null && !_viewersLoaded)
            {
                _viewersLoaded = true;

                foreach (var item in _viewers.Viewers)
                {
                    if (_users.Contains(item.UserId))
                    {
                        continue;
                    }

                    totalCount++;
                    _items.Add(item);
                }
            }

            ShowHideSkeleton();

            // Reactions first, then the viewers who did not react, and that second phase is one
            // shot. With neither left the list is done: claiming more here loops forever, because
            // the framework keeps asking for as long as the viewport is not full. It used to,
            // whenever the message had no viewers to fall back to.
            return new IncrementalLoadResult(totalCount, _nextOffset != null || (_viewers != null && !_viewersLoaded));
        }
    }
}
