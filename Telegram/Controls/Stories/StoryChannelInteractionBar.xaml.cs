//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
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

        // The size the template draws the reaction at, as large as the reply bar's other icons
        // look next to it. Telegram's painting of an emoji reaction
        // fills just a part of its animation's frame, and is drawn EmojiScale times this size
        // for it to look as large as a custom emoji, which fills its own.
        public static readonly DependencyProperty FrameSizeProperty = DependencyProperty.Register(
            nameof(FrameSize), typeof(int), typeof(StoryReactionButton), new PropertyMetadata(28));

        public int FrameSize
        {
            get => (int)GetValue(FrameSizeProperty);
            set => SetValue(FrameSizeProperty, value);
        }

        // How much larger than a custom emoji an emoji reaction's center animation is drawn, for
        // the two to look the same size: 32 against 20, as ReactionButton draws them on messages.
        private const double EmojiScale = 32.0 / 20.0;

        private void SizeIcon(double size)
        {
            Icon.Width = size;
            Icon.Height = size;
            Icon.FrameSize = new Size(size, size);

            // Larger than the box holding it, as an emoji's animation is, an element is laid out
            // from the box's corner rather than centered on it: given the room, it stays centered.
            var box = (Icon.Parent as FrameworkElement)?.Width ?? double.NaN;
            Icon.Margin = new Thickness(double.IsNaN(box) ? 0 : Math.Min(0, (box - size) / 2));
        }

        /// <summary>
        /// Places <paramref name="popup"/>, holding an effect three times the reaction's size, for
        /// the effect to be centered on the reaction whatever size it is drawn at.
        /// </summary>
        private void CenterOverlay(Popup popup)
        {
            var box = (popup.Parent as FrameworkElement)?.Width ?? double.NaN;
            popup.Margin = new Thickness(double.IsNaN(box) ? -FrameSize : (box - FrameSize * 3) / 2);
        }

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
            if (_story != story && Icon != null)
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
            if (story == null || Icon == null)
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
            var drawn = Icon != null && Icon.Visibility == Visibility.Visible && Icon.Source != null;

            if (drawn && _interaction != null && _interaction.AreTheSame(reaction))
            {
                IsChecked = true;
                return;
            }

            ShowEmpty(false);
            IsChecked = true;

            _interaction = reaction;
            _reaction = null;
            _sticker = null;

            // Drawn the way the update showing it draws it, resolving it first: what there is
            // of it in the meantime is its text emoji, for a reaction never shown before.
            DrawReaction(_story.ClientService, reaction, GetEmoji(reaction));
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

            foreach (var sticker in new[] { value.CenterAnimation, value.AroundAnimation })
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

            // The reaction is drawn in the icon, whichever kind of it.
            if (Icon != null)
            {
                Icon.Visibility = empty
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
            if (Icon == null)
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
            if (Icon == null)
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

            ShowEmpty(defaultValue != null && story.ChosenReactionType == null);
            UpdateInteraction(story, interaction, recycled);

            var around = value?.AroundAnimation?.StickerValue;
            if (around != null && around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive && !around.Local.IsDownloadingCompleted)
            {
                _story.ClientService.DownloadFile(around.Id, 32);
            }

            // The default heart drawn for a story with no reaction is held behind the empty one,
            // for the reaction chosen by tapping it to appear at once, at its size.
            DrawReaction(story.ClientService, interaction, story.ChosenReactionType == null ? null : value?.Emoji);
        }

        /// <summary>
        /// Draws <paramref name="reaction"/> as <see cref="ReactionButton"/> draws one, in the
        /// icon: a custom emoji fills it, while Telegram's painting of an emoji takes just a part
        /// of its animation's frame and is drawn <see cref="EmojiScale"/> times the size for the
        /// two to look the same,
        /// until <see cref="CenterOnPaintedContent"/> measures that part and redraws the frame
        /// from it.
        /// </summary>
        private void DrawReaction(IClientService clientService, ReactionType reaction, string fallback = null)
        {
            Icon ??= GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;

            var emoji = reaction is ReactionTypeEmoji;
            var size = emoji ? FrameSize * EmojiScale : FrameSize;

            var source = new ReactionFileSource(clientService, reaction)
            {
                UseCenterAnimation = true,
                IsAnimated = !emoji
            };

            using (Icon.BeginBatchUpdate())
            {
                SizeIcon(size);

                // The emoji's effect plays it once, where it rests, while a custom emoji keeps
                // looping at rest, as custom emoji do elsewhere.
                Icon.LoopCount = emoji ? 1 : 0;

                // A custom emoji fills its frame and stays as drawn; an emoji's frame is redrawn
                // from what it paints, once measured.
                Icon.RenderTransform = null;

                Icon.Source = source;
            }

            if (emoji)
            {
                CenterOnPaintedContent(source);
            }

            // Its image is the one the reactions offered to choose from draw, so usually on
            // disk: as text only when it isn't.
            if (fallback != null)
            {
                ShowFallbackOnDemand(source, fallback);
            }
        }

        /// <summary>
        /// Waits for the animation behind <paramref name="source"/> to be on disk and redraws the
        /// icon from what it paints. Telegram paints an emoji in just a part of that frame, and
        /// not quite in its middle, leaving room for its effect: measured, the frame is drawn for
        /// its painted emoji to rest exactly where a custom emoji does, as large as one and in
        /// the middle of the button, with everything the animation paints moving along.
        /// </summary>
        private void CenterOnPaintedContent(ReactionFileSource source)
        {
            var reaction = _interaction;

            // The event answers a late subscriber as well, and the file is usually on disk
            // already: drawn from whichever of the two speaks first.
            source.Downloaded += (s, args) =>
            {
                if (_interaction.AreTheSame(reaction))
                {
                    DrawPaintedContent(source.FilePath, reaction);
                }
            };

            if (source.IsDownloadingCompleted)
            {
                DrawPaintedContent(source.FilePath, reaction);
            }
        }

        private void DrawPaintedContent(string path, ReactionType reaction)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            // The download may answer from any thread: drawn on the button's own.
            if (!_dispatcher.HasThreadAccess)
            {
                _dispatcher.TryEnqueue(() => DrawPaintedContent(path, reaction));
                return;
            }

            DrawPaintedContentUnsafe(path, reaction);
        }

        private async void DrawPaintedContentUnsafe(string path, ReactionType reaction)
        {
            var content = await GetPaintedContentAsync(path);

            if (content is not Rect rect)
            {
                return;
            }

            if (Icon == null || !_interaction.AreTheSame(reaction))
            {
                return;
            }

            // The frame keeps the size it is drawn at, as ReactionButton draws it, and is
            // translated for its painted part to sit in the middle of the button, where a custom
            // emoji rests. The effect plays within the translated frame, and so starts and ends
            // on the emoji, wherever in its frame Telegram painted it.
            var size = FrameSize * EmojiScale;
            var x = size * (0.5 - (rect.X + rect.Width / 2));
            var y = size * (0.5 - (rect.Y + rect.Height / 2));

            using (Icon.BeginBatchUpdate())
            {
                SizeIcon(size);

                Icon.RenderTransform = new TranslateTransform
                {
                    X = x,
                    Y = y
                };
            }

        }

        // What a reaction's center animation paints, as a fraction of its frame, measured once
        // per file and shared by every button drawing that reaction.
        private static readonly ConcurrentDictionary<string, Task<Rect?>> _paintedContent = new();

        private static Task<Rect?> GetPaintedContentAsync(string path)
        {
            return _paintedContent.GetOrAdd(path, MeasurePaintedContentAsync);
        }

        private static async Task<Rect?> MeasurePaintedContentAsync(string path)
        {
            const int pixels = 96;

            try
            {
                var buffer = BufferSurface.Create((uint)(pixels * pixels * 4));

                var rendered = await Task.Run(() =>
                {
                    var animation = LottieAnimation.LoadFromFile(path, pixels, pixels, false, null);
                    if (animation != null)
                    {
                        animation.RenderSync(buffer, 0);
                        animation.Dispose();
                        return true;
                    }

                    return false;
                });

                if (!rendered)
                {
                    return null;
                }

                // BGRA, so the alpha of each pixel is its fourth byte.
                var data = buffer.ToArray();

                int minX = pixels, minY = pixels, maxX = -1, maxY = -1;

                for (int y = 0; y < pixels; y++)
                {
                    for (int x = 0; x < pixels; x++)
                    {
                        if (data[(y * pixels + x) * 4 + 3] > 8)
                        {
                            minX = Math.Min(minX, x);
                            maxX = Math.Max(maxX, x);
                            minY = Math.Min(minY, y);
                            maxY = Math.Max(maxY, y);
                        }
                    }
                }

                if (maxX < 0)
                {
                    return null;
                }

                var side = Math.Max(maxX - minX, maxY - minY) + 1;
                var left = (minX + maxX + 1 - side) / 2;
                var top = (minY + maxY + 1 - side) / 2;

                // The square, as a fraction of the frame, centered on what it paints.
                return new Rect((double)left / pixels, (double)top / pixels, (double)side / pixels, (double)side / pixels);
            }
            catch (Exception ex)
            {
                // Left as drawn, uncentered, which is how the message reaction button draws one
                // too.
                Logger.Exception(ex);
                return null;
            }
        }

        /// <summary>
        /// Shows <paramref name="emoji"/> as text until <paramref name="source"/> is ready, so
        /// that a reaction never shown before appears at once rather than after a download.
        /// </summary>
        private void ShowFallbackOnDemand(ReactionFileSource source, string emoji)
        {
            var timer = _dispatcher.CreateTimer();
            timer.Interval = TimeSpan.FromMilliseconds(100);
            timer.IsRepeating = false;
            timer.Tick += (s, args) =>
            {
                if (Icon?.Source == source && !source.IsDownloadingCompleted)
                {
                    ShowFallback(emoji);
                }
            };
            timer.Start();

            source.Downloaded += (s, args) =>
            {
                if (Icon?.Source == source)
                {
                    ShowFallback(null);
                }
            };
        }

        public void SetReaction(StoryViewModel story, ReactionType interaction, Sticker value, EmojiReaction defaultValue)
        {
            if (Icon == null)
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

            DrawReaction(story.ClientService, interaction);
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

        protected override void OnApplyTemplate()
        {
            Icon = GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;
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
            if (story == null || Icon == null)
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
                    if (around.Local.IsDownloadingCompleted && IsLoaded && token == _effectToken && _sticker?.FullType is StickerFullTypeCustomEmoji)
                    {
                        // The custom emoji plays along with the effect, from where it is at rest.
                        if (Icon?.Source is ReactionFileSource source && !source.IsAnimated)
                        {
                            Icon.Source = source.Clone(true);
                        }

                        Icon?.Play();

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
                        CenterOverlay(popup);
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
                var popup = Overlay;

                var dispatcher = DispatcherQueue.GetForCurrentThread();

                // The emoji's own animation plays in the icon, where the emoji rests already:
                // the effect starts and ends on it without anything being drawn elsewhere,
                // which is how ReactionButton plays one too. The rest of the effect is drawn
                // around it, in the popup.
                if (Icon?.Source is ReactionFileSource source && !source.IsAnimated)
                {
                    Icon.Source = source.Clone(true);
                }

                Icon?.Play();

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
                CenterOverlay(popup);
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
