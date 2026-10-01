//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Telegram.Common;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class GameContent : HyperlinkButton, IContent, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public GameContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(GameContent);
        }

        #region InitializeComponent

        private DashPath AccentDash;
        private TextBlock TitleLabel;
        private FormattedTextBlock DescriptionLabel;
        private Border Media;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            DescriptionLabel = GetTemplateChild(nameof(DescriptionLabel)) as FormattedTextBlock;
            Media = GetTemplateChild(nameof(Media)) as Border;

            Click += Button_Click;

            _templateApplied = true;

            if (_message != null)
            {
                UpdateMessage(_message);
            }
        }

        #endregion

        public void UpdateMessage(MessageViewModel message)
        {
            _message = message;

            var game = message.Content as MessageGame;
            if (game == null || !_templateApplied)
            {
                return;
            }

            TitleLabel.Text = game.Game.Title;

            if (game.Game.Text == null || string.IsNullOrEmpty(game.Game.Text.Text))
            {
                DescriptionLabel.SetText(message.ClientService, game.Game.Description.AsFormattedText());
            }
            else
            {
                DescriptionLabel.SetText(message.ClientService, game.Game.Text);
            }

            UpdateContent(message, game.Game);

            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            var (accent, giftColors, customEmojiId) = outgoing ? (null, null, 0) : message.GetSender() switch
            {
                User user => (message.ClientService.GetAccentColor(user.AccentColorId), user.UpgradedGiftColors, user.BackgroundCustomEmojiId),
                Chat chat => (message.ClientService.GetAccentColor(chat.AccentColorId), chat.UpgradedGiftColors, chat.BackgroundCustomEmojiId),
                _ => (null, null, 0)
            };

            if (giftColors != null)
            {
                Background =
                    HeaderBrush = new SolidColorBrush(giftColors.LightThemeAccentColor.ToColor());

                BorderBrush = new SolidColorBrush(giftColors.LightThemeColors[0].ToColor());

                AccentDash.Stripe1 = giftColors.LightThemeColors.Count > 1
                    ? giftColors.LightThemeColors[1].ToColor()
                    : default;
                AccentDash.Stripe2 = giftColors.LightThemeColors.Count > 2
                    ? giftColors.LightThemeColors[2].ToColor()
                    : default;
            }
            else if (accent != null)
            {
                Background =
                    HeaderBrush =
                    BorderBrush = new SolidColorBrush(accent.LightThemeColors[0]);

                AccentDash.Stripe1 = accent.LightThemeColors.Count > 1
                    ? accent.LightThemeColors[1]
                    : default;
                AccentDash.Stripe2 = accent.LightThemeColors.Count > 2
                    ? accent.LightThemeColors[2]
                    : default;
            }
            else
            {
                ClearValue(BackgroundProperty);
                ClearValue(HeaderBrushProperty);
                ClearValue(BorderBrushProperty);

                AccentDash.Stripe1 = default;
                AccentDash.Stripe2 = default;
            }
        }

        private void UpdateContent(MessageViewModel message, Game game)
        {
            if (Media.Child is IContent media)
            {
                if (media.IsValid(message.Content, false))
                {
                    media.UpdateMessage(message);
                    return;
                }
                else
                {
                    media.Recycle();
                }
            }

            if (game.Animation != null)
            {
                Media.Child = new AnimationContent(message)
                {
                    IsEnabled = false
                };
            }
            else if (game.Photo != null)
            {
                Media.Child = new PhotoContent(message)
                {
                    IsEnabled = false
                };
            }
            else
            {
                Media.Child = null;
            }
        }

        public void Recycle()
        {
            _message = null;

            if (_templateApplied && Media.Child is IContent content)
            {
                content.Recycle();
            }
        }

        public bool IsValid(MessageContent content, bool primary)
        {
            return content is MessageGame;
        }

        public IPlayerView GetPlaybackElement()
        {
            if (Media?.Child is IContentWithPlayback content)
            {
                return content.GetPlaybackElement();
            }
            else if (Media?.Child is IPlayerView playback)
            {
                return playback;
            }

            return null;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            _message.Delegate.OpenGame(_message);
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(GameContent), new PropertyMetadata(null));

        #endregion
    }
}
