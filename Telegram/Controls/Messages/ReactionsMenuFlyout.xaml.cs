//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using LinqToVisualTree;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Linq;
using System.Numerics;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Drawers;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Point = Windows.Foundation.Point;

namespace Telegram.Controls.Messages
{
    public sealed partial class ReactionsMenuFlyout : UserControl
    {
        private readonly AvailableReactions _reactions;

        private readonly MessageViewModel _message;
        private readonly MessageBubble _bubble;
        private readonly MenuFlyout _flyout;

        private MenuFlyoutPresenter _presenter;
        private Popup _popup;

        public static ReactionsMenuFlyout ShowAt(AvailableReactions reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            return new ReactionsMenuFlyout(reactions, message, bubble, flyout);
        }

        private ReactionsMenuFlyout(AvailableReactions reactions, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            _reactions = reactions /*message.ClientService.IsPremium ? reactions : reactions.Where(x => !x.IsPremium).ToList()*/;
            _message = message;
            _bubble = bubble;
            _flyout = flyout;

            InitializeComponent();
            Initialize(reactions, message, bubble, flyout);
        }

        private async void Initialize(AvailableReactions available, MessageViewModel message, MessageBubble bubble, MenuFlyout flyout)
        {
            var last = flyout.Items.LastOrDefault();
            var presenter = last.Ancestors<MenuFlyoutPresenter>().FirstOrDefault();
            presenter.Unloaded += Presenter_Unloaded;

            _presenter = presenter;
            _popup = new Popup();

            var transform = presenter.TransformToVisual(Window.Current.Content);
            var position = transform.TransformPoint(new Point());

            var source = available.TopReactions.ToList();
            if (source.Count < 6)
            {
                var additional = available.RecentReactions.Count > 0
                    ? available.RecentReactions
                    : available.PopularReactions;

                available.TopReactions
                    .Select(x => x.Type)
                    .Discern(out var emoji, out var customEmoji);

                foreach (var item in additional)
                {
                    if (item.Type is ReactionTypeEmoji emojii
                        && emoji != null
                        && emoji.Contains(emojii.Emoji))
                    {
                        continue;
                    }
                    else if (item.Type is ReactionTypeCustomEmoji customEmojii
                        && customEmoji != null
                        && customEmoji.Contains(customEmojii.CustomEmojiId))
                    {
                        continue;
                    }

                    source.Add(item);
                }
            }

            var hasMore = source.Count > 6 || available.AllowCustomEmoji;
            var count = Math.Min(source.Count, 7);

            while (source.Count > 6)
            {
                source.RemoveAt(source.Count - 1);
            }

            var itemSize = 28;
            var itemPadding = 4;

            var itemTotal = itemSize + itemPadding;

            var actualWidth = presenter.ActualSize.X + 18 + 12 + 18;
            var width = Math.Max(36 + 14, 4 + (count * itemTotal));

            var padding = actualWidth - width;
            var count1 = 0;

            Presenter.Padding = new Thickness(4, 0, 0, 0);

            Shadow.Width = width;
            Pill.Width = width;
            Presenter.Width = width;

            var height = 40;
            var haheight = 20;

            Pill.VerticalAlignment = VerticalAlignment.Top;
            Pill.Height = Shadow.Height = height + 20;
            Pill.Margin = Shadow.Margin = new Thickness(0, 0, 0, -20);

            Expand.Visibility = hasMore ? Visibility.Visible : Visibility.Collapsed;

            var figure = new PathFigure();
            figure.StartPoint = new Point(haheight, 0);
            //figure.Segments.Add(new LineSegment { Point = new Point(18 + 20, yy + 0) });


            figure.Segments.Add(new LineSegment { Point = new Point(width - haheight, 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - haheight, height), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            figure.Segments.Add(new ArcSegment { Point = new Point(width - haheight - 14, height), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            figure.Segments.Add(new LineSegment { Point = new Point(haheight, height) });
            figure.Segments.Add(new ArcSegment { Point = new Point(haheight, 0), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            var path = new PathGeometry();
            path.Figures.Add(figure);

            var data = new GeometryGroup();
            data.FillRule = FillRule.Nonzero;
            data.Children.Add(path);
            data.Children.Add(new EllipseGeometry { Center = new Point(width - haheight - 8 + 5, height + 20 - 7), RadiusX = 3.5f, RadiusY = 3.5f });

            Pill.Data = data;

            LayoutRoot.Padding = new Thickness(16, 40, 16, 32);

            var device = CanvasDevice.GetSharedDevice();
            var rect1 = CanvasGeometry.CreateRectangle(device, Math.Min(width - actualWidth, 0), 0, Math.Max(width + 16 + 16 + Math.Max(0, padding), actualWidth), 860);
            var elli1 = CanvasGeometry.CreateRoundedRectangle(device, width - actualWidth + 18 + 16, height + height + 5, presenter.ActualSize.X, 860, 8, 8);
            var group1 = CanvasGeometry.CreateGroup(device, new[] { elli1, rect1 }, CanvasFilledRegionDetermination.Alternate);

            var rootVisual = ElementCompositionPreview.GetElementVisual(LayoutRoot);
            var compositor = rootVisual.Compositor;
            rootVisual.Clip = rootVisual.Compositor.CreateGeometricClip(rootVisual.Compositor.CreatePathGeometry(new CompositionPath(group1)));

            var pillShadow = compositor.CreateDropShadow();
            pillShadow.BlurRadius = 16f;
            pillShadow.Opacity = 0.14f;
            pillShadow.Color = Colors.Black;
            pillShadow.Mask = Pill.GetAlphaMask();

            var pillReceiver = compositor.CreateSpriteVisual();
            pillReceiver.Shadow = pillShadow;
            pillReceiver.Size = new Vector2(width, height + 20);
            pillReceiver.Offset = new Vector3(0, 8, 0);

            ElementCompositionPreview.SetElementChildVisual(Shadow, pillReceiver);

            var x = position.X - 18 + padding;
            var y = position.Y - (40 + 4);

            _popup.Child = this;
            _popup.Margin = new Thickness(x - 16, y - height, 0, 0);
            _popup.RequestedTheme = presenter.ActualTheme;
            _popup.ShouldConstrainToRootBounds = false;
            _popup.AllowFocusOnInteraction = false;
            _popup.IsOpen = true;

            var visualPill = ElementCompositionPreview.GetElementVisual(Pill);
            visualPill.CenterPoint = new Vector3(height / 2, height / 2, 0);
            visualPill.CenterPoint = new Vector3(width - height / 2, height / 2, 0);

            var visualExpand = ElementCompositionPreview.GetElementVisual(Expand);
            visualExpand.CenterPoint = new Vector3(32 / 2f, 24 / 2f, 0);

            var clip = compositor.CreateRoundedRectangleGeometry();
            clip.CornerRadius = new Vector2(height / 2);

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            var scalePill = compositor.CreateSpringVector3Animation();
            scalePill.InitialValue = Vector3.Zero;
            scalePill.FinalValue = Vector3.One;
            scalePill.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            scalePill.DampingRatio = 0.7f;

            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, 0);
            translation.InsertKeyFrame(1, 16);

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, 0);
            opacity.InsertKeyFrame(1, 0.14f);

            visualPill.StartAnimation("Scale", scalePill);
            visualExpand.StartAnimation("Scale", scalePill);

            translation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            translation.DelayTime = TimeSpan.FromMilliseconds(150 + 100);
            opacity.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            opacity.DelayTime = TimeSpan.FromMilliseconds(150 + 100);

            pillShadow.StartAnimation("BlurRadius", translation);
            pillShadow.StartAnimation("Opacity", opacity);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(0, new Vector2(height, height));
            resize.InsertKeyFrame(1, new Vector2(width, height));
            resize.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            resize.DelayTime = TimeSpan.FromMilliseconds(100);
            resize.Duration = Constants.FastAnimation;

            var move = compositor.CreateVector2KeyFrameAnimation();
            move.InsertKeyFrame(0, new Vector2(width - height, 40));
            move.InsertKeyFrame(1, new Vector2(0, 40));
            move.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;
            move.DelayTime = TimeSpan.FromMilliseconds(100);
            move.Duration = Constants.FastAnimation;

            var viewVisual = ElementCompositionPreview.GetElementVisual(Presenter);
            viewVisual.CenterPoint = new Vector3(height / 2, height / 2, 0);
            viewVisual.CenterPoint = new Vector3(width - height / 2, height / 2, 0);
            viewVisual.StartAnimation("Scale", scalePill);

            batch.End();


            var viewModel = EmojiDrawerViewModel.GetForCurrentView(message.ClientService.SessionId, EmojiDrawerMode.Reactions);
            var yolo = await viewModel.UpdateReactions(available, null);

            foreach (var item in yolo)
            {
                static AnimatedImage Create(double size, bool auto)
                {
                    var animated = new AnimatedImage();
                    animated.AutoPlay = auto;
                    animated.LimitFps = !auto;
                    animated.LoopCount = auto ? 1 : 0;
                    animated.FrameSize = new Size(size, size);
                    animated.DecodeFrameType = DecodePixelType.Logical;
                    animated.Width = size;
                    animated.Height = size;

                    return animated;
                }

                var visible = Create(28, true);
                var preload = Create(32, false);

                var delayed = new DelayedFileSource(message.ClientService, item.Item2);

                visible.Source = delayed;
                preload.Source = delayed;
                preload.LoopCompleted += (s, args) => args.Cancel = true;
                preload.Opacity = 0;
                preload.Play();

                var button = new HyperlinkButton();
                button.Width = 32;
                button.Height = 28;
                button.Background = new SolidColorBrush(Colors.Red);
                //button.Margin = new Thickness(4, 0, 0, 0);
                button.Content = visible;
                button.Style = BootStrapper.Current.Resources["EmptyHyperlinkButtonStyle"] as Style;
                button.Tag = item.Item1.Type;
                button.Click += Reaction_Click;

                Grid.SetColumn(preload, count1);
                Grid.SetColumn(button, count1);

                Presenter.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Auto) });
                Presenter.Children.Add(preload);
                Presenter.Children.Add(button);
                count1++;
            }
        }

        private void Presenter_Unloaded(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = false;
        }

        private void Reaction_Click(object sender, RoutedEventArgs e)
        {
            _flyout.Hide();

            if (sender is HyperlinkButton button && button.Tag is ReactionType reaction)
            {
                ToggleReaction(reaction);
            }
        }

        private async void ToggleReaction(ReactionType reaction)
        {
            if (_message.InteractionInfo != null && _message.InteractionInfo.Reactions.IsChosen(reaction))
            {
                _message.ClientService.Send(new RemoveMessageReaction(_message.ChatId, _message.Id, reaction));
            }
            else
            {
                await _message.ClientService.SendAsync(new AddMessageReaction(_message.ChatId, _message.Id, reaction, false, true));

                if (_bubble != null && _bubble.IsLoaded)
                {
                    var unread = new UnreadReaction(reaction, null, false);

                    _message.UnreadReactions.Add(unread);
                    _bubble.UpdateMessageReactions(_message, true);
                    _message.UnreadReactions.Remove(unread);
                }
            }
        }

        private void Expand_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(_message.ClientService, EmojiDrawerMode.Reactions, this, HorizontalAlignment.Center, _message, _reactions);
            flyout.Loaded += (s, args) =>
            {
                _flyout.Hide();
            };
        }
    }
}
