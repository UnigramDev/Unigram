//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System.Numerics;
using Telegram.Common;
using Telegram.Services;
using Telegram.Services.Settings;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Cells
{
    public sealed partial class ChatThemeCell : UserControl
    {
        private SelectorItem _container;
        private ChatThemeViewModel _theme;
        private long _usedChatId;

        private long _selectionChangedToken;

        public ChatThemeCell()
        {
            InitializeComponent();

            var visual = ElementComposition.GetElementVisual(Animated);
            visual.CenterPoint = new Vector3(24, 48, 0);
            visual.Scale = new Vector3(0.625f);
        }

        public void Recycle()
        {
            _container.UnregisterPropertyChangedCallback(SelectorItem.IsSelectedProperty, ref _selectionChangedToken);
        }

        public void Update(SelectorItem container, ChatThemeViewModel theme, long usedChatId = 0)
        {
            _theme = theme;
            _usedChatId = usedChatId;
            _container = container;

            container.RegisterPropertyChangedCallback(SelectorItem.IsSelectedProperty, OnSelectionChanged, ref _selectionChangedToken);

            if (theme.Type is ChatThemeEmoji emoji)
            {
                Name.Text = string.Empty;
                Animated.Source = new AnimatedEmojiFileSource(theme.ClientService, emoji.Name);
                UsedTheme.Visibility = Visibility.Collapsed;
            }
            else if (theme.Type is ChatThemeGift gift)
            {
                Name.Text = string.Empty;
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
                Name.Text = "\u274C";
                Animated.Source = null;
                UsedTheme.Visibility = Visibility.Collapsed;
            }

            var settings = ActualTheme == ElementTheme.Light ? theme.LightSettings : theme.DarkSettings;
            if (settings == null)
            {
                NoTheme.Text = theme.IsChannel ? Strings.ChannelNoWallpaper : Strings.ChatNoTheme;
                NoTheme.Visibility = Visibility.Visible;

                Preview.Visibility = Visibility.Collapsed;

                Outgoing.Fill = null;
                Incoming.Fill = null;
                return;
            }

            NoTheme.Visibility = Visibility.Collapsed;

            Preview.Visibility = Visibility.Visible;
            Preview.UpdateSource(theme.ClientService, settings.Background, true);

            if (settings.OutgoingMessageFill != null)
            {
                Outgoing.Fill = settings.OutgoingMessageFill;
            }
            else
            {
                var accent = settings.AccentColor.ToColor();
                var outgoing = settings.OutgoingMessageAccentColor.ToColor();
                var info = ThemeAccentInfo.FromAccent(ActualTheme == ElementTheme.Light ? TelegramThemeType.Day : TelegramThemeType.Tinted, accent, outgoing);

                Outgoing.Fill = new BackgroundFillSolid(info.Values["MessageBackgroundOutgoing"].ToValue());
            }

            Incoming.Fill = new SolidColorBrush(ThemeAccentInfo.Colorize(ActualTheme == ElementTheme.Light ? TelegramThemeType.Day : TelegramThemeType.Tinted, settings.AccentColor.ToColor(), "MessageBackgroundBrush"));
        }

        private void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            if (_theme != null)
            {
                Update(_container, _theme, _usedChatId);
            }
        }

        private void OnSelectionChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (_container.IsSelected)
            {
                Animated.Play();
                OnLoopStarted();
            }
            else
            {
                OnLoopCompleted();
            }
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
    }
}
