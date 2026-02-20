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
using Telegram.Common;
using Telegram.Controls.Views;
using Telegram.Converters;
using Telegram.Native.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages
{
    public partial class ReactionButton : ToggleButtonEx
    {
        private CustomEmojiIcon Icon;
        private Popup Overlay;
        protected AnimatedTextBlock Count;
        private RecentUserHeads RecentChoosers;

        public ReactionButton()
        {
            DefaultStyleKey = typeof(ReactionButton);

            Click += OnClick;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReactionButtonAutomationPeer(this);
        }

        public string GetAutomationName()
        {
            if (_reaction is MessageReaction interaction)
            {
                string GetAutomationName(MessageSender sender, string emoji)
                {
                    if (_message.ClientService.TryGetUser(sender, out Td.Api.User user))
                    {
                        if (user.Id == _message.ClientService.Options.MyId)
                        {
                            return string.Format(Strings.AccDescrYouReactedWith, emoji);
                        }

                        return string.Format(Strings.AccDescrReactedWith, user.FullName(true), emoji);
                    }
                    else if (_message.ClientService.TryGetChat(sender, out Chat chat))
                    {
                        return string.Format(Strings.AccDescrReactedWith, chat.Title, emoji);
                    }

                    return Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, emoji);
                }

                if (interaction.Type is ReactionTypeEmoji emoji)
                {
                    return interaction.TotalCount > 1 || interaction.RecentSenderIds.Count == 0
                        ? Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, emoji.Emoji)
                        : GetAutomationName(interaction.RecentSenderIds[0], emoji.Emoji);
                }
                else
                {
                    return interaction.TotalCount > 1 || interaction.RecentSenderIds.Count == 0
                        ? Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, Strings.AccDescrCustomEmoji2)
                        : GetAutomationName(interaction.RecentSenderIds[0], Strings.AccDescrCustomEmoji2);
                }
            }

            return null;
        }

        protected MessageViewModel _message;
        protected MessageReaction _reaction;
        private ReactionType _reactionType;
        private bool _chosen;

        private UnreadReaction _unread;

        public MessageReaction Reaction => _reaction;

        public void SetUnread(UnreadReaction unread)
        {
            if (Icon == null)
            {
                _unread = unread;
            }
            else
            {
                _unread = null;

                if (unread != null)
                {
                    Animate();
                }
            }
        }

        public void SetReaction(MessageViewModel message, MessageReaction reaction)
        {
            if (Icon == null)
            {
                _message = message;
                _reaction = reaction;
                return;
            }

            var recycled = message.Id == _message?.Id
                && message.ChatId == _message?.ChatId
                && reaction.Type.AreTheSame(_reaction?.Type);

            _message = message;
            _reaction = reaction;

            UpdateInteraction(message, reaction, recycled, _chosen != reaction.IsChosen);

            _chosen = reaction.IsChosen;

            if (reaction.Type.AreTheSame(_reactionType))
            {
                return;
            }

            _reactionType = reaction.Type;

            using (Icon.BeginBatchUpdate())
            {
                var custom = reaction.Type is ReactionTypeCustomEmoji;
                var size = reaction.Type is ReactionTypeCustomEmoji or ReactionTypePaid ? 20 : 32;

                Icon.Width = Icon.Height = size;
                Icon.FrameSize = new Size(size, size);
                Icon.LoopCount = custom ? 3 : 1;
                Icon.IsCachingEnabled = reaction.Type is not ReactionTypePaid;
                Icon.IsViewportAware = custom;

                Icon.Source = new ReactionFileSource(message.ClientService, reaction.Type)
                {
                    UseCenterAnimation = true,
                    IsAnimated = false
                };
            }
        }

        protected virtual void UpdateInteraction(MessageViewModel message, MessageReaction interaction, bool recycled, bool chosen)
        {
            IsChecked = interaction.IsChosen;

            if (interaction.TotalCount > interaction.RecentSenderIds.Count)
            {
                Count.Visibility = Visibility.Visible;
                Count.SetText(Formatter.ShortNumber(interaction.TotalCount), recycled && chosen);

                RecentChoosers?.Visibility = Visibility.Collapsed;
            }
            else
            {
                RecentChoosers ??= GetRecentChoosers();
                RecentChoosers.Visibility = Visibility.Visible;

                var destination = RecentChoosers.Items;
                var origin = interaction.RecentSenderIds;

                if (destination.Count > 0 && recycled)
                {
                    destination.ReplaceDiff(origin);
                }
                else
                {
                    destination.ReplaceWith(origin);
                }

                Count?.Visibility = Visibility.Collapsed;
            }
        }

        private RecentUserHeads GetRecentChoosers()
        {
            RecentChoosers ??= GetTemplateChild(nameof(RecentChoosers)) as RecentUserHeads;
            RecentChoosers.RecentUserHeadChanged += RecentChoosers_RecentUserHeadChanged;

            return RecentChoosers;
        }

        private void RecentChoosers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            photo.Source = ProfilePictureSource.MessageSender(_message.ClientService, sender);
        }

        protected override void OnApplyTemplate()
        {
            Count = GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
            Overlay = GetTemplateChild(nameof(Overlay)) as Popup;
            Icon = GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;
            Icon.Ready += OnReady;
            Icon.LoopCompleted += OnLoopCompleted;

            if (_reaction != null)
            {
                SetReaction(_message, _reaction);
            }

            SetUnread(_unread);

            base.OnApplyTemplate();
        }

        private void OnReady(object sender, EventArgs e)
        {
            SetUnread(_unread);
        }

        private void OnLoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            this.BeginOnUIThread(OnLoopCompleted);
        }

        private void OnLoopCompleted()
        {
            if (Icon?.Source is ReactionFileSource reaction && Icon.Source.IsAnimated && IsConnected)
            {
                Icon.Source = reaction.Clone(false);
            }
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        public virtual async void OnContextRequested(ContextRequestedEventArgs args)
        {
            var message = _message;
            if (message == null || (message.IsChannelPost && _reactionType is not ReactionTypeCustomEmoji))
            {
                return;
            }

            var flyout = new MenuFlyout();
            if (!message.IsChannelPost)
            {
                var popup = new InteractionsView(message.ClientService, message.ChatId, message.Id, _reactionType)
                {
                    Width = 264,
                    Height = 48 * _reaction.TotalCount,
                    MinHeight = 48,
                    MaxHeight = 360
                };

                void handler(InteractionsView sender, ItemClickEventArgs e)
                {
                    sender.ItemClick -= handler;
                    flyout.Hide();

                    if (e.ClickedItem is AddedReaction addedReaction)
                    {
                        message.Delegate.NavigationService.NavigateToSender(addedReaction.SenderId, state: new NavigationState { { "report_reactions", new ReportMessageReactions(message.ChatId, message.Id, addedReaction.SenderId) } });
                    }
                    else if (e.ClickedItem is MessageViewer messageViewer)
                    {
                        message.Delegate.NavigationService.NavigateToUser(messageViewer.UserId);
                    }
                }

                popup.ItemClick += handler;

                flyout.Items.Add(new MenuFlyoutContent
                {
                    Content = popup,
                    Padding = new Thickness(0)
                });
            }

            if (_reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                var grid = new Grid
                {
                    // Approximate height for two lines of text
                    //Height = 46,
                    Width = 264 - 4 - 4,
                    HorizontalAlignment = HorizontalAlignment.Left
                };

                ShowSkeleton(grid);

                var button = new Button
                {
                    Content = grid,
                    Style = BootStrapper.Current.Resources["ListEmptyButtonStyle"] as Style,
                    CornerRadius = new CornerRadius(4),
                    IsEnabled = false
                };

                var block = new RichTextBlock
                {
                    IsTextSelectionEnabled = false,
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Margin = new Thickness(11, 3, 11, 5),
                    VerticalAlignment = VerticalAlignment.Center
                };

                var paragraph = new Paragraph();
                paragraph.Inlines.Add("\n");
                block.Blocks.Add(paragraph);
                grid.Children.Add(block);

                void click(object sender, RoutedEventArgs e)
                {
                    button.Click -= click;
                    flyout.Hide();

                    ShowCustomEmoji();
                }

                button.Click += click;

                var content = new MenuFlyoutContent
                {
                    Content = button,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                flyout.CreateFlyoutSeparator();
                flyout.Items.Add(content);

                flyout.ShowAt(this, args);

                var function = _message.ClientService.GetCustomEmojiStickerSets(new[] { customEmoji.CustomEmojiId });

                await Task.WhenAll(function, Task.Delay(250));

                var response = await function;
                if (response is StickerSets stickerSets)
                {
                    button.IsEnabled = true;

                    if (stickerSets.Sets.Count != 1)
                    {
                        TextBlockHelper.SetMarkdown(block, paragraph.Inlines, Locale.Declension(Strings.R.MessageContainsReactionsPacks, stickerSets.Sets.Count));
                    }
                    else
                    {
                        var player = new CustomEmojiIcon();
                        player.LoopCount = 0;
                        player.Source = DelayedFileSource.FromStickerSetInfo(_message.ClientService, stickerSets.Sets[0]);

                        player.HorizontalAlignment = HorizontalAlignment.Left;
                        player.FlowDirection = FlowDirection.LeftToRight;
                        player.Margin = new Thickness(0, 0, 0, -4);
                        player.Width = 16;
                        player.Height = 16;
                        player.FrameSize = new Size(16, 16);

                        var inline = new InlineUIContainer();
                        inline.Child = player;

                        var text = Strings.MessageContainsReactionPack;
                        var index = text.IndexOf("{0}");

                        var prefix = text.Substring(0, index);
                        var suffix = text.Substring(index + 3);

                        paragraph.Inlines.Clear();
                        paragraph.Inlines.Add(prefix);
                        paragraph.Inlines.Add(inline);
                        paragraph.Inlines.Add($" {stickerSets.Sets[0].Title}", FontWeights.SemiBold);
                        paragraph.Inlines.Add(suffix);
                    }

                    var visual = ElementCompositionPreview.GetElementChildVisual(grid);
                    var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
                    animation.InsertKeyFrame(0, 1);
                    animation.InsertKeyFrame(1, 0);

                    visual.StartAnimation("Opacity", animation);
                }
            }
            else
            {
                flyout.ShowAt(this, args);
            }
        }

        private void ShowSkeleton(UIElement element)
        {
            var size = new Vector2(264, 48);
            var itemHeight = 6 + 36 + 6;

            var shapes = new List<CanvasGeometry>();

            shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 8, 6, 220, 14, 4, 4));
            shapes.Add(CanvasGeometry.CreateRoundedRectangle(null, 8, 6 + 16, 180, 14, 4, 4));

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

            ElementCompositionPreview.SetElementChildVisual(element, visual);
        }

        private async void ShowCustomEmoji()
        {
            if (_reactionType is not ReactionTypeCustomEmoji customEmoji)
            {
                return;
            }

            var response = await _message.ClientService.SendAsync(new GetCustomEmojiStickers(new[] { customEmoji.CustomEmojiId }));
            if (response is Stickers stickers)
            {
                var sets = new HashSet<long>();

                foreach (var sticker in stickers.StickersValue)
                {
                    sets.Add(sticker.SetId);
                }

                await StickersPopup.ShowAsync(_message.Delegate.NavigationService, sets);
            }
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var chosen = _reaction;
            if (chosen != null && Icon != null && _message?.Id != 0)
            {
                OnClick(_message, chosen);
            }


            //if (_isTag)
            //{
            //    ContextMenuRequested();
            //    return;
            //}

        }

        protected virtual void OnClick(MessageViewModel message, MessageReaction chosen)
        {
            if (chosen.IsChosen)
            {
                message.ClientService.Send(new RemoveMessageReaction(message.ChatId, message.Id, chosen.Type));
            }
            else
            {
                Animate();
                message.ClientService.Send(new AddMessageReaction(message.ChatId, message.Id, chosen.Type, false, false));
            }
        }

        protected async void Animate()
        {
            if (_reactionType is ReactionTypeEmoji emoji)
            {
                var response = await _message.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                if (response is EmojiReaction reaction && reaction.AroundAnimation != null)
                {
                    var around = await _message.ClientService.DownloadFileAsync(reaction.AroundAnimation.StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && IsConnected)
                    {
                        _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, true));
                    }
                }
            }
            else if (_reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                var response = await _message.ClientService.SendAsync(new GetCustomEmojiReactionAnimations());
                if (response is Stickers stickers)
                {
                    var random = new Random();
                    var next = random.Next(0, stickers.StickersValue.Count);

                    var around = await _message.ClientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && IsConnected)
                    {
                        _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, true));
                    }
                }
            }
            else if (_reactionType is ReactionTypePaid)
            {
                var random = new Random();
                var next = random.Next(1, 6);

                var around = TdExtensions.GetLocalFile($"Assets\\Animations\\PaidReactionAround{next}.tgs");
                if (around.Local.IsDownloadingCompleted && IsConnected)
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, false));
                }
            }
        }

        protected void Animate(File around, bool cache)
        {
            if (Icon?.Source is ReactionFileSource reaction && !Icon.Source.IsAnimated)
            {
                Icon.Source = reaction.Clone(true);
            }

            Icon?.Play();

            var popup = Overlay;
            var dispatcher = DispatcherQueue.GetForCurrentThread();

            var aroundView = new AnimatedImage();
            aroundView.Width = 32 * 3;
            aroundView.Height = 32 * 3;
            aroundView.LoopCount = 1;
            aroundView.FrameSize = new Size(32 * 3, 32 * 3);
            aroundView.DecodeFrameType = DecodePixelType.Logical;
            aroundView.IsCachingEnabled = cache;
            aroundView.AutoPlay = true;
            aroundView.Source = new LocalFileSource(around);
            aroundView.LoopCompleted += (s, args) =>
            {
                dispatcher.TryEnqueue(Continue);
            };

            var root = new Grid();
            root.Width = 32 * 3;
            root.Height = 32 * 3;
            root.Children.Add(aroundView);

            popup.Child = root;
            popup.XamlRoot = XamlRoot;
            popup.IsOpen = true;
        }

        private void Continue()
        {
            Logger.Info();

            var popup = Overlay;
            if (popup == null)
            {
                return;
            }

            popup.IsOpen = false;
            popup.Child = null;
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right && Parent is Panel panel)
            {
                e.Handled = true;

                var index = panel.Children.IndexOf(this);

                Control control = null;
                if (e.Key == VirtualKey.Left && index > 0)
                {
                    control = panel.Children[index - 1] as Control;
                }
                else if (e.Key == VirtualKey.Right && index < panel.Children.Count - 1)
                {
                    control = panel.Children[index + 1] as Control;
                }

                control?.Focus(Windows.UI.Xaml.FocusState.Keyboard);
            }
            if (e.Key is >= VirtualKey.Left and <= VirtualKey.Down && false)
            {
                e.Handled = true;

                var direction = e.Key switch
                {
                    VirtualKey.Left => FocusNavigationDirection.Left,
                    VirtualKey.Up => FocusNavigationDirection.Up,
                    VirtualKey.Right => FocusNavigationDirection.Right,
                    VirtualKey.Down => FocusNavigationDirection.Down,
                    _ => FocusNavigationDirection.Next
                };

                FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = Parent });
            }

            base.OnKeyDown(e);
        }

        //private CompositionPath GetClipGeometry(float width)
        //{
        //    CanvasGeometry result;
        //    using (var builder = new CanvasPathBuilder(null))
        //    {
        //        var far = 28f;

        //        var blp = width - (far - 14.4508f);
        //        var brp = width - (far - 20.1773f);
        //        var trp = width - (far - 14.4108f);

        //        var brp1trp2 = width - (far - 16.6541f);
        //        var brp2trp1 = width - (far - 18.7758f);

        //        var tipep = width - (far - 27.1917f);
        //        var tipp12 = width - (far - 28.2705f);

        //        builder.BeginFigure(5.53846f, 0);
        //        builder.AddCubicBezier(new Vector2(2.47964f, 0), new Vector2(0, 2.47964f), new Vector2(0, 5.53846f));
        //        builder.AddLine(0, 18.4638f);
        //        builder.AddCubicBezier(new Vector2(0, 21.5225f), new Vector2(2.47964f, 24.0022f), new Vector2(5.53846f, 24.0022f));
        //        builder.AddLine(blp, 24.0022f);
        //        builder.AddCubicBezier(new Vector2(brp1trp2, 24.0022f), new Vector2(brp2trp1, 22.9825f), new Vector2(brp, 21.2308f));
        //        builder.AddLine(tipep, 14.3088f);
        //        builder.AddCubicBezier(new Vector2(tipp12, 12.9603f), new Vector2(tipp12, 11.0442f), new Vector2(tipep, 9.69554f));
        //        builder.AddLine(brp, 2.77148f);
        //        builder.AddCubicBezier(new Vector2(brp2trp1, 1.01976f), new Vector2(brp1trp2, 0), new Vector2(trp, 0));
        //        builder.AddLine(5.53846f, 0);
        //        builder.EndFigure(CanvasFigureLoop.Closed);
        //        builder.AddGeometry(CanvasGeometry.CreateEllipse(null, width - (far - 17), 9 + 3, 3, 3));

        //        result = CanvasGeometry.CreatePath(builder);
        //    }
        //    return new CompositionPath(result);
        //}
    }

    public partial class ReactionButtonAutomationPeer : ToggleButtonAutomationPeer
    {
        private readonly ReactionButton _owner;

        public ReactionButtonAutomationPeer(ReactionButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName() ?? base.GetNameCore();
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ListItem;
        }
    }
}
