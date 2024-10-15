//
// Copyright Fela Ameghino 2015-2024
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
using Windows.UI.Xaml.Documents;
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
            if (!_templateApplied)
            {
                return Strings.AccDescrLinkPreview;
            }

            var builder = new StringBuilder();
            builder.Append(TitleLabel.Text);
            builder.Prepend(SubtitleLabel.Text, ", ");
            builder.Prepend(ContentLabel.Text, ", ");

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
        private RichTextBlock Label;
        private RichTextBlockOverflow OverflowArea;
        private Run TitleLabel;
        private Run SubtitleLabel;
        private Run ContentLabel;
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
            Label = GetTemplateChild(nameof(Label)) as RichTextBlock;
            OverflowArea = GetTemplateChild(nameof(OverflowArea)) as RichTextBlockOverflow;
            TitleLabel = GetTemplateChild(nameof(TitleLabel)) as Run;
            SubtitleLabel = GetTemplateChild(nameof(SubtitleLabel)) as Run;
            ContentLabel = GetTemplateChild(nameof(ContentLabel)) as Run;
            MediaPanel = GetTemplateChild(nameof(MediaPanel)) as Grid;
            Media = GetTemplateChild(nameof(Media)) as Border;
            Overlay = GetTemplateChild(nameof(Overlay)) as Border;
            Subtitle = GetTemplateChild(nameof(Subtitle)) as TextBlock;
            ButtonLine = GetTemplateChild(nameof(ButtonLine)) as Grid;
            Button = GetTemplateChild(nameof(Button)) as TextBlock;

            Label.OverflowContentTarget = OverflowArea;
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

            UpdateWebPage(linkPreview);
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
                    ButtonLine.Margin = new Thickness(0, 0, 0, 0);

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

            var sender = message.GetSender();
            var accent = outgoing ? null : sender switch
            {
                User user => message.ClientService.GetAccentColor(user.AccentColorId),
                Chat chat => message.ClientService.GetAccentColor(chat.AccentColorId),
                _ => null
            };

            var customEmojiId = sender switch
            {
                User user2 => user2.BackgroundCustomEmojiId,
                Chat chat2 => chat2.BackgroundCustomEmojiId,
                _ => 0
            };

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

            if (customEmojiId != 0)
            {
                Pattern.Source = new CustomEmojiFileSource(message.ClientService, customEmojiId);
            }
            else
            {
                Pattern.Source = null;
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
                if (linkPreview.Type is LinkPreviewTypeStickerSet)
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
            var text = GetContent(_message);
            if (text?.LinkPreview != null)
            {
                _message?.Delegate?.OpenWebPage(text);
            }

        }

        public void Mockup(LinkPreview linkPreview)
        {
            UpdateWebPage(linkPreview);

            MediaPanel.Visibility = Visibility.Collapsed;
            OverflowArea.Margin = new Thickness(0, 0, 0, 0);

            ButtonLine.Visibility = Visibility.Collapsed;
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

        private void UpdateWebPage(LinkPreview linkPreview)
        {
            var empty = true;

            if (linkPreview.Type is LinkPreviewTypeBackground)
            {
                empty = false;
                TitleLabel.Text = Strings.ChatBackground;
                SubtitleLabel.Text = string.Empty;
                ContentLabel.Text = string.Empty;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(linkPreview.SiteName))
                {
                    empty = false;
                    TitleLabel.Text = linkPreview.SiteName;
                }
                else
                {
                    TitleLabel.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Title))
                {
                    if (TitleLabel.Text.Length > 0)
                    {
                        SubtitleLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    SubtitleLabel.Text += linkPreview.Title;
                }
                else if (!string.IsNullOrWhiteSpace(linkPreview.Author))
                {
                    if (TitleLabel.Text.Length > 0)
                    {
                        SubtitleLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    SubtitleLabel.Text += linkPreview.Author;
                }
                else
                {
                    SubtitleLabel.Text = string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(linkPreview.Description?.Text))
                {
                    if (TitleLabel.Text.Length > 0 || SubtitleLabel.Text.Length > 0)
                    {
                        ContentLabel.Text = Environment.NewLine;
                    }

                    empty = false;
                    ContentLabel.Text += linkPreview.Description.Text;
                }
                else
                {
                    ContentLabel.Text = string.Empty;
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
                ShowButton(videoChat.IsLiveStream ? Strings.VoipGroupJoinAsSpeaker : Strings.VoipGroupJoinAsLinstener);
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
                var response = await _message.ClientService.SendAsync(new GetWebPageInstantView(linkPreview.Url, false));
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
}
