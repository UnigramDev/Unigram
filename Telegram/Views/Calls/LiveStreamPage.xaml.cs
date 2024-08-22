//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.Devices.Enumeration;
using Windows.Devices.Input;
using Windows.System.Display;
using Windows.UI;
using Microsoft.UI.Composition;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace Telegram.Views.Calls
{
    public sealed partial class LiveStreamPage : Page
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private IVoipGroupService _service;

        private VoipGroupManager _manager;
        private bool _disposed;

        private readonly DispatcherTimer _inactivityTimer;
        private readonly DispatcherTimer _scheduledTimer;
        private readonly DispatcherTimer _debouncerTimer;

        private readonly DisplayRequest _displayRequest = new();

        public LiveStreamPage(IClientService clientService, IEventAggregator aggregator, IVoipGroupService voipService)
        {
            InitializeComponent();
            Logger.Info();

            _clientService = clientService;
            _aggregator = aggregator;

            _inactivityTimer = new DispatcherTimer();
            _inactivityTimer.Interval = TimeSpan.FromSeconds(2);
            _inactivityTimer.Tick += (s, args) =>
            {
                _inactivityTimer.Stop();
                ShowHideTransport(false);
            };
            _inactivityTimer.Start();

            _scheduledTimer = new DispatcherTimer();
            _scheduledTimer.Interval = TimeSpan.FromSeconds(1);
            _scheduledTimer.Tick += OnTick;

            _debouncerTimer = new DispatcherTimer();
            _debouncerTimer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncerTimer.Tick += (s, args) =>
            {
                _debouncerTimer.Stop();
            };

            _service = voipService;
            _service.AvailableStreamsChanged += OnAvailableStreamsChanged;

            var titleBar = ApplicationView.GetForCurrentView().TitleBar;
            titleBar.ButtonBackgroundColor = Colors.Transparent;
            titleBar.ButtonForegroundColor = Colors.White;
            titleBar.ButtonInactiveBackgroundColor = Colors.Transparent;
            titleBar.ButtonInactiveForegroundColor = Colors.White;
            titleBar.ButtonHoverBackgroundColor = ColorEx.FromHex(0x19FFFFFF);
            titleBar.ButtonHoverForegroundColor = ColorEx.FromHex(0xCCFFFFFF);
            titleBar.ButtonPressedBackgroundColor = ColorEx.FromHex(0x33FFFFFF);
            titleBar.ButtonPressedForegroundColor = ColorEx.FromHex(0x99FFFFFF);

            Window.Current.SetTitleBar(TitleArea);

            if (voipService.Call != null)
            {
                Update(voipService.Call, voipService.CurrentUser);
            }

            if (voipService.Manager != null)
            {
                Connect(voipService.Manager);
            }

            ElementCompositionPreview.SetIsTranslationEnabled(Viewport, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedInfo, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedGlyph, true);
            //ViewportAspect.Constraint = new Size(16, 9);

            OnAvailableStreamsChanged();
        }

        protected override void OnPointerMoved(PointerRoutedEventArgs e)
        {
            _inactivityTimer.Stop();
            ShowHideTransport(true);

            base.OnPointerMoved(e);

            if (e.OriginalSource is FrameworkElement element)
            {
                var button = element.GetParentOrSelf<ButtonBase>();
                if (button != null)
                {
                    return;
                }
            }

            _inactivityTimer.Start();
        }

        protected override void OnPointerReleased(PointerRoutedEventArgs e)
        {
            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
            {
                _inactivityTimer.Stop();
                ShowHideTransport(true);
            }

            base.OnPointerReleased(e);
        }

        private bool _transportCollapsed = false;
        private bool _transportFocused = false;
        private bool _transportUnavailable = false;

        private void ShowHideTransport(bool show)
        {
            if (show != _transportCollapsed || ((_transportFocused || _transportUnavailable) && !show))
            {
                return;
            }

            _transportCollapsed = !show;
            TopButtons.IsHitTestVisible = false;
            BottomPanel.IsHitTestVisible = false;

            var top = ElementComposition.GetElementVisual(TopButtons);
            var top1 = ElementComposition.GetElementVisual(TitlePanel);
            var top2 = ElementComposition.GetElementVisual(TopShadow);
            var top3 = ElementComposition.GetElementVisual(SubtitleInfo);
            var bottom = ElementComposition.GetElementVisual(BottomPanel);
            var bottom1 = ElementComposition.GetElementVisual(BottomShadow);

            var batch = top.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                TopButtons.IsHitTestVisible = !_transportCollapsed;
                BottomPanel.IsHitTestVisible = !_transportCollapsed;
            };

            var opacity = top.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            top.StartAnimation("Opacity", opacity);
            top1.StartAnimation("Opacity", opacity);
            top2.StartAnimation("Opacity", opacity);
            top3.StartAnimation("Opacity", opacity);
            bottom.StartAnimation("Opacity", opacity);
            bottom1.StartAnimation("Opacity", opacity);

            batch.End();
        }

        private void Transport_GotFocus(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is Control control && control.FocusState is FocusState.Keyboard or FocusState.Programmatic)
            {
                _transportFocused = true;
                ShowHideTransport(true);
            }
        }

        private void Transport_LostFocus(object sender, RoutedEventArgs e)
        {
            _transportFocused = false;
            _inactivityTimer.Start();
        }

        private void OnAvailableStreamsChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(OnAvailableStreamsChanged);
        }

        private void OnAvailableStreamsChanged()
        {
            if (_service.AvailableStreamsCount > 0)
            {
                _transportUnavailable = false;
                _inactivityTimer.Start();

                NoStream.Visibility = Visibility.Collapsed;
                Viewport.Visibility = Visibility.Visible;
            }
            else
            {
                _transportUnavailable = true;
                ShowHideTransport(true);

                if (_clientService.TryGetSupergroup(_service.Chat, out Supergroup supergroup))
                {
                    TextBlockHelper.SetMarkdown(NoStream, supergroup.Status is ChatMemberStatusCreator ? Strings.NoRtmpStreamFromAppOwner : string.Format(Strings.NoRtmpStreamFromAppViewer, _service.Chat.Title));
                }
                else if (_clientService.TryGetBasicGroup(_service.Chat, out BasicGroup basicGroup))
                {
                    TextBlockHelper.SetMarkdown(NoStream, basicGroup.Status is ChatMemberStatusCreator ? Strings.NoRtmpStreamFromAppOwner : string.Format(Strings.NoRtmpStreamFromAppViewer, _service.Chat.Title));
                }

                NoStream.Visibility = Visibility.Visible;
                Viewport.Visibility = Visibility.Collapsed;
            }
        }

        private void OnTick(object sender, object e)
        {
            if (_service != null && _service.Call != null && _service.Call.ScheduledStartDate != 0)
            {
                StartsIn.Text = _service.Call.GetStartsIn();
            }
            else
            {
                _scheduledTimer.Stop();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _displayRequest.RequestActive();
            }
            catch { }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();

            try
            {
                _displayRequest.RequestRelease();
            }
            catch { }
        }

        public void Dispose(bool? discard = null)
        {
            if (discard == true && _service != null)
            {
                _service.DiscardAsync();
            }
            else if (discard == false && _service != null)
            {
                _service.LeaveAsync();
            }

            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_manager != null)
            {
                _manager.NetworkStateUpdated -= OnNetworkStateUpdated;

                _manager.AddUnifiedVideoOutput(null);

                _manager = null;
            }

            if (_service != null)
            {
                _service.AvailableStreamsChanged -= OnAvailableStreamsChanged;
                _service = null;
            }

            Viewport.RemoveFromVisualTree();
        }

        public void Connect(VoipGroupManager controller)
        {
            if (_disposed)
            {
                return;
            }

            if (_manager != null)
            {
                // Let's avoid duplicated events
                _manager.NetworkStateUpdated -= OnNetworkStateUpdated;

                _manager.AddUnifiedVideoOutput(null);
            }

            controller.NetworkStateUpdated += OnNetworkStateUpdated;

            controller.AddUnifiedVideoOutput(Viewport);

            _manager = controller;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
        }

        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            if (view.IsFullScreenMode)
            {
                view.ExitFullScreenMode();

                Resize.Glyph = Icons.ArrowMaximize;
            }
            else if (view.TryEnterFullScreenMode())
            {
                Resize.Glyph = Icons.ArrowMinimize;
            }
        }

        public void Update(GroupCall call, GroupCallParticipant currentUser)
        {
            if (_disposed)
            {
                return;
            }

            this.BeginOnUIThread(() =>
            {
                TitleInfo.Text = call.Title.Length > 0 ? call.Title : _clientService.GetTitle(_service.Chat);

                RecordingInfo.Visibility = call.RecordDuration > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (call.ScheduledStartDate != 0)
                {
                    var date = Formatter.ToLocalTime(call.ScheduledStartDate);
                    var duration = date - DateTime.Now;

                    if (duration.TotalDays < 1)
                    {
                        _scheduledTimer.Start();
                    }
                    else
                    {
                        _scheduledTimer.Stop();
                    }

                    FindName(nameof(ScheduledPanel));
                    SubtitleInfo.Text = _service.IsChannel ? Strings.VoipChannelScheduledVoiceChat : Strings.VoipGroupScheduledVoiceChat;
                    ParticipantsPanel.Visibility = Visibility.Collapsed;
                    ScheduledInfo.Text = duration < TimeSpan.Zero ? Strings.VoipChatLateBy : Strings.VoipChatStartsIn;
                    StartsAt.Text = call.GetStartsAt();
                    StartsIn.Text = call.GetStartsIn();

                    Menu.Visibility = Visibility.Collapsed;
                    Resize.Visibility = Visibility.Collapsed;

                    var padding = 0;

                    foreach (var item in TopButtons.Children)
                    {
                        if (item.Visibility == Visibility.Visible)
                        {
                            if (padding > 0)
                            {
                                padding -= 8;
                            }

                            padding += 40;
                        }
                    }

                    TitlePanel.Margin = new Thickness(padding > 0 ? padding - 8 : 0, 0, 0, 0);
                    SubtitleInfo.Margin = new Thickness(padding > 0 ? padding + 4 : 12, -8, 0, 12);
                }
                else
                {
                    _scheduledTimer.Stop();

                    if (ScheduledPanel != null)
                    {
                        UnloadObject(ScheduledPanel);
                    }

                    Menu.Visibility = Visibility.Visible;
                    Resize.Visibility = Visibility.Visible;

                    var padding = 0;

                    foreach (var item in TopButtons.Children)
                    {
                        if (item.Visibility == Visibility.Visible)
                        {
                            if (padding > 0)
                            {
                                padding -= 8;
                            }

                            padding += 40;
                        }
                    }

                    TitlePanel.Margin = new Thickness(padding > 0 ? padding - 8 : 0, 0, 0, 0);
                    SubtitleInfo.Margin = new Thickness(padding > 0 ? padding + 4 : 12, -8, 0, 12);

                    SubtitleInfo.Text = Locale.Declension(Strings.R.Participants, call.ParticipantCount);
                    ParticipantsPanel.Visibility = Visibility.Visible;
                }

                UpdateNetworkState(call, currentUser, _service.IsConnected);
            });
        }

        private void OnNetworkStateUpdated(VoipGroupManager sender, GroupNetworkStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() =>
            {
                if (_service != null)
                {
                    UpdateNetworkState(_service.Call, _service.CurrentUser, args.IsConnected);
                }
            });
        }

        private Task ConsolidateAsync()
        {
            if (Window.Current.Content is RootPage root)
            {
                root.PresentContent(null);
                return Task.CompletedTask;
            }

            return WindowContext.Current.ConsolidateAsync();
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            var chat = _service?.Chat;
            var call = _service?.Call;

            if (chat == null || call == null)
            {
                await ConsolidateAsync();
                return;
            }

            if (call.CanBeManaged)
            {
                var popup = new MessagePopup
                {
                    RequestedTheme = ElementTheme.Dark,
                    Title = _service.IsChannel ? Strings.VoipChannelLeaveAlertTitle : Strings.VoipGroupLeaveAlertTitle,
                    Message = _service.IsChannel ? Strings.VoipChannelLeaveAlertText : Strings.VoipGroupLeaveAlertText,
                    PrimaryButtonText = Strings.VoipGroupLeave,
                    SecondaryButtonText = Strings.Cancel,
                    CheckBoxLabel = _service.IsChannel ? Strings.VoipChannelLeaveAlertEndChat : Strings.VoipGroupLeaveAlertEndChat
                };

                var confirm = await popup.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    Dispose(popup.IsChecked == true);
                    await ConsolidateAsync();
                }
            }
            else
            {
                Dispose(false);
                await ConsolidateAsync();
            }
        }

        private async void Discard()
        {
            var chat = _service?.Chat;
            var call = _service?.Call;

            if (chat == null || call == null)
            {
                await ConsolidateAsync();
                return;
            }

            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = _service.IsChannel ? Strings.VoipChannelEndAlertTitle : Strings.VoipGroupEndAlertTitle;
            popup.Message = _service.IsChannel ? Strings.VoipChannelEndAlertText : Strings.VoipGroupEndAlertText;
            popup.PrimaryButtonText = Strings.VoipGroupEnd;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                Dispose(true);
                await ConsolidateAsync();
            }
        }

        private void UpdateNetworkState(GroupCall call, GroupCallParticipant currentUser, bool? connected = null)
        {

        }

        private async void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var chat = _service?.Chat;
            var call = _service?.Call;

            if (chat == null || call == null)
            {
                return;
            }

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(SetTitle, _service.IsChannel ? Strings.VoipChannelEditTitle : Strings.VoipGroupEditTitle, Icons.Edit);
            }

            if (call.CanBeManaged && call.ScheduledStartDate == 0)
            {
                if (call.RecordDuration > 0)
                {
                    flyout.CreateFlyoutItem(StopRecording, Strings.VoipGroupStopRecordCall, Icons.Record);
                }
                else
                {
                    flyout.CreateFlyoutItem(StartRecording, Strings.VoipGroupRecordCall, Icons.Record);
                }
            }

            if (call.ScheduledStartDate == 0)
            {
                flyout.CreateFlyoutSeparator();

                var outputId = _service.CurrentAudioOutput;

                var defaultOutput = new ToggleMenuFlyoutItem();
                defaultOutput.Text = Strings.Default;
                defaultOutput.IsChecked = outputId == string.Empty;
                defaultOutput.Click += (s, args) =>
                {
                    _service.CurrentAudioOutput = string.Empty;
                };

                var output = new MenuFlyoutSubItem();
                output.Text = Strings.VoipDeviceOutput;
                output.Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3);
                output.Items.Add(defaultOutput);

                var outputDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioRender);
                foreach (var device in outputDevices)
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = outputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _service.CurrentAudioOutput = device.Id;
                    };

                    output.Items.Add(deviceItem);
                }

                flyout.Items.Add(output);
            }

            //flyout.CreateFlyoutItem(ShareInviteLink, Strings.VoipGroupShareInviteLink, Icons.Link);

            if (chat.Type is ChatTypeSupergroup && _clientService.TryGetSupergroup(chat, out Supergroup supergroup))
            {
                if (supergroup.Status is ChatMemberStatusCreator)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(StreamWith, Strings.VoipStreamWith, Icons.Live);
                }
            }
            else if (chat.Type is ChatTypeBasicGroup && _clientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusCreator)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(StreamWith, Strings.VoipStreamWith, Icons.Live);
                }
            }

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutSeparator();

                var discard = flyout.CreateFlyoutItem(Discard, _service.IsChannel ? Strings.VoipChannelEndChat : Strings.VoipGroupEndChat, Icons.Dismiss);
                discard.Foreground = new SolidColorBrush(Colors.IndianRed);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedLeft);
            }
        }

        private async void StreamWith()
        {
            var popup = new VideoChatStreamsPopup(_clientService, _service.Chat.Id, false);
            popup.RequestedTheme = ElementTheme.Dark;

            await popup.ShowQueuedAsync();
        }

        private async void SetTitle()
        {
            var call = _service.Call;
            var chat = _service.Chat;

            if (call == null || chat == null)
            {
                return;
            }

            var input = new InputPopup();
            input.RequestedTheme = ElementTheme.Dark;
            input.Title = _service.IsChannel ? Strings.VoipChannelTitle : Strings.VoipGroupTitle;
            input.PrimaryButtonText = Strings.Save;
            input.SecondaryButtonText = Strings.Cancel;
            input.PlaceholderText = chat.Title;
            input.Text = call.Title;
            input.MaxLength = 64;
            input.MinLength = 0;

            var confirm = await input.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _clientService.Send(new SetGroupCallTitle(call.Id, input.Text));
            }
        }

        private async void StartRecording()
        {
            var call = _service.Call;
            var chat = _service.Chat;

            if (call == null || chat == null)
            {
                return;
            }

            var input = new RecordVideoChatPopup(call.Title);
            input.RequestedTheme = ElementTheme.Dark;

            var confirm = await input.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _clientService.Send(new StartGroupCallRecording(call.Id, input.FileName, input.RecordVideo, input.UsePortraitOrientation));
            }
        }

        private async void StopRecording()
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = Strings.VoipGroupStopRecordingTitle;
            popup.Message = _service.IsChannel ? Strings.VoipChannelStopRecordingText : Strings.VoipGroupStopRecordingText;
            popup.PrimaryButtonText = Strings.Stop;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _clientService.Send(new EndGroupCallRecording(call.Id));
            }
        }

        private async void ShareInviteLink()
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            await this.ShowPopupAsync(_clientService.SessionId, typeof(ChooseChatsPopup), new ChooseChatsConfigurationGroupCall(call));
        }

        private readonly ScrollViewer _scrollingHost;

        private bool _bottomRootCollapsed;

        private void ShowHideBottomRoot(bool show)
        {
            if (_bottomRootCollapsed == !show)
            {
                return;
            }

            _bottomRootCollapsed = !show;

            var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0, show ? 0 : 1);
            anim.InsertKeyFrame(1, show ? 1 : 0);

            var root = ElementComposition.GetElementVisual(BottomPanel);

            root.StartAnimation("Opacity", anim);
        }

        private void Viewport_PointerEntered(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

        }

        private void Viewport_PointerExited(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
        {

        }
    }
}
