//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using Telegram.Common;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views.Host;
using Telegram.Views.Stars.Popups;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace Telegram.Controls.Stories.Popups
{
    public sealed partial class StoryReactPopup : ContentPopup
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;
        private readonly StoryViewModel _story;
        private readonly FormattedText _text;

        private MessageSender _selection;

        private List<PaidReactor> _reactors;
        private PaidReactor _self;
        private long _count;

        private bool _loaded;

        private TeachingTipEx _balance;

        public StoryReactPopup(IClientService clientService, INavigationService navigationService, StoryViewModel story, FormattedText text, long minimumStarCount, long starCount = 50)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;
            _story = story;
            _text = text ?? string.Empty.AsFormattedText();

            starCount = Math.Max(minimumStarCount, starCount);

            StarCountSlider.Initialize(/*_starCount =*/ starCount, minimumStarCount, clientService.Options.PaidGroupCallMessageStarCountMax);

            if (clientService.TryGetChat(story.PosterChatId, out Chat chat))
            {
                TextBlockHelper.SetMarkdown(Subtitle, string.Format(Strings.LiveStoryReactText, chat.Title));

                if (text != null)
                {
                    LevelRoot.Visibility = Visibility.Visible;
                    TopReactorsRoot.Visibility = Visibility.Collapsed;

                    _reactors = [.. Array.Empty<PaidReactor>()];
                    _selection = _story.GroupCall.MessageSenderId;

                    UpdateOrder();
                    UpdateAlias();

                }
                else
                {
                    InitializeTopDonors();
                }

                //_reactors = new List<PaidReactor>(message.InteractionInfo?.Reactions?.PaidReactors ?? Array.Empty<PaidReactor>());

                //if (_reactors.Count > 0)
                //{
                //    UpdateOrder();
                //}
                //else
                //{
                //    TopReactorsRoot.Visibility = Visibility.Collapsed;
                //}

                //Anonymous.IsChecked = !(_self?.IsAnonymous ?? (clientService.DefaultPaidReactionType is PaidReactionTypeAnonymous));

                //UpdateAlias();
            }

            Opened += OnOpened;
            Closed += OnClosed;
        }

        private async void InitializeTopDonors()
        {
            var response = await _clientService.SendAsync(new GetLiveStoryTopDonors(_story.GroupCall.Id));
            if (response is LiveStoryDonors donors)
            {
                _reactors = [.. donors.TopDonors ?? Array.Empty<PaidReactor>()];

                if (_reactors.Count > 0)
                {
                    UpdateOrder();
                }
                else
                {
                    _selection = _story.GroupCall.MessageSenderId;
                    UpdateOrder();

                    TopReactorsRoot.Visibility = Visibility.Collapsed;
                }

                UpdateAlias();

                StarCountSlider_ValueChanged(null, null);
            }
        }

        private void OnOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            if (XamlRoot.Content is not IToastHost host)
            {
                return;
            }

            var markdown = ClientEx.ParseMarkdown(Strings.Gift2MessageStarsInfoLink);

            var hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(markdown.Text);
            hyperlink.UnderlineStyle = UnderlineStyle.None;
            hyperlink.Click += Buy_Click;

            var content = new TextBlock();
            content.Inlines.Add(string.Format(Strings.Gift2MessageStarsInfo.ReplaceStar(Icons.Premium), _clientService.OwnedStarCount.ToValue()));
            content.Inlines.Add(new LineBreak());
            content.Inlines.Add(hyperlink);
            content.HorizontalTextAlignment = TextAlignment.Center;
            content.FontFamily = BootStrapper.Current.Resources["EmojiThemeFontFamilyWithSymbols"] as FontFamily;
            content.Style = BootStrapper.Current.Resources["CaptionTextBlockStyle"] as Style;
            content.Margin = new Thickness(0, -8, 0, -6);

            var popup = new TeachingTipEx
            {
                Content = content,
                PreferredPlacement = Microsoft.UI.Xaml.Controls.TeachingTipPlacementMode.Top,
                MinWidth = 0,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                IsLightDismissEnabled = false,
                ShouldConstrainToRootBounds = true,
                RequestedTheme = ElementTheme.Dark,
                XamlRoot = XamlRoot
            };

            AutomationProperties.SetName(popup, "title");

            popup.Closed += (s, args) =>
            {
                host.ToastClosed(s);
            };

            host.ToastOpened(popup);
            popup.IsOpen = true;

            _balance = popup;
        }

        private void Buy_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            Hide();
            _navigationService.ShowPopup(new BuyPopup());
        }

        private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            _balance?.IsOpen = false;
        }

        private void StarCountSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_self != null && _starCount != StarCount)
            {
                UpdateOrder();

                Preview.Update(_clientService, new GroupCallMessage(0, _self.SenderId, 0, _text, StarCount, false, false), 0);

                if (_clientService.TryGetGroupCallMessageLevel(StarCount, out GroupCallMessageLevel level))
                {
                    StarCountSlider.Foreground = new SolidColorBrush(level.SecondColor.ToColor());

                    Duration.Text = Locale.FormatShortTime(level.PinDuration);
                    Length.Text = level.MaxTextLength.ToString();
                    Emoji.Text = level.MaxCustomEmojiCount.ToString();
                }

                _starCount = StarCount;

                if (PurchaseText != null)
                {
                    PurchaseText.Text = string.Format(Strings.StarsReactionSend.ReplaceStar(Icons.Premium), StarCount.ToString("N0"));
                    AutomationProperties.SetName(PurchaseCommand, PurchaseText.Text);
                }
            }
        }

        private void Anonymous_Click(object sender, RoutedEventArgs e)
        {
            if (_self != null)
            {
                UpdateOrder();
            }
        }

        private void UpdateOrder()
        {
            if (_self == null)
            {
                _self = _reactors.FirstOrDefault(x => x.IsMe);

                if (_self == null)
                {
                    if (_selection != null)
                    {
                        _self = new PaidReactor(_selection, 0, false, true, false);
                    }
                    else if (_clientService.DefaultPaidReactionType is PaidReactionTypeChat reactionTypeChat)
                    {
                        _self = new PaidReactor(new MessageSenderChat(reactionTypeChat.ChatId), 0, false, true, false);
                    }
                    else
                    {
                        _self = new PaidReactor(_clientService.MyId, 0, false, true, false);
                    }
                }

                _count = _self.StarCount;
            }
            else
            {
                _self.StarCount = _count + StarCount;
                _self.IsAnonymous = false;
            }

            _reactors.Remove(_self);

            var missing = true;

            for (int i = 0; i < _reactors.Count; i++)
            {
                if (_self.StarCount > _reactors[i].StarCount)
                {
                    _reactors.Insert(i, _self);
                    missing = false;
                    break;
                }
            }

            if (missing)
            {
                _reactors.Add(_self);
            }

            TopReactors.UpdateMessageReactions(_clientService, _reactors, _self);
        }

        private long _starCount;
        public long StarCount => StarCountSlider.RealValue;

        public PaidReactionType Type
        {
            get
            {
                if (_self?.SenderId is MessageSenderChat messageSenderChat)
                {
                    return new PaidReactionTypeChat(messageSenderChat.ChatId);
                }

                return new PaidReactionTypeRegular();
            }
        }

        private void Purchase_Click(object sender, Windows.UI.Xaml.RoutedEventArgs e)
        {
            Hide(ContentDialogResult.Primary);
        }

        private void SettingsFooter_Click(object sender, TextUrlClickEventArgs e)
        {
            MessageHelper.OpenUrl(null, null, Strings.StarsReactionTermsLink);
        }

        private void UpdateAlias()
        {
            var senderId = _self?.SenderId ?? _selection;
            if (senderId == null)
            {
                senderId = _clientService.DefaultPaidReactionType switch
                {
                    PaidReactionTypeChat paidReactionTypeChat => new MessageSenderChat(paidReactionTypeChat.ChatId),
                    _ => _clientService.MyId
                };
            }

            if (_clientService.TryGetUser(senderId, out User senderUser))
            {
                Photo.Source = ProfilePictureSource.User(_clientService, senderUser);
            }
            else if (_clientService.TryGetChat(senderId, out Chat senderChat))
            {
                Photo.Source = ProfilePictureSource.Chat(_clientService, senderChat);
            }
        }

        private async void Alias_Click(object sender, RoutedEventArgs e)
        {
            if (_story.Content is not StoryContentLive live)
            {
                return;
            }

            var flyout = new MenuFlyout();

            var response = await _clientService.SendAsync(new GetLiveStoryAvailableMessageSenders(live.GroupCallId));
            if (response is ChatMessageSenders senders)
            {
                void handler(object sender, RoutedEventArgs _)
                {
                    if (sender is MenuFlyoutItem item && item.CommandParameter is MessageSender messageSender)
                    {
                        item.Click -= handler;

                        if (_self != null)
                        {
                            _self.SenderId = messageSender;
                        }
                        else
                        {
                            _selection = messageSender;
                        }

                        UpdateAlias();
                        UpdateOrder();
                    }
                }

                foreach (var messageSender in senders.Senders)
                {
                    var picture = new ProfilePicture();
                    picture.Size = 36;
                    picture.Margin = new Thickness(-4, -2, 0, -2);

                    var item = new MenuFlyoutProfile();
                    item.Click += handler;
                    item.CommandParameter = messageSender;
                    item.Style = BootStrapper.Current.Resources["SendAsMenuFlyoutItemStyle"] as Style;
                    item.Icon = new FontIcon();
                    item.Tag = picture;

                    if (_clientService.TryGetUser(messageSender.Sender, out User senderUser))
                    {
                        picture.Source = ProfilePictureSource.User(_clientService, senderUser);

                        item.Text = senderUser.FullName();
                        item.Info = Strings.VoipGroupPersonalAccount;
                    }
                    else if (_clientService.TryGetChat(messageSender.Sender, out Chat senderChat))
                    {
                        picture.Source = ProfilePictureSource.Chat(_clientService, senderChat);

                        item.Text = senderChat.Title;

                        if (_clientService.TryGetSupergroup(senderChat, out Supergroup supergroup))
                        {
                            item.Info = Locale.Declension(Strings.R.Subscribers, supergroup.MemberCount);
                        }
                    }

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(Alias, FlyoutPlacementMode.TopEdgeAlignedLeft);
        }
    }

    public partial class PaidReactorsPanel : Panel, IDiffEqualityComparer<PaidReactor>
    {
        private readonly Dictionary<PaidReactor, PaidReactorCell> _cache = new();

        private PaidReactor[] _prevValue;

        private int _offset;

        public void UpdateMessageReactions(IClientService clientService, IList<PaidReactor> reactors, PaidReactor self)
        {
            if (reactors == null)
            {
                _prevValue = null;

                _cache.Clear();
                Children.Clear();
            }

            if (reactors?.Count > 0)
            {
                void UpdateItem(PaidReactor oldItem, PaidReactor newItem, int index = 0)
                {
                    if (newItem != null)
                    {
                        oldItem.IsAnonymous = newItem.IsAnonymous;
                        oldItem.IsMe = newItem.IsMe;
                        oldItem.IsTop = newItem.IsTop;
                        oldItem.StarCount = newItem.StarCount;
                    }

                    //var changed = Animate(oldItem.Type);
                    UpdateButton(clientService, oldItem, index);
                }

                _offset = self?.StarCount > 0 && reactors?.Count < 4 ? 1 : 0;

                if (_prevValue == null)
                {
                    for (int i = 0; i < reactors.Count; i++)
                    {
                        UpdateItem(reactors[i], null, i);
                    }
                }
                else
                {
                    // PERF: run diff asynchronously?
                    var prev = _prevValue ?? Array.Empty<PaidReactor>();
                    var diff = DiffUtil.CalculateDiff(prev, reactors, this, Constants.DiffOptions);

                    foreach (var step in diff.Steps)
                    {
                        if (step.Status == DiffStatus.Add)
                        {
                            UpdateItem(step.Items[0].NewValue, null, step.NewStartIndex);
                        }
                        else if (step.Status == DiffStatus.Move && step.OldStartIndex < Children.Count && step.NewStartIndex < Children.Count)
                        {
                            UpdateItem(step.Items[0].OldValue, step.Items[0].NewValue, step.NewStartIndex);
                            Children.Move((uint)step.OldStartIndex, (uint)step.NewStartIndex);
                        }
                        else if (step.Status == DiffStatus.Remove && step.OldStartIndex < Children.Count)
                        {
                            if (step.Items[0].OldValue is PaidReactor oldReaction)
                            {
                                _cache.Remove(oldReaction);
                            }

                            Children.RemoveAt(step.OldStartIndex);
                        }
                    }

                    foreach (var item in diff.NotMovedItems)
                    {
                        UpdateItem(item.OldValue, item.NewValue, item.NewSeqIndex);
                    }
                }

                _prevValue = reactors.ToArray();
            }
        }

        private void UpdateButton(IClientService clientService, PaidReactor item, int index)
        {
            var button = GetOrCreateButton(item, index);
            button.UpdateCell(clientService, item, index + 1, true);
            //button.SetReaction(message, item);

            //if (animate)
            //{
            //    button.SetUnread(new UnreadReaction(item.Type, null, false));
            //}
        }

        private PaidReactorCell GetOrCreateButton(PaidReactor key, int index)
        {
            if (_cache.TryGetValue(key, out PaidReactorCell button))
            {
                return button;
            }

            //button = isTag
            //    ? new ReactionAsTagButton()
            //    : key is ReactionTypePaid
            //    ? new ReactionAsPaidButton()
            //    : new ReactionButton();

            button = new PaidReactorCell();

            _cache[key] = button;
            Children.Insert(Math.Min(index, Children.Count), button);

            return button;
        }

        public bool CompareItems(PaidReactor oldItem, PaidReactor newItem)
        {
            return oldItem == newItem;

            if (oldItem.IsMe)
            {
                return newItem.IsMe;
            }
            else if (oldItem.SenderId != null)
            {
                return oldItem.SenderId.AreTheSame(newItem.SenderId);
            }
            else if (oldItem.IsAnonymous)
            {
                return newItem.IsAnonymous && oldItem.StarCount == newItem.StarCount;
            }

            return false;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var width = (availableSize.Width - 48) / (Children.Count - 1 + _offset);
            var height = 0d;

            for (int i = 0; i < Children.Count; i++)
            {
                Children[i].Measure(new Size(width, availableSize.Height));
                height = Math.Max(height, Children[i].DesiredSize.Height);
            }

            return new Size(availableSize.Width, height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var width = (finalSize.Width - 48) / (Children.Count - 1 + _offset);

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var center = (width - child.DesiredSize.Width) / 2;

                var j = i; // - 1;
                if (j < 0)
                {
                    child.Arrange(new Rect(-child.DesiredSize.Width - 12, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else if (j >= Children.Count - 1 + _offset)
                {
                    child.Arrange(new Rect(finalSize.Width + 12, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
                else
                {
                    child.Arrange(new Rect(j * width + 24 + center, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
            }

            return finalSize;
        }
    }

    public partial class SteppedValue
    {
        public double progress = 0;
        public double aprogress;
        public int steps;
        public int[] stops;

        public SteppedValue(int steps, IList<int> stops)
        {
            this.steps = steps;
            this.stops = stops.ToArray();
        }

        public void setValue(int value)
        {
            setValue(value, false);
        }
        public void setValue(int value, bool byScroll)
        {
            this.progress = getProgress(value);
            if (!byScroll)
            {
                this.aprogress = this.progress;
            }
            //updateText(true);
        }

        public int getValue()
        {
            return getValue(progress);
        }

        public double getProgress()
        {
            return progress;
        }

        public int getValue(double progress)
        {
            if (progress <= 0f) return stops[0];
            if (progress >= 1f) return stops[stops.Length - 1];
            double scaledProgress = progress * (stops.Length - 1);
            int index = (int)scaledProgress;
            double localProgress = scaledProgress - index;
            return (int)Math.Round(stops[index] + localProgress * (stops[index + 1] - stops[index]));
        }

        public float getProgress(int value)
        {
            for (int i = 1; i < stops.Length; ++i)
            {
                if (value <= stops[i])
                {
                    float local = (float)(value - stops[i - 1]) / (stops[i] - stops[i - 1]);
                    return (i - 1 + local) / (stops.Length - 1);
                }
            }
            return 1f;
        }
    }
}
