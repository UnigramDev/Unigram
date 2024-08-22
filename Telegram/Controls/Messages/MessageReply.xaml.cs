//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Numerics;
using System.Text;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public class MessageReplyPattern : Control
    {
        private static readonly Vector4[] _clones = new[]
        {
            new Vector4(9, 5, 20, 0.1f),
            new Vector4(41, 12, 19, 0.2f),
            new Vector4(64, 0, 13, 0.2f),
            new Vector4(77, 18, 15, 0.3f),
            new Vector4(103, 7, 19, 0.4f),
            new Vector4(23, 33, 13, 0.2f),
            new Vector4(58, 37, 19, 0.3f),
            new Vector4(99, 34, 15, 0.4f),
        };

        public MessageReplyPattern()
        {
            DefaultStyleKey = typeof(MessageReplyPattern);
        }

        protected override void OnApplyTemplate()
        {
            var animated = GetTemplateChild("Animated") as AnimatedImage;
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;

            var visual = ElementComposition.GetElementVisual(animated);
            var compositor = visual.Compositor;

            // Create a VisualSurface positioned at the same location as this control and feed that
            // through the color effect.
            var surfaceBrush = compositor.CreateSurfaceBrush();
            var surface = compositor.CreateVisualSurface();

            // Select the source visual and the offset/size of this control in that element's space.
            surface.SourceVisual = visual;
            surface.SourceOffset = new Vector2(0, 0);
            surface.SourceSize = new Vector2(21, 21);
            surfaceBrush.HorizontalAlignmentRatio = 0.5f;
            surfaceBrush.VerticalAlignmentRatio = 0.5f;
            surfaceBrush.Surface = surface;
            surfaceBrush.Stretch = CompositionStretch.Fill;
            surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.NearestNeighbor;
            surfaceBrush.SnapToPixels = true;

            var container = compositor.CreateContainerVisual();
            container.Size = new Vector2(122);

            for (int i = 1; i < _clones.Length; i++)
            {
                Vector4 clone = _clones[i];

                var redirect = compositor.CreateSpriteVisual();
                redirect.Size = new Vector2(clone.Z);
                redirect.Offset = new Vector3(clone.X, clone.Y, 0);
                redirect.Opacity = clone.W;
                redirect.Brush = surfaceBrush;

                container.Children.InsertAtTop(redirect);
            }

            ElementCompositionPreview.SetElementChildVisual(layoutRoot, container);
        }

        #region Source

        public AnimatedImageSource Source
        {
            get { return (AnimatedImageSource)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(AnimatedImageSource), typeof(MessageReplyPattern), new PropertyMetadata(null));

        #endregion
    }

    public sealed class MessageReply : MessageReferenceBase
    {
        public MessageReply()
        {
            DefaultStyleKey = typeof(MessageReply);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new MessageReplyAutomationPeer(this);
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
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(MessageReply), new PropertyMetadata(null));

        #endregion

        #region SubtleBrush

        public Brush SubtleBrush
        {
            get { return (Brush)GetValue(SubtleBrushProperty); }
            set { SetValue(SubtleBrushProperty, value); }
        }

        public static readonly DependencyProperty SubtleBrushProperty =
            DependencyProperty.Register("SubtleBrush", typeof(Brush), typeof(MessageReply), new PropertyMetadata(null));

        #endregion

        #region InitializeComponent

        private Grid LayoutRoot;
        private Rectangle BackgroundOverlay;
        private RichTextBlock Label;
        private Run TitleLabel;
        private Run ServiceLabel;
        private Span MessageLabel;
        private DashPath AccentDash;
        private MessageReplyPattern Pattern;
        private TextBlock Quote;

        // Lazy loaded
        private Border ThumbRoot;
        private Border ThumbEllipse;
        private ImageBrush ThumbImage;

        protected override void OnApplyTemplate()
        {
            LayoutRoot = GetTemplateChild(nameof(LayoutRoot)) as Grid;
            BackgroundOverlay = GetTemplateChild(nameof(BackgroundOverlay)) as Rectangle;
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            Pattern = GetTemplateChild(nameof(Pattern)) as MessageReplyPattern;
            Quote = GetTemplateChild(nameof(Quote)) as TextBlock;

            BackgroundOverlay.Margin = new Thickness(0, 0, -Padding.Right, 0);

            _templateApplied = true;

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

        private bool _quote;

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

        protected override void SetText(IClientService clientService, bool outgoing, MessageSender messageSender, string title, string service, FormattedText text, bool quote, bool white)
        {
            if (TitleLabel != null)
            {
                TitleLabel.Text = title ?? string.Empty;
                ServiceLabel.Text = service ?? string.Empty;

                if (!string.IsNullOrEmpty(text?.Text) && !string.IsNullOrEmpty(service))
                {
                    ServiceLabel.Text += ", ";
                }

                _quote = quote;
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

                if (white && !_light)
                {
                    Foreground =
                        SubtleBrush =
                        HeaderBrush =
                        BorderBrush = new SolidColorBrush(Colors.White);

                    AccentDash.Stripe1 = null;
                    AccentDash.Stripe2 = null;

                    Margin = new Thickness(-8, -2, -8, -4);
                }
                else if ((_accent != accent || _light) && !white)
                {
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

                    Margin = new Thickness(0, 4, 0, 4);
                }

                if (customEmojiId != 0)
                {
                    Pattern.Source = new CustomEmojiFileSource(clientService, customEmojiId);
                }
                else
                {
                    Pattern.Source = null;
                }

                _accent = white ? null : accent;
                _light = white;

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
                            player.LoopCount = 0;
                            player.Source = new CustomEmojiFileSource(clientService, customEmoji.CustomEmojiId);
                            player.Style = BootStrapper.Current.Resources["MessageCustomEmojiStyle"] as Style;

                            var inline = new InlineUIContainer();
                            inline.Child = new CustomEmojiContainer(Label, player, 14);

                            MessageLabel.Inlines.Add(inline);
                            MessageLabel.Inlines.Add(Icons.ZWNJ);

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
            if (ContentWidth > 0 && ContentWidth <= availableSize.Width && !_quote)
            {
                LayoutRoot.Measure(new Size(Math.Max(144, ContentWidth), availableSize.Height));
                return LayoutRoot.DesiredSize;
            }

            return base.MeasureOverride(availableSize);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            if (ContentWidth > 0 && ContentWidth <= finalSize.Width && !_quote)
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

    public class MessageReplyAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private readonly MessageReply _owner;

        public MessageReplyAutomationPeer(MessageReply owner)
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
