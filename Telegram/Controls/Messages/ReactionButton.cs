//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Converters;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages
{
    public class ReactionButton : ToggleButtonEx
    {
        private Image Presenter;
        private CustomEmojiIcon Icon;
        private Popup Overlay;
        protected AnimatedTextBlock Count;
        private RecentUserHeads RecentChoosers;

        public ReactionButton()
        {
            DefaultStyleKey = typeof(ReactionButton);

            Click += OnClick;
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new ReactionButtonAutomationPeer(this);
        }

        public string GetAutomationName()
        {
            if (_interaction is MessageReaction interaction)
            {
                if (interaction.Type is ReactionTypeEmoji emoji)
                {
                    return Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, emoji.Emoji);
                }
                else if (_sticker is Sticker sticker)
                {
                    return Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, string.Format(Strings.AccDescrCustomEmoji, sticker.Emoji));
                }
            }

            return null;
        }

        protected MessageViewModel _message;
        protected MessageReaction _interaction;
        private EmojiReaction _reaction;
        private Sticker _sticker;
        private UnreadReaction _unread;

        public MessageReaction Reaction => _interaction;
        public EmojiReaction EmojiReaction => _reaction;
        public Sticker CustomReaction => _sticker;

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

        public async void SetReaction(MessageViewModel message, MessageReaction interaction, EmojiReaction value)
        {
            if (Presenter == null)
            {
                _message = message;
                _interaction = interaction;
                _reaction = value;
                return;
            }

            var recycled = message.Id == _message?.Id
                && message.ChatId == _message?.ChatId
                && interaction.Type.AreTheSame(_interaction?.Type);

            _message = message;
            _interaction = interaction;
            _reaction = value;

            UpdateInteraction(message, interaction, recycled);

            var around = value?.AroundAnimation?.StickerValue;
            if (around != null && around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive && !around.Local.IsDownloadingCompleted)
            {
                _message.ClientService.DownloadFile(around.Id, 32);
            }

            var center = value?.CenterAnimation?.StickerValue;
            if (center == null || center.Id == _presenterId)
            {
                return;
            }

            _presenterId = center.Id;
            Presenter.Source = null;

            var bitmap = await EmojiCache.GetLottieFrameAsync(_message.ClientService, center, 32, 32);
            if (center.Id == _presenterId)
            {
                Presenter.Source = bitmap;
            }
        }

        public void SetReaction(MessageViewModel message, MessageReaction interaction, Sticker value)
        {
            if (Presenter == null)
            {
                _message = message;
                _interaction = interaction;
                _sticker = value;
                return;
            }

            var recycled = message.Id == _message?.Id
                && message.ChatId == _message?.ChatId
                && interaction.Type.AreTheSame(_interaction?.Type);

            _message = message;
            _interaction = interaction;
            _sticker = value;

            UpdateInteraction(message, interaction, recycled);

            if (_presenterId == value?.StickerValue.Id)
            {
                return;
            }

            _presenterId = value.StickerValue.Id;

            Icon ??= GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;
            Icon.Source = new DelayedFileSource(message.ClientService, value);
        }

        protected virtual void UpdateInteraction(MessageViewModel message, MessageReaction interaction, bool recycled)
        {
            IsChecked = interaction.IsChosen;

            if (interaction.TotalCount > interaction.RecentSenderIds.Count)
            {
                Count ??= GetTemplateChild(nameof(Count)) as AnimatedTextBlock;
                Count.Visibility = Visibility.Visible;

                Count.Text = Formatter.ShortNumber(interaction.TotalCount);

                if (RecentChoosers != null)
                {
                    RecentChoosers.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                RecentChoosers ??= GetRecentChoosers();
                RecentChoosers.Visibility = Visibility.Visible;

                var destination = RecentChoosers.Items;
                var origin = interaction.RecentSenderIds;

                if (destination.Count > 0 && recycled)
                {
                    destination.ReplaceDiff(origin);
                }
                else
                {
                    destination.Clear();
                    destination.AddRange(origin);
                }

                if (Count != null)
                {
                    Count.Visibility = Visibility.Collapsed;
                }
            }
        }

        private RecentUserHeads GetRecentChoosers()
        {
            RecentChoosers ??= GetTemplateChild(nameof(RecentChoosers)) as RecentUserHeads;
            RecentChoosers.RecentUserHeadChanged += RecentChoosers_RecentUserHeadChanged;

            return RecentChoosers;
        }

        private void RecentChoosers_RecentUserHeadChanged(ProfilePicture photo, MessageSender sender)
        {
            if (_message.ClientService.TryGetUser(sender, out Td.Api.User user))
            {
                photo.SetUser(_message.ClientService, user, 20);
            }
            else if (_message.ClientService.TryGetChat(sender, out Chat chat))
            {
                photo.SetChat(_message.ClientService, chat, 20);
            }
            else
            {
                photo.Clear();
            }
        }

        protected override void OnApplyTemplate()
        {
            Presenter = GetTemplateChild(nameof(Presenter)) as Image;
            Overlay = GetTemplateChild(nameof(Overlay)) as Popup;

            if (_sticker != null)
            {
                SetReaction(_message, _interaction, _sticker);
            }
            else if (_interaction != null)
            {
                SetReaction(_message, _interaction, _reaction);
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
            var chosen = _interaction;
            if (chosen != null && Presenter != null)
            {
                OnClick(_message, chosen);
            }


            //if (_isTag)
            //{
            //    ContextMenuRequested();
            //    return;
            //}

        }

        protected virtual void OnClick(MessageViewModel message, MessageReaction chosen)
        {
            if (chosen.IsChosen)
            {
                message.ClientService.Send(new RemoveMessageReaction(message.ChatId, message.Id, chosen.Type));
            }
            else
            {
                message.ClientService.Send(new AddMessageReaction(message.ChatId, message.Id, chosen.Type, false, false));
            }

            if (chosen.IsChosen is false)
            {
                Animate();
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
                var response = await _message.ClientService.SendAsync(new GetCustomEmojiReactionAnimations());
                if (response is Stickers stickers)
                {
                    var random = new Random();
                    var next = random.Next(0, stickers.StickersValue.Count);

                    var around = await _message.ClientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && IsLoaded && _sticker?.FullType is StickerFullTypeCustomEmoji customEmoji)
                    {
                        if (Icon != null)
                        {
                            Icon.Source = new CustomEmojiFileSource(_message.ClientService, customEmoji.CustomEmojiId);
                            Icon.Play();
                        }

                        _centerCompleted = true;
                        _aroundCompleted = false;

                        var presenter = Presenter;
                        var popup = Overlay;

                        var dispatcher = DispatcherQueue.GetForCurrentThread();

                        var aroundView = new AnimatedImage();
                        aroundView.Width = 32 * 3;
                        aroundView.Height = 32 * 3;
                        aroundView.LoopCount = 1;
                        aroundView.FrameSize = new Size(32 * 3, 32 * 3);
                        aroundView.DecodeFrameType = DecodePixelType.Logical;
                        aroundView.AutoPlay = true;
                        aroundView.Source = new LocalFileSource(around);
                        aroundView.LoopCompleted += (s, args) =>
                        {
                            dispatcher.TryEnqueue(Continue2);
                        };

                        var root = new Grid();
                        root.Width = 32 * 3;
                        root.Height = 32 * 3;
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
                _centerCompleted = false;
                _aroundCompleted = false;

                var presenter = Presenter;
                var popup = Overlay;

                var dispatcher = DispatcherQueue.GetForCurrentThread();

                var centerView = new AnimatedImage();
                centerView.Width = 32;
                centerView.Height = 32;
                centerView.LoopCount = 1;
                centerView.FrameSize = new Size(32, 32);
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
                aroundView.Width = 32 * 3;
                aroundView.Height = 32 * 3;
                aroundView.LoopCount = 1;
                aroundView.FrameSize = new Size(32 * 3, 32 * 3);
                aroundView.DecodeFrameType = DecodePixelType.Logical;
                aroundView.AutoPlay = true;
                aroundView.Source = new LocalFileSource(around);
                aroundView.LoopCompleted += (s, args) =>
                {
                    dispatcher.TryEnqueue(Continue2);
                };

                var root = new Grid();
                root.Width = 32 * 3;
                root.Height = 32 * 3;
                root.Children.Add(centerView);
                root.Children.Add(aroundView);

                popup.Child = root;
                popup.IsOpen = true;
            }
            else
            {
                if (center.Local.CanBeDownloaded && !center.Local.IsDownloadingActive)
                {
                    _message.ClientService.DownloadFile(center.Id, 32);
                }

                if (around.Local.CanBeDownloaded && !around.Local.IsDownloadingActive)
                {
                    _message.ClientService.DownloadFile(around.Id, 32);
                }
            }
        }

        private bool _centerCompleted;
        private bool _aroundCompleted;

        private void Start()
        {
            Logger.Info();

            var presenter = Presenter;
            if (presenter == null)
            {
                return;
            }

            presenter.Opacity = 0;
        }

        private void Continue1()
        {
            Logger.Info();

            _centerCompleted = true;

            if (_aroundCompleted)
            {
                Continue();
            }
        }

        private void Continue2()
        {
            Logger.Info();

            _aroundCompleted = true;

            if (_centerCompleted)
            {
                Continue();
            }
        }

        private void Continue()
        {
            var presenter = Presenter;
            var popup = Overlay;

            if (presenter == null || popup == null)
            {
                return;
            }

            presenter.Opacity = 1;

            popup.IsOpen = false;
            popup.Child = null;
        }

        protected override void OnKeyDown(KeyRoutedEventArgs e)
        {
            if (e.Key is VirtualKey.Left or VirtualKey.Right && Parent is Panel panel)
            {
                e.Handled = true;

                var index = panel.Children.IndexOf(this);

                Control control = null;
                if (e.Key == VirtualKey.Left && index > 0)
                {
                    control = panel.Children[index - 1] as Control;
                }
                else if (e.Key == VirtualKey.Right && index < panel.Children.Count - 1)
                {
                    control = panel.Children[index + 1] as Control;
                }

                control?.Focus(Windows.UI.Xaml.FocusState.Keyboard);
            }
            if (e.Key is >= VirtualKey.Left and <= VirtualKey.Down && false)
            {
                e.Handled = true;

                var direction = e.Key switch
                {
                    VirtualKey.Left => FocusNavigationDirection.Left,
                    VirtualKey.Up => FocusNavigationDirection.Up,
                    VirtualKey.Right => FocusNavigationDirection.Right,
                    VirtualKey.Down => FocusNavigationDirection.Down,
                    _ => FocusNavigationDirection.Next
                };

                FocusManager.TryMoveFocus(direction, new FindNextElementOptions { SearchRoot = Parent });
            }

            base.OnKeyDown(e);
        }

        //private CompositionPath GetClipGeometry(float width)
        //{
        //    CanvasGeometry result;
        //    using (var builder = new CanvasPathBuilder(null))
        //    {
        //        var far = 28f;

        //        var blp = width - (far - 14.4508f);
        //        var brp = width - (far - 20.1773f);
        //        var trp = width - (far - 14.4108f);

        //        var brp1trp2 = width - (far - 16.6541f);
        //        var brp2trp1 = width - (far - 18.7758f);

        //        var tipep = width - (far - 27.1917f);
        //        var tipp12 = width - (far - 28.2705f);

        //        builder.BeginFigure(5.53846f, 0);
        //        builder.AddCubicBezier(new Vector2(2.47964f, 0), new Vector2(0, 2.47964f), new Vector2(0, 5.53846f));
        //        builder.AddLine(0, 18.4638f);
        //        builder.AddCubicBezier(new Vector2(0, 21.5225f), new Vector2(2.47964f, 24.0022f), new Vector2(5.53846f, 24.0022f));
        //        builder.AddLine(blp, 24.0022f);
        //        builder.AddCubicBezier(new Vector2(brp1trp2, 24.0022f), new Vector2(brp2trp1, 22.9825f), new Vector2(brp, 21.2308f));
        //        builder.AddLine(tipep, 14.3088f);
        //        builder.AddCubicBezier(new Vector2(tipp12, 12.9603f), new Vector2(tipp12, 11.0442f), new Vector2(tipep, 9.69554f));
        //        builder.AddLine(brp, 2.77148f);
        //        builder.AddCubicBezier(new Vector2(brp2trp1, 1.01976f), new Vector2(brp1trp2, 0), new Vector2(trp, 0));
        //        builder.AddLine(5.53846f, 0);
        //        builder.EndFigure(CanvasFigureLoop.Closed);
        //        builder.AddGeometry(CanvasGeometry.CreateEllipse(null, width - (far - 17), 9 + 3, 3, 3));

        //        result = CanvasGeometry.CreatePath(builder);
        //    }
        //    return new CompositionPath(result);
        //}
    }

    public class ReactionButtonAutomationPeer : ToggleButtonAutomationPeer
    {
        private readonly ReactionButton _owner;

        public ReactionButtonAutomationPeer(ReactionButton owner)
            : base(owner)
        {
            _owner = owner;
        }

        protected override string GetNameCore()
        {
            return _owner.GetAutomationName() ?? base.GetNameCore();
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.ListItem;
        }
    }
}
