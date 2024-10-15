//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services.Calls;
using Telegram.Td.Api;
using Telegram.Views.Calls.Popups;
using Telegram.Views.Popups;
using Windows.Devices.Input;
using Windows.System.Display;
using Windows.UI.Composition;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Calls
{
    public sealed partial class LiveStreamPage : WindowEx
    {
        private readonly VoipGroupCall _call;

        private readonly VoipVideoOutputSink _unifiedVideo;

        private readonly DispatcherTimer _inactivityTimer;
        private readonly DispatcherTimer _scheduledTimer;

        private readonly DisplayRequest _displayRequest = new();

        public LiveStreamPage(VoipGroupCall call)
        {
            InitializeComponent();
            Logger.Info();

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

            _call = call;
            _call.NetworkStateChanged += OnNetworkStateChanged;
            _call.JoinedStateChanged += OnJoinedStateChanged;
            _call.AvailableStreamsChanged += OnAvailableStreamsChanged;
            _call.PropertyChanged += OnPropertyChanged;
            _call.AddIncomingVideoOutput("unified", _unifiedVideo = VoipVideoOutput.CreateSink(Viewport, false));

            Window.Current.SetTitleBar(TitleArea);

            ElementCompositionPreview.SetIsTranslationEnabled(Viewport, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedInfo, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedGlyph, true);
            //ViewportAspect.Constraint = new Size(16, 9);

            OnAvailableStreamsChanged();
            OnPropertyChanged();
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

            if (show is false && XamlRoot != null)
            {
                foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(XamlRoot))
                {
                    return;
                }
            }

            _transportCollapsed = !show;
            BottomPanel.IsHitTestVisible = false;

            var top = ElementComposition.GetElementVisual(TitleBar);
            var top1 = ElementComposition.GetElementVisual(TopShadow);
            var bottom = ElementComposition.GetElementVisual(BottomPanel);
            var bottom1 = ElementComposition.GetElementVisual(BottomShadow);

            var batch = top.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                BottomPanel.IsHitTestVisible = !_transportCollapsed;
            };

            var opacity = top.Compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(0, show ? 0 : 1);
            opacity.InsertKeyFrame(1, show ? 1 : 0);

            top.StartAnimation("Opacity", opacity);
            top1.StartAnimation("Opacity", opacity);
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
            if (_call.AvailableStreamsCount > 0 || !_call.IsConnected)
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

                if (_call.ClientService.TryGetSupergroup(_call.Chat, out Supergroup supergroup))
                {
                    TextBlockHelper.SetMarkdown(NoStream, supergroup.Status is ChatMemberStatusCreator ? Strings.NoRtmpStreamFromAppOwner : string.Format(Strings.NoRtmpStreamFromAppViewer, _call.Chat.Title));
                }
                else if (_call.ClientService.TryGetBasicGroup(_call.Chat, out BasicGroup basicGroup))
                {
                    TextBlockHelper.SetMarkdown(NoStream, basicGroup.Status is ChatMemberStatusCreator ? Strings.NoRtmpStreamFromAppOwner : string.Format(Strings.NoRtmpStreamFromAppViewer, _call.Chat.Title));
                }

                NoStream.Visibility = Visibility.Visible;
                Viewport.Visibility = Visibility.Collapsed;
            }
        }

        private void OnTick(object sender, object e)
        {
            if (_call != null && _call != null && _call.ScheduledStartDate != 0)
            {
                StartsIn.Text = _call.GetStartsIn();
            }
            else
            {
                _scheduledTimer.Stop();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _displayRequest.TryRequestActive();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _displayRequest.TryRequestRelease();

            _scheduledTimer.Stop();
            _inactivityTimer.Stop();

            _unifiedVideo?.Stop();

            _call.NetworkStateChanged -= OnNetworkStateChanged;
            _call.JoinedStateChanged -= OnJoinedStateChanged;
            _call.AvailableStreamsChanged -= OnAvailableStreamsChanged;
            _call.PropertyChanged -= OnPropertyChanged;
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

                Resize.Glyph = Icons.ArrowMaximizeFilled24;
                Resize.Content = ResizeText.Text = Strings.VoipMaximize;
            }
            else if (view.TryEnterFullScreenMode())
            {
                Resize.Glyph = Icons.ArrowMinimizeFilled24;
                Resize.Content = ResizeText.Text = Strings.VoipMinimize;
            }
        }

        private void OnPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            this.BeginOnUIThread(() => OnPropertyChanged());
        }

        private void OnPropertyChanged()
        {
            TitleInfo.Text = _call.GetTitle();

            if (_call.ScheduledStartDate != 0)
            {
                var date = Formatter.ToLocalTime(_call.ScheduledStartDate);
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
                SubtitleInfo.Text = _call.IsChannel ? Strings.VoipChannelScheduledVoiceChat : Strings.VoipGroupScheduledVoiceChat;
                ParticipantsPanel.Visibility = Visibility.Collapsed;
                ScheduledInfo.Text = duration < TimeSpan.Zero ? Strings.VoipChatLateBy : Strings.VoipChatStartsIn;
                StartsAt.Text = _call.GetStartsAt();
                StartsIn.Text = _call.GetStartsIn();

                Menu.Visibility = Visibility.Collapsed;
                Resize.Visibility = Visibility.Collapsed;
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

                SubtitleInfo.Text = Locale.Declension(Strings.R.Participants, _call.ParticipantCount);
                ParticipantsPanel.Visibility = Visibility.Visible;
            }
        }

        private void OnNetworkStateChanged(VoipGroupCall sender, VoipGroupCallNetworkStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => OnAvailableStreamsChanged());
        }

        private void OnJoinedStateChanged(VoipGroupCall sender, VoipGroupCallJoinedStateChangedEventArgs args)
        {
            if (sender.IsClosed)
            {
                this.BeginOnUIThread(() => Close());
            }
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            if (_call.CanBeManaged)
            {
                var popup = new MessagePopup
                {
                    RequestedTheme = ElementTheme.Dark,
                    Title = _call.IsChannel ? Strings.VoipChannelLeaveAlertTitle : Strings.VoipGroupLeaveAlertTitle,
                    Message = _call.IsChannel ? Strings.VoipChannelLeaveAlertText : Strings.VoipGroupLeaveAlertText,
                    PrimaryButtonText = Strings.VoipGroupLeave,
                    SecondaryButtonText = Strings.Cancel,
                    CheckBoxLabel = _call.IsChannel ? Strings.VoipChannelLeaveAlertEndChat : Strings.VoipGroupLeaveAlertEndChat
                };

                var confirm = await popup.ShowQueuedAsync(XamlRoot);
                if (confirm == ContentDialogResult.Primary)
                {
                    _call.Discard(popup.IsChecked == true);
                }
            }
            else
            {
                _call.Discard(false);
            }
        }

        private async void Discard()
        {
            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = _call.IsChannel ? Strings.VoipChannelEndAlertTitle : Strings.VoipGroupEndAlertTitle;
            popup.Message = _call.IsChannel ? Strings.VoipChannelEndAlertText : Strings.VoipGroupEndAlertText;
            popup.PrimaryButtonText = Strings.VoipGroupEnd;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _call.Discard(true);
                Close();
            }
        }

        private void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            if (_call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(SetTitle, _call.IsChannel ? Strings.VoipChannelEditTitle : Strings.VoipGroupEditTitle, Icons.Edit);
            }

            if (_call.CanBeManaged && _call.ScheduledStartDate == 0)
            {
                if (_call.RecordDuration > 0)
                {
                    flyout.CreateFlyoutItem(StopRecording, Strings.VoipGroupStopRecordCall, Icons.Record);
                }
                else
                {
                    flyout.CreateFlyoutItem(StartRecording, Strings.VoipGroupRecordCall, Icons.Record);
                }
            }

            if (_call.ScheduledStartDate == 0)
            {
                flyout.CreateFlyoutSeparator();

                var outputId = _call.AudioOutputId;

                var output = new MenuFlyoutSubItem();
                output.Text = Strings.VoipDeviceOutput;
                output.Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3);

                flyout.Items.Add(output);

                var hasOutput = _call.Devices.HasDevices(MediaDeviceClass.AudioOutput);
                if (hasOutput is true)
                {
                    foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.AudioOutput))
                    {
                        var deviceItem = new ToggleMenuFlyoutItem();
                        deviceItem.Text = device.Name;
                        deviceItem.IsChecked = outputId == device.Id;
                        deviceItem.Click += (s, args) =>
                        {
                            _call.AudioOutputId = device.Id;
                        };

                        output.Items.Add(deviceItem);
                    }
                }
                else
                {
                    output.CreateFlyoutItem(null, hasOutput.HasValue ? Strings.NotFoundSpeakers : Strings.Loading);
                }
            }

            //flyout.CreateFlyoutItem(ShareInviteLink, Strings.VoipGroupShareInviteLink, Icons.Link);

            if (_call.ClientService.TryGetSupergroup(_call.Chat, out Supergroup supergroup))
            {
                if (supergroup.Status is ChatMemberStatusCreator)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(StreamWith, Strings.VoipStreamWith, Icons.Live);
                }
            }
            else if (_call.ClientService.TryGetBasicGroup(_call.Chat, out BasicGroup basicGroup))
            {
                if (basicGroup.Status is ChatMemberStatusCreator)
                {
                    flyout.CreateFlyoutSeparator();
                    flyout.CreateFlyoutItem(StreamWith, Strings.VoipStreamWith, Icons.Live);
                }
            }

            if (_call.CanBeManaged)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(Discard, _call.IsChannel ? Strings.VoipChannelEndChat : Strings.VoipGroupEndChat, Icons.Dismiss, destructive: true);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.Top);
            }
        }

        private async void StreamWith()
        {
            var popup = new VideoChatStreamsPopup(_call.ClientService, _call.Chat.Id, false);
            popup.RequestedTheme = ElementTheme.Dark;

            await popup.ShowQueuedAsync(XamlRoot);
        }

        private async void SetTitle()
        {
            var input = new InputPopup();
            input.RequestedTheme = ElementTheme.Dark;
            input.Title = _call.IsChannel ? Strings.VoipChannelTitle : Strings.VoipGroupTitle;
            input.PrimaryButtonText = Strings.Save;
            input.SecondaryButtonText = Strings.Cancel;
            input.PlaceholderText = _call.Chat.Title;
            input.Text = _call.Title;
            input.MaxLength = 64;
            input.MinLength = 0;

            var confirm = await input.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _call.ClientService.Send(new SetGroupCallTitle(_call.Id, input.Text));
            }
        }

        private async void StartRecording()
        {
            var input = new RecordVideoChatPopup(_call.Title);
            input.RequestedTheme = ElementTheme.Dark;

            var confirm = await input.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _call.ClientService.Send(new StartGroupCallRecording(_call.Id, input.FileName, input.RecordVideo, input.UsePortraitOrientation));
            }
        }

        private async void StopRecording()
        {
            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = Strings.VoipGroupStopRecordingTitle;
            popup.Message = _call.IsChannel ? Strings.VoipChannelStopRecordingText : Strings.VoipGroupStopRecordingText;
            popup.PrimaryButtonText = Strings.Stop;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _call.ClientService.Send(new EndGroupCallRecording(_call.Id));
            }
        }

        private async void ShareInviteLink()
        {
            await this.ShowPopupAsync(_call.ClientService.SessionId, new ChooseChatsPopup(), new ChooseChatsConfigurationGroupCall(_call.Id));
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

            var anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0, show ? 0 : 1);
            anim.InsertKeyFrame(1, show ? 1 : 0);

            var root = ElementComposition.GetElementVisual(BottomPanel);

            root.StartAnimation("Opacity", anim);
        }

        private void Viewport_PointerEntered(object sender, PointerRoutedEventArgs e)
        {

        }

        private void Viewport_PointerExited(object sender, PointerRoutedEventArgs e)
        {

        }
    }
}
