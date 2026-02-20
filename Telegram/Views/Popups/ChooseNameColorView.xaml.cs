//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Settings;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

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

            var colors = clientService.GetAvailableAccentColors();
            List.ItemsSource = colors;

            Preview.CreateInsetClip();

            var customEmojiId = 0L;
            var accentColorId = 0;
            var upgradedGift = default(UpgradedGiftColors);

            if (clientService.TryGetUser(sender, out User user))
            {
                customEmojiId = user.BackgroundCustomEmojiId;
                accentColorId = user.AccentColorId;
                upgradedGift = user.UpgradedGiftColors;

                var linkPreview = new LinkPreview
                {
                    SiteName = Strings.AppName,
                    Title = Strings.UserColorPreviewLinkTitle,
                    Description = Strings.UserColorPreviewLinkDescription.AsFormattedText()
                };

                Message1.Mockup(clientService, Strings.UserColorPreview, sender, Strings.UserColorPreviewReply, linkPreview, false, DateTime.Now);

                BadgeText.Text = Strings.UserReplyIcon;
                NameColor.Footer = Strings.UserColorHint;
                PrimaryButtonText = Strings.UserColorApplyIcon;

                BackgroundControl.Update(clientService, null);
            }
            else if (clientService.TryGetChat(sender, out Chat chat))
            {
                customEmojiId = chat.BackgroundCustomEmojiId;
                accentColorId = chat.AccentColorId;
                upgradedGift = chat.UpgradedGiftColors;

                var linkPreview = new LinkPreview
                {
                    SiteName = Strings.AppName,
                    Title = Strings.ChannelColorPreviewLinkTitle,
                    Description = Strings.ChannelColorPreviewLinkDescription.AsFormattedText()
                };

                Message1.Mockup(clientService, Strings.ChannelColorPreview, sender, Strings.ChannelColorPreviewReply, linkPreview, false, DateTime.Now);

                BadgeText.Text = Strings.ChannelReplyLogo;
                NameColor.Footer = Strings.ChannelReplyInfo;
                PrimaryButtonText = Strings.ChannelColorApply;

                BackgroundControl.UpdateChat(clientService, chat.Background, chat.Theme);
            }

            var accent = upgradedGift == null ? colors.FirstOrDefault(x => x.Id == accentColorId) ?? colors.FirstOrDefault() : null;

            if (customEmojiId != 0 && upgradedGift == null)
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

            Message1.UpdateMockup(_clientService, customEmojiId, accent?.Id ?? 0, upgradedGift);
        }

        #region Recycle

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new GridViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.SizeChanged += NameColor_SizeChanged;
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
                SelectedGiftColors = null;

                Message1.UpdateMockup(_clientService, SelectedCustomEmojiId, accent.Id, null);
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
            if (e.Type is not ReactionTypeCustomEmoji customEmoji)
            {
                return;
            }

            // TODO: restore SelectedAccentColor

            SelectedCustomEmojiId = customEmoji.CustomEmojiId;
            SelectedGiftColors = null;

            Message1.UpdateMockup(_clientService, SelectedCustomEmojiId, SelectedAccentColor?.Id ?? 0, null);

            if (customEmoji.CustomEmojiId != 0)
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
            if (sender is GridViewItem selector)
            {
                var width = e.NewSize.Width;
                var content = selector.ContentTemplateRoot as Grid;
                var polygon = content.Children[0] as Polygon;

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
            get => 0;
            set
            {
                if (value > 0)
                {
                    BadgeInfo.Text = Icons.LockClosedFilled12 + Icons.Spacing + string.Format(Strings.BoostLevel, value);
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

        #region SelectedGiftColors

        public UpgradedGift SelectedGiftColors
        {
            get { return (UpgradedGift)GetValue(SelectedGiftColorsProperty); }
            set { SetValue(SelectedGiftColorsProperty, value); }
        }

        public static readonly DependencyProperty SelectedGiftColorsProperty =
            DependencyProperty.Register("SelectedGiftColors", typeof(UpgradedGift), typeof(ChooseNameColorView), new PropertyMetadata(null, OnSelectedGiftColorsChanged));

        private static void OnSelectedGiftColorsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChooseNameColorView)d).OnSelectedGiftColorsChanged(e.NewValue as UpgradedGift);
        }

        private void OnSelectedGiftColorsChanged(UpgradedGift upgradedGift)
        {
            if (upgradedGift == null)
            {
                return;
            }

            SelectedAccentColor = null;
            SelectedCustomEmojiId = 0;
            List.SelectedItem = null;

            Message1.UpdateMockup(_clientService, 0, 0, upgradedGift.Colors);
            Animated.ReplacementColor = new SolidColorBrush(upgradedGift.Colors.LightThemeAccentColor.ToColor());

            Animated.Source = null;
            Badge.Badge = Strings.UserReplyIconOff;
        }

        #endregion

        #region ChatTheme

        public ChatThemeViewModel ChatTheme
        {
            get { return (ChatThemeViewModel)GetValue(ChatThemeProperty); }
            set { SetValue(ChatThemeProperty, value); }
        }

        public static readonly DependencyProperty ChatThemeProperty =
            DependencyProperty.Register("ChatTheme", typeof(ChatThemeViewModel), typeof(ChooseNameColorView), new PropertyMetadata(null, OnChatThemeChanged));

        private static void OnChatThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((ChooseNameColorView)d).OnChatThemeChanged(e.NewValue as ChatThemeViewModel, e.OldValue as ChatThemeViewModel);
        }

        private void OnChatThemeChanged(ChatThemeViewModel newValue, ChatThemeViewModel oldValue)
        {
            BackgroundControl.UpdateChat(_clientService, null, newValue.Type);
        }

        #endregion
    }
}
