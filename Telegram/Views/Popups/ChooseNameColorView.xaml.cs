//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;
using Point = Windows.Foundation.Point;

namespace Telegram.Views.Popups
{
    public sealed partial class ChooseNameColorView : UserControl
    {
        private IClientService _clientService;
        private MessageSender _sender;

        public ChooseNameColorView()
        {
            InitializeComponent();
        }

        public string PrimaryButtonText { get; private set; }

        public void Initialize(IClientService clientService, MessageSender sender)
        {
            //Title = Strings.UserColorTitle;

            _clientService = clientService;
            _sender = sender;

            var peerColors = clientService.AccentColors;
            var peerColorsAvailable = clientService.AvailableAccentColors;

            if (peerColors == null || peerColorsAvailable == null)
            {
                return;
            }

            var colors = new List<NameColor>();

            foreach (var id in peerColorsAvailable)
            {
                if (peerColors.TryGetValue(id, out NameColor value))
                {
                    colors.Add(value);
                }
            }

            List.ItemsSource = colors;

            var preview = ElementCompositionPreview.GetElementVisual(Preview);
            preview.Clip = preview.Compositor.CreateInsetClip();

            var customEmojiId = 0L;
            var accentColorId = 0;

            if (clientService.TryGetUser(sender, out User user))
            {
                customEmojiId = user.BackgroundCustomEmojiId;
                accentColorId = user.AccentColorId;

                var webPage = new WebPage
                {
                    SiteName = Strings.AppName,
                    Title = Strings.UserColorPreviewLinkTitle,
                    Description = new FormattedText(Strings.UserColorPreviewLinkDescription, Array.Empty<TextEntity>())
                };

                Message1.Mockup(clientService, Strings.UserColorPreview, sender, Strings.UserColorPreviewReply, webPage, false, DateTime.Now);

                BadgeText.Text = Strings.UserReplyIcon;
                NameColor.Footer = Strings.UserColorHint;
                PrimaryButtonText = Strings.UserColorApplyIcon;
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                customEmojiId = chat.BackgroundCustomEmojiId;
                accentColorId = chat.AccentColorId;

                var webPage = new WebPage
                {
                    SiteName = Strings.AppName,
                    Title = Strings.ChannelColorPreviewLinkTitle,
                    Description = new FormattedText(Strings.ChannelColorPreviewLinkDescription, Array.Empty<TextEntity>())
                };

                Message1.Mockup(clientService, Strings.ChannelColorPreview, sender, Strings.ChannelColorPreviewReply, webPage, false, DateTime.Now);

                BadgeText.Text = Strings.ChannelReplyLogo;
                NameColor.Footer = Strings.ChannelReplyInfo;
                PrimaryButtonText = Strings.ChannelColorApply;
            }

            var accent = colors.FirstOrDefault(x => x.Id == accentColorId);
            accent ??= colors.FirstOrDefault();

            if (customEmojiId != 0)
            {
                Badge.Badge = null;
                Badge.Glyph = string.Empty;

                Animated.Source = new CustomEmojiFileSource(clientService, customEmojiId);
                Animated.ReplacementColor = new SolidColorBrush(accent.LightThemeColors[0]);
            }
            else
            {
                Badge.Badge = Strings.UserReplyIconOff;
                Badge.Glyph = Icons.Color;

                Animated.Source = null;
                Animated.ReplacementColor = null;
            }

            SelectedCustomEmojiId = customEmojiId;
            SelectedAccentColor = accent;

            List.SelectedItem = accent;
            BackgroundControl.Update(clientService, null);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is Grid content
                && content.Children[0] is Polygon polygon
                && content.Children[1] is Rectangle rectangle
                && args.Item is NameColor colors)
            {
                content.Background = new SolidColorBrush(colors.LightThemeColors[0]);
                polygon.Fill = colors.LightThemeColors.Count > 1
                    ? new SolidColorBrush(colors.LightThemeColors[1])
                    : null;

                rectangle.Fill = colors.LightThemeColors.Count > 2
                    ? new SolidColorBrush(colors.LightThemeColors[2])
                    : null;

                args.Handled = true;
            }
        }

        #endregion

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (List.SelectedItem is NameColor accent)
            {
                SelectedAccentColor = accent;

                Message1.UpdateMockup(_clientService, SelectedCustomEmojiId, accent.Id);
                Animated.ReplacementColor = new SolidColorBrush(accent.LightThemeColors[0]);
            }
        }

        private void Badge_Click(object sender, RoutedEventArgs e)
        {
            var flyout = EmojiMenuFlyout.ShowAt(_clientService, EmojiDrawerMode.Background, Animated, EmojiFlyoutAlignment.TopRight);
            flyout.EmojiSelected += Flyout_EmojiSelected;
        }

        private void Flyout_EmojiSelected(object sender, EmojiSelectedEventArgs e)
        {
            SelectedCustomEmojiId = e.CustomEmojiId;

            Message1.UpdateMockup(_clientService, SelectedCustomEmojiId, SelectedAccentColor.Id);

            if (e.CustomEmojiId != 0)
            {
                Animated.Source = new CustomEmojiFileSource(_clientService, SelectedCustomEmojiId);
                Badge.Badge = string.Empty;
            }
            else
            {
                Animated.Source = null;
                Badge.Badge = Strings.UserReplyIconOff;
            }
        }

        private void NameColor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is Grid content && content.Children[0] is Polygon polygon)
            {
                var width = e.NewSize.Width;

                //content.Width = width;
                content.Height = width;

                content.CornerRadius = new CornerRadius(width / 2);
                polygon.Points = new PointCollection
                {
                    new Point(width, 0),
                    new Point(width, width),
                    new Point(0, width)
                };
            }
        }

        public int RequiredLevel
        {
            set
            {
                if (value > 0)
                {
                    BadgeInfo.Text = Icons.LockClosedFilled14 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
                    BadgeInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    BadgeInfo.Visibility = Visibility.Collapsed;
                }
            }
        }

        #region SelectedAccentColor

        public NameColor SelectedAccentColor
        {
            get { return (NameColor)GetValue(SelectedAccentColorProperty); }
            set { SetValue(SelectedAccentColorProperty, value); }
        }

        public static readonly DependencyProperty SelectedAccentColorProperty =
            DependencyProperty.Register("SelectedAccentColor", typeof(NameColor), typeof(ChooseNameColorView), new PropertyMetadata(null));

        #endregion

        #region SelectedCustomEmojiId

        public long SelectedCustomEmojiId
        {
            get { return (long)GetValue(SelectedCustomEmojiIdProperty); }
            set { SetValue(SelectedCustomEmojiIdProperty, value); }
        }

        public static readonly DependencyProperty SelectedCustomEmojiIdProperty =
            DependencyProperty.Register("SelectedCustomEmojiId", typeof(long), typeof(ChooseNameColorView), new PropertyMetadata(0L));

        #endregion
    }
}
