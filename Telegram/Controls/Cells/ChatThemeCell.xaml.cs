//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatThemeCell : UserControl, IMultipleElement
    {
        private ChatThemeViewModel _theme;
        private long _usedChatId;

        public ChatThemeCell()
        {
            InitializeComponent();

            var visual = ElementComposition.GetElementVisual(Animated);
            visual.CenterPoint = new Vector3(24, 48, 0);
            visual.Scale = new Vector3(0.625f);
        }

        public void Update(ChatThemeViewModel theme, long usedChatId = 0)
        {
            _theme = theme;
            _usedChatId = usedChatId;

            if (theme.Type is ChatThemeEmoji emoji)
            {
                NameText.Text = string.Empty;
                Animated.Source = new AnimatedEmojiFileSource(theme.ClientService, emoji.Name);
                UsedTheme.Visibility = Visibility.Collapsed;
            }
            else if (theme.Type is ChatThemeGift gift)
            {
                NameText.Text = string.Empty;
                Animated.Source = DelayedFileSource.FromSticker(theme.ClientService, gift.GiftTheme.Gift.Model.Sticker);

                if (usedChatId != gift.GiftTheme.Gift.UsedThemeChatId && theme.ClientService.TryGetChat(gift.GiftTheme.Gift.UsedThemeChatId, out Chat userChat))
                {
                    UsedThemePhoto.Source = ProfilePictureSource.Chat(theme.ClientService, userChat);
                    UsedTheme.Visibility = Visibility.Visible;
                }
                else
                {
                    UsedTheme.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                NameText.Text = "\u274C";
                Animated.Source = null;
                UsedTheme.Visibility = Visibility.Collapsed;
            }

            _backgroundBrush = ActualTheme == ElementTheme.Dark ? Colors.Black : Colors.White;
            _innerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(_backgroundBrush);

            var settings = ActualTheme == ElementTheme.Light ? theme.LightSettings : theme.DarkSettings;
            if (settings == null)
            {
                NoTheme.Text = theme.IsChannel ? Strings.ChannelNoWallpaper : Strings.ChatNoTheme;
                NoTheme.Visibility = Visibility.Visible;

                Preview.Visibility = Visibility.Collapsed;

                Outgoing.Fill = null;
                Incoming.Fill = null;

                _selectionBrush = ActualTheme == ElementTheme.Light ? Theme.AccentLight.Default : Theme.AccentDark.Default;
                _outerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(_selectionBrush);
                return;
            }

            NoTheme.Visibility = Visibility.Collapsed;

            Preview.Visibility = Visibility.Visible;

            if (settings.Background?.Type is BackgroundTypePattern && settings.Background?.Document == null)
            {
                Load();

                async void Load()
                {
                    var response = await theme.ClientService.SendAsync(new SearchBackground(settings.Background.Name));
                    if (response is Background test)
                    {
                        settings.Background.Document = test.Document;
                        Update(theme, _usedChatId);
                    }
                }
            }
            else if (settings.Background != null)
            {
                Preview.UpdateSource(theme.ClientService, settings.Background, true);
            }

            var accent = settings.AccentColor.ToColor();
            var info = ThemeAccentInfo.FromSettings(ActualTheme == ElementTheme.Light ? TelegramTheme.Light : TelegramTheme.Dark, settings);

            // Before Fill: it decides the order of the stops.
            Outgoing.IsAnimated = settings.AnimateOutgoingMessageFill;

            Outgoing.Fill = settings.OutgoingMessageFill switch
            {
                BackgroundFillGradient => settings.OutgoingMessageFill,
                BackgroundFillFreeformGradient => settings.OutgoingMessageFill,
                _ => new BackgroundFillSolid(info.MessageBackgroundOutColor.ToValue())
            };

            Incoming.Fill = new SolidColorBrush(info.MessageBackgroundColor);

            _selectionBrush = accent;
            _outerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(accent);
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_theme != null)
            {
                Update(_theme, _usedChatId);
            }

            _backgroundBrush = ActualTheme == ElementTheme.Dark ? Colors.Black : Colors.White;
            _innerShape?.StrokeBrush = BootStrapper.Current.Compositor.CreateColorBrush(_backgroundBrush);
        }

        private void Animated_LoopCompleted(object sender, AnimatedImageLoopCompletedEventArgs e)
        {
            this.BeginOnUIThread(OnLoopCompleted);
        }

        private void OnLoopStarted()
        {
            var visual = ElementComposition.GetElementVisual(Animated);
            var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1, new Vector3(1.0f));
            visual.StartAnimation("Scale", animation);
        }

        private void OnLoopCompleted()
        {
            var visual = ElementComposition.GetElementVisual(Animated);
            var animation = visual.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(1, new Vector3(0.625f));
            visual.StartAnimation("Scale", animation);
        }

        private CompositionRoundedRectangleGeometry _outer;
        private CompositionRoundedRectangleGeometry _inner;

        private CompositionSpriteShape _outerShape;
        private CompositionSpriteShape _innerShape;

        private ShapeVisual _visual;

        private Color _selectionBrush;
        private Color _backgroundBrush;

        public void UpdateState(bool selected, bool animate, bool multiple)
        {
            if (animate)
            {
                if (selected)
                {
                    Animated.Play();
                    OnLoopStarted();
                }
                else
                {
                    OnLoopCompleted();
                }
            }

            if (!selected && !animate && _visual == null)
            {
                return;
            }

            EnsureVisuals();

            if (animate)
            {
                var compositor = BootStrapper.Current.Compositor;
                var duration = Constants.FastAnimation;

                var scaleAnim = compositor.CreateVector2KeyFrameAnimation();
                scaleAnim.InsertKeyFrame(selected ? 0 : 1, new Vector2((ActualSize.X + 4) / ActualSize.X, (ActualSize.Y + 4) / ActualSize.Y));
                scaleAnim.InsertKeyFrame(selected ? 1 : 0, new Vector2(1));
                scaleAnim.Duration = duration;

                var fadeIn = compositor.CreateScalarKeyFrameAnimation();
                fadeIn.InsertKeyFrame(selected ? 0 : 1, 0);
                fadeIn.InsertKeyFrame(selected ? 1 : 0, 1);
                fadeIn.Duration = duration;

                _outerShape.StartAnimation("Scale", scaleAnim);
                _innerShape.StartAnimation("Scale", scaleAnim);

                _visual.StartAnimation("Opacity", fadeIn);
            }
            else
            {
                //outerShape.Scale = new Vector2(selected ? 1 : size / 28);
                //innerShape1.Offset = new Vector2(selected ? -6 : 0, 0);
                //innerShape3.Offset = new Vector2(selected ? 6 : 0, 0);

                _visual.Opacity = selected ? 1 : 0;
            }
        }

        private void EnsureVisuals()
        {
            var compositor = BootStrapper.Current.Compositor;

            _outer = compositor.CreateRoundedRectangleGeometry();
            _outer.CornerRadius = new Vector2(4);
            _outer.Offset = new Vector2(1);

            _inner = compositor.CreateRoundedRectangleGeometry();
            _inner.CornerRadius = new Vector2(4);
            _inner.Offset = new Vector2(2);

            _outerShape = compositor.CreateSpriteShape(_outer);
            _outerShape.StrokeBrush = compositor.CreateColorBrush(_selectionBrush);
            _outerShape.StrokeThickness = 2;
            _outerShape.IsStrokeNonScaling = true;

            _innerShape = compositor.CreateSpriteShape(_inner);
            _innerShape.StrokeBrush = compositor.CreateColorBrush(_backgroundBrush);
            _innerShape.StrokeThickness = 4;
            _innerShape.IsStrokeNonScaling = true;

            _visual = compositor.CreateShapeVisual();
            _visual.Shapes.Add(_innerShape);
            _visual.Shapes.Add(_outerShape);

            ElementCompositionPreview.SetElementChildVisual(RootGrid, _visual);

            if (ActualSize.X > 0 && ActualSize.Y > 0)
            {
                var final = ActualSize;

                _outerShape.CenterPoint = final / 2;
                _innerShape.CenterPoint = final / 2;

                _outer.Size = new Vector2(final.X - 2, final.Y - 2);
                _inner.Size = new Vector2(final.X - 4, final.Y - 4);
                _visual.Size = final;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var final = finalSize.ToVector2();

            _outerShape?.CenterPoint = final / 2;
            _innerShape?.CenterPoint = final / 2;

            _outer?.Size = new Vector2(final.X - 2, final.Y - 2);
            _inner?.Size = new Vector2(final.X - 4, final.Y - 4);
            _visual?.Size = final;

            return base.ArrangeOverride(finalSize);
        }
    }
}
