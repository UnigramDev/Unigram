//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Controls.Media;
using Telegram.Controls.Stories.Popups;
using Telegram.Converters;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Drawers;
using Telegram.ViewModels.Stories;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace Telegram.Controls.Stories
{
    public sealed partial class StoryLiveInteractionBar : UserControl
    {
        private StoryContent _content;
        private VoipGroupCall _groupCall;

        private ActiveStoriesViewModel _activeStories;

        private StoryViewModel _viewModel;
        public StoryViewModel ViewModel => _viewModel;

        private GroupCallPaidReactionService _paidReaction;

        public StoryLiveInteractionBar()
        {
            InitializeComponent();

            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Unload();
        }

        public void Unload()
        {
            _content = null;
            _activeStories?.Aggregator.Unsubscribe(this);
            _paidReaction?.Completed -= PaidReaction_Completed;
            _groupCall?.TotalStarCountChanged -= OnTotalStarCountChanged;
        }

        public void Update(StoryContent content, ActiveStoriesViewModel activeStories, StoryViewModel story)
        {
            _content = content;
            _activeStories = activeStories;
            _viewModel = story;
            _groupCall = story.GroupCall;

            if (_groupCall == null)
            {
                return;
            }

            _groupCall.TotalStarCountChanged += OnTotalStarCountChanged;
            _groupCall.PropertyChanged += OnPropertyChanged;

            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(activeStories.Session);
            MessageField.CustomEmoji = CustomEmoji;
            MessageField.DataContext = EmojiPanel.DataContext;

            if (story.GroupCall != null)
            {
                _minimumStarCount = story.GroupCall.PaidMessageStarCount;
                UpdateStars(story.GroupCall.PaidMessageStarCount);

                activeStories.Aggregator.Unsubscribe(this);
                activeStories.Aggregator.Subscribe<UpdateGroupCall>(this, Handle, EventType.GroupCall, story.GroupCall.Id);
            }
        }

        private void OnPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Call))
            {
                this.BeginOnUIThread(UpdateMessageSender);
            }
        }

        private void UpdateMessageSender()
        {
            if (_groupCall.MessageSenderId == null)
            {
                ShowHideAliasButton(false);
            }
            else
            {
                PhotoAlias.Source = ProfilePictureSource.MessageSender(ViewModel.ClientService, _groupCall.MessageSenderId);
                ShowHideAliasButton(true);
            }
        }

        private bool _aliasCollapsed = true;

        private void ShowHideAliasButton(bool show)
        {
            if (_aliasCollapsed != show)
            {
                return;
            }

            _aliasCollapsed = !show;
            ButtonAlias.Visibility = Visibility.Visible;

            var alias = ElementComposition.GetElementVisual(ButtonAlias);
            var field = ElementComposition.GetElementVisual(MessageField);

            ElementCompositionPreview.SetIsTranslationEnabled(MessageField, true);

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                field.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_aliasCollapsed)
                {
                    ButtonAlias.Visibility = Visibility.Collapsed;
                }
            };

            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(0, new Vector3(-ButtonAlias.ActualSize.X - 8, 0, 0));
            offset.InsertKeyFrame(1, new Vector3());
            offset.Duration = Constants.FastAnimation;

            var scaleShow = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scaleShow.InsertKeyFrame(0, Vector3.Zero);
            scaleShow.InsertKeyFrame(1, Vector3.One);
            scaleShow.Duration = Constants.FastAnimation;

            var opacityShow = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityShow.InsertKeyFrame(0, 0);
            opacityShow.InsertKeyFrame(1, 1);
            opacityShow.Duration = Constants.FastAnimation;

            var scaleHide = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            scaleHide.InsertKeyFrame(0, Vector3.One);
            scaleHide.InsertKeyFrame(1, Vector3.Zero);
            scaleHide.Duration = Constants.FastAnimation;

            var opacityHide = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            opacityHide.InsertKeyFrame(0, 1);
            opacityHide.InsertKeyFrame(1, 0);
            opacityHide.Duration = Constants.FastAnimation;

            alias.CenterPoint = new Vector3(16, 16, 0);
            alias.StartAnimation("Scale", show ? scaleShow : scaleHide);
            alias.StartAnimation("Opacity", show ? opacityShow : opacityHide);
            field.StartAnimation("Translation", offset);

            batch.End();
        }

        private void OnTotalStarCountChanged(VoipGroupCall sender, VoipGroupCallTotalStarCountChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateTotalStarCount(args.TotalStarCount));
        }

        private void UpdateTotalStarCount(long starCount)
        {
            ReactCount.Text = Formatter.ShortNumber(starCount);
            ReactCount.Visibility = starCount > 0
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void Handle(UpdateGroupCall update)
        {
            this.BeginOnUIThread(() => UpdateGroupCall(update));
        }

        private void UpdateGroupCall(UpdateGroupCall update)
        {
            if (_minimumStarCount != update.GroupCall.PaidMessageStarCount)
            {
                _minimumStarCount = update.GroupCall.PaidMessageStarCount;
                UpdateStars(update.GroupCall.PaidMessageStarCount);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var service = GroupCallPaidReactionService.AddPending(_activeStories.NavigationService, ViewModel.GroupCall, 1, null);
            if (service != null)
            {
                _paidReaction?.Completed -= PaidReaction_Completed;

                _paidReaction = service;
                _paidReaction.Completed += PaidReaction_Completed;

                ReactActive.Visibility = Visibility.Visible;
            }

            // TODO: proper return from GroupCall... to determine if animation should be started
            var random = new Random();
            var next = random.Next(1, 6);

            var around = TdExtensions.GetLocalFile($"Assets\\Animations\\PaidReactionAround{next}.tgs");
            if (around.Local.IsDownloadingCompleted /*&& IsConnected*/)
            {
                Animate(around);
            }
        }

        private void PaidReaction_Completed(GroupCallPaidReactionService sender, object args)
        {
            if (_paidReaction == sender)
            {
                _paidReaction.Completed -= PaidReaction_Completed;
                _paidReaction = null;

                ReactActive.Visibility = Visibility.Collapsed;
            }
        }

        private void Emoji_ItemClick(object sender, Drawers.EmojiDrawerItemClickEventArgs e)
        {
            if (e.ClickedItem is EmojiData emoji)
            {
                MessageField.InsertText(emoji.Value);
                MessageField.Focus(FocusState.Programmatic);
            }
            else if (e.ClickedItem is StickerViewModel sticker)
            {
                MessageField.InsertEmoji(sticker);
                MessageField.Focus(FocusState.Programmatic);
            }
        }

        private void MessageField_TextChanged(object sender, RoutedEventArgs e)
        {
            UpdateState();
        }

        enum SendVisibility
        {
            Collapsed,
            Visible,
            Count
        }

        private SendVisibility _sendCollapsed = SendVisibility.Collapsed;
        private int _sendGeneration;

        private async void ShowHideSendButton(SendVisibility visibility)
        {
            if (_sendCollapsed == visibility)
            {
                return;
            }

            var generation = ++_sendGeneration;
            var show = visibility != SendVisibility.Collapsed && _sendCollapsed == SendVisibility.Collapsed;
            var hide = visibility == SendVisibility.Collapsed && _sendCollapsed != SendVisibility.Collapsed;

            Button showButton = visibility switch
            {
                SendVisibility.Collapsed => Paid,
                SendVisibility.Visible => SendMessage,
                SendVisibility.Count => btnPaidMessage,
                _ => null
            };

            Button hideButton = _sendCollapsed switch
            {
                SendVisibility.Collapsed => Paid,
                SendVisibility.Visible => SendMessage,
                SendVisibility.Count => btnPaidMessage,
                _ => null
            };

            _sendCollapsed = visibility;
            showButton.Visibility = Visibility.Visible;
            hideButton.Visibility = Visibility.Visible;

            if (visibility == SendVisibility.Count)
            {
                await btnPaidMessage.UpdateLayoutAsync();
            }

            if (generation != _sendGeneration)
            {
                return;
            }

            var diff = 40 - SendRoot.ActualSize.X;

            var visualShow = ElementComposition.GetElementVisual(showButton);
            var visualHide = ElementComposition.GetElementVisual(hideButton);
            var reaction = ElementComposition.GetElementVisual(ReactRoot);
            var send = ElementComposition.GetElementVisual(SendRoot);
            var emoji = ElementComposition.GetElementVisual(Emoji);
            var background = ElementComposition.GetElementVisual(BackgroundGrid);
            var clip = visualShow.Compositor.CreateRoundedRectangleGeometry();
            clip.Size = new Vector2(ActualSize.X - 52, ActualSize.Y);
            clip.CornerRadius = new Vector2(24);

            background.Clip = visualShow.Compositor.CreateGeometricClip(clip);

            ElementCompositionPreview.SetIsTranslationEnabled(SendRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Emoji, true);

            var batch = visualShow.Compositor.CreateScopedBatch(Windows.UI.Composition.CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_sendGeneration != generation)
                {
                    return;
                }

                background.Clip = null;
                emoji.Properties.InsertVector3("Translation", Vector3.Zero);

                if (_sendCollapsed == SendVisibility.Collapsed)
                {
                    Grid.SetColumnSpan(BackgroundGrid, 1);

                    Paid.Visibility = Visibility.Visible;
                    SendMessage.Visibility = Visibility.Collapsed;
                    btnPaidMessage.Visibility = Visibility.Collapsed;
                }
                else if (_sendCollapsed == SendVisibility.Visible)
                {
                    Grid.SetColumnSpan(BackgroundGrid, 2);

                    Paid.Visibility = Visibility.Collapsed;
                    SendMessage.Visibility = Visibility.Visible;
                    btnPaidMessage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    Grid.SetColumnSpan(BackgroundGrid, 2);

                    Paid.Visibility = Visibility.Collapsed;
                    SendMessage.Visibility = Visibility.Collapsed;
                    btnPaidMessage.Visibility = Visibility.Visible;
                }
            };

            var duration = TimeSpan.FromSeconds(0.2500);

            var animShow = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            animShow.InsertKeyFrame(0, Vector3.Zero);
            animShow.InsertKeyFrame(1, Vector3.One);
            animShow.Duration = duration;

            var animHide = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            animHide.InsertKeyFrame(0, Vector3.One);
            animHide.InsertKeyFrame(1, Vector3.Zero);
            animHide.Duration = duration;

            if (show || hide)
            {
                Grid.SetColumnSpan(BackgroundGrid, 2);
                Grid.SetColumnSpan(MessagePanel, show ? 2 : 1);

                var translate = visualShow.Compositor.CreateScalarKeyFrameAnimation();
                translate.InsertKeyFrame(0, show ? -52 : 52); // 48 + 4 margin
                translate.InsertKeyFrame(1, show ? 0 : 0);
                translate.Duration = duration;

                var translate2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
                translate2.InsertKeyFrame(0, show ? -52 - diff : 52); // 48 + 4 margin
                translate2.InsertKeyFrame(1, show ? 0 : -diff);
                translate2.Duration = duration;

                var size = visualShow.Compositor.CreateScalarKeyFrameAnimation();
                size.InsertKeyFrame(show ? 0 : 1, ActualSize.X - 52 * 2);
                size.InsertKeyFrame(show ? 1 : 0, ActualSize.X - 52);
                size.Duration = duration;

                emoji.StartAnimation("Translation.X", translate2);
                send.StartAnimation("Translation.X", translate);
                clip.StartAnimation("Size.X", size);
                reaction.StartAnimation("Scale", show ? animHide : animShow);
            }
            else
            {
                var count = visibility == SendVisibility.Count;

                var translate2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
                translate2.InsertKeyFrame(0, count ? -diff : 0); // 48 + 4 margin
                translate2.InsertKeyFrame(1, count ? 0 : -diff);
                translate2.Duration = duration;

                emoji.StartAnimation("Translation.X", translate2);
            }

            visualShow.StartAnimation("Scale", animShow);
            visualHide.StartAnimation("Scale", animHide);

            batch.End();
        }

        private void MessageField_Accept(FormattedTextBox sender, System.EventArgs args)
        {
            Send();
        }

        private void Send()
        {
            if (SendMessage.IsReadOnly)
            {
                var text = MessageField.GetFormattedText();
                var customEmoji = text.Entities.Count(x => x.Type is TextEntityTypeCustomEmoji);
                var length = text.Text.Length;

                if (ViewModel.ClientService.TryGetGroupCallMinimumMessageLevel(length, customEmoji, out GroupCallMessageLevel minimumLevel))
                {
                    EditStars(Math.Max(_minimumStarCount, minimumLevel.MinStarCount));
                }
                else
                {
                    EditStars(_minimumStarCount);
                }
            }
            else
            {
                ViewModel.GroupCall.SendMessage(MessageField.GetFormattedText(true), _starCount);
                RemoveStars();
            }
        }

        private void MessageField_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void MessageField_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(MessagePanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            Send();
        }

        private long _minimumStarCount;
        private long _starCount;
        private GroupCallMessageLevel _level;

        private async void Send_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            EditStars(_starCount);
        }

        private async void Paid_Click(object sender, RoutedEventArgs e)
        {
            EditStars(_starCount);
        }

        private async void ReactButton_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var popup = new StoryReactPopup(ViewModel.ClientService, _activeStories.NavigationService, ViewModel, null, 1);
            popup.RequestedTheme = ElementTheme.Dark;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            // TODO: switch message sender

            var service = GroupCallPaidReactionService.AddPending(_activeStories.NavigationService, ViewModel.GroupCall, popup.StarCount, null);
            if (service != null)
            {
                _paidReaction?.Completed -= PaidReaction_Completed;

                _paidReaction = service;
                _paidReaction.Completed += PaidReaction_Completed;

                ReactActive.Visibility = Visibility.Visible;
            }

            // TODO: proper return from GroupCall... to determine if animation should be started
            var random = new Random();
            var next = random.Next(1, 6);

            var around = TdExtensions.GetLocalFile($"Assets\\Animations\\PaidReactionAround{next}.tgs");
            if (around.Local.IsDownloadingCompleted /*&& IsConnected*/)
            {
                Animate(around);
            }
        }

        private void Button_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var next = e.NewSize.ToVector2();
            var visual = ElementComposition.GetElementVisual(sender as UIElement);
            visual.CenterPoint = new Vector3(next / 2, 0);

            if (sender == btnPaidMessage)
            {
                visual.CenterPoint = new Vector3(40 + (next.X - 40) / 2, next.Y / 2, 0);
            }
        }

        private void Paid_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();
            if (_minimumStarCount == 0)
            {
                flyout.CreateFlyoutItem(EditStars, _starCount, Strings.LiveStoryMessageEditStars);
                flyout.CreateFlyoutItem(RemoveStars, Strings.LiveStoryMessageRemoveStars);
            }
            else
            {
                flyout.CreateFlyoutItem(EditStars, _starCount, Strings.LiveStoryMessageEditStars);
            }

            flyout.ShowAt(sender, FlyoutPlacementMode.TopEdgeAlignedRight);
        }

        private async void EditStars(long starCount)
        {
            var text = MessageField.GetFormattedText(false);
            var popup = new StoryReactPopup(ViewModel.ClientService, _activeStories.NavigationService, ViewModel, text, _minimumStarCount, starCount == 0 ? 50 : starCount);
            popup.RequestedTheme = ElementTheme.Dark;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            UpdateStars(popup.StarCount);
        }

        private void RemoveStars()
        {
            UpdateStars(0);
        }

        private void UpdateStars(long starCount)
        {
            _starCount = Math.Max(_minimumStarCount, starCount);
            _viewModel.ClientService.TryGetGroupCallMessageLevel(_starCount, out _level);

            if (starCount > 0)
            {
                btnPaidMessage.Content = Icons.Premium16 + Icons.Spacing + Formatter.ShortNumber(_starCount);
                btnPaidMessage.Background = new SolidColorBrush(_level.SecondColor.ToColor());
                btnPaidMessage.BorderBrush = new SolidColorBrush(_level.SecondColor.ToColor());
            }

            UpdateState();
        }

        private void UpdateState()
        {
            var text = MessageField.GetFormattedText();
            var starCount = _starCount;

            if (_level == null)
            {
                _viewModel.ClientService.TryGetGroupCallMessageLevel(_starCount, out _level);
            }

            var customEmoji = text.Entities.Count(x => x.Type is TextEntityTypeCustomEmoji);
            var length = text.Text.Length;

            SendMessage.IsReadOnly = customEmoji > _level.MaxCustomEmojiCount || length > _level.MaxTextLength;
            btnPaidMessage.IsReadOnly = customEmoji > _level.MaxCustomEmojiCount || length > _level.MaxTextLength;

            if (MessageField.IsEmpty)
            {
                ShowHideSendButton(SendVisibility.Collapsed);
            }
            else if (_starCount > 0)
            {
                ShowHideSendButton(SendVisibility.Count);
            }
            else
            {
                ShowHideSendButton(SendVisibility.Visible);
            }
        }

        private void Animate(File around)
        {
            var popup = ReactionPopup;
            var dispatcher = DispatcherQueue.GetForCurrentThread();

            var aroundView = new AnimatedImage();
            aroundView.Width = 48 * 3;
            aroundView.Height = 48 * 3;
            aroundView.LoopCount = 1;
            aroundView.FrameSize = new Size(48 * 3, 48 * 3);
            aroundView.DecodeFrameType = DecodePixelType.Logical;
            aroundView.IsCachingEnabled = false;
            aroundView.AutoPlay = true;
            aroundView.Source = new LocalFileSource(around);
            aroundView.LoopCompleted += (s, args) =>
            {
                dispatcher.TryEnqueue(Continue);
            };

            var root = new Grid();
            root.Width = 48 * 3;
            root.Height = 48 * 3;
            root.Children.Add(aroundView);

            popup.Child = root;
            popup.XamlRoot = XamlRoot;
            popup.IsOpen = true;
        }

        private void Continue()
        {
            Logger.Info();

            var popup = ReactionPopup;
            if (popup == null)
            {
                return;
            }

            popup.IsOpen = false;
            popup.Child = null;
        }

        private void ShowHide_Click(object sender, RoutedEventArgs e)
        {
            _content?.ShowHideMessages(ShowHideButton.IsChecked is false);
        }

        public async void SetSender(ChatMessageSender messageSender)
        {
            if (messageSender.NeedsPremium && !_activeStories.IsPremium)
            {
                await _activeStories.ShowPopupAsync(Strings.SelectSendAsPeerPremiumHint, Strings.AppName, Strings.OK);
                return;
            }

            _activeStories.ClientService.Send(new SetLiveStoryMessageSender(_groupCall.Id, messageSender.Sender));
        }

        private async void ButtonAlias_Click(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();
            flyout.Items.Add(new MenuFlyoutLabel { Text = Strings.SendMessageAsTitle });
            flyout.Closing += (s, args) =>
            {
                MessageField.Focus(FocusState.Programmatic);
            };

            var response = await ViewModel.ClientService.SendAsync(new GetLiveStoryAvailableMessageSenders(_groupCall.Id));
            if (response is ChatMessageSenders senders)
            {
                void handler(object sender, RoutedEventArgs _)
                {
                    if (sender is MenuFlyoutItem item && item.CommandParameter is ChatMessageSender messageSender)
                    {
                        item.Click -= handler;
                        SetSender(messageSender);
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

                    if (ViewModel.ClientService.TryGetUser(messageSender.Sender, out User senderUser))
                    {
                        picture.Source = ProfilePictureSource.User(ViewModel.ClientService, senderUser);

                        item.Text = senderUser.FullName();
                        item.Info = Strings.VoipGroupPersonalAccount;
                    }
                    else if (ViewModel.ClientService.TryGetChat(messageSender.Sender, out Chat senderChat))
                    {
                        picture.Source = ProfilePictureSource.Chat(ViewModel.ClientService, senderChat);

                        item.Text = senderChat.Title;

                        if (ViewModel.ClientService.TryGetSupergroup(senderChat, out Supergroup supergroup))
                        {
                            item.Info = Locale.Declension(Strings.R.Subscribers, supergroup.MemberCount);
                        }
                    }

                    flyout.Items.Add(item);
                }
            }

            flyout.ShowAt(ButtonAlias, FlyoutPlacementMode.TopEdgeAlignedLeft);
        }
    }

    public partial class StoryPaidButton : Button
    {
        public StoryPaidButton()
        {
            DefaultStyleKey = typeof(StoryPaidButton);
        }

        protected override void OnApplyTemplate()
        {
            OnReadOnlyChanged(IsReadOnly);

            base.OnApplyTemplate();
        }

        #region IsReadOnly

        public bool IsReadOnly
        {
            get { return (bool)GetValue(IsReadOnlyProperty); }
            set { SetValue(IsReadOnlyProperty, value); }
        }

        public static readonly DependencyProperty IsReadOnlyProperty =
            DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(StoryPaidButton), new PropertyMetadata(false, OnReadOnlyChanged));

        private static void OnReadOnlyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((StoryPaidButton)d).OnReadOnlyChanged((bool)e.NewValue);
        }

        private void OnReadOnlyChanged(bool newValue)
        {
            VisualStateManager.GoToState(this, newValue ? "ReadOnly" : "NotReadOnly", false);
        }

        #endregion
    }

}
