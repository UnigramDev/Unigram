//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Drawers;
using Telegram.Converters;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Supergroups.Popups
{
    public sealed partial class SupergroupTopicPopup : ContentPopup
    {
        private readonly IClientService _clientService;

        private readonly bool _creating;

        private int _colorIndex;
        private long _customEmojiId;

        public SupergroupTopicPopup(IClientService clientService, ForumTopicInfo topic)
        {
            InitializeComponent();

            _clientService = clientService;
            _creating = topic == null;

            if (topic != null)
            {
                _colorIndex = ForumTopicCell.FindIconColorIndex(topic.Icon.Color);
            }
            else
            {
                _colorIndex = new Random().Next(0, ForumTopicCell.ServerSupportedColors.Length);
            }

            _customEmojiId = topic?.Icon?.CustomEmojiId ?? 0;

            Title = topic == null ? Strings.NewTopic : Strings.EditTopic;

            Header.Text = topic?.IsGeneral is true ? Strings.CreateGeneralTopicTitle : Strings.CreateTopicTitle;

            PrimaryButtonText = topic == null ? Strings.Create : Strings.Done;
            SecondaryButtonText = Strings.Cancel;

            NameLabel.Text = topic?.Name ?? string.Empty;
            NameLabel.SelectionStart = NameLabel.Text.Length;

            if (topic?.IsGeneral is true)
            {
                EmojiRoot.Visibility = Visibility.Collapsed;
                RootGrid.Margin = new Thickness(0);
                Padding = new Thickness(24);
                VerticalContentAlignment = VerticalAlignment.Center;

                Identity.SetStatus(_clientService, new ForumTopicIcon(0, 0));
            }
            else
            {
                Emoji.DataContext = EmojiDrawerViewModel.Create(clientService.Session, EmojiDrawerMode.Topics);
                Emoji.ViewModel.Update();
                Emoji.ItemClick += OnItemClick;

                UpdateTopicIcon();
            }
        }

        private void UpdateTopicIcon()
        {
            var color = ForumTopicCell.ServerSupportedColors[_colorIndex % ForumTopicCell.ServerSupportedColors.Length].ToValue();
            var brush = ForumTopicCell.GetIconGradient(new ForumTopicIcon(color, 0));

            if (_customEmojiId == 0)
            {
                IconPath.Fill = brush;
                IconPath.Stroke = new SolidColorBrush(brush.GradientStops[1].Color);
                IconText.Text = InitialNameStringConverter.Convert(NameLabel.Text);

                IconRoot.Visibility = Visibility.Visible;
            }
            else
            {
                IconRoot.Visibility = Visibility.Collapsed;
            }

            Emoji.UpdateTopicIcon(NameLabel.Text, color);
            Identity.SetStatus(_clientService, new ForumTopicIcon(0, _customEmojiId));
        }

        public string SelectedName => NameLabel.Text;
        public ForumTopicIcon SelectedIcon => new(ForumTopicCell.ServerSupportedColors[_colorIndex % ForumTopicCell.ServerSupportedColors.Length].ToValue(), _customEmojiId);

        private void OnItemClick(object sender, EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is StickerViewModel sticker && sticker.FullType is StickerFullTypeCustomEmoji customEmoji)
            {
                _customEmojiId = customEmoji.CustomEmojiId;
            }
            else
            {
                _customEmojiId = 0;
            }

            UpdateTopicIcon();
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrEmpty(NameLabel.Text))
            {
                VisualUtilities.ShakeView(NameLabel);
                NameLabel.Focus(FocusState.Keyboard);
                args.Cancel = true;
            }
        }

        private void NameLabel_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateTopicIcon();
        }

        private void Color_Click(object sender, RoutedEventArgs e)
        {
            if (_customEmojiId == 0 && _creating)
            {
                _colorIndex++;
                UpdateTopicIcon();
            }
        }
    }
}
