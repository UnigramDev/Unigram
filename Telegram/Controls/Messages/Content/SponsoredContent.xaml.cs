//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages.Content
{
    public sealed partial class SponsoredContent : HyperlinkButton, IContentWithPlayback
    {
        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public SponsoredContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(SponsoredContent);
        }

        public SponsoredContent()
        {
            DefaultStyleKey = typeof(SponsoredContent);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new SponsoredContentAutomationPeer(this);
        }

        public string GetAutomationName()
        {
            if (!_templateApplied)
            {
                return Strings.AccDescrLinkPreview;
            }

            var builder = new StringBuilder();

            var peer = FrameworkElementAutomationPeer.FromElement(Label);
            if (peer == null)
            {
                return builder.ToString();
            }

            builder.Append(peer.GetName());

            if (builder.Length > 0)
            {
                builder.Insert(0, ": ");
            }

            builder.Insert(0, Strings.AccDescrLinkPreview);

            if (ButtonLine.Visibility == Visibility.Visible)
            {
                builder.Prepend(Button.Text, ", ");
            }

            return builder.ToString();
        }

        #region InitializeComponent

        private DashPath AccentDash;
        private MessageReplyPattern Pattern;
        private Grid Label;
        private RichTextBlockOverflow OverflowArea;
        private TextBlock TitleLabel;
        private TextBlock SubtitleLabel;
        private FormattedTextBlock ContentLabel;
        private Grid MediaPanel;
        private Border Media;
        private Border Overlay;
        private TextBlock Subtitle;
        private Grid ButtonLine;
        private TextBlock Button;
        private bool _templateApplied;

        protected override void OnApplyTemplate()
        {
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            Pattern = GetTemplateChild(nameof(Pattern)) as MessageReplyPattern;
            Label = GetTemplateChild(nameof(Label)) as Grid;
            OverflowArea = GetTemplateChild(nameof(OverflowArea)) as RichTextBlockOverflow;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as TextBlock;
            ContentLabel = GetTemplateChild(nameof(ContentLabel)) as FormattedTextBlock;
            MediaPanel = GetTemplateChild(nameof(MediaPanel)) as Grid;
            Media = GetTemplateChild(nameof(Media)) as Border;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            ButtonLine = GetTemplateChild(nameof(ButtonLine)) as Grid;
            Button = GetTemplateChild(nameof(Button)) as TextBlock;

            ContentLabel.OverflowContentTarget = OverflowArea;
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

            var sponsored = GetContent(message);
            if (sponsored == null || !_templateApplied)
            {
                return;
            }

            UpdateWebPage(message, sponsored);
            UpdateInstantView(sponsored);

            if (sponsored.Content is MessageAnimation or MessagePhoto or MessageVideo)
            {
                MediaPanel.Width = double.NaN;
                MediaPanel.Height = double.NaN;
                MediaPanel.Margin = new Thickness(0, 0, 0, 8);

                Grid.SetRow(MediaPanel, 2);
                Grid.SetColumn(MediaPanel, 0);

                OverflowArea.Margin = new Thickness(0, 0, 0, 8);
                ButtonLine.Margin = new Thickness(0);

                UpdateContent(message, sponsored);
            }
            else if (sponsored.Sponsor.Photo != null)
            {
                MediaPanel.Width = 44;
                MediaPanel.Height = 44;
                MediaPanel.Margin = new Thickness(8, 8, 0, 0);

                Grid.SetRow(MediaPanel, 0);
                Grid.SetColumn(MediaPanel, 1);

                OverflowArea.Margin = new Thickness(0, 0, 0, 4);
                ButtonLine.Margin = new Thickness(0, 4, 0, 0);

                UpdateContent(message, sponsored);
            }
            else
            {
                OverflowArea.Margin = new Thickness(0, 0, 0, 4);
                ButtonLine.Margin = new Thickness(0, 4, 0, 0);

                MediaPanel.Visibility = Visibility.Collapsed;
                Media.Child = null;
            }

            var outgoing = message.IsOutgoing && !message.IsChannelPost;
            var accent = message.ClientService.GetAccentColor(sponsored.AccentColorId);
            var giftColors = default(UpgradedGiftColors);
            var customEmojiId = sponsored.BackgroundCustomEmojiId;

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
                ClearValue(HeaderBrushProperty);
                ClearValue(BorderBrushProperty);

                AccentDash.Stripe1 = default;
                AccentDash.Stripe2 = default;
            }

            if (giftColors != null)
            {
                Pattern.Source = new CustomEmojiFileSource(message.ClientService, giftColors.SymbolCustomEmojiId);
                Pattern.Model = new CustomEmojiFileSource(message.ClientService, giftColors.ModelCustomEmojiId);
            }
            else if (customEmojiId != 0)
            {
                Pattern.Source = new CustomEmojiFileSource(message.ClientService, customEmojiId);
                Pattern.Model = null;
            }
            else
            {
                Pattern.Source = null;
                Pattern.Model = null;
            }
        }

        private void UpdateContent(MessageViewModel message, MessageSponsored sponsored)
        {
            MediaPanel.Visibility = Visibility.Visible;

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

            double maxWidth;
            //if (small)
            //{
            //    maxWidth = 44;
            //}
            //else
            //{
            maxWidth = (double)BootStrapper.Current.Resources["MessageMaxWidth"];
            maxWidth -= 10 + 8 + 2 + 10;
            //}

            if (sponsored.Content is MessageAnimation animation)
            {
                Media.Child = new AnimationContent(message)
                {
                    MaxWidth = maxWidth
                };
            }
            else if (sponsored.Content is MessagePhoto photo)
            {
                Media.Child = new PhotoContent(message)
                {
                    MaxWidth = maxWidth
                };
            }
            else if (sponsored.Content is MessageVideo video)
            {
                Media.Child = new VideoContent(message)
                {
                    MaxWidth = maxWidth
                };
            }
            else if (sponsored.Sponsor.Photo != null)
            {
                Media.Child = new ThumbnailContent(message);
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
            return content is MessageText text && text.LinkPreview != null;
        }

        private MessageSponsored GetContent(MessageViewModel message)
        {
            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageSponsored text)
            {
                return text;
            }

            return null;
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
            var content = GetContent(_message);
            if (content != null)
            {
                _message.ClientService.Send(new ClickChatSponsoredMessage(_message.ChatId, _message.Id, false, false));
                _message.Delegate?.OpenUrl(content.Sponsor.Url, false);
            }
        }

        private void UpdateWebPage(MessageViewModel message, MessageSponsored sponsored)
        {
            var empty = false;
            TitleLabel.Text = sponsored.IsRecommended ? Strings.SponsoredMessage2Recommended : Strings.SponsoredMessage2;
            SubtitleLabel.Text = sponsored.Title;

            if (sponsored.Content is MessageText text)
            {
                ContentLabel.SetText(message.ClientService, text.Text);
            }
            else if (sponsored.Content is MessageAnimation animation)
            {
                ContentLabel.SetText(message.ClientService, animation.Caption);
            }
            else if (sponsored.Content is MessagePhoto photo)
            {
                ContentLabel.SetText(message.ClientService, photo.Caption);
            }
            else if (sponsored.Content is MessageVideo video)
            {
                ContentLabel.SetText(message.ClientService, video.Caption);
            }

            Label.Visibility = empty
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void UpdateInstantView(MessageSponsored sponsored)
        {
            if (string.IsNullOrEmpty(sponsored.ButtonText))
            {
                ButtonLine.Visibility = Visibility.Collapsed;
            }
            else
            {
                ShowButton(sponsored.ButtonText);
            }
        }

        private void ShowButton(string text, string glyph = "")
        {
            if (string.IsNullOrEmpty(glyph))
            {
                Button.Text = text.ToUpper();
            }
            else
            {
                Button.Text = $"{glyph}\u2004\u200A{text.ToUpper()}";
            }

            ButtonLine.Visibility = Visibility.Visible;
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(SponsoredContent), new PropertyMetadata(null));

        #endregion

        #region Skeleton

        public void ShowSkeleton()
        {
            if (ActualSize.X == 0 || ActualSize.Y == 0)
            {
                return;
            }

            var compositor = BootStrapper.Current.Compositor;
            var rectangle = compositor.CreateRoundedRectangleGeometry();
            rectangle.Size = new Vector2(ActualSize.X - 2, ActualSize.Y - 2);
            rectangle.Offset = new Vector2(1, 1);
            rectangle.CornerRadius = new Vector2(4);

            var strokeColor = BorderBrush is SolidColorBrush brush ? brush.Color : Colors.White;

            var stroke = compositor.CreateLinearGradientBrush();
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x55, strokeColor.R, strokeColor.G, strokeColor.B)));
            stroke.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));

            var fill = compositor.CreateLinearGradientBrush();
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(0.5f, Color.FromArgb(0x22, strokeColor.R, strokeColor.G, strokeColor.B)));
            fill.ColorStops.Add(compositor.CreateColorGradientStop(1.0f, Color.FromArgb(0x00, strokeColor.R, strokeColor.G, strokeColor.B)));

            var shape = compositor.CreateSpriteShape();
            shape.Geometry = rectangle;
            shape.FillBrush = fill;
            shape.StrokeBrush = stroke;
            shape.StrokeThickness = 1;

            var visual = compositor.CreateShapeVisual();
            visual.Size = new Vector2(ActualSize.X, ActualSize.Y);
            visual.Shapes.Add(shape);

            var endless = compositor.CreateScalarKeyFrameAnimation();
            endless.InsertKeyFrame(0, -ActualSize.X);
            endless.InsertKeyFrame(1, +ActualSize.X);
            endless.IterationBehavior = AnimationIterationBehavior.Forever;
            endless.Duration = TimeSpan.FromMilliseconds(1500);

            stroke.StartAnimation("Offset.X", endless);
            fill.StartAnimation("Offset.X", endless);

            ElementCompositionPreview.SetElementChildVisual(this, visual);
        }

        public void HideSkeleton()
        {
            ElementCompositionPreview.SetElementChildVisual(this, BootStrapper.Current.Compositor.CreateSpriteVisual());
        }

        #endregion
    }

    public partial class SponsoredContentAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private SponsoredContent _owner;

        public SponsoredContentAutomationPeer(SponsoredContent owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName();
        }
    }
}
