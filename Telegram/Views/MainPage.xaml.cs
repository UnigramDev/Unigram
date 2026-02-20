//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Gallery;
using Telegram.Controls.Media;
using Telegram.Controls.Messages;
using Telegram.Controls.Views;
using Telegram.Native;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Calls;
using Telegram.Services.Updates;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Create;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Telegram.Views.Settings;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.UI.Composition;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation.Peers;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace Telegram.Views
{
    public sealed partial class MainPage : CorePage, IRootContentPage, INavigatingPage, IChatListDelegate
    {
        private MainViewModel _viewModel;
        public MainViewModel ViewModel => _viewModel ??= DataContext as MainViewModel;

        public RootPage Root { get; set; }

        private readonly IClientService _clientService;

        private readonly DispatcherTimer _memoryUsageTimer;
        private double _memoryUsage;

        private bool _unloaded;

        public MainPage()
        {
            InitializeComponent();
            DataContext = LifetimeService.Current.ActiveItem.Resolve<MainViewModel>();

            _clientService = ViewModel.ClientService;

            ViewModel.Chats.Delegate = this;
            LifetimeService.Current.Playback.SourceChanged += OnPlaybackSourceChanged;

            InitializeLock();

            UpdateChatFolders();

            VisualUtilities.DropShadow(UpdateShadow);

            RootGrid.CreateInsetClip(0, -40, 0, 0);

            ChatsList.RegisterPropertyChangedCallback(ListViewBase.SelectionModeProperty, List_SelectionModeChanged);

            //var show = !((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats;
            //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            ArchivedChatsPanel.Visibility = ViewModel.Settings.Settings.HideArchivedChats
                ? Visibility.Collapsed
                : Visibility.Visible;

            ElementCompositionPreview.SetIsTranslationEnabled(ManagePanel, true);
            ElementCompositionPreview.SetIsTranslationEnabled(DialogsPanel, true);

            if (SettingsService.Current.Diagnostics.ShowMemoryUsage)
            {
                _memoryUsageTimer = new DispatcherTimer();
                _memoryUsageTimer.Interval = TimeSpan.FromSeconds(1);
                _memoryUsageTimer.Tick += MemoryUsageTimer_Tick;
                _memoryUsageTimer.Start();

                MemoryUsageTimer_Tick(null, null);
            }

            if (Constants.DEBUG)
            {
                FocusManager.GettingFocus += OnGettingFocus;
            }
        }

        private void OnGettingFocus(object sender, GettingFocusEventArgs args)
        {
            Logger.Info(string.Format("New: {0}, Old: {1}, {2}, {3} ~> {4}",
                args.NewFocusedElement?.GetType().Name ?? "null",
                args.OldFocusedElement?.GetType().Name ?? "null",
                args.Direction, args.InputDevice, args.FocusState));
        }

        private readonly int[] _lastCollectionCount = new int[3]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2)
        };

        private string PollGC()
        {
            var occurred = GarbageCollectionMonitor.Debug();
            var first = true;

            for (int i = 0; i <= 2; i++)
            {
                int count = GC.CollectionCount(i);
                if (count != _lastCollectionCount[i])
                {
                    _lastCollectionCount[i] = count;

                    if (first)
                        occurred += " (";

                    first = false;
                    occurred += i.ToString();
                }
            }

            if (!first)
                occurred += ")";

            return occurred;
        }

        private void MemoryUsageTimer_Tick(object sender, object e)
        {
            var memoryUsage = Math.Round(Windows.System.MemoryManager.AppMemoryUsage / 1024.0 / 1024.0);
            var occurred = PollGC();

            //var currentProcess = HeapSizeCalculator.GetHeapSizes(true);
            //double unmanaged = currentProcess.NativeHeap / 1024.0 / 1024.0;
            double managed = GC.GetTotalMemory(false) / 1024.0 / 1024.0; // currentProcess.ManagedHeap / 1024.0 / 1024.0;

            if (MasterDetail?.NavigationService?.Frame?.Content is ChatPage page)
            {
                MemoryLabel.Text = $"- {memoryUsage:F0} MB, {managed:F0} MB" + occurred + page.View.GetVirtualizationInfo();
            }
            else if (memoryUsage != _memoryUsage)
            {
                MemoryLabel.Text = $"- {memoryUsage:F0} MB, {managed:F0} MB" + occurred;
            }

            _memoryUsage = memoryUsage;
        }

        public INavigationService NavigationService => MasterDetail.NavigationService;

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            Initialize();
            NavigationService.Window.SetTitleBar(TitleBarHandle);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            NavigationService.Window.SetTitleBar(null);
        }

        public void Dispose()
        {
            try
            {
                Bindings.StopTracking();

                var viewModel = _viewModel;
                if (viewModel != null)
                {
                    viewModel.Settings.Delegate = null;
                    viewModel.Chats.Delegate = null;
                    viewModel.Topics.Delegate = null;

                    viewModel.Aggregator.Unsubscribe(this);
                    viewModel.Dispose();
                }

                LifetimeService.Current.Playback.SourceChanged -= OnPlaybackSourceChanged;

                MasterDetail.NavigationService.FrameFacade.Navigating -= OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated -= OnNavigated;
                MasterDetail.Dispose();
                SettingsView?.Dispose();

                if (_memoryUsageTimer != null)
                {
                    _memoryUsageTimer.Tick -= MemoryUsageTimer_Tick;
                    _memoryUsageTimer.Stop();
                }

                if (Constants.DEBUG)
                {
                    FocusManager.GettingFocus -= OnGettingFocus;
                }
            }
            catch { }
        }

        protected override void OnLayoutMetricsChanged(SystemOverlayMetrics metrics)
        {
            TitleBarrr.ColumnDefinitions[0].Width = new GridLength(metrics.LeftInset > 0 ? 138 : 0, GridUnitType.Pixel);
            TitleBarrr.ColumnDefinitions[4].Width = new GridLength(metrics.RightInset > 0 ? 138 : 0, GridUnitType.Pixel);

            Grid.SetColumn(TitleBarLogo, metrics.LeftInset > 0 ? 3 : 1);
            TitleText.FlowDirection = metrics.LeftInset > 0
                ? FlowDirection.RightToLeft
                : FlowDirection.LeftToRight;

            Photo.HorizontalAlignment = metrics.LeftInset > 0
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;

            UpdateTitleBarMargins();

            Stories.SystemOverlayLeftInset = metrics.LeftInset > 0 ? 138 : 0;
            Stories.SystemOverlayRightInset = metrics.RightInset > 0 ? 138 : 0;
        }

        private void UpdateTitleBarMargins()
        {
            var pad = MasterDetail.MasterVisibility != Visibility.Visible || !_tabsLeftCollapsed;
            var left = pad ? 14 : 48;
            var right = pad ? -50 : 10;

            if (TitleText.FlowDirection == FlowDirection.LeftToRight)
            {
                TitleBarrr.Margin = new Thickness(left, TitleBarrr.Margin.Top, right, TitleBarrr.Margin.Bottom);
            }
            else
            {
                TitleBarrr.Margin = new Thickness(right, TitleBarrr.Margin.Top, left, TitleBarrr.Margin.Bottom);
            }
        }

        private void InitializeLock()
        {
            Lock.Visibility = ViewModel.Passcode.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #region Handle

        public void UpdateChatLastMessage(Chat chat)
        {
            Handle(chat, (chatView, chat) =>
            {
                chatView.UpdateChatReadInbox(chat);
                chatView.UpdateChatLastMessage(chat);
            });
        }

        public void Handle(UpdateChatActiveStories update)
        {
            if (update.ActiveStories.List is StoryListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive()));
            }
            else
            {
                Handle(update.ActiveStories.ChatId, (chatView, chat) => chatView.UpdateChatActiveStories(update.ActiveStories));
            }
        }

        public void Handle(UpdateFileDownloads update)
        {
            this.BeginOnUIThread(() => UpdateFileDownloads(update));
        }

        private void UpdateFileDownloads(UpdateFileDownloads update)
        {
            if (update.TotalSize > 0)
            {
                FindName(nameof(Downloads));
            }

            Downloads?.UpdateFileDownloads(update);
        }

        public void Handle(UpdateChatIsMarkedAsUnread update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
        }

        public void Handle(UpdateChatReadInbox update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
        }

        public void Handle(UpdateChatUnreadTopicCount update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadInbox(chat));
        }

        public void Handle(UpdateChatReadOutbox update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatReadOutbox(chat));
        }

        public void Handle(UpdateChatUnreadMentionCount update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateChatUnreadReactionCount update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateChatAddedToList update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatChatLists(chat));
        }

        public void Handle(UpdateChatRemovedFromList update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatChatLists(chat));
        }

        public void Handle(UpdateChatTitle update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatTitle(chat));

            // TODO: threading is not great here
            if (update.ChatId == _viewModel.Topics.Chat?.Id)
            {
                this.BeginOnUIThread(() => TopicListPresenter?.UpdateChatTitle(_viewModel.Topics.Chat));
            }
        }

        public void Handle(UpdateChatPhoto update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatPhoto(chat));
        }

        public void Handle(UpdateChatEmojiStatus update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatEmojiStatus(chat));

            // TODO: threading is not great here
            if (update.ChatId == _viewModel.Topics.Chat?.Id)
            {
                this.BeginOnUIThread(() => TopicListPresenter?.UpdateChatEmojiStatus(_viewModel.Topics.Chat));
            }
        }

        public void Handle(UpdateChatVideoChat update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatVideoChat(chat));
        }

        public void Handle(UpdateChatViewAsTopics update)
        {
            // TODO: threading is not great here
            // TODO: ignore if chatId is saved messages
            if (update.ChatId == _viewModel.Topics.Chat?.Id && !update.ViewAsTopics)
            {
                this.BeginOnUIThread(() => HideTopicList());
            }
            else if (update.ChatId == _viewModel.Chats.SelectedItem && update.ViewAsTopics && update.ChatId != _viewModel.ClientService.Options.MyId)
            {
                this.BeginOnUIThread(() => ShowTopicList(_viewModel.ClientService.GetChat(update.ChatId)));
            }
        }

        public void Handle(UpdateUser update)
        {
            if (update.User.Id == _clientService.Options.MyId)
            {
                this.BeginOnUIThread(() => UpdateUser(update.User));
            }
        }

        public void Handle(UpdateUserStatus update)
        {
            if (update.UserId != _clientService.Options.MyId && update.UserId != 777000 && _clientService.TryGetChatFromUser(update.UserId, out long chatId))
            {
                Handle(chatId, (chatView, chat) => chatView.UpdateUserStatus(chat, update.Status));
            }
        }

        public void Handle(UpdateChatMessageAutoDeleteTime update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatMessageAutoDeleteTime(chat, true));
        }

        public void Handle(UpdateChatAction update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatActions(chat, ViewModel.ClientService.GetChatActions(chat.Id)));
        }

        public void Handle(UpdateMessageMentionRead update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public void Handle(UpdateMessageUnreadReactions update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatUnreadMentionCount(chat));
        }

        public async void Handle(UpdateSecretChat update)
        {
            var response = await _clientService.SendAsync(new CreateSecretChat(update.SecretChat.Id));
            if (response is Chat result)
            {
                Handle(result.Id, (chatView, chat) => chatView.UpdateChatLastMessage(chat));
            }
        }

        public void Handle(UpdateChatNotificationSettings update)
        {
            Handle(update.ChatId, (chatView, chat) => chatView.UpdateChatNotificationSettings(chat));
        }

        public void Handle(UpdateUnreadChatCount update)
        {
            if (update.ChatList is ChatListArchive)
            {
                this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ClientService, update.ChatList));
            }
        }

        private void Handle(long chatId, long messageId, Action<Chat> update, Action<ChatCell, Chat> action)
        {
            var chat = _clientService.GetChat(chatId);
            if (chat.LastMessage == null || chat.LastMessage.Id != messageId)
            {
                return;
            }

            update(chat);

            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetCell(chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(long chatId, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetChatAndCell(chatId, out Chat chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        private void Handle(Chat chat, Action<ChatCell, Chat> action)
        {
            this.BeginOnUIThread(() =>
            {
                if (ChatsList.TryGetCell(chat, out ChatCell chatView))
                {
                    action(chatView, chat);
                }
            });
        }

        public void Handle(UpdatePasscodeLock update)
        {
            this.BeginOnUIThread(() =>
            {
                Lock.Visibility = update.IsEnabled ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        public void Handle(UpdateConfetti update)
        {
            this.BeginOnUIThread(() =>
            {
                FindName(nameof(Confetti));
                Confetti.Start();
            });
        }

        public void Handle(UpdateUnconfirmedSession update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.Session == null)
                {
                    UnloadObject(UnconfirmedCard);

                    SetBirthdateCard?.Visibility = Visibility.Visible;
                }
                else
                {
                    FindName(nameof(UnconfirmedCard));
                    UnconfirmedCard.Update(update.Session);

                    SetBirthdateCard?.Visibility = Visibility.Collapsed;
                }
            });
        }

        public void Handle(UpdateFreezeState update)
        {
            this.BeginOnUIThread(() =>
            {
                if (update.IsFrozen)
                {
                    FindName(nameof(FrozenCard));

                    SetBirthdateCard?.Visibility = Visibility.Collapsed;

                    UnconfirmedCard?.Visibility = Visibility.Collapsed;
                }
                else
                {
                    UnloadObject(FrozenCard);

                    SetBirthdateCard?.Visibility = Visibility.Visible;

                    UnconfirmedCard?.Visibility = Visibility.Visible;
                }
            });
        }

        public void Handle(UpdateConnectionState update)
        {
            this.BeginOnUIThread(() =>
            {
                SetProxyVisibility(_clientService.Options.ExpectBlocking, _clientService.Options.EnabledProxyId, update.State);

                switch (update.State)
                {
                    case ConnectionStateWaitingForNetwork waitingForNetwork:
                        ShowState(Strings.WaitingForNetwork);
                        break;
                    case ConnectionStateConnecting connecting:
                        ShowState(Strings.Connecting);
                        break;
                    case ConnectionStateConnectingToProxy connectingToProxy:
                        ShowState(Strings.ConnectingToProxy);
                        break;
                    case ConnectionStateUpdating updating:
                        ShowState(Strings.Updating);
                        break;
                    case ConnectionStateReady ready:
                        HideState();
                        return;
                }
            });
        }

        public void Handle(UpdateSuggestedActions update)
        {
            this.BeginOnUIThread(() =>
            {
                if (_clientService.HasSuggestedAction(new SuggestedActionSetBirthdate()))
                {
                    FindName(nameof(SetBirthdateCard));
                }
                else
                {
                    UnloadObject(SetBirthdateCard);
                }
            });
        }

        public void Handle(UpdateOption update)
        {
            if (update.Name == OptionsService.R.ExpectBlocking || update.Name == OptionsService.R.EnabledProxyId)
            {
                this.BeginOnUIThread(() => SetProxyVisibility(_clientService.Options.ExpectBlocking, _clientService.Options.EnabledProxyId, _clientService.ConnectionState));
            }
        }

        private void SetProxyVisibility(bool expectBlocking, long proxyId, ConnectionState connectionState)
        {
            if (expectBlocking || proxyId != 0)
            {
                Proxy.Visibility = Visibility.Visible;
            }
            else
            {
                switch (connectionState)
                {
                    case ConnectionStateWaitingForNetwork:
                    case ConnectionStateConnecting:
                    case ConnectionStateConnectingToProxy:
                        Proxy.Visibility = Visibility.Visible;
                        break;
                    default:
                        Proxy.Visibility = Visibility.Collapsed;
                        break;
                }
            }

            Proxy.Glyph = connectionState is ConnectionStateReady && proxyId != 0 ? Icons.ShieldCheckmark : Icons.ShieldError;
        }

        private void ShowState(string text)
        {
            State.IsIndeterminate = true;
            StateLabel.Text = text;

            var peer = FrameworkElementAutomationPeer.FromElement(TitleText);
            peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);

            try
            {
                NavigationService.Window.Title = text;
            }
            catch { }
        }

        private void HideState()
        {
            State.IsIndeterminate = false;
            StateLabel.Text = Constants.RELEASE
                ? Strings.AppDisplayName
                : Strings.AppName;

            try
            {
                NavigationService.Window.Title = string.Empty;
            }
            catch { }
        }

        public void Handle(UpdateActiveCall update)
        {
            void UpdatePlaybackHidden(bool hidden)
            {
                Playback?.IsHidden = hidden;
            }

            this.BeginOnUIThread(() =>
            {
                var call = ViewModel.VoipService.ActiveCall;
                if (call != null)
                {
                    UpdatePlaybackHidden(true);
                    FindName(nameof(CallBanner));

                    CallBanner.Update(call);

                    _activeCallVisible = true;
                    MasterDetail.ShowHideBanner(_activeCallVisible || _playbackVisible);
                }
                else
                {
                    UpdatePlaybackHidden(false);

                    CallBanner?.Update(null);

                    _activeCallVisible = false;
                    MasterDetail.ShowHideBanner(_activeCallVisible || _playbackVisible);
                }
            });
        }

        private void OnBannerCollapsed(object sender, EventArgs e)
        {
            if (CallBanner != null && !_activeCallVisible)
            {
                CallBanner.Update(null);
                UnloadObject(CallBanner);
            }
        }

        public void Handle(UpdateChatFoldersLayout update)
        {
            this.BeginOnUIThread(UpdateChatFoldersLayout);
        }

        #endregion

        private bool _tabsTopCollapsed = true;
        private bool _tabsLeftCollapsed = true;

        private void ShowHideTopTabs(bool show)
        {
            if (_tabsTopCollapsed != show)
            {
                return;
            }

            _tabsTopCollapsed = !show;
            FindName(nameof(ChatTabs));

            if (TopicListPresenter != null)
            {
                var padding = ChatTabs != null
                    ? _tabsTopCollapsed ? -74 : -78
                    : -12;

                TopicListPresenter.Margin = new Thickness(68, padding, 0, 0);
            }

            Stories.TabsTopCollapsed = !show;
            Stories.ChatTabs = ChatTabs;
            Stories.ControlledList = ChatsList;

            void ShowHideTopTabsCompleted()
            {
                DialogsPanel.Margin = new Thickness();
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
                ChatTabs.Visibility = _tabsTopCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideTopTabsCompleted();
                return;
            }

            var topPadding = Stories.GetTopPadding(false);

            ChatTabs.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, topPadding, 0, 0);
            DialogsPanel.Margin = new Thickness(0, 0, 0, -40);

            var visual = ElementComposition.GetElementVisual(DialogsPanel);
            var header = ElementComposition.GetElementVisual(ChatTabsView);
            header.Clip = visual.Compositor.CreateInsetClip();

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ShowHideTopTabsCompleted();
            };

            var offset1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, -36);
            offset1.InsertKeyFrame(show ? 1 : 0, 0);
            //offset.Duration = Constants.FastAnimation;

            var offset2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            offset2.InsertKeyFrame(show ? 0 : 1, 36);
            offset2.InsertKeyFrame(show ? 1 : 0, 0);
            //offset.Duration = Constants.FastAnimation;

            header.Clip.StartAnimation("TopInset", offset2);
            visual.StartAnimation("Offset.Y", offset1);

            batch.End();
        }

        private void ShowHideLeftTabs(bool show)
        {
            if (_tabsLeftCollapsed != show)
            {
                return;
            }

            _tabsLeftCollapsed = !show;
            FindName(nameof(ChatTabsLeft));

            Root?.SetSidebarEnabled(show);

            Stories.TabsLeftCollapsed = !show;

            UpdateTitleBarMargins();

            Photo.Width = show ? 72 : 48;
            Photo.Visibility = show || MasterDetail.MasterVisibility == Visibility.Visible
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (ChatTabsLeft == null)
            {
                FindName(nameof(ChatTabsLeft));
            }

            void ShowHideLeftTabsCompleted()
            {
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
                ChatTabsLeft.Visibility = _tabsLeftCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            ShowHideLeftTabsCompleted();
            return;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideLeftTabsCompleted();
                return;
            }

            ChatTabsLeft.Visibility = Visibility.Visible;
            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -40);

            var parent = ElementComposition.GetElementVisual(ChatsList);

            var visual = ElementComposition.GetElementVisual(element);
            var header = ElementComposition.GetElementVisual(ChatTabsView);

            parent.Clip = null;

            var batch = visual.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                header.Offset = new Vector3();
                visual.Offset = new Vector3();

                ShowHideLeftTabsCompleted();
            };

            var offset = visual.Compositor.CreateVector3KeyFrameAnimation();
            offset.InsertKeyFrame(show ? 0 : 1, new Vector3(0, -40, 0));
            offset.InsertKeyFrame(show ? 1 : 0, new Vector3());
            //offset.Duration = Constants.FastAnimation;

            var opacity1 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration /= 2;

            var opacity2 = visual.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration /= 2;

            header.StartAnimation("Offset", offset);
            visual.StartAnimation("Offset", offset);

            batch.End();
        }

        public void OnBackRequesting(BackRequestedRoutedEventArgs args)
        {
            if (Root?.IsPaneOpen is true)
            {
                Root.IsPaneOpen = false;
                args.Handled = true;
            }
            else if (!_searchCollapsed)
            {
                DialogsSearchPanel.OnBackRequested(args);

                if (args.Handled)
                {
                    return;
                }

                Search_LostFocus(null, null);
                args.Handled = true;
            }
            else if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                Manage_Click(null, null);
                args.Handled = true;
            }
        }

        public void OnBackRequested(BackRequestedRoutedEventArgs args)
        {
            OnBackRequesting(args);

            if (args.Handled)
            {
                return;
            }

            if (!_topicListCollapsed)
            {
                HideTopicList();
                args.Handled = true;
            }
            else if (_prevIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
                args.Handled = true;
            }
            else
            {
                var scrollViewer = ChatsList.GetScrollViewer();
                if (scrollViewer != null && scrollViewer.VerticalOffset > 50)
                {
                    Logger.Info("ChangeView");

                    scrollViewer.TryChangeView(null, 0, null);
                    args.Handled = true;
                }
                else if (ViewModel.Chats.Items.ChatList is ChatListArchive
                    || ViewModel.Folders.Count > 0 && !ViewModel.Chats.Items.ChatList.AreTheSame(ViewModel.Folders[0].ChatList))
                {
                    UpdateFolder(ViewModel.Folders.Count > 0 ? ViewModel.Folders[0] : ChatFolderViewModel.Main);
                    args.Handled = true;
                }
            }
        }

        private int _prevIndex;

        private void LoadAtIndex(int index)
        {
            if (index == _prevIndex)
            {
                return;
            }

            if (index == 0)
            {
                SettingsRoot?.Visibility = Visibility.Collapsed;

                Show(ChatsRoot, _prevIndex > index, 0);
            }
            else if (index == 1)
            {
                ChatsRoot.Visibility = Visibility.Collapsed;

                if (SettingsRoot != null)
                {
                    Show(SettingsRoot, _prevIndex > index, 1);
                }
            }

            _prevIndex = index;
            Pivot_SelectionChanged(null, null);
        }

        private void Show(UIElement element, bool leftToRight, int index)
        {
            if (_prevIndex == -1)
            {
                return;
            }

            element.Visibility = Visibility.Visible;

            if (!PowerSavingPolicy.AreSmoothTransitionsEnabled)
            {
                return;
            }

            ElementCompositionPreview.SetIsTranslationEnabled(element, true);

            var visualIn = ElementComposition.GetElementVisual(element);
            var offsetIn = visualIn.Compositor.CreateScalarKeyFrameAnimation();
            offsetIn.InsertKeyFrame(0, leftToRight ? -48 : 48);
            offsetIn.InsertKeyFrame(1, 0);
            offsetIn.Duration = Constants.SoftAnimation;

            var opacityIn = visualIn.Compositor.CreateScalarKeyFrameAnimation();
            opacityIn.InsertKeyFrame(0, 0);
            opacityIn.InsertKeyFrame(1, 1);
            opacityIn.Duration = Constants.SoftAnimation;

            visualIn.StartAnimation("Translation.X", offsetIn);
            visualIn.StartAnimation("Opacity", opacityIn);
        }

        private void SettingsRoot_Loaded(object sender, RoutedEventArgs e)
        {
            SettingsRoot.Loaded -= SettingsRoot_Loaded;
            Show(SettingsRoot, _prevIndex > 1, 1);
        }

        private void UpdateUser(User user)
        {
            TitleBarLogo.IsEnabled = _clientService.IsPremium;

            if (user.EmojiStatus != null)
            {
                LogoBasic.Visibility = Visibility.Collapsed;
                LogoEmoji.Visibility = Visibility.Visible;
                LogoEmoji.Source = new CustomEmojiFileSource(_clientService, user.EmojiStatus.Type);

                if (user.EmojiStatus.Type is EmojiStatusTypeUpgradedGift upgradedGift)
                {
                    LogoEmojiParticles.Source = new ParticlesImageSource(upgradedGift.BackdropColors);
                }
                else
                {
                    LogoEmojiParticles.Source = null;
                }
            }
            else
            {
                LogoBasic.Visibility = Visibility.Visible;
                LogoEmoji.Visibility = Visibility.Collapsed;
                LogoEmoji.Source = null;
                LogoEmojiParticles.Source = null;
            }
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_clientService.TryGetUser(_clientService.Options.MyId, out User user))
            {
                UpdateUser(user);
            }

            Subscribe();

            var context = WindowContext.ForXamlRoot(XamlRoot);
            if (context != null)
            {
                context.CoreWindow.CharacterReceived += OnCharacterReceived;
            }

            OnStateChanged(null, null);

            ShowHideBanner(LifetimeService.Current.Playback);

            var update = new UpdateConnectionState(ViewModel.ClientService.ConnectionState);
            if (update.State != null)
            {
                Handle(update);
                ViewModel.Aggregator.Publish(update);
            }

            Handle(new UpdateUnconfirmedSession(ViewModel.ClientService.UnconfirmedSession));
            Handle(new UpdateActiveCall());
            Handle(ViewModel.ClientService.FreezeState);
            UpdateChatFolders();

            if (_clientService.HasSuggestedAction(new SuggestedActionSetBirthdate()))
            {
                FindName(nameof(SetBirthdateCard));
            }

            if (_unloaded)
            {
                _unloaded = false;
                ChatsList.ItemsSource = null;

                Bindings.StopTracking();
                Bindings.Update();
            }

            WatchDog.TrackEvent("MainPage");

            if (SettingsService.Current.Diagnostics.IsLastErrorDiskFull)
            {
                SettingsService.Current.Diagnostics.IsLastErrorDiskFull = false;

                // TODO: Missing translation
                var confirm = await ViewModel.ShowPopupAsync("Unigram has previously failed to launch because the device storage was full.\r\n\r\nMake sure there's enough storage space available and press **OK** to continue.", "Disk storage is full", Strings.OK, Strings.StorageUsage);
                if (confirm == ContentDialogResult.Secondary)
                {
                    MasterDetail.NavigationService.Navigate(typeof(SettingsStoragePage));
                }
            }
        }

        private void Subscribe()
        {
            ViewModel.Aggregator.Subscribe<UpdateFileDownloads>(this, Handle)
                .Subscribe<UpdateChatActiveStories>(Handle)
                .Subscribe<UpdateChatIsMarkedAsUnread>(Handle)
                .Subscribe<UpdateChatReadInbox>(Handle)
                .Subscribe<UpdateChatReadOutbox>(Handle)
                .Subscribe<UpdateChatUnreadMentionCount>(Handle)
                .Subscribe<UpdateChatUnreadReactionCount>(Handle)
                .Subscribe<UpdateChatAddedToList>(Handle)
                .Subscribe<UpdateChatRemovedFromList>(Handle)
                .Subscribe<UpdateChatTitle>(Handle)
                .Subscribe<UpdateChatPhoto>(Handle)
                .Subscribe<UpdateChatEmojiStatus>(Handle)
                .Subscribe<UpdateChatVideoChat>(Handle)
                .Subscribe<UpdateChatViewAsTopics>(Handle)
                .Subscribe<UpdateUserStatus>(Handle)
                .Subscribe<UpdateUser>(Handle)
                .Subscribe<UpdateChatMessageAutoDeleteTime>(Handle)
                .Subscribe<UpdateChatAction>(Handle)
                .Subscribe<UpdateMessageMentionRead>(Handle)
                .Subscribe<UpdateMessageUnreadReactions>(Handle)
                .Subscribe<UpdateUnreadChatCount>(Handle)
                .Subscribe<UpdateChatUnreadTopicCount>(Handle)
                .Subscribe<UpdateSecretChat>(Handle)
                .Subscribe<UpdateChatNotificationSettings>(Handle)
                .Subscribe<UpdatePasscodeLock>(Handle)
                .Subscribe<UpdateUnconfirmedSession>(Handle)
                .Subscribe<UpdateFreezeState>(Handle)
                .Subscribe<UpdateConnectionState>(Handle)
                .Subscribe<UpdateOption>(Handle)
                .Subscribe<UpdateSuggestedActions>(Handle)
                .Subscribe<UpdateActiveCall>(Handle)
                .Subscribe<UpdateChatFoldersLayout>(Handle)
                .Subscribe<UpdateConfetti>(Handle);
        }

        private void OnPlaybackSourceChanged(IPlaybackService sender, object e)
        {
            this.BeginOnUIThread(() => ShowHideBanner(sender));
        }

        private bool _playbackVisible;
        private bool _activeCallVisible;

        private void ShowHideBanner(IPlaybackService sender)
        {
            if (sender.CurrentItem != null && Playback == null)
            {
                FindName(nameof(Playback));
                Playback.Update(ViewModel.ClientService, ViewModel.NavigationService);
            }

            _playbackVisible = sender.CurrentItem != null;
            MasterDetail.ShowHideBanner(_playbackVisible || _activeCallVisible);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var context = WindowContext.ForXamlRoot(this);
            if (context != null)
            {
                context.CoreWindow.CharacterReceived -= OnCharacterReceived;
            }

            Bindings.StopTracking();

            _unloaded = true;

            LeakTest(false);
        }

        private void OnCharacterReceived(CoreWindow sender, CharacterReceivedEventArgs args)
        {
            if (MasterDetail.NavigationService?.Frame.Content is not BlankPage)
            {
                return;
            }

            var character = System.Text.Encoding.UTF32.GetString(BitConverter.GetBytes(args.KeyCode));
            if (character.Length == 0 || char.IsControl(character[0]) || char.IsWhiteSpace(character[0]))
            {
                return;
            }

            var focused = FocusManagerEx.TryGetFocusedElement();
            if (focused is null or (not TextBox and not RichEditBox))
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
                if (popups.Count > 0)
                {
                    return;
                }

                Search_Click(null, null);
                SearchField.Focus(FocusState.Keyboard);
                SearchField.Text = character;
                SearchField.SelectionStart = character.Length;

                args.Handled = true;
            }
        }

        public void ProcessKeyboardAccelerators(ShortcutInvokedEventArgs args)
        {
            foreach (var command in args.Shortcut.Commands)
            {
                if (SettingsService.Current.Diagnostics.ShowMemoryUsage && command == ShortcutCommand.Quit)
                {
                    if (!MasterDetail.NavigationService.CanGoBack)
                    {
                        MasterDetail.NavigationService.ClearCache(true);
                    }

                    //GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
                    //GCSettings.LatencyMode = GCSettings.LatencyMode == GCLatencyMode.Interactive
                    //    ? GCLatencyMode.SustainedLowLatency
                    //    : GCLatencyMode.Interactive;

                    //NativeUtils.Collect = true;
                    //GC.Collect();
                    //GC.WaitForPendingFinalizers();
                    //GC.Collect();
                    //NativeUtils.Collect = false;

                    GarbageCollectionMonitor.DisconnectUnusedReferenceSources();
                    return;
                }

                ProcessChatCommands(command, args);
                ProcessFolderCommands(command, args);
                ProcessAppCommands(command, args);
            }
        }

        private async void ProcessAppCommands(ShortcutCommand command, ShortcutInvokedEventArgs args)
        {
            if (command is ShortcutCommand.SetStatus)
            {
                Status_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.Search)
            {
                if (MasterDetail.NavigationService.Frame.Content is ISearchablePage child)
                {
                    child.Search();
                }
                else
                {
                    SearchField.Focus(FocusState.Keyboard);
                    Search_Click(null, null);
                }

                args.Handled = true;
            }
            else if (command is ShortcutCommand.SearchChats)
            {
                SearchField.Focus(FocusState.Keyboard);
                Search_Click(null, null);

                args.Handled = true;
            }
            else if (command is ShortcutCommand.Quit)
            {
                await BridgeApplicationContext.ExitAsync();
                await BootStrapper.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Close)
            {
                await WindowContext.Current.ConsolidateAsync();
            }
            else if (command is ShortcutCommand.Lock)
            {
                Lock_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.Downloads)
            {
                Downloads_Click(null, null);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.MediaStop)
            {
                LifetimeService.Current.Playback.Clear();
                args.Handled = true;
            }
            else if (command is ShortcutCommand.CallAccept && ViewModel.VoipService.ActiveCall is VoipCall acceptCall)
            {
                acceptCall.Accept(false);
                args.Handled = true;
            }
            else if (command is ShortcutCommand.CallReject && ViewModel.VoipService.ActiveCall is VoipCall rejectCall)
            {
                rejectCall.Discard();
                args.Handled = true;
            }
        }

        private void ProcessFolderCommands(ShortcutCommand command, ShortcutInvokedEventArgs args)
        {
            var folders = ViewModel.Folders;
            if (folders.Empty())
            {
                return;
            }

            if (command == ShortcutCommand.FolderPrevious)
            {
                args.Handled = true;
                ScrollFolder(-1, true);
            }
            else if (command == ShortcutCommand.FolderNext)
            {
                args.Handled = false;
                ScrollFolder(+1, true);
            }
            else if (command == ShortcutCommand.ShowAllChats)
            {
                args.Handled = true;
                ScrollFolder(int.MinValue, true);
            }
            else if (command == ShortcutCommand.ShowFolderLast)
            {
                args.Handled = true;
                ScrollFolder(int.MaxValue, true);
            }
            else if (command == ShortcutCommand.ShowArchive)
            {
                args.Handled = true;
                ArchivedChats_Click(null, null);
            }
            else if (command is >= ShortcutCommand.ShowFolder1 and <= ShortcutCommand.ShowFolder6)
            {
                var index = command - ShortcutCommand.ShowAllChats;
                if (folders.Count > index)
                {
                    UpdateFolder(folders[index], false);
                }
            }
        }

        private async void ProcessChatCommands(ShortcutCommand command, ShortcutInvokedEventArgs args)
        {
            if (command == ShortcutCommand.ChatRecentPrevious)
            {
                args.Handled = ShowChatSwitch(false);
            }
            else if (command == ShortcutCommand.ChatRecentNext)
            {
                args.Handled = ShowChatSwitch(true);
            }
            if (command == ShortcutCommand.ChatPrevious)
            {
                args.Handled = true;
                Scroll(-1, true);
            }
            else if (command == ShortcutCommand.ChatNext)
            {
                args.Handled = true;
                Scroll(+1, true);
            }
            else if (command == ShortcutCommand.ChatFirst)
            {
                args.Handled = true;
                Scroll(int.MinValue, true);
            }
            else if (command == ShortcutCommand.ChatLast)
            {
                args.Handled = true;
                Scroll(int.MaxValue, true);
            }
            else if (command == ShortcutCommand.ChatSelf)
            {
                args.Handled = true;

                if (ViewModel.ClientService.TryGetChat(ViewModel.ClientService.Options.MyId, out Chat chat))
                {
                    MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    MasterDetail.NavigationService.GoBackAt(0, false);
                }
            }
            else if (command is >= ShortcutCommand.ChatPinned1 and <= ShortcutCommand.ChatPinned5)
            {
                var folders = ViewModel.Folders;
                if (folders.Count > 0)
                {
                    return;
                }

                var index = command - ShortcutCommand.ChatPinned1;

                var response = await ViewModel.ClientService.GetChatListAsync(new ChatListMain(), 0, (int)ViewModel.ClientService.Options.PinnedChatCountMax * 2 + 1);
                if (response is Telegram.Td.Api.Chats chats && index >= 0 && index < chats.ChatIds.Count)
                {
                    for (int i = 0; i < chats.ChatIds.Count; i++)
                    {
                        var chat = ViewModel.ClientService.GetChat(chats.ChatIds[i]);
                        if (chat == null)
                        {
                            return;
                        }

                        //if (chat.Source != null)
                        //{
                        //    index++;
                        //}
                        //else if (i == index)
                        //{
                        //    if (chat.IsPinned)
                        //    {
                        //        MasterDetail.NavigationService.NavigateToChat(chats.ChatIds[index]);
                        //        MasterDetail.NavigationService.GoBackAt(0, false);
                        //    }

                        //    return;
                        //}
                    }
                }
            }
        }

        private bool ShowChatSwitch(bool start)
        {
            foreach (var open in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
            {
                if (open.Child is RecentChatsView)
                {
                    return false;
                }
            }

            if (ViewModel.ClientService.RecentlyOpenedChatsCount > 1)
            {
                var popup = new Popup
                {
                    XamlRoot = XamlRoot
                };

                popup.Child = new RecentChatsView(ViewModel.ClientService, MasterDetail.NavigationService, popup, start)
                {
                    Width = ActualWidth,
                    Height = ActualHeight
                };

                popup.IsOpen = true;
                return true;
            }

            return false;
        }

        public void Scroll(int offset, bool navigate)
        {
            if (!_topicListCollapsed)
            {
                TopicListPresenter.Scroll(offset, navigate);
                return;
            }

            int index;
            if (offset == int.MaxValue)
            {
                index = ViewModel.Chats.Items.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index = ChatsList.SelectedIndex + offset;
            }

            if (index >= 0 && index < ViewModel.Chats.Items.Count)
            {
                if (navigate)
                {
                    Navigate(ViewModel.Chats.Items[index], false);
                }
            }
            else if (index < 0 && offset == -1 && !navigate)
            {
                Search_Click(null, null);
            }
        }

        public void ScrollFolder(int offset, bool navigate)
        {
            var already = ViewModel.SelectedFolder;
            if (already == null)
            {
                return;
            }

            var index = ViewModel.Folders.IndexOf(already);
            if (offset == int.MaxValue)
            {
                index = ViewModel.Folders.Count - 1;
            }
            else if (offset == int.MinValue)
            {
                index = 0;
            }
            else
            {
                index += offset;
            }

            if (index >= 0 && index < ViewModel.Folders.Count)
            {
                UpdateFolder(ViewModel.Folders[index], true);
            }
        }

        public void Initialize()
        {
            Frame.BackStack.Clear();

            if (MasterDetail.NavigationService == null)
            {
                MasterDetail.Initialize("Main", Frame, ViewModel);
                MasterDetail.NavigationService.FrameFacade.Navigating += OnNavigating;
                MasterDetail.NavigationService.FrameFacade.Navigated += OnNavigated;
            }

            ViewModel.NavigationService = MasterDetail.NavigationService;

            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        public void Activate(string parameter)
        {
            Initialize();

            if (parameter == null)
            {
                return;
            }

            if (parameter.StartsWith("tg:toast"))
            {
                parameter = parameter.Substring("tg:toast?".Length).TrimStart('?');
            }
            else if (parameter.StartsWith("tg://toast"))
            {
                parameter = parameter.Substring("tg://toast?".Length).TrimStart('?');
            }

            var data = Toast.SplitArguments(parameter);
            if (data.TryGetValue("web_app", out string webApp))
            {
                parameter = Toast.FromBase64(webApp);
            }

            if (Uri.TryCreate(parameter, UriKind.Absolute, out Uri scheme))
            {
                Activate(scheme);
            }
            else
            {
                data.TryGetValue("chat_id", out string chat_id);
                data.TryGetValue("forum_topic_id", out string forum_topic_id);
                data.TryGetValue("saved_messages_topic_id", out string saved_messages_topic_id);
                data.TryGetValue("feedback_chat_topic_id", out string feedback_chat_topic_id);
                data.TryGetValue("thread_id", out string thread_id);

                long.TryParse(chat_id, out long chatId);

                MessageTopic messageTopic = null;
                if (int.TryParse(forum_topic_id, out int forumTopicId))
                {
                    messageTopic = new MessageTopicForum(forumTopicId);
                }
                else if (long.TryParse(saved_messages_topic_id, out long savedMessagesTopicId))
                {
                    messageTopic = new MessageTopicSavedMessages(savedMessagesTopicId);
                }
                else if (long.TryParse(feedback_chat_topic_id, out long directMessagesChatTopicId))
                {
                    messageTopic = new MessageTopicDirectMessages(directMessagesChatTopicId);
                }
                else if (long.TryParse(thread_id, out long threadId))
                {
                    messageTopic = new MessageTopicThread(threadId);
                }

                if (_clientService.TryGetChat(chatId, out Chat chat))
                {
                    if (chat.ViewAsTopics)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat.Id, topic: messageTopic, force: false);
                    }
                    else
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat.Id, force: false);
                    }
                }
            }

            if (XamlRoot == null)
            {
                return;
            }

            var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot);
            if (popups != null)
            {
                foreach (var popup in popups)
                {
                    if (popup.Child is GalleryWindow gallery)
                    {
                        gallery.OnBackRequested(new BackRequestedRoutedEventArgs());
                        break;
                    }
                }
            }
        }

        public async void Activate(Uri scheme)
        {
            if (MessageHelper.IsTelegramUrl(scheme))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, MasterDetail.NavigationService, scheme);
            }
            else if (scheme.Scheme.Equals("ms-contact-profile") || scheme.Scheme.Equals("ms-ipmessaging"))
            {
                var query = scheme.Query.ParseQueryString();
                if (query.TryGetValue("ContactRemoteIds", out string remote) && int.TryParse(remote.Substring(1), out int from_id))
                {
                    var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(from_id, false));
                    if (response is Chat chat)
                    {
                        MasterDetail.NavigationService.NavigateToChat(chat, force: false);
                    }
                }
            }
        }

        private void OnNavigating(object sender, NavigatingEventArgs e)
        {
            var allowed = e.SourcePageType == typeof(ChatPage) ||
                e.SourcePageType == typeof(ChatPinnedPage) ||
                e.SourcePageType == typeof(ChatScheduledPage) ||
                e.SourcePageType == typeof(ChatEventLogPage) ||
                e.SourcePageType == typeof(ChatBusinessRepliesPage) ||
                e.SourcePageType == typeof(BlankPage);

            var animate = MasterDetail.CurrentState != MasterDetailState.Minimal ||
                (e.SourcePageType != typeof(BlankPage) && e.Content is not BlankPage);

            var type = allowed ? BackgroundKind.Background : BackgroundKind.Material;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal && e.SourcePageType == typeof(BlankPage))
            {
                type = BackgroundKind.None;
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(type, animate);
            }
        }

        private void OnNavigated(object sender, NavigatedEventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                MasterDetail.AllowCompact = true;
            }
            else
            {
                MasterDetail.AllowCompact = e.SourcePageType != typeof(BlankPage) && _prevIndex == INDEX_CHATS;
            }

            _shouldGoBackWithDetail = true;

            UpdatePaneToggleButtonVisibility();
            UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetChatFromBackStack());

            SettingsView?.UpdateSelection(false);
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ChatsList.SelectedItem = null;
                    ChatsList.SelectionMode = ListViewSelectionMode.None;
                }

                Header.Visibility = Visibility.Visible;
                TitleText.Visibility = Visibility.Visible;
            }
            else
            {
                if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
                {
                    ChatsList.SelectionMode = ListViewSelectionMode.Single;
                    ChatsList.SelectedItem = ViewModel.Chats.Items.FirstOrDefault(x => x.Id == ViewModel.Chats.SelectedItem);
                }

                Header.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
                TitleText.Visibility = MasterDetail.CurrentState == MasterDetailState.Expanded ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdatePaneToggleButtonVisibility();

            ChatsList.UpdateViewState(MasterDetail.CurrentState);

            var frame = MasterDetail.NavigationService.Frame;
            var allowed = frame.CurrentSourcePageType == typeof(ChatPage) ||
                frame.CurrentSourcePageType == typeof(ChatPinnedPage) ||
                frame.CurrentSourcePageType == typeof(ChatScheduledPage) ||
                frame.CurrentSourcePageType == typeof(ChatEventLogPage) ||
                frame.CurrentSourcePageType == typeof(ChatBusinessRepliesPage) ||
                frame.CurrentSourcePageType == typeof(BlankPage);

            var type = allowed ? BackgroundKind.Background : BackgroundKind.Material;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal && frame.CurrentSourcePageType == typeof(BlankPage))
            {
                type = BackgroundKind.None;
            }

            if (MasterDetail.CurrentState != MasterDetailState.Unknown)
            {
                MasterDetail.ShowHideBackground(type, false);
            }
        }

        private void OnMasterVisibilityChanged(object sender, EventArgs e)
        {
            UpdateTitleBarMargins();

            Stories.IsVisible = MasterDetail.MasterVisibility == Visibility.Visible;
            Photo.Visibility = MasterDetail.MasterVisibility == Visibility.Visible || !_tabsLeftCollapsed
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdatePaneToggleButtonVisibility()
        {
            var visible = ViewModel.Chats.Items.ChatList is ChatListArchive
                || !_searchCollapsed
                || _prevIndex != INDEX_CHATS;

            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                visible &= MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage);
            }

            Photo.IsChecked = visible;
            //Photo.Glyph = visible
            //    ? Photo.HorizontalAlignment == HorizontalAlignment.Right
            //    ? Icons.ArrowRight
            //    : Icons.ArrowLeft
            //    : Icons.Hamburger;
        }

        private Visibility UpdateScrollingHostHeaderVisibility(MasterDetailState state, bool primaryFolderSelected)
        {
            return state == MasterDetailState.Compact
                ? Visibility.Collapsed
                : primaryFolderSelected
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void UpdateListViewsSelectedItem(ChatMessageTopic openChat, bool fromSelection = false)
        {
            if (openChat.ChatId == 0 && ViewModel.Topics.Chat != null)
            {
                openChat = new ChatMessageTopic(ViewModel.Topics.Chat.Id, null);
            }

            ViewModel.Chats.SelectedItem = openChat.ChatId;

            if (ViewModel.Topics.ChatId == openChat.ChatId)
            {
                ViewModel.Topics.SelectedItem = openChat.MessageTopic;
                ViewModel.Topics.Delegate?.SetSelectedItem(ViewModel.Topics.Items.GetItem(openChat.MessageTopic));
            }
            else
            {
                ViewModel.Topics.SelectedItem = null;
                ViewModel.Topics.Delegate?.SetSelectedItem(null);
            }

            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                try
                {
                    if (ViewModel.ClientService.TryGetChat(openChat.ChatId, out Chat chat) && ViewModel.Chats.Items.Contains(chat))
                    {
                        if (fromSelection)
                        {
                            // If we come from selection we need to delay this as ItemClick comes before SelectionChanged,
                            // hence, if we unselect here, the ListView internal code will re-select the item right away.
                            VisualUtilities.QueueCallbackForCompositionRendered(this, () => ChatsList.SelectedItem = chat);
                        }
                        else if (ChatsList.SelectedItem != chat)
                        {
                            ChatsList.SelectedItem = chat;
                        }
                    }
                    else if (fromSelection)
                    {
                        // If we come from selection we need to delay this as ItemClick comes before SelectionChanged,
                        // hence, if we unselect here, the ListView internal code will re-select the item right away.
                        VisualUtilities.QueueCallbackForCompositionRendered(this, () => ChatsList.ClearValue(Selector.SelectedItemProperty));
                    }
                    else
                    {
                        ChatsList.ClearValue(Selector.SelectedItemProperty);
                    }
                }
                catch
                {
                    // In some cases setting SelectedItem can trigger
                }
            }
        }

        public bool EvaluatePaneToggleButtonVisibility()
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal)
            {
                return MasterDetail.NavigationService.CurrentPageType == typeof(BlankPage);
            }

            return true;
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e == null)
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
                return;
            }

            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple && e.ClickedItem is Chat chat)
            {
                if (ViewModel.Chats.SelectedItems.Contains(chat))
                {
                    ViewModel.Chats.SelectedItems.Remove(chat);
                }
                else
                {
                    ViewModel.Chats.SelectedItems.Add(chat);
                }

                if (ViewModel.Chats.SelectedItems.Empty())
                {
                    ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                        ? ListViewSelectionMode.None
                        : ListViewSelectionMode.Single;
                }
            }
            else
            {
                Navigate(e.ClickedItem, true);
            }
        }

        private void ListView_ItemClick(object sender, ForumViewItemClickEventArgs e)
        {
            Navigate(e.ClickedItem, e.FromSelection);
        }

        public async void Navigate(object item, bool selectionChanged)
        {
#if MOCKUP
            if (item is Chat cat)
            {
                if (cat.Id == 0)
                {
                    MasterDetail.NavigationService.Navigate(typeof(ChatPage), 9L);
                }
                else if (cat.Id == 1)
                {
                    MasterDetail.NavigationService.Navigate(typeof(ChatPage), 10L);
                }
            }

            ChatsList.SelectedItem = null;

            return;
#endif

            var profile = false;

            if (item is TLCallGroup callGroup)
            {
                item = callGroup.Message;
            }

            if (item is Message message)
            {
                ViewModel.Chats.SelectedItem = message.ChatId;

                var modifiers = WindowContext.KeyModifiers();
                var createNewWindow = modifiers == Windows.System.VirtualKeyModifiers.Control;

                var messageChat = ViewModel.ClientService.GetChat(message.ChatId);
                var hasTabs = ViewModel.ClientService.HasTabs(messageChat);

                MasterDetail.NavigationService.NavigateToChat(
                    messageChat,
                    message: message.Id,
                    topic: hasTabs ? message.TopicId : null,
                    force: false,
                    createNewWindow: createNewWindow);
            }
            else
            {
                SearchField.Text = string.Empty;
                Search_LostFocus(null, null);
            }

            if (item is TLCallGroup group)
            {
                item = group.Message;
            }
            else if (item is SearchResult result)
            {
                if (result.Chat != null)
                {
                    item = result.Chat;
                    ViewModel.ClientService.Send(new AddRecentlyFoundChat(result.Chat.Id));
                }
                else
                {
                    item = result.User;
                }

                profile = result.Type == SearchResultType.WebApps;
            }

            //if (item is TLMessageCommonBase message)
            //{
            //    if (message.Parent != null)
            //    {
            //        MasterDetail.NavigationService.NavigateToDialog(message.Parent, message.Id);
            //    }
            //}
            //else
            //{
            //    SearchField.Text = string.Empty;
            //}

            if (item is User user)
            {
                var response = await ViewModel.ClientService.SendAsync(new CreatePrivateChat(user.Id, false));
                if (response is Chat)
                {
                    item = response as Chat;
                }
            }

            if (item is Chat chat)
            {
                ViewModel.Chats.SelectedItem = chat.Id;

                if (chat.ViewAsTopics && chat.Type is ChatTypeSupergroup && !ViewModel.ClientService.HasTabs(chat))
                {
                    if (ViewModel.Chats.SelectedItem != ViewModel.Topics.Chat?.Id)
                    {
                        ShowTopicList(chat);
                    }
                    else
                    {
                        HideTopicList(true);
                    }
                }
                else
                {
                    if (profile)
                    {
                        MasterDetail.NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                    else
                    {
                        var modifiers = WindowContext.KeyModifiers();
                        if (modifiers == VirtualKeyModifiers.Menu && selectionChanged)
                        {
                            Handle(chat, (cell, chat) => cell.ShowPreview(null));
                        }
                        else
                        {
                            var createNewWindow = selectionChanged && modifiers == VirtualKeyModifiers.Control;

                            // TODO: new display mode
                            var messageThreadId = chat.LastMessage != null && ViewModel.ClientService.IsForum(chat) ? chat.LastMessage.ForumTopicId() : 0;
                            messageThreadId = 0;

                            MasterDetail.NavigationService.NavigateToChat(chat, force: false, createNewWindow: createNewWindow, clearBackStack: true);
                        }
                    }

                    HideTopicList();
                }
            }
            else if (item is ForumTopic topic)
            {
                var modifiers = WindowContext.KeyModifiers();
                if (modifiers == VirtualKeyModifiers.Menu && selectionChanged)
                {
                    TopicListPresenter.HandleForumTopic(topic, (cell, chat) => cell.ShowPreview(null));
                }
                else
                {
                    var createNewWindow = selectionChanged && modifiers == VirtualKeyModifiers.Control;

                    ViewModel.Chats.SelectedItem = topic.Info.ChatId;
                    ViewModel.Topics.SelectedItem = topic.ToId();
                    MasterDetail.NavigationService.NavigateToChat(ViewModel.Topics.Chat, topic: topic.ToId(), force: false, createNewWindow: createNewWindow, clearBackStack: true);
                }
            }
        }

        private void ListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ViewModel.Chats.SelectedItems.Count > 0)
            {
                var muted = ViewModel.Chats.SelectedItems.Any(x => ViewModel.ClientService.Notifications.IsMuted(x));
                ManageMute.Glyph = muted ? Icons.Alert : Icons.AlertOff;
                Automation.SetToolTip(ManageMute, muted ? Strings.UnmuteNotifications : Strings.MuteNotifications);

                var unread = ViewModel.Chats.SelectedItems.Any(x => x.IsUnread());
                ManageMark.Icon = MenuFlyoutHelper.CreateIcon(unread ? Icons.MarkAsRead : Icons.MarkAsUnread);
                ManageMark.Text = unread ? Strings.MarkAsRead : Strings.MarkAsUnread;

                ManageClear.IsEnabled = ViewModel.Chats.SelectedItems.All(x => DialogClear_Loaded(x));
            }
        }

        private void Pivot_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            MasterDetail.AllowCompact = MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage) && _prevIndex == INDEX_CHATS;

            switch (_prevIndex)
            {
                case INDEX_CHATS:
                    Root?.SetSelectedIndex(RootDestination.Chats);
                    break;
                case INDEX_SETTINGS:
                    _shouldGoBackWithDetail = false;

                    Root?.SetSelectedIndex(RootDestination.Settings);
                    SearchField.ControlledList = null;

                    if (SettingsView == null)
                    {
                        FindName(nameof(SettingsRoot));
                        SettingsView.DataContext = ViewModel.Settings;
                        ViewModel.Settings.Delegate = SettingsView;

                        _ = ViewModel.Settings.NavigatedToAsync(null, NavigationMode.Refresh, null);
                    }
                    break;
            }

            SearchField.Text = string.Empty;

            UpdateHeader();
            UpdatePaneToggleButtonVisibility();

            SearchReset();

            if (_prevIndex != INDEX_CHATS)
            {
                Stories.Collapse();
                ViewModel.IsSettingsSelected = true;
            }
            else
            {
                ViewModel.IsSettingsSelected = false;
            }

            _shouldGoBackWithDetail = false;

            for (int i = 0; i < ViewModel.Children.Count; i++)
            {
                if (ViewModel.Children[i] is IChildViewModel child)
                {
                    if (i == _prevIndex)
                    {
                        child.Activate();
                    }
                    else
                    {
                        child.Deactivate();
                    }
                }
            }
        }

        private void UpdateHeader()
        {
            if (_prevIndex == INDEX_CHATS)
            {
                ChatsOptions.Visibility = Visibility.Visible;
                SearchField.Padding = new Thickness(10, 5, 40, 6);
            }
            else
            {
                ChatsOptions.Visibility = Visibility.Collapsed;
                SearchField.Padding = new Thickness(10, 5, 6, 6);
            }

            SearchField.PlaceholderText = _prevIndex == INDEX_SETTINGS
                ? Strings.SearchInSettings
                : Strings.Search;
        }

        #region Search

        private bool _searchCollapsed = true;

        private void ShowHideSearch(bool show)
        {
            if (_searchCollapsed != show)
            {
                return;
            }

            _searchCollapsed = !show;

            FindName(nameof(DialogsSearchPanel));
            DialogsPanel.Visibility = Visibility.Visible;
            DialogsSearchPanel.Visibility = Visibility.Visible;

            if (show)
            {
                DialogsSearchPanel.Activate();
                SearchField.ControlledList = DialogsSearchPanel.Root;
                Stories.Collapse();
            }

            var chats = ElementComposition.GetElementVisual(DialogsPanel);
            var panel = ElementComposition.GetElementVisual(DialogsSearchPanel);

            chats.CenterPoint = panel.CenterPoint = new Vector3(DialogsPanel.ActualSize / 2, 0);

            var batch = panel.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                DialogsPanel.Visibility = _searchCollapsed ? Visibility.Visible : Visibility.Collapsed;
                DialogsSearchPanel.Visibility = _searchCollapsed ? Visibility.Collapsed : Visibility.Visible;

                if (_searchCollapsed)
                {
                    DialogsSearchPanel.Deactivate();
                }
            };

            var scale1 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale1.InsertKeyFrame(show ? 0 : 1, new Vector3(1.05f, 1.05f, 1));
            scale1.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
            scale1.Duration = TimeSpan.FromMilliseconds(200);

            var scale2 = panel.Compositor.CreateVector3KeyFrameAnimation();
            scale2.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            scale2.InsertKeyFrame(show ? 1 : 0, new Vector3(0.95f, 0.95f, 1));
            scale2.Duration = TimeSpan.FromMilliseconds(200);

            var opacity1 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);
            opacity1.Duration = TimeSpan.FromMilliseconds(200);

            var opacity2 = panel.Compositor.CreateScalarKeyFrameAnimation();
            opacity2.InsertKeyFrame(show ? 0 : 1, 1);
            opacity2.InsertKeyFrame(show ? 1 : 0, 0);
            opacity2.Duration = TimeSpan.FromMilliseconds(200);

            panel.StartAnimation("Scale", scale1);
            panel.StartAnimation("Opacity", opacity1);

            chats.StartAnimation("Scale", scale2);
            chats.StartAnimation("Opacity", opacity2);

            if (!_topicListCollapsed)
            {
                var header = ElementComposition.GetElementVisual(Header);
                header.StartAnimation("Opacity", opacity1);
            }

            batch.End();
        }

        public void Search()
        {
            SearchField.Focus(FocusState.Keyboard);
            Search_Click(null, null);
        }

        private void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Keyboard && sender == SearchField)
            {
                return;
            }

            Search_TextChanged(null, null);
        }

        private void Search_LostFocus(object sender, RoutedEventArgs e)
        {
            MasterDetail.AllowCompact = MasterDetail.NavigationService?.CurrentPageType != typeof(BlankPage)
                && _prevIndex == INDEX_CHATS;

            SearchReset();

            UpdatePaneToggleButtonVisibility();
        }

        private const int INDEX_CHATS = 0;
        private const int INDEX_SETTINGS = 1;

        private void Search_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SearchField.FocusState == FocusState.Unfocused && string.IsNullOrWhiteSpace(SearchField.Text))
            {
                return;
            }

            _shouldGoBackWithDetail = false;

            MasterDetail.AllowCompact = false;

            if (_prevIndex == INDEX_CHATS)
            {
                ShowHideSearch(true);
                ViewModel.SearchChats.Query = SearchField.Text;
            }
            else if (_prevIndex == INDEX_SETTINGS)
            {
                if (string.IsNullOrWhiteSpace(SearchField.Text))
                {
                    SearchReset();
                }
                else
                {
                    SettingsView?.Visibility = Visibility.Collapsed;

                    ViewModel.Settings.Search(SearchField.Text);
                }
            }

            UpdatePaneToggleButtonVisibility();
        }

        private void SearchReset()
        {
            //DialogsPanel.Visibility = Visibility.Visible;
            ShowHideSearch(false);

            if (_prevIndex == INDEX_CHATS && SearchField.FocusState != FocusState.Unfocused)
            {
                Photo.Focus(FocusState.Programmatic);
            }

            SearchField.Text = string.Empty;

            SettingsView?.Visibility = Visibility.Visible;

            ViewModel.Settings.Results.Clear();
        }

        #endregion

        private void Lock_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Passcode.Lock(false);
        }

        private void Settings_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }

            static string GetPath(SettingsSearchEntry item)
            {
                if (item.Parent != null)
                {
                    return GetPath(item.Parent) + " > " + item.Text;
                }

                return item.Text;
            }

            var entry = args.Item as SettingsSearchEntry;
            var button = args.ItemContainer.ContentTemplateRoot as SettingsButton;
            button.Command = ViewModel.Settings.NavigateCommand;
            button.CommandParameter = entry;

            button.Content = entry.Text;
            button.IconSource = entry.Icon;

            var icon = button.GetChild<AnimatedIcon>();
            icon?.InvalidateMeasure();

            if (entry.Parent == null)
            {
                button.Badge = null;
                button.BadgeVisibility = Visibility.Collapsed;
            }
            else
            {
                button.Badge = GetPath(entry.Parent);
                button.BadgeVisibility = Visibility.Visible;
            }

            args.Handled = true;
        }

        private void SetPivotSelectedIndex(int index, bool updateBackStack = false)
        {
            if (_prevIndex != index || updateBackStack)
            {
                LoadAtIndex(index);

                if (MasterDetail.CurrentState == MasterDetailState.Minimal &&
                    MasterDetail.NavigationService.CurrentPageType != typeof(BlankPage))
                {
                    MasterDetail.NavigationService.GoBackAt(0);
                }
            }
        }

        public void NavigationView_ItemClick(RootDestination destination)
        {
            if (destination == RootDestination.Chats)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
            }
            else if (destination == RootDestination.Contacts)
            {
                ViewModel.NavigationService.ShowPopup(new ContactsPopup());
            }
            else if (destination == RootDestination.Calls)
            {
                ViewModel.NavigationService.ShowPopup(new CallsPopup());
            }
            else if (destination == RootDestination.Settings)
            {
                SetPivotSelectedIndex(INDEX_SETTINGS);
            }
            else if (destination == RootDestination.ArchivedChats)
            {
                ArchivedChats_Click(null, null);
            }
            else if (destination == RootDestination.Status)
            {
                Status_Click(null, null);
            }
            else if (destination == RootDestination.NewGroup)
            {
                ViewModel.NavigationService.ShowPopup(new NewGroupPopup());
            }
            else if (destination == RootDestination.NewChannel)
            {
                ViewModel.NavigationService.ShowPopup(new NewChannelPopup());
            }
            else if (destination == RootDestination.MyProfile)
            {
                ViewModel.NavigateToMyProfile(false);
            }
            else if (destination == RootDestination.SavedMessages)
            {
                ViewModel.NavigateToMyProfile(true);
            }
            else if (destination == RootDestination.Tips && Uri.TryCreate(Strings.TelegramFeaturesUrl, UriKind.Absolute, out Uri tipsUri))
            {
                MessageHelper.OpenTelegramUrl(ViewModel.ClientService, MasterDetail.NavigationService, tipsUri);
            }
            else if (destination == RootDestination.News)
            {
                MessageHelper.NavigateToUsername(ViewModel.ClientService, MasterDetail.NavigationService, "unigram");
            }
        }

        private void Arrow_Click(object sender, RoutedEventArgs e)
        {
            var scrollViewer = ChatsList.GetScrollViewer();
            scrollViewer?.TryChangeView(null, 0, null);
        }

        private void Proxy_Click(object sender, RoutedEventArgs e)
        {
            MasterDetail.NavigationService.Navigate(typeof(SettingsProxyPage));
        }

        public void UpdateChatListArchive()
        {
            this.BeginOnUIThread(() => ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive()));
        }

        public void UpdateChatFoldersLayout()
        {
            void handler(object sender, object e)
            {
                ChatsList.LayoutUpdated -= handler;
                ChatsList.ItemContainerTransitions.Clear();
            }

            ChatsList.ItemContainerTransitions.Clear();
            ChatsList.ItemContainerTransitions.Add(new RepositionThemeTransition());

            ChatsList.LayoutUpdated += handler;
            ChatsList.UpdateVisibleChats();

            ConvertFolder(ViewModel.SelectedFolder);
        }

        public void UpdateChatFolders()
        {
            ConvertFolder(ViewModel.SelectedFolder);
        }

        private ChatFolderViewModel ConvertFolder(ChatFolderViewModel folder, bool updateBackStack = true)
        {
            ShowHideArchive(folder?.ChatList is ChatListMain or null && ViewModel.Chats.Items.ChatList is not ChatListArchive, false);
            ShowHideLeftTabs(SettingsService.Current.UseLeftTabsForChats && ViewModel.Folders.Count > 0);
            ShowHideTopTabs(!SettingsService.Current.UseLeftTabsForChats && ViewModel.Folders.Count > 0 && folder.ChatList is not ChatListArchive);

            UpdatePaneToggleButtonVisibility();

            if (MasterDetail.CurrentState != MasterDetailState.Minimal && updateBackStack)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
            }

            ChatsList.CanGoNext = ViewModel.Folders.Count > 0 && ViewModel.Folders[^1] != folder;
            ChatsList.CanGoPrev = ViewModel.Folders.Count > 0 && ViewModel.Folders[0] != folder;

            return folder;
        }

        private void ChatFolders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListViewBase listView && listView.SelectedItem is ChatFolderViewModel folder)
            {
                UpdateFolder(folder, updateBackStack: false);

                SetPivotSelectedIndex(INDEX_CHATS, true);
                HideTopicList();
            }
        }

        public void ArchivedChats_Click(object sender, RoutedEventArgs e)
        {
            UpdateFolder(ChatFolderViewModel.Archive);
        }

        private void ChatFolder_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var element = sender as FrameworkElement;
            var folder = ChatFolders?.ItemFromContainer(sender) as ChatFolderViewModel;

            folder ??= ChatFoldersSide.ItemFromContainer(sender) as ChatFolderViewModel;

            if (folder.IsNavigationItem)
            {
                return;
            }

            var flyout = new MenuFlyout();

            if (folder.ChatFolderId == Constants.ChatListMain)
            {
                flyout.CreateFlyoutItem(ViewModel.EditFolder, folder, Strings.FilterEditAll, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, folder, Strings.MarkAllAsRead, Icons.MarkAsRead);
            }
            else
            {
                flyout.CreateFlyoutItem(ViewModel.EditFolder, folder, Strings.FilterEdit, Icons.Edit);
                flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, folder, Strings.MarkAllAsRead, Icons.MarkAsRead);
                flyout.CreateFlyoutItem(ViewModel.AddToFolder, folder, Strings.FilterAddChats, Icons.Add);
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ViewModel.DeleteFolder, folder, Strings.Remove, Icons.Delete, destructive: true);
            }

            flyout.ShowAt(element, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        private void ArchivedChats_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var viewModel = ViewModel;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();

            //if (((TLViewModelBase)ViewModel).Settings.CollapseArchivedChats)
            //{
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.AccDescrExpandPanel, Icons.Expand);
            //}
            //else
            //{
            //    flyout.CreateFlyoutItem(new RelayCommand(ToggleArchive), Strings.AccDescrCollapsePanel, Icons.Collapse);
            //}

            flyout.CreateFlyoutItem(ToggleArchive, Strings.ArchiveMoveToMainMenu, Icons.SubtractCircle);
            flyout.CreateFlyoutItem(ViewModel.MarkFolderAsRead, ChatFolderViewModel.Archive, Strings.MarkAllAsRead, Icons.MarkAsRead);

            flyout.ShowAt(sender, args);
        }

        public async void ToggleArchive()
        {
            ViewModel.ToggleArchive();

            ArchivedChatsPanel.Visibility = Visibility.Visible;
            //ArchivedChatsCompactPanel.Visibility = Visibility.Visible;

            await ArchivedChatsPanel.UpdateLayoutAsync();

            void ToggleActiveCompleted()
            {
                ArchivedChatsPanel.Visibility = ((ViewModelBase)ViewModel).Settings.HideArchivedChats
                    ? Visibility.Collapsed
                    : Visibility.Visible;
                //ArchivedChatsCompactPanel.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);

                Root.UpdateSessions();

                if (((ViewModelBase)ViewModel).Settings.HideArchivedChats)
                {
                    ToastPopup.Show(Photo, Strings.ArchiveMoveToMainMenuInfo, TeachingTipPlacementMode.BottomRight);
                }
            }

            var show = !((ViewModelBase)ViewModel).Settings.HideArchivedChats;

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ToggleActiveCompleted();
            }

            var presenter = ElementComposition.GetElementVisual(ArchivedChatsPresenter);
            var parent = ElementComposition.GetElementVisual(ChatsList);

            var chats = ElementComposition.GetElementVisual(element);
            var panel = ElementComposition.GetElementVisual(ArchivedChatsPanel);
            //var compact = ElementComposition.GetElementVisual(ArchivedChatsCompactPanel);

            presenter.Clip = chats.Compositor.CreateInsetClip();
            parent.Clip = chats.Compositor.CreateInsetClip();

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                panel.Offset = new Vector3();
                //compact.Offset = new Vector3();

                ToggleActiveCompleted();
            };

            var panelY = ArchivedChatsPanel.ActualSize.Y;
            var compactY = 0; //(float)ArchivedChatsCompactPanel.ActualHeight;

            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -(panelY - compactY));

            float y0, y1;

            if (show)
            {
                y0 = -(panelY - compactY);
                y1 = 0;
            }
            else
            {
                y0 = 0;
                y1 = -(panelY - compactY);
            }

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, y0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, y1, 0));
            chats.StartAnimation("Offset", offset0);

            //var offset1 = chats.Compositor.CreateVector3KeyFrameAnimation();
            //offset1.InsertKeyFrame(0, new Vector3(0, show ? 0 : compactY, 0));
            //offset1.InsertKeyFrame(1, new Vector3(0, show ? compactY : 0, 0));
            //compact.StartAnimation("Offset", offset1);

            var offset2 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset2.InsertKeyFrame(0, new Vector3(0, show ? -compactY : 0, 0));
            offset2.InsertKeyFrame(1, new Vector3(0, show ? 0 : -compactY, 0));
            panel.StartAnimation("Offset", offset2);

            batch.End();
        }

        private bool _archiveCollapsed;

        private async void ShowHideArchive(bool show, bool animate)
        {
            if (_archiveCollapsed != show)
            {
                return;
            }

            _archiveCollapsed = !show;
            ArchivedChatsPresenter.Visibility = Visibility.Visible;

            void ShowHideArchiveCompleted()
            {
                ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, 0);
                ArchivedChatsPresenter.Visibility = _archiveCollapsed
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null || !animate || ((ViewModelBase)ViewModel).Settings.HideArchivedChats)
            {
                ShowHideArchiveCompleted();
                return;
            }

            if (ArchivedChatsPanel.ActualWidth == 0)
            {
                await ArchivedChatsPanel.UpdateLayoutAsync();
            }

            var parent = ElementComposition.GetElementVisual(ChatsList);
            var chats = ElementComposition.GetElementVisual(element);

            parent.Clip = chats.Compositor.CreateInsetClip();
            chats.StopAnimation("Offset");

            var batch = chats.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                chats.Offset = new Vector3();
                ShowHideArchiveCompleted();
            };

            var y = ArchivedChatsPresenter.ActualSize.Y;

            ChatsList.Margin = new Thickness(0, Stories.TopPadding, 0, -y);

            var offset0 = chats.Compositor.CreateVector3KeyFrameAnimation();
            offset0.InsertKeyFrame(0, new Vector3(0, show ? -y : 0, 0));
            offset0.InsertKeyFrame(1, new Vector3(0, show ? 0 : -y, 0));
            chats.StartAnimation("Offset", offset0);

            batch.End();
        }

        private bool _shouldGoBackWithDetail = true;

        public void BackRequested()
        {
            if (_shouldGoBackWithDetail && MasterDetail.NavigationService.CanGoBack)
            {
                BootStrapper.Current.RaiseBackRequested();
            }
            else
            {
                _shouldGoBackWithDetail = true;
                OnBackRequested(new BackRequestedRoutedEventArgs());
            }
        }

        private void UpdateFolder(ChatFolderViewModel folder, bool update = true, bool updateBackStack = true)
        {
            CarouselDirection direction = CarouselDirection.None;
            if (folder.ChatList is ChatListArchive)
            {
                direction = CarouselDirection.Next;
            }
            else if (ViewModel.Chats.Items.ChatList is ChatListArchive)
            {
                direction = CarouselDirection.Previous;
            }
            else if (_prevIndex == INDEX_CHATS)
            {
                var nextIndex = ViewModel.Folders.IndexOf(folder);
                var prevIndex = ViewModel.Folders.IndexOf(ViewModel.SelectedFolder);

                if (nextIndex == prevIndex)
                {
                    return;
                }

                direction = nextIndex <= prevIndex
                    ? CarouselDirection.Previous
                    : CarouselDirection.Next;
            }

            ChatsList.ChangeView(direction, () =>
            {
                ViewModel.SelectedFolder = folder;

                if (update)
                {
                    ConvertFolder(folder, updateBackStack);

                    Logger.Info("ChangeView");

                    var scrollingHost = ChatsList.GetScrollViewer();
                    scrollingHost?.TryChangeView(null, 0, null, true);
                }
            });

            if (folder.ChatList is ChatListArchive)
            {
                _shouldGoBackWithDetail = false;
            }

            Search_LostFocus(null, null);
        }

        #region Selection

        private void List_SelectionModeChanged(DependencyObject sender, DependencyProperty dp)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                ShowHideManagePanel(true);
            }
            else
            {
                ShowHideManagePanel(false);
            }

            UpdatePaneToggleButtonVisibility();
        }

        private bool _manageCollapsed = true;

        private void ShowHideManagePanel(bool show)
        {
            if (_manageCollapsed != show)
            {
                return;
            }

            if (show)
            {
                HideTopicList();
            }

            _manageCollapsed = !show;
            ManagePanel.Visibility = Visibility.Visible;

            var manage = ElementComposition.GetElementVisual(ManagePanel);
            //manage.Offset = new Vector3(show ? -20 : 12, 8, 0);
            manage.Opacity = show ? 0 : 1;

            var batch = manage.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                //manage.Offset = new Vector3(show ? 12 : -20, 8, 0);
                manage.Opacity = show ? 1 : 0;

                if (show)
                {
                    ManagePanel.Visibility = Visibility.Visible;
                }
                else
                {
                    ManagePanel.Visibility = Visibility.Collapsed;
                    ViewModel.Chats.SelectedItems.Clear();
                }
            };

            var offset1 = manage.Compositor.CreateVector3KeyFrameAnimation();
            offset1.InsertKeyFrame(show ? 0 : 1, new Vector3(0, 48, 0));
            offset1.InsertKeyFrame(show ? 1 : 0, new Vector3(0, 0, 0));

            var opacity1 = manage.Compositor.CreateScalarKeyFrameAnimation();
            opacity1.InsertKeyFrame(show ? 0 : 1, 0);
            opacity1.InsertKeyFrame(show ? 1 : 0, 1);

            manage.StartAnimation("Translation", offset1);
            manage.StartAnimation("Opacity", opacity1);

            batch.End();

            if (show)
            {
                ManagePanel.Visibility = Visibility.Visible;
            }
            else
            {
                MainHeader.Visibility = Visibility.Visible;
            }
        }

        private void Manage_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                ViewModel.Chats.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                    ? ListViewSelectionMode.None
                    : ListViewSelectionMode.Single;
            }
        }

        public void SetSelectionMode(bool enabled)
        {
            if (enabled)
            {
                ViewModel.Chats.SelectionMode = ListViewSelectionMode.Multiple;
            }
            else
            {
                ViewModel.Chats.SelectionMode = MasterDetail.CurrentState == MasterDetailState.Minimal
                    ? ListViewSelectionMode.None
                    : ListViewSelectionMode.Single;
            }
        }

        public async void SetSelectedItem(Chat chat)
        {
            await System.Threading.Tasks.Task.Delay(100);

            if (ViewModel.Chats.SelectionMode != ListViewSelectionMode.Multiple)
            {
                try
                {
                    ChatsList.SelectedItem = chat;

                    // TODO: would be great, but doesn't seem to work well enough :(
                    //VisualUtilities.QueueCallbackForCompositionRendered(() => ChatsList.SelectedItem = chat);
                }
                catch
                {
                    // All the remote procedure calls must be wrapped in a try-catch block
                }
            }
        }

        public void SetSelectedItems(IList<Chat> chats)
        {
            if (ViewModel.Chats.SelectionMode == ListViewSelectionMode.Multiple)
            {
                try
                {
                    foreach (var item in chats)
                    {
                        if (!ChatsList.SelectedItems.Contains(item))
                        {
                            ChatsList.SelectedItems.Add(item);
                        }
                    }

                    foreach (Chat item in ChatsList.SelectedItems)
                    {
                        if (!chats.Contains(item))
                        {
                            ChatsList.SelectedItems.Remove(item);
                        }
                    }
                }
                catch
                {
                    // SelectedItems likes to throw
                }
            }
        }

        #endregion

        private void Confetti_Completed(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() =>
            {
                UnloadObject(Confetti);
            });
        }

        public static string GetFolderIcon(ChatListFolderFlags folder)
        {
            if (folder == ChatListFolderFlags.ExcludeMuted)
            {
                return Icons.AlertFilled;
            }
            else if (folder == ChatListFolderFlags.ExcludeRead)
            {
                return Icons.ChatUnreadFilled; //FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
            }
            else if (folder == ChatListFolderFlags.ExcludeArchived)
            {
                return Icons.ArchiveFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeContacts)
            {
                return Icons.PersonFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeNonContacts)
            {
                return Icons.PersonQuestionMarkFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeGroups)
            {
                return Icons.PeopleFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeChannels)
            {
                return Icons.MegaphoneFilled;
            }
            else if (folder == ChatListFolderFlags.IncludeBots)
            {
                return Icons.BotFilled;
            }
            else if (folder == ChatListFolderFlags.ExistingChats)
            {
                return Icons.ChatMultipleFilled;
            }
            else if (folder == ChatListFolderFlags.NewChats)
            {
                return Icons.ChatUnreadFilled;
            }

            return null;
        }

        private void ChatFolders_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            var list = sender as TopNavView;
            if (list == null)
            {
                return;
            }

            if (e.Items.Count > 1)
            {
                list.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                var items = ViewModel?.Folders;
                if (items == null || items.Count < 2)
                {
                    list.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    list.CanReorderItems = true;
                }
            }
        }

        private void ChatFolders_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is ChatFolderViewModel folder)
            {
                var items = ViewModel?.Folders;
                var index = items.IndexOf(folder);

                var compare = items[index > 0 ? index - 1 : index + 1];
                if (compare.ChatList is ChatListMain && index > 0 && index < items.Count - 1 && !_clientService.IsPremium)
                {
                    compare = items[index + 1];
                }

                if ((compare.ChatList is ChatListMain || folder.ChatList is ChatListMain) && !_clientService.IsPremium)
                {
                    ViewModel.Handle(new UpdateChatFolders(ViewModel.ClientService.ChatFolders, 0, false));

                    ToastPopup.ShowPromo(ViewModel.NavigationService, string.Format(Strings.LimitReachedReorderFolder, Strings.FilterAllChats), Strings.PremiumMore, new PremiumSourceLimitExceeded(new PremiumLimitTypeChatFolderCount()));
                }
                else
                {
                    var folders = items.Where(x => x.ChatList is ChatListFolder).Select(x => x.ChatFolderId).ToArray();
                    var main = _clientService.IsPremium ? items.IndexOf(items.FirstOrDefault(x => x.ChatList is ChatListMain)) : 0;

                    ViewModel.ClientService.Send(new ReorderChatFolders(folders, main));
                }
            }
        }

        private void ArchivedChats_ActualThemeChanged(FrameworkElement sender, object args)
        {
            ArchivedChats.UpdateChatList(ViewModel.ClientService, new ChatListArchive());
            ArchivedChats.UpdateStoryList(ViewModel.ClientService, new StoryListArchive());
        }

        private async void Downloads_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.NavigationService.ShowPopupAsync(new DownloadsPopup());
        }

        private void Photo_Click(object sender, RoutedEventArgs e)
        {
            if (MasterDetail.CurrentState == MasterDetailState.Minimal && MasterDetail.NavigationService.CanGoBack)
            {
                Root.IsPaneOpen = true;
            }
            else if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }
            else if (_prevIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
            }
            else if (ViewModel.Chats.Items.ChatList is ChatListArchive)
            {
                UpdateFolder(ViewModel.Folders.Count > 0 ? ViewModel.Folders[0] : ChatFolderViewModel.Main);
            }
            else
            {
                Root.IsPaneOpen = true;
            }
        }

        private void ChatsList_GettingFocus(UIElement sender, GettingFocusEventArgs args)
        {
            if (!AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                if (Photo == sender)
                {
                    Photo.UseSystemFocusVisuals = args.Direction != FocusNavigationDirection.None;
                }

                return;
            }

            try
            {
                // ListViewBase ignores GettingFocus events with Direction equals to None
                // What we do here is to simulate the default behavior, so that closing the active chat
                // will move the focus to the last selected item in the chat list if possible.
                if (args.Direction == FocusNavigationDirection.None && args.OldFocusedElement is not ChatListListViewItem)
                {
                    if (!_topicListCollapsed && ViewModel?.Topics.LastSelectedItem is MessageTopicForum forum && TopicListPresenter.TryGetContainer(forum.ForumTopicId, out SelectorItem container))
                    {
                        Logger.Info("Set topic item as focused element");

                        if (args.TrySetNewFocusedElement(container))
                        {
                            args.Handled = true;
                        }
                    }
                    else if (ChatsList.TryGetContainer(ViewModel?.Chats.LastSelectedItem ?? 0, out container))
                    {
                        Logger.Info("Set chat item as focused element");

                        if (args.TrySetNewFocusedElement(container))
                        {
                            args.Handled = true;
                        }
                    }
                    else if (sender != ChatsList)
                    {
                        Logger.Info("Set chat list as focused element");

                        if (args.TrySetNewFocusedElement(ChatsList))
                        {
                            args.Handled = true;
                        }
                    }

                    if (args.NewFocusedElement is ChatListListViewItem item)
                    {
                        // Let's disable the awkward focus rect that would appear on activation.
                        // ChatListListViewItem.OnLostFocus takes care of reenabling it.
                        item.UseSystemFocusVisuals = false;
                    }
                }
            }
            catch
            {
                // All the remote procedure calls must be wrapped in a try-catch block
            }
        }

        private void ChatFolders_ChoosingGroupHeaderContainer(ListViewBase sender, ChoosingGroupHeaderContainerEventArgs args)
        {
            args.GroupHeaderContainer = new ListViewHeaderItem
            {
                Visibility = args.GroupIndex == 0
                    ? Visibility.Collapsed
                    : Visibility.Visible
            };
        }

        private void ChatsList_ChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ChatListListViewItem(ChatsList);
                args.ItemContainer.ContentTemplate = ChatsList.ItemTemplate;
                args.ItemContainer.ContextRequested += Chat_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        public void PopupOpened()
        {
            NavigationService.Window.SetTitleBar(null);

            if (NavigationService.Frame.Content is IActivablePage page)
            {
                page.PopupOpened();
            }
        }

        public void PopupClosed()
        {
            NavigationService.Window.SetTitleBar(TitleBarHandle);

            if (NavigationService.Frame.Content is IActivablePage page)
            {
                page.PopupClosed();
            }
        }

        #region Context menu

        private void DialogsSearchPanel_ItemContextRequested(UIElement sender, ItemContextRequestedEventArgs args)
        {
            if (args.Item is SearchResult result && result.Chat != null)
            {
                var element = sender as FrameworkElement;
                var chat = result.Chat;

                Chat_ContextRequested(chat, element, args.EventArgs, false);
            }
        }

        private void Chat_ContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var element = sender as FrameworkElement;
            var chat = ChatsList.ItemFromContainer(element) as Chat;

            Chat_ContextRequested(chat, sender, args, true);
        }

        private async void Chat_ContextRequested(Chat chat, UIElement sender, ContextRequestedEventArgs args, bool allowSelection)
        {
            var viewModel = ViewModel.Chats;
            if (viewModel == null)
            {
                return;
            }

            var flyout = new MenuFlyout();
            var element = sender as FrameworkElement;

            var position = chat?.GetPosition(viewModel.Items.ChatList);
            if (position == null)
            {
                return;
            }

            var muted = ViewModel.ClientService.Notifications.IsMuted(chat);
            var archived = chat.Positions.Any(x => x.List is ChatListArchive);

            if (DialogArchive_Loaded(chat))
            {
                // Suggest to unarchive only when archive is open
                if (viewModel.Items.ChatList is ChatListArchive && archived)
                {
                    flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ArchiveChat, chat, Strings.Unarchive, Icons.Unarchive);
                }
                else if (viewModel.Items.ChatList is not ChatListArchive && !archived)
                {
                    flyout.CreateFlyoutItem(DialogArchive_Loaded, viewModel.ArchiveChat, chat, Strings.Archive, Icons.Archive);
                }
            }

            flyout.CreateFlyoutItem(DialogPin_Loaded, viewModel.PinChat, chat, position.IsPinned ? Strings.UnpinFromTop : Strings.PinToTop, position.IsPinned ? Icons.PinOff : Icons.Pin);

            var chatLists = await ViewModel.ClientService.SendAsync(new GetChatListsToAddChat(chat.Id)) as ChatLists;
            if (chatLists != null && chatLists.ChatListsValue.Count > 0)
            {
                var folders = ViewModel.ClientService.ChatFolders.ToDictionary(x => x.Id);

                var item = new MenuFlyoutSubItem();
                item.Text = Strings.FilterAddTo;
                item.Icon = MenuFlyoutHelper.CreateIcon(Icons.FolderAdd);

                foreach (var chatList in chatLists.ChatListsValue.OfType<ChatListFolder>())
                {
                    // Skip current folder from "Add to folder" list to avoid confusion
                    if (chatList.AreTheSame(viewModel.Items.ChatList))
                    {
                        continue;
                    }

                    if (folders.TryGetValue(chatList.ChatFolderId, out ChatFolderInfo folder))
                    {
                        var icon = Icons.ParseFolder(folder.Icon);
                        var glyph = Icons.FolderToGlyph(icon);

                        // TODO: Custom emojis
                        item.CreateFlyoutItem(viewModel.AddToFolder, (folder.Id, chat), folder.Name.Text.Text, glyph.Item1);
                    }
                }

                if (folders.Count < 10 && item.Items.Count > 0)
                {
                    item.CreateFlyoutSeparator();
                    item.CreateFlyoutItem(viewModel.CreateFolder, chat, Strings.CreateNewFilter, Icons.Add);
                }

                if (item.Items.Count > 0)
                {
                    flyout.Items.Add(item);
                }
            }

            if (viewModel.Items.ChatList is ChatListFolder chatListFolder)
            {
                var response = await ViewModel.ClientService.SendAsync(new GetChatFolder(chatListFolder.ChatFolderId)) as ChatFolder;
                if (response != null)
                {
                    response.IncludedChatIds.Remove(chat.Id);

                    if (response.Any())
                    {
                        flyout.CreateFlyoutItem(viewModel.RemoveFromFolder, (chatListFolder.ChatFolderId, chat), Strings.FilterRemoveFrom, Icons.FolderMove);
                    }
                }
            }

            if (DialogNotify_Loaded(chat))
            {
                var silent = ViewModel.ClientService.Notifications.IsSilent(chat);

                var mute = new MenuFlyoutSubItem();
                mute.Text = Strings.Mute;
                mute.Icon = MenuFlyoutHelper.CreateIcon(muted ? Icons.Alert : Icons.AlertOff);

                if (muted is false)
                {
                    mute.CreateFlyoutItem(ViewModel.Chats.SetChatSound, Tuple.Create<Chat, bool>(chat, !silent),
                        silent ? Strings.SoundOn : Strings.SoundOff,
                        silent ? Icons.MusicNote2 : Icons.MusicNoteOff2);
                }

                mute.CreateFlyoutItem(ViewModel.Chats.MuteChatFor, Tuple.Create<Chat, int?>(chat, 60 * 60), Strings.MuteFor1h, Icons.ClockAlarmHour);
                mute.CreateFlyoutItem(ViewModel.Chats.MuteChatFor, Tuple.Create<Chat, int?>(chat, null), Strings.MuteForPopup, Icons.AlertSnooze);

                var toggle = mute.CreateFlyoutItem(
                    ViewModel.Chats.NotifyChat,
                    chat,
                    muted ? Strings.UnmuteNotifications : Strings.MuteNotifications,
                    muted ? Icons.Speaker3 : Icons.SpeakerOff);

                if (muted is false)
                {
                    toggle.Foreground = BootStrapper.Current.Resources["DangerButtonBackground"] as Brush;
                }

                flyout.Items.Add(mute);

            }

            flyout.CreateFlyoutItem(DialogMark_Loaded, viewModel.MarkChatAsRead, chat, chat.IsUnread() ? Strings.MarkAsRead : Strings.MarkAsUnread, chat.IsUnread() ? Icons.MarkAsRead : Icons.MarkAsUnread);
            flyout.CreateFlyoutItem(DialogClear_Loaded, viewModel.ClearChat, chat, Strings.ClearHistory, Icons.Broom);
            flyout.CreateFlyoutItem(DialogDelete_Loaded, viewModel.DeleteChat, chat, DialogDelete_Text(chat), Icons.Delete, destructive: true);

            if (viewModel.SelectionMode != ListViewSelectionMode.Multiple)
            {
                if (ApiInfo.HasMultipleViews)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.OpenChat, chat, Strings.OpenInNewWindow, Icons.WindowNew);
                }

                if (allowSelection)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(viewModel.SelectChat, chat, Strings.Select, Icons.CheckmarkCircle);
                }
            }

            flyout.ShowAt(sender, args);
        }

        private bool DialogMark_Loaded(Chat chat)
        {
            return true;
        }

        private bool DialogPin_Loaded(Chat chat)
        {
            //if (!chat.IsPinned)
            //{
            //    var count = ViewModel.Dialogs.LegacyItems.Where(x => x.IsPinned).Count();
            //    var max = ViewModel.ClientService.Config.PinnedDialogsCountMax;

            //    return count < max ? Visibility.Visible : Visibility.Collapsed;
            //}

            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            return true;
        }

        private bool DialogArchive_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (ViewModel.ClientService.IsSavedMessages(chat) || position?.Source != null || chat.Id == 777000)
            {
                return false;
            }

            return true;
        }

        private bool DialogNotify_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (ViewModel.ClientService.IsSavedMessages(chat) || position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return false;
            }

            return true;
        }

        public bool DialogClear_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source != null)
            {
                return false;
            }

            if (chat.Type is ChatTypeSupergroup super)
            {
                var supergroup = ViewModel.ClientService.GetSupergroup(super.SupergroupId);
                if (supergroup != null)
                {
                    return !supergroup.HasActiveUsername() && !super.IsChannel;
                }
            }

            return true;
        }

        private bool DialogDelete_Loaded(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source is ChatSourceMtprotoProxy)
            {
                return false;
            }

            //if (dialog.With is TLChannel channel)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerUser userPeer)
            //{
            //    return Visibility.Visible;
            //}
            //else if (dialog.Peer is TLPeerChat chatPeer)
            //{
            //    return dialog.With is TLChatForbidden || dialog.With is TLChatEmpty ? Visibility.Visible : Visibility.Collapsed;
            //}

            //return Visibility.Collapsed;

            return true;
        }

        private string DialogDelete_Text(Chat chat)
        {
            var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
            if (position?.Source is ChatSourcePublicServiceAnnouncement)
            {
                return Strings.PsaHide;
            }
            else if (chat.Type is ChatTypeSupergroup super)
            {
                return super.IsChannel ? Strings.LeaveChannelMenu : Strings.LeaveMegaMenu;
            }
            else if (chat.Type is ChatTypeBasicGroup)
            {
                return Strings.DeleteAndExit;
            }

            return Strings.Delete;
        }

        #endregion

        private void Status_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsPremium)
            {
                EmojiMenuFlyout.ShowAt(ViewModel.ClientService, EmojiDrawerMode.EmojiStatus, LogoEmoji, EmojiFlyoutAlignment.TopLeft);
            }
        }

        public void ShowTopicList(Chat chat)
        {
            ViewModel.Topics.SetChat(chat);
            ShowHideTopicList(true);
            TopicListPresenter?.UpdateChat(chat);

            var currentChat = MasterDetail.NavigationService.GetChatFromBackStack();
            if (currentChat.ChatId == chat.Id)
            {
                UpdateListViewsSelectedItem(currentChat);
            }
            else
            {
                UpdateListViewsSelectedItem(new ChatMessageTopic(chat.Id, null));
            }
        }

        public void HideTopicList(bool fromSelection = false)
        {
            var chatId = ViewModel.Topics.Chat?.Id;

            ShowHideTopicList(false);

            if (ViewModel.Chats.SelectedItem == chatId)
            {
                UpdateListViewsSelectedItem(MasterDetail.NavigationService.GetChatFromBackStack(), fromSelection);
            }
        }

        private bool _topicListCollapsed = true;

        private void ShowHideTopicList(bool show)
        {
            if (_topicListCollapsed != show)
            {
                return;
            }

            FindName(nameof(TopicListPresenter));
            TopicListPresenter.DataContext = ViewModel.Topics;

            ViewModel.Topics.Delegate = TopicListPresenter;

            _topicListCollapsed = !show;
            TopicListPresenter.Visibility = Visibility.Visible;

            MasterDetail.CornerRadius = new CornerRadius(show ? 0 : 8, 0, 0, 0);
            Canvas.SetZIndex(ChatsRoot, show ? 1 : 0);

            if (show)
            {
                Stories.Collapse();
                TopicListPresenter.Focus(FocusState.Programmatic);
            }
            else
            {
                ViewModel.Topics.SetChat(null);
            }

            var padding = ChatTabs != null
                ? _tabsTopCollapsed ? -74 : -78
                : -14;

            var margin = _tabsTopCollapsed ? 38 : 74;

            DialogsPanel.Margin = new Thickness(0, -margin, 0, 0);
            TopicListPresenter.Margin = new Thickness(68, padding, 0, 0);

            void ShowHideTopicListCompleted()
            {
                if (_topicListCollapsed)
                {
                    TopicListPresenter.Visibility = Visibility.Collapsed;
                }
            }

            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                ShowHideTopicListCompleted();
                return;
            }

            var scrollingHost = VisualTreeHelper.GetChild(element, 1) as UIElement;

            var chats = ElementComposition.GetElementVisual(element);
            var panel = ElementComposition.GetElementVisual(TopicListPresenter);

            var dialogs = ElementComposition.GetElementVisual(DialogsPanel);
            var header = ElementComposition.GetElementVisual(Header);

            var compositor = chats.Compositor;

            var inset = 68;
            var width = ChatsList.ActualSize.X - inset;

            var sourceOffset = new Vector2(inset, 0);
            var sourceSize = new Vector2(width, ChatsList.ActualSize.Y);

            var redirect = compositor.CreateRedirectVisual(scrollingHost, sourceOffset, sourceSize);
            redirect.Offset = new Vector3(sourceOffset, 0);
            redirect.Clip = compositor.CreateInsetClip();

            ElementCompositionPreview.SetElementChildVisual(ChatsList, redirect);
            ElementCompositionPreview.SetIsTranslationEnabled(TopicListPresenter, true);

            chats.Clip = compositor.CreateInsetClip(0, 0, width, 0);
            dialogs.Clip = null;

            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                redirect.Size = Vector2.Zero;
                ElementCompositionPreview.SetElementChildVisual(ChatsList, null);

                if (_topicListCollapsed)
                {
                    chats.Clip = null;
                    TopicListPresenter.Visibility = Visibility.Collapsed;

                    dialogs.Properties.InsertVector3("Translation", Vector3.Zero);
                    DialogsPanel.Margin = new Thickness(0);
                }
                else
                {
                    dialogs.Clip = compositor.CreateInsetClip(0, (float)ChatsList.Margin.Top, 0, 0);
                }
            };

            var offset0 = compositor.CreateScalarKeyFrameAnimation();
            offset0.InsertKeyFrame(0, show ? width : 0);
            offset0.InsertKeyFrame(1, show ? 0 : width);
            offset0.Duration = Constants.FastAnimation;

            var offset1 = compositor.CreateScalarKeyFrameAnimation();
            offset1.InsertKeyFrame(0, show ? inset : -width + inset);
            offset1.InsertKeyFrame(1, show ? -width + inset : inset);
            offset1.Duration = Constants.FastAnimation;

            var clip0 = compositor.CreateScalarKeyFrameAnimation();
            clip0.InsertKeyFrame(0, show ? 0 : width);
            clip0.InsertKeyFrame(1, show ? width : 0);
            clip0.Duration = Constants.FastAnimation;

            panel.StartAnimation("Translation.X", offset0);
            redirect.StartAnimation("Offset.X", offset1);
            redirect.Clip.StartAnimation("LeftInset", clip0);

            ChatsList.UpdateViewState(show ? MasterDetailState.Compact : MasterDetail.CurrentState);

            var offset2 = compositor.CreateScalarKeyFrameAnimation();
            offset2.InsertKeyFrame(0, show ? margin : 0);
            offset2.InsertKeyFrame(1, show ? 0 : margin);
            offset2.Duration = Constants.FastAnimation;

            var clip1 = compositor.CreateScalarKeyFrameAnimation();
            clip1.InsertKeyFrame(0, show ? 0 : 40);
            clip1.InsertKeyFrame(1, show ? 40 : 0);
            clip1.Duration = Constants.FastAnimation;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 1 : 0);
            opacity.InsertKeyFrame(1, show ? 0 : 1);
            opacity.Duration = Constants.FastAnimation;

            dialogs.StartAnimation("Translation.Y", offset2);
            header.StartAnimation("Opacity", opacity);

            if (ChatTabs != null)
            {
                var tabs = ElementComposition.GetElementVisual(ChatTabs);
                tabs.StartAnimation("Opacity", opacity);
            }

            batch.End();
        }

        private void ChatList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var element = VisualTreeHelper.GetChild(ChatsList, 0) as UIElement;
            if (element == null)
            {
                return;
            }

            var chats = ElementComposition.GetElementVisual(element);
            if (chats.Clip is InsetClip inset && inset.RightInset != 0 && TopicListPresenter != null)
            {
                inset.RightInset = TopicListPresenter.ActualSize.X;
            }
        }

        private void Banner_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MasterDetail.BackgroundMargin = new Thickness(0, -e.NewSize.Height, 0, 0);
        }

        private void Stories_Expanding(object sender, EventArgs e)
        {
            if (_prevIndex != INDEX_CHATS)
            {
                SetPivotSelectedIndex(INDEX_CHATS);
                ViewModel.RaisePropertyChanged(nameof(ViewModel.SelectedFolder));
            }
            else if (!_searchCollapsed)
            {
                Search_LostFocus(null, null);
            }

            HideTopicList();
        }

        private void ComposeButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.ClientService.FreezeState.IsFrozen)
            {
                ViewModel.NavigationService.ShowPopup(new FrozenPopup(ViewModel.ClientService.FreezeState));
            }
            else
            {
                ViewModel.NavigationService.ShowPopup(new ContactsPopup
                {
                    Title = Strings.NewMessageTitle
                });
            }
        }

        private void ChatCell_StoryClick(object sender, Chat chat)
        {
            if (sender is ActiveStoriesSegments segments)
            {
                segments.Open(ViewModel.NavigationService, ViewModel.ClientService, chat, 48, story =>
                {
                    var container = ChatsList.ContainerFromItem(story.Chat) as SelectorItem;
                    if (container != null)
                    {
                        var transform = container.TransformToVisual(null);
                        var point = transform.TransformPoint(new Point());

                        return new Rect(point.X + 4 + 8, point.Y + 4 + 8, 40, 40);
                    }

                    return Rect.Empty;
                });
            }
        }

        public Task UpdateLayoutAsync()
        {
            if (ChatsList.IsConnected)
            {
                if (ChatsList.ItemsPanelRoot != null)
                {
                    return ChatsList.ItemsPanelRoot.UpdateLayoutAsync();
                }

                return ChatsList.UpdateLayoutAsync();
            }

            return Task.CompletedTask;
        }

        #region Chat List

        private void Chats_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            try
            {
                if (e.Items[0] is Chat chat)
                {
                    var position = chat.GetPosition(ViewModel.Chats.Items.ChatList);
                    if (position == null || !position.IsPinned || e.Items.Count > 1 || ChatsList.SelectionMode == ListViewSelectionMode.Multiple)
                    {
                        ChatsList.CanReorderItems = false;
                        e.Cancel = true;
                    }
                    else
                    {
                        ChatsList.CanReorderItems = true;
                    }
                }
            }
            catch
            {
                ChatsList.CanReorderItems = false;
                e.Cancel = true;
            }
        }

        private void Chats_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            ChatsList.CanReorderItems = false;

            var chatList = ViewModel.Chats.Items.ChatList;
            if (chatList == null)
            {
                return;
            }

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is Chat chat)
            {
                var items = ViewModel.Chats.Items;
                if (items.Count == 1)
                {
                    return;
                }

                var index = items.IndexOf(chat);
                var compare = items[index > 0 ? index - 1 : index + 1];

                var position = compare.GetPosition(items.ChatList);
                if (position == null)
                {
                    return;
                }

                if (position.Source != null && index > 0)
                {
                    position = items[index + 1].GetPosition(items.ChatList);
                }

                if (position.IsPinned)
                {
                    var pinned = items.Where(x =>
                    {
                        var position = x.GetPosition(items.ChatList);
                        if (position == null)
                        {
                            return false;
                        }

                        return position.IsPinned;
                    }).Select(x => x.Id).ToArray();

                    ViewModel.ClientService.Send(new SetPinnedChats(chatList, pinned));
                }
                else
                {
                    var real = chat.GetPosition(items.ChatList);
                    if (real != null)
                    {
                        items.Handle(chat.Id, real.Order);
                    }
                }
            }
        }

        private void ChatsList_Swiped(object sender, ChatListSwipedEventArgs e)
        {
            ScrollFolder(e.Direction == CarouselDirection.Next ? 1 : -1, true);
        }

        #endregion

        private bool _testLeak;

        [Conditional("DEBUG")]
        public void LeakTest(bool enable)
        {
            if (!_testLeak)
            {
                if (enable)
                {
                    _testLeak = true;
                }

                return;
            }

            return;

            _viewModel = null;
            LayoutRoot.Children.Clear();

            LayoutRoot = null;
            State = null;
            TitleBarrr = null;
            ChatTabsLeft = null;
            Photo = null;
            MasterDetail = null;
            Confetti = null;
            Playback = null;
            CallBanner = null;
            Header = null;
            Stories = null;
            SettingsRoot = null;
            SettingsView = null;
            DialogsSearchPanel = null;
            DialogsPanel = null;
            ChatsPanel = null;
            Downloads = null;
            ManagePanel = null;
            ButtonManage = null;
            ManageCount = null;
            ManageMute = null;
            ManageMark = null;
            ManageClear = null;
            UpdateShadow = null;
            UpdateCloud = null;
            ChatListHeader = null;
            ChatsList = null;
            TopicListPresenter = null;
            EmptyState = null;
            ArchivedChatsPresenter = null;
            ArchivedChatsPanel = null;
            ArchivedChats = null;
            SetBirthdateCard = null;
            UnconfirmedCard = null;
            ChatTabs = null;
            ChatTabsView = null;
            ChatFolders = null;
            MainHeader = null;
            SearchField = null;
            ChatsOptions = null;
            Proxy = null;
            Lock = null;
            ChatFoldersSide = null;
            TitleBarHandle = null;
            TitleBarLogo = null;
            TitleText = null;
            StateLabel = null;
            MemoryLabel = null;
            LogoBasic = null;
            LogoEmoji = null;
        }

        private void ChatFolders_ItemClick(object sender, ItemClickEventArgs e)
        {
            if (e.ClickedItem == ViewModel.SelectedFolder)
            {
                var scrollViewer = ChatsList.GetScrollViewer();
                scrollViewer?.TryChangeView(null, 0, null);
            }
        }
    }
}
