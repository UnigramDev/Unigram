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
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Shapes;

namespace Telegram.Controls.Messages
{
    public partial class MessageReplyPattern : Control
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

        private SpriteVisual _modelVisual;
        private AnimatedImage ModelAnimated;

        private bool _templateApplied;

        public MessageReplyPattern()
        {
            DefaultStyleKey = typeof(MessageReplyPattern);
        }

        protected override void OnApplyTemplate()
        {
            // TODO: Name
            var animated = GetTemplateChild("Animated") as AnimatedImage;
            var layoutRoot = GetTemplateChild("LayoutRoot") as Border;

            var hasModel = Model != null;
            if (hasModel)
            {
                ModelAnimated = GetTemplateChild(nameof(ModelAnimated)) as AnimatedImage;
            }

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

                if (i == 4)
                {
                    _modelVisual = redirect;
                    _modelVisual.IsVisible = !hasModel;
                }
            }

            ElementCompositionPreview.SetElementChildVisual(layoutRoot, container);

            _templateApplied = true;
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

        #region Model

        public AnimatedImageSource Model
        {
            get { return (AnimatedImageSource)GetValue(ModelProperty); }
            set { SetValue(ModelProperty, value); }
        }

        public static readonly DependencyProperty ModelProperty =
            DependencyProperty.Register("Model", typeof(AnimatedImageSource), typeof(MessageReplyPattern), new PropertyMetadata(null, OnModelChanged));

        private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MessageReplyPattern)d).OnModelChanged(e.NewValue as AnimatedImageSource);
        }

        private void OnModelChanged(AnimatedImageSource newValue)
        {
            _modelVisual?.IsVisible = newValue == null;

            if (newValue != null && _templateApplied && ModelAnimated == null)
            {
                ModelAnimated = GetTemplateChild(nameof(ModelAnimated)) as AnimatedImage;
            }
        }

        #endregion
    }

    public sealed partial class MessageReply : MessageReferenceBase
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
        private FormattedTextBlock Label;
        private TextBlock TitleLabel;
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
            Label = GetTemplateChild(nameof(Label)) as FormattedTextBlock;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as TextBlock;
            ServiceLabel = GetTemplateChild(nameof(ServiceLabel)) as Run;
            MessageLabel = GetTemplateChild(nameof(MessageLabel)) as Span;
            AccentDash = GetTemplateChild(nameof(AccentDash)) as DashPath;
            Pattern = GetTemplateChild(nameof(Pattern)) as MessageReplyPattern;
            Quote = GetTemplateChild(nameof(Quote)) as TextBlock;

            BindingOperations.SetBinding(ServiceLabel, Run.ForegroundProperty, new Binding
            {
                Path = new PropertyPath("SubtleBrush"),
                Source = this
            });

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
            else if (_composerHeader != null)
            {
                UpdateComposerHeader(_composerHeader);
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
            _thumbnailController?.Recycle();

            ThumbRoot?.Visibility = Visibility.Collapsed;
        }

        protected override ImageBrush ShowThumbnail(CornerRadius radius = default)
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

            return ThumbImage;
        }

        protected override void SetText(MessageViewModel message, bool outgoing, MessageSender messageSender, string title, string service, FormattedText text, bool quote, bool white)
        {
            if (TitleLabel == null)
            {
                return;
            }

            TitleLabel.Text = title ?? string.Empty;
            ServiceLabel.Text = service ?? string.Empty;

            var textz = message?.TranslatedText switch
            {
                MessageTranslateResultText translated => message.Delegate.IsTranslating
                    ? translated.Text
                    : message.Text,
                _ => message?.Text
            };

            if (!string.IsNullOrEmpty(text?.Text ?? textz?.Text) && !string.IsNullOrEmpty(service))
            {
                ServiceLabel.Text += ", ";
            }

            _quote = quote;
            Quote.Visibility = quote
                ? Visibility.Visible
                : Visibility.Collapsed;

            Label.MaxLines = quote ? 5 : 1;

            var (accent, giftColors, customEmojiId) = outgoing ? (null, null, 0) : message?.ClientService.GetMessageSender(messageSender) switch
            {
                User user => (message.ClientService.GetAccentColor(user.AccentColorId), user.UpgradedGiftColors, user.BackgroundCustomEmojiId),
                Chat chat => (message.ClientService.GetAccentColor(chat.AccentColorId), chat.UpgradedGiftColors, chat.BackgroundCustomEmojiId),
                _ => (null, null, 0)
            };

            if (white && !_light)
            {
                Foreground =
                    Background =
                    SubtleBrush =
                    HeaderBrush =
                    BorderBrush = new SolidColorBrush(Colors.White);

                AccentDash.Stripe1 = default;
                AccentDash.Stripe2 = default;

                Margin = new Thickness(-8, -2, -8, -4);
            }
            else if ((_accent != accent || _light) && !white)
            {
                ClearValue(ForegroundProperty);
                ClearValue(SubtleBrushProperty);

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

                Margin = new Thickness(0, 4, 0, 4);
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

            _accent = white ? null : accent;
            _light = white;

            if (text != null)
            {
                Label.SetText(message?.ClientService, text);
            }
            else
            {
                Label.SetText(message?.ClientService, textz);
            }

            Label.SetQuery(string.Empty);
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

        public void UpdateMockup(IClientService clientService, long customEmojiId, int color, UpgradedGiftColors upgradedGift)
        {
            if (Pattern != null)
            {
                if (upgradedGift != null)
                {
                    Pattern.Source = new CustomEmojiFileSource(clientService, upgradedGift.SymbolCustomEmojiId);
                    Pattern.Model = new CustomEmojiFileSource(clientService, upgradedGift.ModelCustomEmojiId);
                }
                else
                {
                    Pattern.Source = new CustomEmojiFileSource(clientService, customEmojiId);
                    Pattern.Model = null;
                }
            }

            if (upgradedGift != null)
            {
                Background =
                    HeaderBrush = new SolidColorBrush(upgradedGift.LightThemeAccentColor.ToColor());

                BorderBrush = new SolidColorBrush(upgradedGift.LightThemeColors[0].ToColor());

                if (AccentDash != null)
                {
                    AccentDash.Stripe1 = upgradedGift.LightThemeColors.Count > 1
                        ? upgradedGift.LightThemeColors[1].ToColor()
                        : default;
                    AccentDash.Stripe2 = upgradedGift.LightThemeColors.Count > 2
                        ? upgradedGift.LightThemeColors[2].ToColor()
                        : default;
                }
            }
            else
            {
                var accent = clientService.GetAccentColor(color);

                Background =
                    HeaderBrush =
                    BorderBrush = new SolidColorBrush(accent.LightThemeColors[0]);

                if (AccentDash != null)
                {
                    AccentDash.Stripe1 = accent.LightThemeColors.Count > 1
                        ? accent.LightThemeColors[1]
                        : default;
                    AccentDash.Stripe2 = accent.LightThemeColors.Count > 2
                        ? accent.LightThemeColors[2]
                        : default;
                }
            }
        }
    }

    public partial class MessageReplyAutomationPeer : HyperlinkButtonAutomationPeer
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
