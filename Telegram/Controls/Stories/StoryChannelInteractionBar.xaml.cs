//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls.Messages;
using Telegram.Converters;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Composition;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
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

        public StoryReactionButton ReactionButton => ReactButton;

        public event RoutedEventHandler ShareClick
        {
            add => ShareButton.Click += value;
            remove => ShareButton.Click -= value;
        }

        public void Update(StoryViewModel story)
        {
            _viewModel = story;

            if (story.ClientService.TryGetUser(story.PosterChatId, out User user) && user.Type is UserTypeBot)
            {
                Visibility = Visibility.Collapsed;
            }
            else
            {
                Visibility = Visibility.Visible;
            }

            if (story.Chat.Type is ChatTypeSupergroup)
            {
                ReplyDisabled.Visibility = Visibility.Collapsed;

                if (story.InteractionInfo != null)
                {
                    ViewersCount.Text = story.InteractionInfo.ViewCount.ToString("N0");

                    //ReactionCount.Text = story.InteractionInfo.ReactionCount.ToString("N0");
                    //ReactionCount.Visibility = story.InteractionInfo.ReactionCount > 0
                    //        ? Visibility.Visible
                    //        : Visibility.Collapsed;
                }
                else
                {
                    //Viewers.Items.Clear();
                    //Viewers.Visibility = Visibility.Collapsed;

                    //ViewersCount.Text = Strings.NobodyViews;

                    //ReactionCount.Visibility =
                    //    ReactionIcon.Visibility = Visibility.Collapsed;
                }

                ReactButton.Visibility = Visibility.Visible;
                ReactButton.Update(story);
            }
            else
            {
                ReplyDisabled.Visibility = Visibility.Visible;

                ViewersIcon.Visibility = Visibility.Collapsed;
                ViewersCount.Visibility = Visibility.Collapsed;

                ReactButton.Visibility = Visibility.Collapsed;
            }
        }

        private void Viewers_RecentUserHeadChanged(ProfilePicture sender, MessageSender messageSender)
        {
            sender.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, messageSender);
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
                story.ClientService.Send(new SetStoryReaction(story.PosterChatId, story.Id, new ReactionTypeEmoji("\u2764\uFE0F"), false));
            }
            else
            {
                story.ClientService.Send(new SetStoryReaction(story.PosterChatId, story.Id, null, false));
            }
        }
    }

    public partial class StoryReactionButton : ToggleButton
    {
        private Image Presenter;
        private TextBlock Glyph;
        private TextBlock Fallback;
        private CustomEmojiIcon Icon;
        private Popup Overlay;
        private AnimatedTextBlock Count;

        private readonly DispatcherQueue _dispatcher;

        public StoryReactionButton()
        {
            _dispatcher = DispatcherQueue.GetForCurrentThread();

            DefaultStyleKey = typeof(StoryReactionButton);
            Click += OnClick;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // The effect is drawn in a popup, which outlives the stories being closed otherwise.
            StopEffects();
        }

        private StoryViewModel _story;
        private ReactionType _interaction;
        private EmojiReaction _reaction;
        private EmojiReaction _defaultValue;
        private Sticker _sticker;
        private UnreadReaction _unread;

        public ReactionType Reaction => _interaction;
        public EmojiReaction EmojiReaction => _reaction;
        public Sticker CustomReaction => _sticker;

        // As large as the empty heart, for the reaction not to jump in size when chosen.
        public int FrameSize => _defaultValue != null ? 24 : 70;

        private long _presenterId;

        private StoryViewModel _requested;

        // The reaction last shown as chosen, to tell a new one apart from the same one shown again.
        private StoryViewModel _chosenStory;
        private ReactionType _chosen;

        // Bumped to cancel the effects already started, including those still loading.
        private int _effectToken;

        /// <summary>
        /// Shows the reaction chosen for <paramref name="story"/>, or an empty heart if there is
        /// none, resolving either one first if it isn't cached. A reaction newly chosen for the
        /// story already shown, from here or from anywhere else, plays its effect.
        /// </summary>
        public async void Update(StoryViewModel story)
        {
            _requested = story;

            // Nothing of the previous story may linger while this one's reaction is resolved, and
            // nothing shown waits on it either: right after starting the app that is a download.
            if (_story != story && Presenter != null)
            {
                StopEffects();

                if (story.ChosenReactionType != null)
                {
                    ShowChosen(story.ChosenReactionType);
                }
                else
                {
                    ShowEmpty(true);
                }
            }

            // Taken back from anywhere, including while its effect is still playing here. Other
            // updates to the story can arrive while a reaction is on its way, and leave it alone.
            if (story == _chosenStory && _chosen != null && story.ChosenReactionType == null)
            {
                StopEffects();
            }

            var defaultReaction = await GetDefaultReactionAsync(story.ClientService);
            var reactionType = story.ChosenReactionType ?? new ReactionTypeEmoji("❤");

            // Lookups complete out of order when moving between stories quickly.
            if (_requested != story)
            {
                return;
            }

            var changed = story == _chosenStory
                && story.ChosenReactionType != null
                && (_chosen == null || !_chosen.AreTheSame(story.ChosenReactionType));

            _chosenStory = story;
            _chosen = story.ChosenReactionType;

            if (story.ChosenReactionType == null)
            {
                SetReaction(story, reactionType, defaultReaction, defaultReaction);
            }
            else if (reactionType is ReactionTypeEmoji emoji)
            {
                if (!story.ClientService.TryGetCachedReaction(emoji.Emoji, out EmojiReaction reaction))
                {
                    reaction = await story.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji)) as EmojiReaction;
                }

                if (reaction == null || _requested != story)
                {
                    return;
                }

                SetReaction(story, reactionType, reaction, defaultReaction);
            }
            else if (reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                if (!EmojiCache.TryGet(customEmoji.CustomEmojiId, out Sticker sticker))
                {
                    sticker = await EmojiCache.GetAsync(story.ClientService, customEmoji.CustomEmojiId) as Sticker;
                }

                if (sticker == null || _requested != story)
                {
                    return;
                }

                SetReaction(story, reactionType, sticker, defaultReaction);
            }
            else
            {
                return;
            }

            if (changed)
            {
                Animate();
            }
        }

        /// <summary>
        /// Shows <paramref name="reaction"/>, just chosen elsewhere, at once: its effect then plays
        /// when the story is updated with it.
        /// </summary>
        public void Choose(ReactionType reaction)
        {
            var story = _story;
            if (story == null || Presenter == null)
            {
                return;
            }

            StopEffects();
            Prefetch(story.ClientService, reaction);

            ShowChosen(reaction);
        }

        /// <summary>
        /// Shows <paramref name="reaction"/> as chosen before the story says so, as it is drawn
        /// once chosen: as text only while an emoji's image is still downloading.
        /// </summary>
        private void ShowChosen(ReactionType reaction)
        {
            // Already drawn, as moving between stories with the same reaction finds it.
            var drawn = _sticker != null
                ? Icon != null && Icon.Visibility == Visibility.Visible && Icon.Source != null
                : Presenter.Source != null && Presenter.Visibility == Visibility.Visible;

            if (drawn && _interaction != null && _interaction.AreTheSame(reaction))
            {
                IsChecked = true;
                return;
            }

            ShowEmpty(false);
            IsChecked = true;

            // Drawn for good: the update showing it finds it drawn already, and leaves it be.
            if (reaction is ReactionTypeCustomEmoji customEmoji && EmojiCache.TryGet(customEmoji.CustomEmojiId, out Sticker sticker))
            {
                _interaction = reaction;
                _sticker = sticker;
                _reaction = null;

                DrawSticker(_story.ClientService, sticker);
                return;
            }

            // Its image is the one the reactions offered to choose from draw, so usually on disk:
            // as text only when it isn't.
            if (reaction is ReactionTypeEmoji emoji && _story.ClientService.TryGetCachedReaction(emoji.Emoji, out EmojiReaction value))
            {
                _interaction = reaction;
                _reaction = value;
                _sticker = null;

                DrawEmoji(_story.ClientService, value, true, emoji.Emoji);
                return;
            }

            // Not even looked up yet: nothing to draw but its emoji, until the update showing it.
            Presenter.Source = null;
            _presenterId = 0;

            if (Icon != null)
            {
                Icon.Visibility = Visibility.Collapsed;
            }

            ShowFallback(GetEmoji(reaction));
        }

        /// <summary>
        /// The emoji standing in for a regular emoji reaction until its image is ready. Custom
        /// emoji have none, and are drawn as themselves.
        /// </summary>
        private static string GetEmoji(ReactionType reaction)
        {
            return reaction is ReactionTypeEmoji emoji
                ? emoji.Emoji
                : null;
        }

        /// <summary>
        /// Starts downloading what the button needs to show <paramref name="reaction"/> and play
        /// its effect, while the story is still being updated with it.
        /// </summary>
        private static async void Prefetch(IClientService clientService, ReactionType reaction)
        {
            if (reaction is not ReactionTypeEmoji emoji)
            {
                return;
            }

            if (!clientService.TryGetCachedReaction(emoji.Emoji, out EmojiReaction value))
            {
                value = await clientService.SendAsync(new GetEmojiReaction(emoji.Emoji)) as EmojiReaction;
            }

            if (value == null)
            {
                return;
            }

            foreach (var sticker in new[] { value.ActivateAnimation, value.CenterAnimation, value.AroundAnimation })
            {
                var file = sticker?.StickerValue;
                if (file != null && file.Local.CanBeDownloaded && !file.Local.IsDownloadingActive && !file.Local.IsDownloadingCompleted)
                {
                    clientService.DownloadFile(file.Id, 32);
                }
            }
        }

        private void ShowEmpty(bool empty)
        {
            Glyph ??= GetTemplateChild(nameof(Glyph)) as TextBlock;

            // Templates without the glyph, as the suggested reactions on the story, keep the image.
            if (Glyph == null)
            {
                return;
            }

            Glyph.Visibility = empty
                ? Visibility.Visible
                : Visibility.Collapsed;

            // Regular emoji are drawn in the image, custom emoji in the icon.
            Presenter.Visibility = empty || _sticker != null
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (Icon != null)
            {
                Icon.Visibility = empty || _sticker == null
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (empty)
            {
                ShowFallback(null);
            }
        }

        /// <summary>
        /// Shows <paramref name="emoji"/> as text until the image of the reaction is ready, so
        /// that a reaction never shown before appears at once rather than after a download.
        /// </summary>
        private void ShowFallback(string emoji)
        {
            Fallback ??= GetTemplateChild(nameof(Fallback)) as TextBlock;

            if (Fallback == null)
            {
                return;
            }

            Fallback.Text = emoji ?? string.Empty;
            Fallback.Visibility = emoji != null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        /// <summary>
        /// Ends whatever effect is playing or about to. Effects are drawn in popups of their own,
        /// so whatever closes the button must end them too.
        /// </summary>
        public void StopEffects()
        {
            _effectToken++;

            if (Overlay != null)
            {
                Overlay.IsOpen = false;
                Overlay.Child = null;
            }

            if (Presenter != null)
            {
                Presenter.Opacity = 1;
            }

            if (Icon != null)
            {
                Icon.Opacity = 1;
            }

            if (Fallback != null)
            {
                Fallback.Opacity = 1;
            }
        }

        private static async Task<EmojiReaction> GetDefaultReactionAsync(IClientService clientService)
        {
            if (clientService.TryGetCachedReaction("❤️", out EmojiReaction reaction))
            {
                return reaction;
            }
            else
            {
                var response = await clientService.SendAsync(new GetEmojiReaction("❤"));
                if (response is EmojiReaction reaction2)
                {
                    return reaction2;
                }
            }

            return null;
        }

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

        public void SetReaction(StoryViewModel story, ReactionType interaction, EmojiReaction value, EmojiReaction defaultValue)
        {
            if (Presenter == null)
            {
                _story = story;
                _interaction = interaction;
                _reaction = value;
                _sticker = null;
                _defaultValue = defaultValue;
                return;
            }

            var recycled = story.Id == _story?.Id
                && story.PosterChatId == _story?.PosterChatId
                && interaction.AreTheSame(_interaction);

            _story = story;
            _interaction = interaction;
            _reaction = value;
            _sticker = null;
            _defaultValue = defaultValue;

            // A suggested reaction draws a regular emoji in the image, and may have been a custom
            // emoji until now. The button draws both the same way, in the icon.
            if (Icon != null && defaultValue == null)
            {
                Icon.Visibility = Visibility.Collapsed;
            }

            ShowEmpty(defaultValue != null && story.ChosenReactionType == null);
            UpdateInteraction(story, interaction, recycled);

            var around = value?.AroundAnimation?.StickerValue;
            if (around != null && around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive && !around.Local.IsDownloadingCompleted)
            {
                _story.ClientService.DownloadFile(around.Id, 32);
            }

            DrawEmoji(story.ClientService, value, defaultValue != null, defaultValue != null && story.ChosenReactionType == null ? null : value?.Emoji);
        }

        /// <summary>
        /// Draws <paramref name="value"/> as Telegram paints it, showing <paramref name="fallback"/>
        /// as text in its place only while its image is still downloading, once that takes long
        /// enough to notice.
        /// </summary>
        private async void DrawEmoji(IClientService clientService, EmojiReaction value, bool icon, string fallback)
        {
            // The first frame of the center animation is Telegram's painting of the emoji, which
            // the effect starts and ends on. The activate animation starts from a plain emoji
            // instead, and only turns into it as it plays. The emoji fills just the middle of the
            // frame, so the button draws it at twice the size of the reaction, as the effect does.
            var still = value?.CenterAnimation?.StickerValue;
            if (still == null)
            {
                return;
            }

            if (icon)
            {
                // Custom emoji are drawn in the icon instead.
                if (Icon != null)
                {
                    Icon.Visibility = Visibility.Collapsed;
                }

                Presenter.Visibility = Glyph?.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (still.Id == _presenterId)
            {
                return;
            }

            _presenterId = still.Id;

            // Never the previous reaction's image while this one's is prepared.
            Presenter.Source = null;
            ShowFallback(null);

            // Asked of TDLib rather than read from the sticker, whose file can say it isn't
            // downloaded when the reactions offered to choose from have downloaded it already.
            // That answer is immediate then, well before the emoji would show: only a download
            // in progress shows it.
            var downloaded = false;

            if (fallback != null)
            {
                var timer = _dispatcher.CreateTimer();
                timer.Interval = TimeSpan.FromMilliseconds(100);
                timer.IsRepeating = false;
                timer.Tick += (s, args) =>
                {
                    if (_presenterId == still.Id && !downloaded)
                    {
                        ShowFallback(fallback);
                    }
                };
                timer.Start();
            }

            var file = still.Local.IsDownloadingCompleted
                ? still
                : await clientService.DownloadFileAsync(still, 32);

            downloaded = true;

            if (_presenterId != still.Id || !file.Local.IsDownloadingCompleted)
            {
                return;
            }

            ImageSource source;
            Rect content = default;

            if (icon)
            {
                // Rendered large enough for the emoji, a part of the frame, to stay sharp once
                // cropped to it and drawn as large as the reaction.
                (source, content) = await LottieContent.RenderAsync(file.Local.Path, FrameSize * 4);
            }
            else
            {
                source = await GetLottieFrame(file.Local.Path, 0, FrameSize, FrameSize);
            }

            // A newer reaction may have been shown while this frame was rendered.
            if (_presenterId == still.Id)
            {
                Presenter.Source = source;
                _content = content;

                ShowFallback(null);
            }
        }

        // Where in the center animation's frame the emoji is painted, as a fraction of it, for its
        // effect to draw the emoji in the same place and at the same size as the button does.
        private Rect _content;

        public void SetReaction(StoryViewModel story, ReactionType interaction, Sticker value, EmojiReaction defaultValue)
        {
            if (Presenter == null)
            {
                _story = story;
                _interaction = interaction;
                _sticker = value;
                _reaction = null;
                _defaultValue = defaultValue;
                return;
            }

            var recycled = story.Id == _story?.Id
                && story.PosterChatId == _story?.PosterChatId
                && interaction.AreTheSame(_interaction);

            _story = story;
            _interaction = interaction;
            _sticker = value;
            _reaction = null;
            _defaultValue = defaultValue;

            ShowEmpty(false);
            UpdateInteraction(story, interaction, recycled);

            DrawSticker(story.ClientService, value);
        }

        /// <summary>
        /// Draws custom emoji <paramref name="value"/>, which having been offered to choose from is
        /// downloaded already.
        /// </summary>
        private void DrawSticker(IClientService clientService, Sticker value)
        {
            Icon ??= GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;

            // Hidden behind the empty heart until the reaction is chosen.
            Icon.Visibility = Glyph?.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (_presenterId == value.StickerValue.Id)
            {
                return;
            }

            _presenterId = value.StickerValue.Id;

            // Regular emoji are drawn in the image instead.
            Presenter.Source = null;
            ShowFallback(null);

            Icon.FrameSize = new Size(FrameSize, FrameSize);
            Icon.Source = new DelayedFileSource(clientService, value);
        }

        private void UpdateInteraction(StoryViewModel story, ReactionType interaction, bool recycled)
        {
            IsChecked = story.ChosenReactionType != null && story.ChosenReactionType.AreTheSame(interaction);
            //AutomationProperties.SetName(this, Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, interaction.Type));

            Count ??= GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
            Count.Visibility = Visibility.Visible;

            if (_defaultValue != null)
            {
                // Only channel and group stories carry interaction info, so there is no count
                // to show next to the reaction for a story posted by a user.
                if (story.InteractionInfo == null)
                {
                    Count.Visibility = Visibility.Collapsed;
                    return;
                }

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

        private static async Task<ImageSource> GetLottieFrame(string path, int frame, int width, int height)
        {
            var dpi = WindowContext.Current.RasterizationScale;

            width = (int)(width * dpi);
            height = (int)(height * dpi);

            if (width > 0 && height > 0)
            {
                var buffer = BufferSurface.Create((uint)(width * height * 4));

                var rendered = await Task.Run(() =>
                {
                    var animation = LottieAnimation.LoadFromFile(path, width, height, false, null);
                    if (animation != null)
                    {
                        animation.RenderSync(buffer, frame);
                        animation.Dispose();
                        return true;
                    }

                    return false;
                });

                var bitmap = new WriteableBitmap(width, height);

                // Otherwise the buffer holds uninitialised memory.
                if (rendered)
                {
                    BufferSurface.Copy(buffer, bitmap.PixelBuffer);
                }

                return bitmap;
            }

            return null;
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

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var story = _story;
            if (story == null || Presenter == null)
            {
                return;
            }

            // Shown right away rather than when the story is updated, which then changes nothing:
            // recording the outcome as already shown keeps the update from playing the effect again.
            var chosen = story.ChosenReactionType != null;
            if (chosen && _defaultValue != null)
            {
                // Whatever is still playing belongs to the reaction being taken back.
                StopEffects();

                _story.ClientService.Send(new SetStoryReaction(_story.PosterChatId, _story.Id, null, false));
                SetReaction(_story, new ReactionTypeEmoji(_defaultValue.Emoji), _defaultValue, _defaultValue);

                _chosen = null;
                ShowEmpty(true);
                IsChecked = false;
            }
            else
            {
                _story.ClientService.Send(new SetStoryReaction(_story.PosterChatId, _story.Id, _interaction, false));

                _chosenStory = _story;
                _chosen = _interaction;
                ShowEmpty(false);
                IsChecked = true;

                Animate();
            }
        }

        private async void Animate()
        {
            var token = _effectToken;

            // Right away, as the effect may wait on a download.
            PopFallback();

            if (_reaction != null)
            {
                AnimateReaction();
            }
            else
            {
                var response = await _story.ClientService.SendAsync(new GetCustomEmojiReactionAnimations());
                if (response is Stickers stickers && token == _effectToken)
                {
                    var random = new Random();
                    var next = random.Next(0, stickers.StickersValue.Count);

                    var around = await _story.ClientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && IsLoaded && token == _effectToken && _sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        if (Icon != null)
                        {
                            Icon.Source = new CustomEmojiFileSource(_story.ClientService, customEmoji.CustomEmojiId);
                            Icon.Play();
                        }

                        var presenter = Presenter;
                        var popup = Overlay;

                        var dispatcher = DispatcherQueue.GetForCurrentThread();

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
                        popup.XamlRoot = XamlRoot;
                        popup.IsOpen = true;
                    }
                }
            }
        }

        private async void AnimateReaction()
        {
            var reaction = _reaction;
            if (reaction == null)
            {
                return;
            }

            var token = _effectToken;

            var center = reaction.CenterAnimation?.StickerValue;
            var around = reaction.AroundAnimation?.StickerValue;

            if (center == null || around == null)
            {
                return;
            }

            // A reaction chosen elsewhere has usually never been animated here, so its effect
            // isn't downloaded yet: wait for it rather than skip it.
            if (!center.Local.IsDownloadingCompleted && center.Local.CanBeDownloaded)
            {
                center = await _story.ClientService.DownloadFileAsync(center, 32);
            }

            if (!around.Local.IsDownloadingCompleted && around.Local.CanBeDownloaded)
            {
                around = await _story.ClientService.DownloadFileAsync(around, 32);
            }

            if (!IsLoaded || reaction != _reaction || token != _effectToken)
            {
                return;
            }

            if (center.Local.IsDownloadingCompleted && around.Local.IsDownloadingCompleted)
            {
                var presenter = Presenter;
                var popup = Overlay;

                var dispatcher = DispatcherQueue.GetForCurrentThread();

                // The emoji fills only a part of the center animation, not quite in its middle.
                // Measured where the button draws it, the animation is scaled and moved for the
                // emoji to start and end where it is at rest, at the same size.
                var content = _defaultValue != null ? _content : default;
                var centerSize = content.Width > 0
                    ? (int)Math.Round(FrameSize / content.Width)
                    : FrameSize;

                var centerView = new AnimatedImage();
                centerView.Width = centerSize;
                centerView.Height = centerSize;

                if (content.Width > 0)
                {
                    centerView.RenderTransform = new TranslateTransform
                    {
                        X = (0.5 - (content.X + content.Width / 2)) * centerSize,
                        Y = (0.5 - (content.Y + content.Height / 2)) * centerSize
                    };
                }
                centerView.LoopCount = 1;
                centerView.FrameSize = new Size(centerSize, centerSize);
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
                popup.XamlRoot = XamlRoot;
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

            // The effect draws the emoji itself, which the button's own drawing of it and the
            // stand-in would double.
            if (Icon != null)
            {
                Icon.Opacity = 0;
            }

            if (Fallback != null)
            {
                Fallback.Opacity = 0;
            }
        }

        private void Continue1()
        {
            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 1;

            if (Icon != null)
            {
                Icon.Opacity = 1;
            }

            if (Fallback != null)
            {
                Fallback.Opacity = 1;
            }
        }

        /// <summary>
        /// Gives the stand-in emoji the pop of a reaction being chosen, while its effect is still
        /// being downloaded.
        /// </summary>
        private void PopFallback()
        {
            if (Fallback == null || Fallback.Visibility == Visibility.Collapsed)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(Fallback);

            // Just shown, it may not be measured yet: centered on whatever size it gets.
            var center = visual.Compositor.CreateExpressionAnimation("Vector3(this.Target.Size.X * 0.5, this.Target.Size.Y * 0.5, 0)");
            visual.StartAnimation("CenterPoint", center);

            var pop = visual.Compositor.CreateVector3KeyFrameAnimation();
            pop.InsertKeyFrame(0, new Vector3(0.6f));
            pop.InsertKeyFrame(0.5f, new Vector3(1.3f));
            pop.InsertKeyFrame(0.8f, new Vector3(0.9f));
            pop.InsertKeyFrame(1, Vector3.One);
            pop.Duration = TimeSpan.FromMilliseconds(350);

            visual.StartAnimation("Scale", pop);
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
