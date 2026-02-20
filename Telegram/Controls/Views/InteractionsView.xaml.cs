//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;

namespace Telegram.Controls.Views
{
    public sealed partial class InteractionsView : UserControl, IIncrementalCollectionOwner
    {
        private readonly IClientService _clientService;

        private readonly MessageViewers _viewers;

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

            ElementCompositionPreview.SetElementChildVisual(ScrollingHost.ItemsPanelRoot, visual);
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
                    var element = FocusManagerEx.TryGetFocusedElement();
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

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
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
            else if (_viewers != null)
            {
                HasMoreItems = false;

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

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;
    }
}
