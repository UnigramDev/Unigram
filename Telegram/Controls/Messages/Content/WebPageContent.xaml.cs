//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Text;
using System.Threading;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Services;
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
    public sealed partial class WebPageContent : HyperlinkButton, IContentWithPlayback
    {
        private CancellationTokenSource _instantViewToken;

        private MessageViewModel _message;
        public MessageViewModel Message => _message;

        public WebPageContent(MessageViewModel message)
        {
            _message = message;

            DefaultStyleKey = typeof(WebPageContent);
        }

        public WebPageContent()
        {
            DefaultStyleKey = typeof(WebPageContent);
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new WebPageContentAutomationPeer(this);
        }

        public string GetAutomationName()
        {
            var linkPreview = GetContent(_message)?.LinkPreview;
            if (linkPreview == null)
            {
                return Strings.AccDescrLinkPreview;
            }

            var builder = new StringBuilder();

            if (linkPreview.Type is LinkPreviewTypeBackground)
            {
                builder.Append(Strings.AppName + ", ");
                builder.Append(Strings.ChatBackground);
            }
            else if (linkPreview.Type is LinkPreviewTypeUpgradedGift upgradedGift)
            {
                builder.Append(Strings.AppName + ", ");
                builder.Append(upgradedGift.Gift.ToName());
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(linkPreview.SiteName))
                {
                    builder.Append(linkPreview.SiteName);
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Title))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(linkPreview.Title);
                }
                else if (!string.IsNullOrWhiteSpace(linkPreview.Author))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(linkPreview.Author);
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Description?.Text))
                {
                    if (builder.Length > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(linkPreview.Description.Text);
                    ContentLabel.Visibility = Visibility.Visible;
                }
            }

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
            _instantViewToken?.Cancel();
            _instantViewToken = new CancellationTokenSource();

            _message = message;

            var text = GetContent(message);
            if (text == null || !_templateApplied)
            {
                return;
            }

            var linkPreview = text.LinkPreview;
            if (linkPreview == null)
            {
                return;
            }

            UpdateWebPage(message.ClientService, linkPreview);
            UpdateInstantView(linkPreview);

            if (linkPreview.HasMedia())
            {
                if (linkPreview.ShowLargeMedia || !linkPreview.CanBeSmall() || !linkPreview.HasThumbnail())
                {
                    MediaPanel.Width = double.NaN;
                    MediaPanel.Height = double.NaN;
                    MediaPanel.Margin = new Thickness(0, 0, 0, 8);

                    Grid.SetRow(MediaPanel, 2);
                    Grid.SetColumn(MediaPanel, 0);

                    OverflowArea.Margin = new Thickness(0, 0, 0, 8);
                    ButtonLine.Margin = new Thickness(0);

                    UpdateContent(message, linkPreview, false);
                    UpdateInstantView(linkPreview, _instantViewToken.Token);
                }
                else
                {
                    MediaPanel.Width = 44;
                    MediaPanel.Height = 44;
                    MediaPanel.Margin = new Thickness(8, 8, 0, 0);

                    Grid.SetRow(MediaPanel, 0);
                    Grid.SetColumn(MediaPanel, 1);

                    OverflowArea.Margin = new Thickness(0, 0, 0, 4);
                    ButtonLine.Margin = new Thickness(0, 4, 0, 0);

                    UpdateContent(message, linkPreview, true);
                }
            }
            else
            {
                OverflowArea.Margin = new Thickness(0, 0, 0, 4);
                ButtonLine.Margin = new Thickness(0, 4, 0, 0);

                MediaPanel.Visibility = Visibility.Collapsed;
                Media.Child = null;
            }

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

        private void UpdateContent(MessageViewModel message, LinkPreview linkPreview, bool small)
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
            if (small)
            {
                maxWidth = 44;
            }
            else
            {
                maxWidth = (double)BootStrapper.Current.Resources["MessageMaxWidth"];
                maxWidth -= 10 + 8 + 2 + 10;
            }

            if (small)
            {
                if (linkPreview.Type is LinkPreviewTypeStickerSet or LinkPreviewTypeGiftCollection)
                {
                    Media.Child = new StickerSetContent(message);
                }
                else
                {
                    Media.Child = new ThumbnailContent(message);
                }
            }
            else if (linkPreview.Type is LinkPreviewTypeAlbum album)
            {
                if (album.Media[0] is LinkPreviewAlbumMediaPhoto)
                {
                    Media.Child = new PhotoContent(message);
                }
                else
                {
                    Media.Child = new VideoContent(message);
                }
            }
            else if (linkPreview.Type is LinkPreviewTypeBackground)
            {
                Media.Child = new WallpaperContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeAnimation)
            {
                Media.Child = new AnimationContent(message)
                {
                    MaxWidth = maxWidth,
                };
            }
            else if (linkPreview.Type is LinkPreviewTypeAudio)
            {
                Media.Child = new AudioContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeDocument)
            {
                Media.Child = new DocumentContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeSticker)
            {
                Media.Child = new StickerContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeVideo)
            {
                Media.Child = new VideoContent(message)
                {
                    MaxWidth = maxWidth
                };
            }
            else if (linkPreview.Type is LinkPreviewTypeVideoNote)
            {
                Media.Child = new VideoNoteContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeVoiceNote)
            {
                Media.Child = new VoiceNoteContent(message);
            }
            else if (linkPreview.Type is LinkPreviewTypeUpgradedGift upgradedGift)
            {
                Media.Child = new WebPageUpgradedGiftContent(message, upgradedGift);
            }
            else if (linkPreview.Type is LinkPreviewTypeStoryAlbum storyAlbum)
            {
                if (storyAlbum.VideoIcon != null)
                {
                    Media.Child = new VideoContent(message);
                }
                else if (storyAlbum.PhotoIcon != null)
                {
                    Media.Child = new PhotoContent(message);
                }
            }
            else if (linkPreview.Type is LinkPreviewTypePhoto or
                                         LinkPreviewTypeEmbeddedAudioPlayer or
                                         LinkPreviewTypeEmbeddedAnimationPlayer or
                                         LinkPreviewTypeEmbeddedVideoPlayer or
                                         LinkPreviewTypeApp or
                                         LinkPreviewTypeArticle or
                                         LinkPreviewTypeChannelBoost or
                                         LinkPreviewTypeChat or
                                         LinkPreviewTypeSupergroupBoost or
                                         LinkPreviewTypeUser or
                                         LinkPreviewTypeVideoChat or
                                         LinkPreviewTypeWebApp)
            {
                // Photo at last: web page preview might have both a file and a thumbnail
                Media.Child = new PhotoContent(message)
                {
                    MaxWidth = maxWidth,
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
            return content is MessageText text && text.LinkPreview != null;
        }

        private MessageText GetContent(MessageViewModel message)
        {
            var content = message?.GeneratedContent ?? message?.Content;
            if (content is MessageText text)
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
            _message?.Delegate?.OpenWebPage(_message);

        }

        public void Mockup(IClientService clientService, LinkPreview linkPreview)
        {
            UpdateWebPage(clientService, linkPreview);

            MediaPanel.Visibility = Visibility.Collapsed;
            OverflowArea.Margin = new Thickness(0);

            ButtonLine.Visibility = Visibility.Collapsed;
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

        private void UpdateWebPage(IClientService clientService, LinkPreview linkPreview)
        {
            var empty = true;

            if (linkPreview.Type is LinkPreviewTypeBackground)
            {
                empty = false;
                TitleLabel.Text = Strings.AppName;
                SubtitleLabel.Text = Strings.ChatBackground;
                ContentLabel.SetText(clientService, string.Empty.AsFormattedText());
            }
            else if (linkPreview.Type is LinkPreviewTypeUpgradedGift upgradedGift)
            {
                empty = false;
                TitleLabel.Text = Strings.AppName;
                SubtitleLabel.Text = upgradedGift.Gift.ToName();
                ContentLabel.SetText(clientService, string.Empty.AsFormattedText());
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(linkPreview.SiteName))
                {
                    empty = false;
                    TitleLabel.Text = linkPreview.SiteName;
                    TitleLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    TitleLabel.Text = string.Empty;
                    TitleLabel.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Title))
                {
                    empty = false;
                    SubtitleLabel.Text = linkPreview.Title;
                    SubtitleLabel.Visibility = Visibility.Visible;
                }
                else if (!string.IsNullOrWhiteSpace(linkPreview.Author))
                {
                    empty = false;
                    SubtitleLabel.Text = linkPreview.Author;
                    SubtitleLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    SubtitleLabel.Text = string.Empty;
                    SubtitleLabel.Visibility = Visibility.Collapsed;
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Description?.Text))
                {
                    empty = false;
                    ContentLabel.SetText(clientService, linkPreview.Description);
                    ContentLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    ContentLabel.SetText(clientService, string.Empty.AsFormattedText());
                    ContentLabel.Visibility = Visibility.Collapsed;
                }
            }

            Label.Visibility = empty
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private void UpdateInstantView(LinkPreview linkPreview)
        {
            if (linkPreview.InstantViewVersion != 0)
            {
                if (linkPreview.Type is LinkPreviewTypeAlbum)
                {
                    ButtonLine.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ShowButton(Strings.InstantView, "\uE60E");
                }
            }
            else if (linkPreview.Type is LinkPreviewTypeChat typeChat)
            {
                if (typeChat.CreatesJoinRequest)
                {
                    ShowButton(Strings.RequestToJoin);
                }
                else if (typeChat.Type is InviteLinkChatTypeSupergroup)
                {
                    ShowButton(Strings.OpenGroup);
                }
                else if (typeChat.Type is InviteLinkChatTypeBasicGroup)
                {
                    ShowButton(Strings.OpenGroup);
                }
                else if (typeChat.Type is InviteLinkChatTypeChannel)
                {
                    ShowButton(Strings.OpenChannel);
                }
            }
            else if (linkPreview.Type is LinkPreviewTypeMessage)
            {
                ShowButton(Strings.OpenMessage);
            }
            else if (linkPreview.Type is LinkPreviewTypeVideoChat videoChat)
            {
                ShowButton(videoChat.JoinsAsSpeaker ? Strings.VoipGroupJoinAsSpeaker : Strings.VoipGroupJoinAsLinstener);
            }
            else if (linkPreview.Type is LinkPreviewTypeBackground)
            {
                ShowButton(Strings.OpenBackground);
            }
            else if (linkPreview.Type is LinkPreviewTypeShareableChatFolder)
            {
                ShowButton(Strings.ViewChatList);
            }
            else if (linkPreview.Type is LinkPreviewTypeUser)
            {
                ShowButton(Strings.SendMessage);
            }
            else if (linkPreview.Type is LinkPreviewTypeWebApp)
            {
                ShowButton(Strings.BotWebAppInstantViewOpen);
            }
            else if (linkPreview.Type is LinkPreviewTypeChannelBoost or LinkPreviewTypeSupergroupBoost)
            {
                ShowButton(Strings.BoostLinkButton);
            }
            else if (linkPreview.Type is LinkPreviewTypeStickerSet stickerSet)
            {
                if (stickerSet.Stickers.Count > 0 && stickerSet.Stickers[0].FullType is StickerFullTypeCustomEmoji)
                {
                    ShowButton(Strings.OpenEmojiSet);
                }
                else
                {
                    ShowButton(Strings.OpenStickerSet);
                }
            }
            else if (linkPreview.Type is LinkPreviewTypeGroupCall)
            {
                ShowButton(Strings.JoinCall);
            }
            else if (linkPreview.Type is LinkPreviewTypeUpgradedGift)
            {
                ShowButton(Strings.OpenUniqueGift);
            }
            else if (linkPreview.Type is LinkPreviewTypeDirectMessagesChat)
            {
                ShowButton(Strings.OpenChannelDirect);
            }
            else if (linkPreview.Type is LinkPreviewTypeGiftCollection)
            {
                ShowButton(Strings.ViewCollection);
            }
            else if (linkPreview.Type is LinkPreviewTypeStoryAlbum)
            {
                ShowButton(Strings.ViewAlbum);
            }
            else
            {
                ButtonLine.Visibility = Visibility.Collapsed;
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

        private async void UpdateInstantView(LinkPreview linkPreview, CancellationToken token)
        {
            if (linkPreview.Type is LinkPreviewTypeAlbum)
            {
                var response = await _message.ClientService.SendAsync(new GetWebPageInstantView(linkPreview.Url, true));
                if (response is WebPageInstantView instantView && instantView.IsFull && !token.IsCancellationRequested)
                {
                    var count = CountWebPageMedia(instantView);

                    Overlay.Visibility = Visibility.Visible;
                    Subtitle.Text = string.Format(Strings.Of, 1, count);
                }
                else
                {
                    Overlay.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                Overlay.Visibility = Visibility.Collapsed;
            }
        }

        private static int CountBlock(WebPageInstantView linkPreview, PageBlock pageBlock, int count)
        {
            if (pageBlock is PageBlockPhoto)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockVideo)
            {
                return count + 1;
            }
            else if (pageBlock is PageBlockAnimation)
            {
                return count + 1;
            }

            return count;
        }

        public static int CountWebPageMedia(WebPageInstantView linkPreview)
        {
            var result = 0;

            foreach (var block in linkPreview.PageBlocks)
            {
                if (block is PageBlockSlideshow slideshow)
                {
                    foreach (var item in slideshow.PageBlocks)
                    {
                        result = CountBlock(linkPreview, item, result);
                    }
                }
                else if (block is PageBlockCollage collage)
                {
                    foreach (var item in collage.PageBlocks)
                    {
                        result = CountBlock(linkPreview, item, result);
                    }
                }
            }

            return result;
        }

        #region HeaderBrush

        public Brush HeaderBrush
        {
            get { return (Brush)GetValue(HeaderBrushProperty); }
            set { SetValue(HeaderBrushProperty, value); }
        }

        public static readonly DependencyProperty HeaderBrushProperty =
            DependencyProperty.Register("HeaderBrush", typeof(Brush), typeof(WebPageContent), new PropertyMetadata(null));

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

    public partial class WebPageContentAutomationPeer : HyperlinkButtonAutomationPeer
    {
        private WebPageContent _owner;

        public WebPageContentAutomationPeer(WebPageContent owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName();
        }
    }

    public partial class WebPageUpgradedGiftContent : Grid
    {
        public WebPageUpgradedGiftContent(MessageViewModel message, LinkPreviewTypeUpgradedGift upgradedGift)
        {
            var pattern = new PatternBackground
            {
                Content = new AnimatedImage
                {
                    Width = 120,
                    Height = 120,
                    FrameSize = new Windows.Foundation.Size(120, 120),
                    DecodeFrameType = Windows.UI.Xaml.Media.Imaging.DecodePixelType.Logical,
                    IsViewportAware = true,
                    LoopCount = 1,
                    Source = DelayedFileSource.FromSticker(message.ClientService, upgradedGift.Gift.Model.Sticker),
                    Padding = new Thickness(12, 2, 12, 0),
                    Margin = new Thickness(0, 0, 0, 12)
                },
                Width = 208
            };

            pattern.Update(message.ClientService, upgradedGift.Gift);

            Children.Add(pattern);
        }
    }
}
