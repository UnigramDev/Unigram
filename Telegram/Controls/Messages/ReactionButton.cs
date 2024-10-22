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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Messages
{
    public partial class ReactionButton : ToggleButtonEx
    {
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
            if (_reaction is MessageReaction interaction)
            {
                if (interaction.Type is ReactionTypeEmoji emoji)
                {
                    return Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, emoji.Emoji);
                }
                else
                {
                    return Locale.Declension(Strings.R.AccDescrNumberOfPeopleReactions, interaction.TotalCount, Strings.AccDescrCustomEmoji2);
                }
            }

            return null;
        }

        protected MessageViewModel _message;
        protected MessageReaction _reaction;
        private ReactionType _reactionType;

        private UnreadReaction _unread;

        public MessageReaction Reaction => _reaction;

        public void SetUnread(UnreadReaction unread)
        {
            if (Icon == null)
            {
                _unread = unread;
            }
            else
            {
                _unread = null;

                if (unread != null)
                {
                    Animate();
                }
            }
        }

        public void SetReaction(MessageViewModel message, MessageReaction reaction)
        {
            if (Icon == null)
            {
                _message = message;
                _reaction = reaction;
                return;
            }

            var recycled = message.Id == _message?.Id
                && message.ChatId == _message?.ChatId
                && reaction.Type.AreTheSame(_reaction?.Type);

            _message = message;
            _reaction = reaction;

            UpdateInteraction(message, reaction, recycled);

            if (reaction.Type.AreTheSame(_reactionType))
            {
                return;
            }

            _reactionType = reaction.Type;

            using (Icon.BeginBatchUpdate())
            {
                var custom = reaction.Type is ReactionTypeCustomEmoji;
                var size = reaction.Type is ReactionTypeCustomEmoji or ReactionTypePaid ? 20 : 32;

                Icon.Width = Icon.Height = size;
                Icon.FrameSize = new Size(size, size);
                Icon.LoopCount = custom ? 3 : 1;
                Icon.IsCachingEnabled = reaction.Type is not ReactionTypePaid;
                Icon.IsViewportAware = custom;

                Icon.Source = new ReactionFileSource(message.ClientService, reaction.Type)
                {
                    UseCenterAnimation = true,
                    IsUnique = true
                };
            }
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
            Overlay = GetTemplateChild(nameof(Overlay)) as Popup;
            Icon = GetTemplateChild(nameof(Icon)) as CustomEmojiIcon;
            Icon.Ready += OnReady;

            if (_reaction != null)
            {
                SetReaction(_message, _reaction);
            }

            SetUnread(_unread);

            base.OnApplyTemplate();
        }

        private void OnReady(object sender, EventArgs e)
        {
            SetUnread(_unread);
        }

        protected override void OnToggle()
        {
            //base.OnToggle();
        }

        private void OnClick(object sender, RoutedEventArgs e)
        {
            var chosen = _reaction;
            if (chosen != null && Icon != null && _message?.Id != 0)
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
                Animate();
                message.ClientService.Send(new AddMessageReaction(message.ChatId, message.Id, chosen.Type, false, false));
            }
        }

        protected async void Animate()
        {
            if (_reactionType is ReactionTypeEmoji emoji)
            {
                var response = await _message.ClientService.SendAsync(new GetEmojiReaction(emoji.Emoji));
                if (response is EmojiReaction reaction && reaction.AroundAnimation != null)
                {
                    var around = await _message.ClientService.DownloadFileAsync(reaction.AroundAnimation.StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && this.IsConnected())
                    {
                        _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, true));
                    }
                }
            }
            else if (_reactionType is ReactionTypeCustomEmoji customEmoji)
            {
                var response = await _message.ClientService.SendAsync(new GetCustomEmojiReactionAnimations());
                if (response is Stickers stickers)
                {
                    var random = new Random();
                    var next = random.Next(0, stickers.StickersValue.Count);

                    var around = await _message.ClientService.DownloadFileAsync(stickers.StickersValue[next].StickerValue, 32);
                    if (around.Local.IsDownloadingCompleted && this.IsConnected())
                    {
                        _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, true));
                    }
                }
            }
            else if (_reactionType is ReactionTypePaid)
            {
                var random = new Random();
                var next = random.Next(1, 6);

                var around = TdExtensions.GetLocalFile($"Assets\\Animations\\PaidReactionAround{next}.tgs");
                if (around.Local.IsDownloadingCompleted && this.IsConnected())
                {
                    _ = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => Animate(around, false));
                }
            }
        }

        protected void Animate(File around, bool cache)
        {
            _aroundCompleted = false;
            Icon?.Play();

            var popup = Overlay;
            var dispatcher = DispatcherQueue.GetForCurrentThread();

            var aroundView = new AnimatedImage();
            aroundView.Width = 32 * 3;
            aroundView.Height = 32 * 3;
            aroundView.LoopCount = 1;
            aroundView.FrameSize = new Size(32 * 3, 32 * 3);
            aroundView.DecodeFrameType = DecodePixelType.Logical;
            aroundView.IsCachingEnabled = cache;
            aroundView.AutoPlay = true;
            aroundView.Source = new LocalFileSource(around);
            aroundView.LoopCompleted += (s, args) =>
            {
                dispatcher.TryEnqueue(Continue);
            };

            var root = new Grid();
            root.Width = 32 * 3;
            root.Height = 32 * 3;
            root.Children.Add(aroundView);

            popup.Child = root;
            popup.XamlRoot = XamlRoot;
            popup.IsOpen = true;
        }

        private bool _aroundCompleted;

        private void Continue()
        {
            Logger.Info();

            _aroundCompleted = true;

            var popup = Overlay;
            if (popup == null)
            {
                return;
            }

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

    public partial class ReactionButtonAutomationPeer : ToggleButtonAutomationPeer
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
