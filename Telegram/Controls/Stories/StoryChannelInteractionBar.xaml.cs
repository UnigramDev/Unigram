using RLottie;
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Stories
{
    public sealed partial class StoryChannelInteractionBar : UserControl
    {
        private StoryViewModel _viewModel;
        public StoryViewModel ViewModel => _viewModel;

        public StoryChannelInteractionBar()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler ShareClick
        {
            add => ShareButton.Click += value;
            remove => ShareButton.Click -= value;
        }

        public async void Update(StoryViewModel story)
        {
            _viewModel = story;

            if (story.InteractionInfo != null)
            {
                ViewersCount.Text = story.InteractionInfo.ViewCount.ToString("N0");

                ReactionCount.Text = story.InteractionInfo.ReactionCount.ToString("N0");
                ReactionCount.Visibility = story.InteractionInfo.ReactionCount > 0
                        ? Visibility.Visible
                        : Visibility.Collapsed;
            }
            else
            {
                //Viewers.Items.Clear();
                //Viewers.Visibility = Visibility.Collapsed;

                //ViewersCount.Text = Strings.NobodyViews;

                //ReactionCount.Visibility =
                //    ReactionIcon.Visibility = Visibility.Collapsed;
            }

            var defaultReaction = await GetDefaultReactionAsync();
            var reactionType = story.ChosenReactionType ?? new ReactionTypeEmoji("\u2764");

            if (story.ChosenReactionType == null)
            {
                ReactButton2.SetReaction(story, reactionType, defaultReaction, defaultReaction);
            }
            else if (reactionType is ReactionTypeEmoji emoji)
            {
                if (story.ClientService.TryGetCachedReaction(emoji.Emoji, out EmojiReaction reaction))
                {
                    ReactButton2.SetReaction(story, reactionType, reaction, defaultReaction);
                }
                else
                {
                    var response = await story.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                    if (response is EmojiReaction reaction2)
                    {
                        ReactButton2.SetReaction(story, reactionType, reaction2, defaultReaction);
                    }
                }
            }
            else if (reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                if (EmojiCache.TryGet(customEmoji.CustomEmojiId, out Sticker sticker))
                {
                    ReactButton2.SetReaction(story, reactionType, sticker, defaultReaction);
                }
                else
                {
                    var response = await EmojiCache.GetAsync(story.ClientService, customEmoji.CustomEmojiId);
                    if (response is Sticker sticker2)
                    {
                        ReactButton2.SetReaction(story, reactionType, sticker2, defaultReaction);
                    }
                }
            }
        }

        private async Task<EmojiReaction> GetDefaultReactionAsync()
        {
            if (ViewModel.ClientService.TryGetCachedReaction("\u2764\uFE0F", out EmojiReaction reaction))
            {
                return reaction;
            }
            else
            {
                var response = await ViewModel.ClientService.SendAsync(new GetEmojiReaction("\u2764"));
                if (response is EmojiReaction reaction2)
                {
                    return reaction2;
                }
            }

            return null;
        }

        private void Viewers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            if (ViewModel.ClientService.TryGetUser(messageSender, out User user))
            {
                sender.SetUser(ViewModel.ClientService, user, 28);
            }
            else if (ViewModel.ClientService.TryGetChat(messageSender, out Chat chat))
            {
                sender.SetChat(ViewModel.ClientService, chat, 28);
            }
        }

        private void ReactButton_Click(object sender, RoutedEventArgs e)
        {
            var story = ViewModel;
            if (story == null)
            {
                return;
            }

            if (story.ChosenReactionType == null)
            {
                story.ClientService.Send(new SetStoryReaction(story.ChatId, story.StoryId, new ReactionTypeEmoji("\u2764\uFE0F"), false));
            }
            else
            {
                story.ClientService.Send(new SetStoryReaction(story.ChatId, story.StoryId, null, false));
            }
        }
    }

    public class StoryReactionButton : ToggleButton
    {
        private Image Presenter;
        private CustomEmojiIcon Icon;
        private Popup Overlay;
        private AnimatedTextBlock Count;

        public StoryReactionButton()
        {
            DefaultStyleKey = typeof(StoryReactionButton);
            Click += OnClick;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            UpdateManager.Unsubscribe(this, ref _fileToken, true);
        }

        private StoryViewModel _story;
        private ReactionType _interaction;
        private EmojiReaction _reaction;
        private EmojiReaction _defaultValue;
        private Sticker _sticker;
        private UnreadReaction _unread;

        private long _fileToken;

        public ReactionType Reaction => _interaction;
        public EmojiReaction EmojiReaction => _reaction;
        public Sticker CustomReaction => _sticker;

        public int FrameSize => _defaultValue != null ? 32 : 70;

        private long _presenterId;

        public void SetUnread(UnreadReaction unread)
        {
            if (Presenter == null)
            {
                _unread = unread;
                return;
            }

            _unread = unread;

            if (unread != null)
            {
                Animate();
            }
        }

        public async void SetReaction(StoryViewModel story, ReactionType interaction, EmojiReaction value, EmojiReaction defaultValue)
        {
            if (Presenter == null)
            {
                _story = story;
                _interaction = interaction;
                _reaction = value;
                _defaultValue = defaultValue;
                return;
            }

            var recycled = story.StoryId == _story?.StoryId
                && story.ChatId == _story?.ChatId
                && interaction.AreTheSame(_interaction);

            _story = story;
            _interaction = interaction;
            _reaction = value;

            UpdateInteraction(story, interaction, recycled);

            var around = value?.AroundAnimation?.StickerValue;
            if (around != null && around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive && !around.Local.IsDownloadingCompleted)
            {
                _story.ClientService.DownloadFile(around.Id, 32);
            }

            var center = value?.CenterAnimation?.StickerValue;
            if (center == null || center.Id == _presenterId)
            {
                return;
            }

            _presenterId = center.Id;

            if (center.Local.IsDownloadingCompleted)
            {
                Presenter.Source = await GetLottieFrame(center.Local.Path, 0, FrameSize, FrameSize);
            }
            else
            {
                Presenter.Source = null;

                UpdateManager.Subscribe(this, _story.ClientService, center, ref _fileToken, UpdateFile, true);

                if (center.Local.CanBeDownloaded && !center.Local.IsDownloadingActive)
                {
                    _story.ClientService.DownloadFile(center.Id, 32);
                }
            }
        }

        public void SetReaction(StoryViewModel story, ReactionType interaction, Sticker value, EmojiReaction defaultValue)
        {
            if (Presenter == null)
            {
                _story = story;
                _interaction = interaction;
                _sticker = value;
                _defaultValue = defaultValue;
                return;
            }

            var recycled = story.StoryId == _story?.StoryId
                && story.ChatId == _story?.ChatId
                && interaction.AreTheSame(_interaction);

            _story = story;
            _interaction = interaction;
            _sticker = value;

            UpdateInteraction(story, interaction, recycled);

            if (_presenterId == value?.StickerValue.Id)
            {
                return;
            }

            _presenterId = value.StickerValue.Id;

            Icon ??= GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;
            Icon.Source = new DelayedFileSource(story.ClientService, value.StickerValue);
        }

        private void UpdateInteraction(StoryViewModel story, ReactionType interaction, bool recycled)
        {
            IsChecked = story.ChosenReactionType != null && story.ChosenReactionType.AreTheSame(interaction);
            //AutomationProperties.SetName(this, Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, interaction.Type));

            Count ??= GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
            Count.Visibility = Windows.UI.Xaml.Visibility.Visible;

            if (_defaultValue != null)
            {
                Count.Text = Formatter.ShortNumber(story.InteractionInfo.ReactionCount);
                Count.Visibility = story.InteractionInfo.ReactionCount > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            else
            {
                foreach (var area in story.Areas)
                {
                    if (area.Type is StoryAreaTypeSuggestedReaction suggestedReaction && suggestedReaction.ReactionType.AreTheSame(interaction))
                    {
                        Count.Text = Formatter.ShortNumber(suggestedReaction.TotalCount);
                        Count.Visibility = suggestedReaction.TotalCount > 0
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                }
            }
        }

        private async void UpdateFile(object target, File file)
        {
            Presenter.Source = await GetLottieFrame(file.Local.Path, 0, FrameSize, FrameSize);
        }

        private static async Task<ImageSource> GetLottieFrame(string path, int frame, int width, int height)
        {
            var dpi = WindowContext.Current.RasterizationScale;

            width = (int)(width * dpi);
            height = (int)(height * dpi);

            var bitmap = new WriteableBitmap(width, height);
            var buffer = new PixelBuffer(bitmap);

            await Task.Run(() =>
            {
                var animation = LottieAnimation.LoadFromFile(path, width, height, false, null);
                if (animation != null)
                {
                    animation.RenderSync(buffer, frame);
                    animation.Dispose();
                }
            });

            return bitmap;
        }

        protected override void OnApplyTemplate()
        {
            Presenter = GetTemplateChild(nameof(Presenter)) as Image;
            Overlay = GetTemplateChild(nameof(Overlay)) as Popup;

            if (_sticker != null)
            {
                SetReaction(_story, _interaction, _sticker, _defaultValue);
            }
            else if (_interaction != null)
            {
                SetReaction(_story, _interaction, _reaction, _defaultValue);
            }

            SetUnread(_unread);

            base.OnApplyTemplate();
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        private void OnClick(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            var story = _story;
            if (story == null || Presenter == null)
            {
                return;
            }

            var chosen = story.ChosenReactionType != null;
            if (chosen && _defaultValue != null)
            {
                _story.ClientService.Send(new SetStoryReaction(_story.ChatId, _story.StoryId, null, false));
                SetReaction(_story, new ReactionTypeEmoji(_defaultValue.Emoji), _defaultValue, _defaultValue);
            }
            else
            {
                _story.ClientService.Send(new SetStoryReaction(_story.ChatId, _story.StoryId, _interaction, false));
                Animate();
            }

            if (chosen is false)
            {
            }
        }

        private async void Animate()
        {
            if (_reaction != null)
            {
                AnimateReaction();
            }
            else
            {
                var response = await _story.ClientService.SendAsync(new GetCustomEmojiReactionAnimations());
                if (response is Stickers stickers)
                {
                    var random = new Random();
                    var next = random.Next(0, stickers.StickersValue.Count);

                    var around = await _story.ClientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && IsLoaded && _sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        if (Icon != null)
                        {
                            Icon.Source = new CustomEmojiFileSource(_story.ClientService, customEmoji.CustomEmojiId);
                            Icon.Play();
                        }

                        var presenter = Presenter;
                        var popup = Overlay;

                        var dispatcher = Windows.System.DispatcherQueue.GetForCurrentThread();

                        var aroundView = new AnimatedImage();
                        aroundView.Width = FrameSize * 3;
                        aroundView.Height = FrameSize * 3;
                        aroundView.LoopCount = 1;
                        aroundView.FrameSize = new Size(FrameSize * 3, FrameSize * 3);
                        aroundView.DecodeFrameType = DecodePixelType.Logical;
                        aroundView.AutoPlay = true;
                        aroundView.Source = new LocalFileSource(around);
                        aroundView.LoopCompleted += (s, args) =>
                        {
                            dispatcher.TryEnqueue(Continue2);
                        };

                        var root = new Grid();
                        root.Width = FrameSize * 3;
                        root.Height = FrameSize * 3;
                        root.Children.Add(aroundView);

                        popup.Child = root;
                        popup.IsOpen = true;
                    }
                }
            }
        }

        private void AnimateReaction()
        {
            var reaction = _reaction;
            if (reaction == null)
            {
                return;
            }

            var center = reaction.CenterAnimation?.StickerValue;
            var around = reaction.AroundAnimation?.StickerValue;

            if (center == null || around == null)
            {
                return;
            }

            if (center.Local.IsDownloadingCompleted && around.Local.IsDownloadingCompleted)
            {
                var presenter = Presenter;
                var popup = Overlay;

                var dispatcher = Windows.System.DispatcherQueue.GetForCurrentThread();

                var centerView = new AnimatedImage();
                centerView.Width = FrameSize;
                centerView.Height = FrameSize;
                centerView.LoopCount = 1;
                centerView.FrameSize = new Size(FrameSize, FrameSize);
                centerView.DecodeFrameType = DecodePixelType.Logical;
                centerView.AutoPlay = true;
                centerView.Source = new LocalFileSource(center);
                centerView.Ready += (s, args) =>
                {
                    dispatcher.TryEnqueue(Start);
                };
                centerView.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(Continue1);
                };

                var aroundView = new AnimatedImage();
                aroundView.Width = FrameSize * 3;
                aroundView.Height = FrameSize * 3;
                aroundView.LoopCount = 1;
                aroundView.FrameSize = new Size(FrameSize * 3, FrameSize * 3);
                aroundView.DecodeFrameType = DecodePixelType.Logical;
                aroundView.AutoPlay = true;
                aroundView.Source = new LocalFileSource(around);
                aroundView.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(Continue2);
                };

                var root = new Grid();
                root.Width = FrameSize * 3;
                root.Height = FrameSize * 3;
                root.Children.Add(centerView);
                root.Children.Add(aroundView);

                popup.Child = root;
                popup.IsOpen = true;
            }
            else
            {
                if (center.Local.CanBeDownloaded && !center.Local.IsDownloadingActive)
                {
                    _story.ClientService.DownloadFile(center.Id, 32);
                }

                if (around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive)
                {
                    _story.ClientService.DownloadFile(around.Id, 32);
                }
            }
        }

        private void Start()
        {
            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 0;
        }

        private void Continue1()
        {
            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 1;
        }

        private void Continue2()
        {
            var popup = Overlay;
            if (popup == null)
            {
                return;
            }

            popup.IsOpen = false;
            popup.Child = null;
        }
    }

}
