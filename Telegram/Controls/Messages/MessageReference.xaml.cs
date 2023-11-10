//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Text;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Messages
{
    public class MessageReferencePattern : AnimatedImage
    {
        private readonly Image[] _images = new Image[8];

        public MessageReferencePattern()
        {
            DefaultStyleKey = typeof(MessageReferencePattern);
        }

        protected override void OnApplyTemplate()
        {
            for (int i = 0; i < _images.Length; i++)
            {
                _images[i] = GetTemplateChild($"Image{i + 1}") as Image;
            }

            base.OnApplyTemplate();
        }

        public override void Invalidate(ImageSource source)
        {
            for (int i = 0; i < _images.Length; i++)
            {
                _images[i].Source = source;
            }

            if (_clean && source != null)
            {
                _clean = false;

                if (ReplacementColor != null)
                {
                    ReplacementColorChanged(true);
                }
            }
        }
    }

    public sealed class MessageReference : MessageReferenceBase
    {
        public MessageReference()
        {
            DefaultStyleKey = typeof(MessageReference);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageReferenceAutomationPeer(this);
        }

        public string GetNameCore()
        {
            var builder = new StringBuilder();

            if (TitleLabel != null)
            {
                builder.Append(TitleLabel.Text);
                builder.Append(": ");
            }

            if (ServiceLabel != null)
            {
                builder.Append(ServiceLabel.Text);
            }

            if (MessageLabel != null)
            {
                foreach (var entity in MessageLabel.Inlines)
                {
                    if (entity is Run run)
                    {
                        builder.Append(run.Text);
                    }
                }
            }

            return builder.ToString();
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(MessageReference), new PropertyMetadata(null));

        #endregion

        #region SubtleBrush

        public Brush SubtleBrush
        {
            get { return (Brush)GetValue(SubtleBrushProperty); }
            set { SetValue(SubtleBrushProperty, value); }
        }

        public static readonly DependencyProperty SubtleBrushProperty =
            DependencyProperty.Register("SubtleBrush", typeof(Brush), typeof(MessageReference), new PropertyMetadata(null));

        #endregion

        #region InitializeComponent

        private Grid LayoutRoot;
        private RichTextBlock Label;
        private Run TitleLabel;
        private Run ServiceLabel;
        private Span MessageLabel;
        private DashPath AccentDash;
        private TextBlock Quote;

        // Lazy loaded
        private Border ThumbRoot;
        private Border ThumbEllipse;
        private ImageBrush ThumbImage;
        private MessageReferencePattern Pattern;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            Quote = GetTemplateChild(nameof(Quote)) as TextBlock;
            Pattern = GetTemplateChild(nameof(Pattern)) as MessageReferencePattern;

            _templateApplied = true;

            if (_light)
            {
                VisualStateManager.GoToState(this, "LightState", false);
            }

            if (_messageReply != null)
            {
                UpdateMessageReply(_messageReply);
            }
            else if (_message != null)
            {
                UpdateMessage(_message, _loading, _title);
            }
            else if (Message != null)
            {
                OnMessageChanged(Message as MessageComposerHeader);
            }
        }

        #endregion

        private bool _light;
        private NameColor _accent;

        public void ToLightState()
        {
            if (_light is false)
            {
                _light = true;
                VisualStateManager.GoToState(this, "LightState", false);

                Foreground =
                    SubtleBrush =
                    HeaderBrush =
                    BorderBrush = new SolidColorBrush(Colors.White);

                if (AccentDash != null)
                {
                    AccentDash.Stripe1 = null;
                    AccentDash.Stripe2 = null;
                }

                Margin = new Thickness(-8, -6, -8, -6);
            }
        }

        public void ToNormalState()
        {
            if (_light)
            {
                _light = false;
                VisualStateManager.GoToState(this, "NormalState", false);

                ClearValue(ForegroundProperty);
                ClearValue(SubtleBrushProperty);

                var accent = _accent;
                if (accent != null)
                {
                    HeaderBrush =
                        BorderBrush = new SolidColorBrush(accent.LightThemeColors[0]);

                    AccentDash.Stripe1 = accent.LightThemeColors.Count > 1
                        ? new SolidColorBrush(accent.LightThemeColors[1])
                        : null;
                    AccentDash.Stripe2 = accent.LightThemeColors.Count > 2
                        ? new SolidColorBrush(accent.LightThemeColors[2])
                        : null;
                }
                else
                {
                    ClearValue(HeaderBrushProperty);
                    ClearValue(BorderBrushProperty);

                    AccentDash.Stripe1 = null;
                    AccentDash.Stripe2 = null;
                }

                Margin = new Thickness(0);
            }
        }

        #region Overrides

        private static readonly CornerRadius _defaultRadius = new(2);

        protected override void HideThumbnail()
        {
            if (ThumbRoot != null)
            {
                ThumbRoot.Visibility = Visibility.Collapsed;
            }
        }

        protected override void ShowThumbnail(CornerRadius radius = default)
        {
            if (ThumbRoot == null)
            {
                ThumbRoot = GetTemplateChild(nameof(ThumbRoot)) as Border;
                ThumbEllipse = GetTemplateChild(nameof(ThumbEllipse)) as Border;
                ThumbImage = GetTemplateChild(nameof(ThumbImage)) as ImageBrush;
            }

            ThumbRoot.Visibility = Visibility.Visible;
            ThumbRoot.CornerRadius =
                ThumbEllipse.CornerRadius = radius == default ? _defaultRadius : radius;
        }

        protected override void SetThumbnail(ImageSource value)
        {
            if (ThumbImage != null)
            {
                ThumbImage.ImageSource = value;
            }
        }

        protected override void SetText(IClientService clientService, bool outgoing, MessageSender messageSender, string title, string service, FormattedText text, bool quote)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title ?? string.Empty;
                ServiceLabel.Text = service ?? string.Empty;

                if (!string.IsNullOrEmpty(text?.Text) && !string.IsNullOrEmpty(service))
                {
                    ServiceLabel.Text += ", ";
                }

                Quote.Visibility = quote
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                var sender = clientService?.GetMessageSender(messageSender);
                var accent = outgoing ? null : sender switch
                {
                    User user => clientService.GetAccentColor(user.AccentColorId),
                    Chat chat => clientService.GetAccentColor(chat.AccentColorId),
                    _ => null
                };

                var customEmojiId = sender switch
                {
                    User user2 => user2.BackgroundCustomEmojiId,
                    Chat chat2 => chat2.BackgroundCustomEmojiId,
                    _ => 0
                };

                if (customEmojiId != 0)
                {
                    Pattern.Source = new CustomEmojiFileSource(clientService, customEmojiId);
                }
                else
                {
                    Pattern.Source = null;
                }

                if (_light)
                {
                    VisualStateManager.GoToState(this, "LightState", false);

                    Foreground =
                        SubtleBrush =
                        HeaderBrush =
                        BorderBrush = new SolidColorBrush(Colors.White);

                    AccentDash.Stripe1 = null;
                    AccentDash.Stripe2 = null;

                    Margin = new Thickness(-8, -6, -8, -6);
                }
                else
                {
                    VisualStateManager.GoToState(this, "NormalState", false);

                    ClearValue(ForegroundProperty);
                    ClearValue(SubtleBrushProperty);

                    if (accent != null)
                    {
                        HeaderBrush =
                            BorderBrush = new SolidColorBrush(accent.LightThemeColors[0]);

                        AccentDash.Stripe1 = accent.LightThemeColors.Count > 1
                            ? new SolidColorBrush(accent.LightThemeColors[1])
                            : null;
                        AccentDash.Stripe2 = accent.LightThemeColors.Count > 2
                            ? new SolidColorBrush(accent.LightThemeColors[2])
                            : null;
                    }
                    else
                    {
                        ClearValue(HeaderBrushProperty);
                        ClearValue(BorderBrushProperty);

                        AccentDash.Stripe1 = null;
                        AccentDash.Stripe2 = null;
                    }

                    Margin = new Thickness(0);
                }

                _accent = accent;

                MessageLabel.Inlines.Clear();

                if (text != null)
                {
                    var clean = text.ReplaceSpoilers();
                    var previous = 0;

                    if (text.Entities != null)
                    {
                        foreach (var entity in clean.Entities)
                        {
                            if (entity.Type is not TextEntityTypeCustomEmoji customEmoji)
                            {
                                continue;
                            }

                            if (entity.Offset > previous)
                            {
                                MessageLabel.Inlines.Add(new Run { Text = clean.Text.Substring(previous, entity.Offset - previous) });
                            }

                            var player = new CustomEmojiIcon();
                            player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                            player.Style = BootStrapper.Current.Resources["MessageCustomEmojiStyle"] as Style;

                            var inline = new InlineUIContainer();
                            inline.Child = new CustomEmojiContainer(Label, player, 14);

                            MessageLabel.Inlines.Add(inline);
                            MessageLabel.Inlines.Add(new Run { Text = "\u200D" });

                            previous = entity.Offset + entity.Length;
                        }
                    }

                    if (clean.Text.Length > previous)
                    {
                        MessageLabel.Inlines.Add(new Run { Text = clean.Text.Substring(previous) });
                    }
                }
            }
        }

        #endregion

        public double ContentWidth { get; set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            if (ContentWidth > 0 && ContentWidth <= availableSize.Width)
            {
                LayoutRoot.Measure(new Size(Math.Max(144, ContentWidth), availableSize.Height));
                return LayoutRoot.DesiredSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (ContentWidth > 0 && ContentWidth <= finalSize.Width)
            {
                LayoutRoot.Arrange(new Rect(0, 0, finalSize.Width, LayoutRoot.DesiredSize.Height));
                return new Size(finalSize.Width, LayoutRoot.DesiredSize.Height);
            }

            return base.ArrangeOverride(finalSize);
        }

        public void UpdateMockup(IClientService clientService, long customEmojiId, int color)
        {
            if (Pattern != null)
            {
                Pattern.Source = new CustomEmojiFileSource(clientService, customEmojiId);
            }

            var accent = clientService.GetAccentColor(color);

            HeaderBrush =
                BorderBrush = new SolidColorBrush(accent.LightThemeColors[0]);

            if (AccentDash != null)
            {
                AccentDash.Stripe1 = accent.LightThemeColors.Count > 1
                    ? new SolidColorBrush(accent.LightThemeColors[1])
                    : null;
                AccentDash.Stripe2 = accent.LightThemeColors.Count > 2
                    ? new SolidColorBrush(accent.LightThemeColors[2])
                    : null;
            }
        }
    }

    public class MessageReferenceAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly MessageReference _owner;

        public MessageReferenceAutomationPeer(MessageReference owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetNameCore();
        }
    }
}
