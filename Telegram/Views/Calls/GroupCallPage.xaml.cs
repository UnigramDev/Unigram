//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Drawers;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services.Calls;
using Telegram.Streams;
using Telegram.Td;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.ViewModels.Drawers;
using Telegram.Views.Calls.Popups;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.Foundation;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;

namespace Telegram.Views.Calls
{
    public record VoipVideoOutputReference(VoipVideoOutputSink Sink, CompositionSurfaceBrush Brush)
    {
        public void Stop()
        {
            Sink.Stop();
            Brush.Dispose();
        }
    }

    public sealed partial class GroupCallPage : WindowEx, IGroupCallDelegate, IPopupHost
    {
        private bool _disposed;

        private readonly VoipGroupCall _call;

        private readonly Dictionary<string, VoipVideoOutputReference> _sinks = new();

        private readonly Dictionary<string, GroupCallParticipantGridCell> _gridCells = new();
        private readonly Dictionary<string, GroupCallParticipantGridCell> _listCells = new();

        private readonly CompositionVoiceBlobVisual _visual;

        private readonly DispatcherTimer _scheduledTimer;
        private readonly DispatcherTimer _debouncerTimer;

        private readonly DisplayRequest _displayRequest = new();

        private readonly DispatcherQueue _dispatcherQueue;

        private readonly ObservableCollection<GroupCallMessage> _messages;

        private ParticipantsGridMode _mode = ParticipantsGridMode.Compact;
        private bool _docked = true;

        public GroupCallPage(VoipGroupCall call)
        {
            InitializeComponent();
            Logger.Info();

            _visual = new CompositionVoiceBlobVisual(AudioBlob, 150, 150, 1.5f);

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _scheduledTimer = new DispatcherTimer();
            _scheduledTimer.Interval = TimeSpan.FromSeconds(1);
            _scheduledTimer.Tick += OnTick;

            _debouncerTimer = new DispatcherTimer();
            _debouncerTimer.Interval = TimeSpan.FromMilliseconds(Constants.AnimatedThrottle);
            _debouncerTimer.Tick += (s, args) =>
            {
                _debouncerTimer.Stop();
                UpdateVisibleParticipants(false);
            };

            _call = call;
            _call.NetworkStateChanged += OnNetworkStateChanged;
            _call.JoinedStateChanged += OnJoinedStateChanged;
            _call.VerificationStateChanged += OnVerificationStateChanged;
            _call.MessagesChanged += OnMessagesChanged;
            _call.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _call.MutedChanged += OnMutedChanged;
            _call.PropertyChanged += OnParticipantsChanged;

            if (_call.Participants != null)
            {
                _call.Participants.Delegate = this;
                _call.Participants.LoadVideoInfo();

                ScrollingHost.ItemsSource = _call.Participants;
            }

            _messages = new ObservableCollection<GroupCallMessage>(_call.Messages);
            MessagesHost.ItemsSource = _messages;
            EmojiPanel.DataContext = EmojiDrawerViewModel.Create(_call.ClientService.Session);
            MessageField.CustomEmoji = CustomEmoji;
            MessageField.DataContext = EmojiPanel.DataContext;
            MessageField.MaxLength = (int)_call.ClientService.Options.GroupCallMessageTextLengthMax;
            MessageReactions.Initialize(_call.ClientService);

            Window.Current.SetTitleBar(TitleArea);

            Update(call, call.CurrentUser);

            var verificationState = _call.VerificationState;
            if (verificationState != null)
            {
                VerificationState.UpdateState(verificationState.Generation, verificationState.Emojis);
            }
            else if (_call.IsVideoChat)
            {
                VerificationState.Visibility = Visibility.Collapsed;
            }
            else
            {
                VerificationState.UpdateState(0, Array.Empty<string>());
            }

            ElementCompositionPreview.SetIsTranslationEnabled(Viewport, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedInfo, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(PinnedGlyph, true);
            //ViewportAspect.Constraint = new Size(16, 9);
        }

        public void PopupOpened()
        {
            Window.Current.SetTitleBar(null);
        }

        public void PopupClosed()
        {
            Window.Current.SetTitleBar(TitleArea);
        }

        public DispatcherQueue DispatcherQueue => _dispatcherQueue;

        public void VideoInfoAdded(GroupCallParticipant participant, GroupCallParticipantVideoInfo[] videoInfos)
        {
            foreach (var item in videoInfos)
            {
                if (item == null)
                {
                    continue;
                }

                if (_gridCells.TryGetValue(item.EndpointId, out var cell))
                {
                    cell.UpdateGroupCallParticipant(_call.ClientService, participant, item);
                }
                else
                {
                    AddGridItem(participant, item, item == participant.ScreenSharingVideoInfo);
                }

                if (_listCells.TryGetValue(item.EndpointId, out cell))
                {
                    cell.UpdateGroupCallParticipant(_call.ClientService, participant, item);
                }
                else if (_mode != ParticipantsGridMode.Compact && _selectedEndpointId != null)
                {
                    AddListItem(participant, item, item == participant.ScreenSharingVideoInfo);
                }
            }

            _debouncerTimer.Stop();
            _debouncerTimer.Start();
        }

        public void VideoInfoRemoved(GroupCallParticipant participant, string[] endpointIds)
        {
            foreach (var item in endpointIds)
            {
                if (item == null)
                {
                    continue;
                }

                if (_gridCells.TryGetValue(item, out var cell))
                {
                    RemoveGridItem(cell);
                }

                if (_listCells.TryGetValue(item, out cell))
                {
                    RemoveListItem(cell);
                }
            }

            _debouncerTimer.Stop();
            _debouncerTimer.Start();
        }

        private enum ButtonState
        {
            None,
            SetReminder,
            CancelReminder,
            Start,
            RaiseHand,
            HandRaised,
            Mute,
            Unmute
        }

        private enum ButtonColors
        {
            None,
            Connecting,
            Disabled,
            Unmute,
            Mute
        }

        private void OnTick(object sender, object e)
        {
            if (_call.ScheduledStartDate != 0)
            {
                StartsIn.Text = _call.GetStartsIn();
            }
            else
            {
                _scheduledTimer.Stop();
            }
        }

        private void OnMutedChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() => UpdateNetworkState(_call.CurrentUser, _call.IsConnected));
        }

        private void OnParticipantsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Participants")
            {
                if (_disposed || _call?.Participants == null)
                {
                    return;
                }

                _call.Participants.Delegate = this;
                _call.Participants.LoadVideoInfo();
                this.BeginOnUIThread(() => ScrollingHost.ItemsSource = _call.Participants);
            }
            else
            {
                this.BeginOnUIThread(() => Update(_call, _call?.CurrentUser));
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
            _debouncerTimer.Stop();

            _disposed = true;

            _call.NetworkStateChanged -= OnNetworkStateChanged;
            _call.JoinedStateChanged -= OnJoinedStateChanged;
            _call.VerificationStateChanged -= OnVerificationStateChanged;
            _call.MessagesChanged -= OnMessagesChanged;
            _call.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            _call.MutedChanged -= OnMutedChanged;
            _call.PropertyChanged -= OnParticipantsChanged;

            _sinks.Values.ForEach(x => x.Stop());
            _sinks.Clear();

            _listCells.Clear();
            _gridCells.Clear();

            _visual.StopAnimating();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var prevSize = e.PreviousSize.ToVector2();
            var nextSize = e.NewSize.ToVector2();

            UpdateLayout(prevSize, nextSize, prevSize.X > 0 && prevSize.Y > 0);
        }

        public static int ColumnWidth => 236;

        private async void UpdateLayout(Vector2 prevSize, Vector2 nextSize, bool animated)
        {
            var service = _call;
            if (service == null)
            {
                return;
            }

            var expanded = nextSize.X >= 600 && Viewport.Children.Count > 0;
            var docked = _docked;

            var prev = _mode;
            var mode = expanded && docked
                ? ParticipantsGridMode.Docked
                : expanded
                ? ParticipantsGridMode.Expanded
                : ParticipantsGridMode.Compact;

            if (mode == _mode)
            {
                return;
            }

            _mode = mode;

            if (mode != ParticipantsGridMode.Compact)
            {
                Menu.Visibility = Visibility.Collapsed;
                LogoBasic.Visibility = Visibility.Visible;

                Grid.SetRowSpan(ParticipantsPanel, 2);

                Grid.SetColumn(ScrollingHost, 1);

                Viewport.Mode = mode;
                ParticipantsPanel.ColumnDefinitions[1].Width = new GridLength(ColumnWidth, GridUnitType.Pixel);
                ParticipantsPanel.Margin = new Thickness();
                ScrollingHost.Padding = new Thickness(8, 0, 12, 8);

                if (ListHeader.Children.Contains(ViewportAspect))
                {
                    ListHeader.Children.Remove(ViewportAspect);
                    ParticipantsPanel.Children.Add(ViewportAspect);
                }

                if (mode == ParticipantsGridMode.Docked)
                {
                    ViewportAspect.MaxWidth = double.PositiveInfinity;
                    ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
                    ScrollingHost.Margin = new Thickness();
                    BottomPanel.Padding = new Thickness(8, 8, ColumnWidth, 42);
                }
                else
                {
                    ViewportAspect.MaxWidth = double.PositiveInfinity;
                    ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
                    ScrollingHost.Margin = new Thickness(ColumnWidth - 8, 0, -ColumnWidth + 8, 0);
                    BottomPanel.Padding = new Thickness(8, 8, 8, 42);
                }

                BottomShadow.Visibility = Visibility.Collapsed;
                BottomRoot.Padding = new Thickness(12, 8, 12, 8);
                BottomRoot.Background = new AcrylicBrush { TintColor = Colors.Black, TintOpacity = 0, FallbackColor = Color.FromArgb(0xDD, 0, 0, 0) /*TintLuminosityOpacity = 0.5*/ }; //new SolidColorBrush(Color.FromArgb(0x99, 0x33, 0x33, 0x33));
                BottomRoot.CornerRadius = new CornerRadius(4);

                AudioInfo.Margin = new Thickness(0, 4, 0, 0);

                MessagesHost.Padding = new Thickness(0, 0, 0, 132);
                MessageRoot.Margin = new Thickness(24, 0, 24, 132);

                UpdateVideo();
                UpdateScreen();
            }
            else
            {
                Menu.Visibility = service.CanEnableVideo ? Visibility.Visible : Visibility.Collapsed;
                LogoBasic.Visibility = service.CanEnableVideo ? Visibility.Collapsed : Visibility.Visible;

                Grid.SetRowSpan(ParticipantsPanel, 1);

                Grid.SetColumn(ScrollingHost, 0);

                Viewport.Mode = mode;
                ViewportAspect.MaxWidth = 324;
                ViewportAspect.Margin = new Thickness(-4, -2, -4, Viewport.Children.Count > 0 ? 4 : 0);
                ParticipantsPanel.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Auto);
                ParticipantsPanel.Margin = new Thickness(0, 0, 0, -56);
                ScrollingHost.Padding = new Thickness(12, 0, 12, 72);
                ScrollingHost.Margin = new Thickness();

                if (ParticipantsPanel.Children.Contains(ViewportAspect))
                {
                    ParticipantsPanel.Children.Remove(ViewportAspect);
                    ListHeader.Children.Add(ViewportAspect);
                }

                BottomShadow.Visibility = Visibility.Visible;
                BottomPanel.Padding = new Thickness(0);
                BottomRoot.Padding = new Thickness(0);
                BottomRoot.Background = null;
                BottomRoot.CornerRadius = new CornerRadius(0);

                AudioInfo.Margin = new Thickness(0, 4, 0, 12);

                MessagesHost.Padding = new Thickness(0, 0, 0, 72);
                MessageRoot.Margin = new Thickness(24, 0, 24, 72);

                UpdateVideo();
                UpdateScreen();
            }

            await this.UpdateLayoutAsync();

            ShowHideInfo(true);

            TransformList(prevSize, nextSize, prev, mode);

            if (PowerSavingPolicy.AreCallsAnimated && animated)
            {
                if (prev == ParticipantsGridMode.Compact || mode == ParticipantsGridMode.Compact)
                {
                    TransformBottomRoot(prevSize, nextSize, prev, mode);
                }
                else
                {
                    TransformDocked();
                }
            }
        }

        private void TransformList(Vector2 prevSize, Vector2 nextSize, ParticipantsGridMode prev, ParticipantsGridMode next)
        {
            if (next != ParticipantsGridMode.Compact && _selectedEndpointId != null)
            {
                foreach (var cell in Viewport.Cells)
                {
                    if (cell.IsSelected && _listCells.TryGetValue(cell.EndpointId, out var listCell))
                    {
                        RemoveListItem(listCell);
                    }
                    else if (!cell.IsSelected && _mode != ParticipantsGridMode.Compact)
                    {
                        AddListItem(cell.Participant, cell.VideoInfo, cell.IsScreenSharing);
                    }
                }

                ShowHideParticipantsWithVideo(false);
            }
            else
            {
                _listCells.Clear();
                ListViewport.Children.Clear();

                ShowHideParticipantsWithVideo(true);
            }

            UpdateVisibleParticipants(false);
        }

        private void ShowHideParticipantsWithVideo(bool show)
        {
            var panel = ScrollingHost.ItemsPanelRoot;
            if (panel == null)
            {
                return;
            }

            foreach (var row in panel.Children.OfType<SelectorItem>())
            {
                var participant = ScrollingHost.ItemFromContainer(row) as GroupCallParticipant;
                if (participant != null && !show && participant.HasVideoInfo())
                {
                    row.IsEnabled = false;
                    row.Visibility = Visibility.Collapsed;
                }
                else
                {
                    row.IsEnabled = true;
                    row.Visibility = Visibility.Visible;
                }
            }
        }

        private void TransformDocked()
        {
            var root = ElementComposition.GetElementVisual(BottomRoot);
            var list = ElementComposition.GetElementVisual(ScrollingHost);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ScrollingHost, true);

            // Root offset
            var rootOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_mode == ParticipantsGridMode.Docked)
            {
                rootOffset.InsertKeyFrame(0, new Vector3(108, 0, 0));
            }
            else
            {
                rootOffset.InsertKeyFrame(0, new Vector3(-108, 0, 0));
            }

            rootOffset.InsertKeyFrame(1, Vector3.Zero);

            // ScrollingHost offset
            var listOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_mode == ParticipantsGridMode.Docked)
            {
                listOffset.InsertKeyFrame(0, new Vector3(ColumnWidth, 0, 0));
            }
            else
            {
                listOffset.InsertKeyFrame(0, new Vector3(-ColumnWidth, 0, 0));
            }

            listOffset.InsertKeyFrame(1, Vector3.Zero);

            listOffset.Duration =
                rootOffset.Duration = TimeSpan.FromMilliseconds(300);

            root.StartAnimation("Translation", rootOffset);
            list.StartAnimation("Translation", listOffset);
        }

        private string _selectedEndpointId;
        private ulong _selectedTimestamp;

        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            var size = view.VisibleBounds;

            if (size.Width >= 600)
            {
                view.TryResizeView(new Size(380, size.Height + 1));
            }
            else
            {
                view.TryResizeView(new Size(780, size.Height + 1));
            }
        }

        private void Participant_ToggleDocked(object sender, EventArgs e)
        {
            _docked = !_docked;
            UpdateLayout(ActualSize, ActualSize, true);
        }

        private void TransformBottomRoot(Vector2 prevSize, Vector2 nextSize, ParticipantsGridMode prev, ParticipantsGridMode next)
        {
            var service = _call;
            if (service == null)
            {
                return;
            }

            var root = ElementComposition.GetElementVisual(BottomRoot);
            var list = ElementComposition.GetElementVisual(ScrollingHost);
            var audio1 = ElementComposition.GetElementVisual(AudioBlob);
            var audio2 = ElementComposition.GetElementVisual(AudioRoot);
            var audioInfo = ElementComposition.GetElementVisual(AudioInfo);
            var video = ElementComposition.GetElementVisual(Video);
            var videoInfo = ElementComposition.GetElementVisual(VideoInfo);
            var screen = ElementComposition.GetElementVisual(Screen);
            var screenInfo = ElementComposition.GetElementVisual(ScreenInfo);
            //var settings = ElementComposition.GetElementVisual(Settings);
            //var settingsInfo = ElementComposition.GetElementVisual(SettingsInfo);
            var leave = ElementComposition.GetElementVisual(Leave);
            var leaveInfo = ElementComposition.GetElementVisual(LeaveInfo);

            var expanded = next is ParticipantsGridMode.Expanded or ParticipantsGridMode.Docked;

            screen.CenterPoint = new Vector3(24, 24, 0);
            screenInfo.CenterPoint = new Vector3(ScreenInfo.ActualSize / 2, 0);

            //settings.CenterPoint = new Vector3(24, 24, 0);
            //settingsInfo.CenterPoint = new Vector3(SettingsInfo.ActualSize / 2, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ScrollingHost, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioBlob, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Lottie, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Video, true);
            ElementCompositionPreview.SetIsTranslationEnabled(VideoInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Screen, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ScreenInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Message, true);
            ElementCompositionPreview.SetIsTranslationEnabled(MessageInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Leave, true);
            ElementCompositionPreview.SetIsTranslationEnabled(LeaveInfo, true);

            // Root
            var rootOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            var prevCenter = prevSize.X / 2 - BottomRoot.ActualSize.X / 2;
            var nextCenter = nextSize.X / 2 - BottomRoot.ActualSize.X / 2;

            if (next == ParticipantsGridMode.Docked)
            {
                nextCenter -= 112;
            }
            else if (prev == ParticipantsGridMode.Docked)
            {
                prevCenter -= 112;
            }

            rootOffset.InsertKeyFrame(0, new Vector3(prevCenter - nextCenter, 0, 0));
            rootOffset.InsertKeyFrame(1, Vector3.Zero);

            // ScrollingHost offset
            var listOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (next == ParticipantsGridMode.Docked)
            {
                listOffset.InsertKeyFrame(0, new Vector3(ColumnWidth, 0, 0));
            }
            else
            {
                //listOffset.InsertKeyFrame(0, new Vector3(-ColumnWidth, 0, 0));
            }

            listOffset.InsertKeyFrame(1, Vector3.Zero);

            // Other offset
            var otherOffset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (expanded)
            {
                otherOffset.InsertKeyFrame(0, new Vector3(-24, 0, 0));
            }
            else
            {
                otherOffset.InsertKeyFrame(0, new Vector3(24, 0, 0));
            }

            otherOffset.InsertKeyFrame(1, Vector3.Zero);

            // Other scales
            var otherScale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (expanded)
            {
                otherScale.InsertKeyFrame(0, Vector3.Zero);
                otherScale.InsertKeyFrame(1, Vector3.One);
            }
            else
            {
                otherScale.InsertKeyFrame(0, Vector3.One);
                otherScale.InsertKeyFrame(1, Vector3.Zero);
            }

            rootOffset.Duration =
                listOffset.Duration =
                otherScale.Duration =
                otherOffset.Duration = TimeSpan.FromMilliseconds(300);
            //rootOffset.DelayTime =
            //    audioScale.DelayTime =
            //    leaveOffset.DelayTime =
            //    otherScale.DelayTime =
            //    videoOffset.DelayTime =
            //    audioInfoOffset.DelayTime =
            //    audioOffset.DelayTime = TimeSpan.FromSeconds(4);
            //rootOffset.DelayBehavior =
            //    audioScale.DelayBehavior =
            //    leaveOffset.DelayBehavior =
            //    otherScale.DelayBehavior =
            //    videoOffset.DelayBehavior =
            //    audioInfoOffset.DelayBehavior =
            //    audioOffset.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

            root.StartAnimation("Translation", rootOffset);
            list.StartAnimation("Translation", listOffset);
            //audio1.StartAnimation("Scale", audioScale);
            //audio2.StartAnimation("Scale", audioScale);
            //audioInfo.StartAnimation("Translation", audioInfoOffset);
            //screen.StartAnimation("Scale", otherScale);
            //screenInfo.StartAnimation("Scale", otherScale);
            screen.StartAnimation("Translation", otherOffset);
            screenInfo.StartAnimation("Translation", otherOffset);
            leave.StartAnimation("Translation", otherOffset);
            leaveInfo.StartAnimation("Translation", otherOffset);

            if (service.CanEnableVideo || next != ParticipantsGridMode.Compact)
            {
                video.StartAnimation("Translation", otherOffset);
                videoInfo.StartAnimation("Translation", otherOffset);
                //settings.StartAnimation("Scale", otherScale);
                //settingsInfo.StartAnimation("Scale", otherScale);
                //settings.StartAnimation("Translation", otherOffset);
                //settingsInfo.StartAnimation("Translation", otherOffset);
            }
            else
            {
                //settings.Scale = Vector3.One;
                //settingsInfo.Scale = Vector3.One;
            }
        }

        private void Update(VoipGroupCall call, GroupCallParticipant currentUser)
        {
            TitleInfo.Text = call.GetTitle();

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
                SubtitleInfo.Text = _call.IsChannel ? Strings.VoipChannelScheduledVoiceChat : Strings.VoipGroupScheduledVoiceChat;
                ScheduledInfo.Text = duration < TimeSpan.Zero ? Strings.VoipChatLateBy : Strings.VoipChatStartsIn;
                StartsAt.Text = call.GetStartsAt();
                StartsIn.Text = call.GetStartsIn();

                Menu.Visibility = Visibility.Collapsed;
                LogoBasic.Visibility = Visibility.Visible;

                Message.Visibility = MessageInfo.Visibility = Visibility.Collapsed;
                Video.Visibility = VideoInfo.Visibility = Visibility.Collapsed;
            }
            else
            {
                _scheduledTimer.Stop();

                if (ScheduledPanel != null)
                {
                    UnloadObject(ScheduledPanel);
                }

                if (_mode != ParticipantsGridMode.Compact)
                {
                    Menu.Visibility = Visibility.Collapsed;
                    Message.Visibility = MessageInfo.Visibility = Visibility.Visible;
                }
                else
                {
                    Menu.Visibility = _call.CanEnableVideo ? Visibility.Visible : Visibility.Collapsed;
                    Message.Visibility = MessageInfo.Visibility = Visibility.Visible;
                }

                Menu.Visibility = Visibility.Visible;
                LogoBasic.Visibility = Visibility.Collapsed;

                SubtitleInfo.Text = Locale.Declension(Strings.R.Participants, call.ParticipantCount);

                Message.Visibility = MessageInfo.Visibility = call.CanSendMessages
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                if (!call.CanSendMessages)
                {
                    ShowHideMessage(false);
                }

                UpdateVideo();
                UpdateScreen();
            }

            UpdateNetworkState(currentUser, _call.IsConnected);
            UpdateLayout(ActualSize, ActualSize, true);
        }

        private void OnNetworkStateChanged(VoipGroupCall sender, VoipGroupCallNetworkStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateNetworkState(_call.CurrentUser, args.IsConnected));
        }

        private void OnJoinedStateChanged(VoipGroupCall sender, VoipGroupCallJoinedStateChangedEventArgs args)
        {
            if (sender.IsClosed)
            {
                this.BeginOnUIThread(() => Close());
            }
        }

        private void OnVerificationStateChanged(VoipGroupCall sender, VoipGroupCallVerificationStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => VerificationState.UpdateState(args.Generation, args.Emojis));
        }

        private void OnMessagesChanged(VoipGroupCall sender, VoipGroupCallMessagesChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateMessages(args.Message, args.Deleted));
        }

        private void UpdateMessages(GroupCallMessage message, bool expired)
        {
            if (expired)
            {
                _messages.Remove(message);
            }
            else
            {
                _messages.Add(message);
            }
        }

        private void OnAudioLevelsUpdated(object sender, IList<VoipGroupParticipant> levels)
        {
            var participants = _call?.Participants;
            if (participants == null)
            {
                return;
            }

            var validLevels = new Dictionary<GroupCallParticipant, float>();

            foreach (var level in levels)
            {
                var value = level.Level;
                if (level.AudioSource == 0 && _call.IsMuted)
                {
                    value = 0;
                }

                if (level.AudioSource == 0)
                {
                    if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
                    {
                        _visual.UpdateLevel(value);
                    }
                }

                if (participants.TryGetFromAudioSourceId(level.AudioSource, out var participant))
                {
                    validLevels[participant] = value;

                    var endpoint = participant.ScreenSharingVideoInfo?.EndpointId ?? participant.VideoInfo?.EndpointId;
                    if (endpoint != null && endpoint == _selectedEndpointId)
                    {
                        const float speakingLevelThreshold = 0.1f;
                        const int cutoffTimeout = 3000;
                        const int silentTimeout = 2000;

                        if (value > speakingLevelThreshold && level.IsSpeaking)
                        {
                            _selectedTimestamp = Logger.TickCount;
                        }
                    }
                }
            }

            this.BeginOnUIThread(() =>
            {
                foreach (var level in validLevels)
                {
                    var container = ScrollingHost.ContainerFromItem(level.Key) as SelectorItem;
                    var content = container?.ContentTemplateRoot as Grid;

                    if (content == null)
                    {
                        continue;
                    }

                    var wave = content.Children[0] as Border;
                    var photo = content.Children[1] as ProfilePicture;

                    UpdateGroupCallParticipantLevel(wave, photo, level.Value);
                }
            });
        }

        private void UpdateGroupCallParticipantLevel(Border waveElement, ProfilePicture photoElement, float value)
        {
            var wave = ElementComposition.GetElementVisual(waveElement);
            var photo = ElementComposition.GetElementVisual(photoElement);

            var amplitude = Math.Min(value, 1);

            var outer = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            outer.InsertKeyFrame(1, new Vector3(0.9f + 0.5f * amplitude));

            var inner = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            inner.InsertKeyFrame(1, new Vector3(1f + 0.15f * amplitude));

            wave.CenterPoint = new Vector3(36 / 2);
            wave.StartAnimation("Scale", outer);

            photo.CenterPoint = new Vector3(36 / 2);
            photo.StartAnimation("Scale", inner);
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
            }
        }

        private async void Audio_Click(object sender, RoutedEventArgs e)
        {
            var currentUser = _call.CurrentUser;

            if (_call.ScheduledStartDate > 0)
            {
                if (_call.CanBeManaged)
                {
                    _call.ClientService.Send(new StartScheduledVideoChat(_call.Id));
                }
                else
                {
                    _call.ClientService.Send(new ToggleVideoChatEnabledStartNotification(_call.Id, !_call.EnabledStartNotification));
                }
            }
            //else if (currentUser != null && (currentUser.CanBeMutedForAllUsers || currentUser.CanBeUnmutedForAllUsers))
            //{
            //    if (_service.Manager != null)
            //    {
            //        _service.Manager.IsMuted = !currentUser.IsMutedForAllUsers;
            //        _clientService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, currentUser.ParticipantId, _service.Manager.IsMuted));
            //    }
            //}
            else if (currentUser != null && currentUser.CanUnmuteSelf && _call.IsMuted)
            {
                var permissions = await MediaDevicePermissions.CheckAccessAsync(XamlRoot, MediaDeviceAccess.Audio, ElementTheme.Dark);
                if (permissions == false || _call == null)
                {
                    return;
                }

                _call.IsMuted = false;
            }
            else if (currentUser != null && !_call.IsMuted)
            {
                _call.IsMuted = true;
            }
            else if (currentUser != null)
            {
                _call.ClientService.Send(new ToggleGroupCallParticipantIsHandRaised(_call.Id, _call.CurrentUser.ParticipantId, true));
            }
        }

        private async void Video_Click(object sender, RoutedEventArgs e)
        {
            if (_call.IsVideoEnabled == false)
            {
                var permissions = await MediaDevicePermissions.CheckAccessAsync(XamlRoot, MediaDeviceAccess.Video, ElementTheme.Dark);
                if (permissions == false)
                {
                    return;
                }
            }

            _call.ToggleCapturing();
            UpdateVideo();
        }

        private void Screen_Click(object sender, RoutedEventArgs e)
        {
            if (_call.IsScreenSharing)
            {
                _call.EndScreenSharing();
            }
            else
            {
                _call.StartScreenSharing(XamlRoot);
            }

            UpdateScreen();
        }

        private void UpdateNetworkState(GroupCallParticipant currentUser, bool connected)
        {
            if (_call.ScheduledStartDate > 0)
            {
                if (_call.CanBeManaged)
                {
                    SetButtonState(ButtonState.Start, connected);
                }
                else
                {
                    SetButtonState(_call.EnabledStartNotification ? ButtonState.SetReminder : ButtonState.CancelReminder, connected);
                }
            }
            //else if (currentUser != null && currentUser.CanBeUnmutedForAllUsers)
            else if (currentUser != null && currentUser.CanUnmuteSelf && (_call.IsMuted || currentUser.IsMutedForAllUsers))
            {
                SetButtonState(ButtonState.Mute, connected);
            }
            //else if (currentUser != null && currentUser.CanBeMutedForAllUsers)
            else if (currentUser != null && (!_call.IsMuted || !currentUser.IsMutedForAllUsers))
            {
                SetButtonState(ButtonState.Unmute, connected);
            }
            else if (currentUser != null && currentUser.IsHandRaised)
            {
                SetButtonState(ButtonState.HandRaised, connected);
            }
            else if (_call.IsJoined)
            {
                SetButtonState(ButtonState.RaiseHand, connected);
            }
        }

        private ButtonState _prevState;
        private ButtonColors _prevColors;
        private bool _prevConnected;

        private void SetButtonState(ButtonState state, bool connected)
        {
            if (state == _prevState && connected == _prevConnected)
            {
                return;
            }

            ButtonColors colors = _prevColors;

            switch (state)
            {
                case ButtonState.CancelReminder:
                    colors = ButtonColors.Disabled;
                    Audio.Content = AudioInfo.Text = Strings.VoipGroupCancelReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceCancelReminder.tgs");
                    break;
                case ButtonState.SetReminder:
                    colors = ButtonColors.Disabled;
                    Audio.Content = AudioInfo.Text = Strings.VoipGroupSetReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceSetReminder.tgs");
                    break;
                case ButtonState.Start:
                    colors = ButtonColors.Disabled;
                    Audio.Content = AudioInfo.Text = Strings.VoipGroupStartNow;
                    Lottie.AutoPlay = false;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceStart.tgs");
                    break;
                case ButtonState.Unmute:
                    colors = ButtonColors.Unmute;
                    Audio.Content = AudioInfo.Text = Strings.VoipTapToMute;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceUnmute.tgs");
                    break;
                case ButtonState.Mute:
                    colors = ButtonColors.Mute;
                    Audio.Content = AudioInfo.Text = Strings.VoipGroupUnmute;
                    switch (_prevState)
                    {
                        case ButtonState.CancelReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceCancelReminderToMute.tgs");
                            break;
                        case ButtonState.RaiseHand:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceRaiseHandToMute.tgs");
                            break;
                        case ButtonState.SetReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceSetReminderToMute.tgs");
                            break;
                        case ButtonState.Start:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceStart.tgs");
                            Lottie.Play();
                            break;
                        default:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceMute.tgs");
                            break;
                    }
                    break;
                case ButtonState.RaiseHand:
                case ButtonState.HandRaised:
                    colors = ButtonColors.Disabled;
                    Audio.Content = AudioInfo.Text = state == ButtonState.HandRaised
                        ? Strings.VoipMutedTapedForSpeak
                        : Strings.VoipMutedByAdmin;
                    switch (_prevState)
                    {
                        case ButtonState.CancelReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceCancelReminderToRaiseHand.tgs");
                            break;
                        case ButtonState.Mute:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceMuteToRaiseHand.tgs");
                            break;
                        case ButtonState.Unmute:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceUnmuteToRaiseHand.tgs");
                            break;
                        case ButtonState.SetReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceSetReminderToRaiseHand.tgs");
                            break;
                        default:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceHand_1.tgs");
                            break;
                    }
                    break;
            }

            _prevState = state;
            _prevConnected = connected;

            if (state is not ButtonState.CancelReminder and not ButtonState.SetReminder and not ButtonState.Start && !connected)
            {
                colors = ButtonColors.Connecting;
            }

            if (colors == _prevColors)
            {
                return;
            }

            if (connected)
            {
                UnloadObject(Indeterminate);
            }
            else
            {
                FindName(nameof(Indeterminate));
            }

            switch (colors)
            {
                case ButtonColors.Disabled:
                    _visual.SetColorStops(0xff57A4FE, 0xffF05459, 0xff766EE9);
                    StartAnimating();
                    Message.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x76, 0x6E, 0xE9));
                    break;
                case ButtonColors.Unmute:
                    _visual.SetColorStops(0xFF0078ff, 0xFF33c659);
                    StartAnimating();
                    Message.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x33, 0xc6, 0x59));
                    break;
                case ButtonColors.Mute:
                    _visual.SetColorStops(0xFF59c7f8, 0xFF0078ff);
                    StartAnimating();
                    Message.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x00, 0x78, 0xff));
                    break;
                case ButtonColors.Connecting:
                    _visual.SetColorStops(0xFF3e3f41, 0xFF3e3f41);
                    StartAnimating();
                    Message.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x3e, 0x3f, 0x41));
                    break;
            }

            _prevColors = colors;

            UpdateVideo();
            UpdateScreen();
        }

        private void StartAnimating()
        {
            if (PowerSavingPolicy.AreMaterialsEnabled && ApiInfo.CanAnimatePaths)
            {
                _visual.StartAnimating();
            }
            else
            {
                _visual.StopAnimating();
                _visual.Clear();
            }
        }

        private void UpdateVideo()
        {
            var service = _call;
            if (service == null)
            {
                return;
            }

            if (service.CanEnableVideo && service.ScheduledStartDate == 0)
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Video.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x76, 0x6E, 0xE9));
                        break;
                    case ButtonColors.Unmute:
                        Video.Background = new SolidColorBrush(Color.FromArgb((byte)(_call.IsVideoEnabled ? 0xFF : 0x66), 0x33, 0xc6, 0x59));
                        break;
                    case ButtonColors.Mute:
                        Video.Background = new SolidColorBrush(Color.FromArgb((byte)(_call.IsVideoEnabled ? 0xFF : 0x66), 0x00, 0x78, 0xff));
                        break;
                    case ButtonColors.Connecting:
                        Video.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x3e, 0x3f, 0x41));
                        break;
                }

                Video.Glyph = service.IsVideoEnabled ? Icons.VideoFilled : Icons.VideoOffFilled;
                Video.Visibility = VideoInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Video.Visibility = VideoInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateScreen()
        {
            var service = _call;
            if (service == null)
            {
                return;
            }

            if (service.CanEnableVideo && VoipScreenCapture.IsSupported())
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Screen.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x76, 0x6E, 0xE9));
                        break;
                    case ButtonColors.Unmute:
                        Screen.Background = new SolidColorBrush(Color.FromArgb((byte)(service.IsScreenSharing ? 0xFF : 0x66), 0x33, 0xc6, 0x59));
                        break;
                    case ButtonColors.Mute:
                        Screen.Background = new SolidColorBrush(Color.FromArgb((byte)(service.IsScreenSharing ? 0xFF : 0x66), 0x00, 0x78, 0xff));
                        break;
                    case ButtonColors.Connecting:
                        Screen.Background = new SolidColorBrush(Color.FromArgb(0x66, 0x3e, 0x3f, 0x41));
                        break;
                }

                Screen.Glyph = service.IsScreenSharing ? Icons.ShareScreenStopFilled : Icons.ShareScreenFilled;
                Screen.Visibility = ScreenInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Screen.Visibility = ScreenInfo.Visibility = Visibility.Collapsed;
            }
        }

        private async void Menu_ContextRequested(object sender, RoutedEventArgs e)
        {
            var flyout = new MenuFlyout();

            var aliases = await _call.CanChooseAliasAsync();
            if (aliases)
            {
                flyout.CreateFlyoutItem(async () => await _call.RejoinAsync(XamlRoot), Strings.VoipGroupDisplayAs, Icons.Person);
                flyout.CreateFlyoutSeparator();
            }

            if (_call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(SetTitle, _call.IsChannel ? Strings.VoipChannelEditTitle : Strings.VoipGroupEditTitle, Icons.Edit);
            }

            if (_call.CanToggleMuteNewParticipants)
            {
                var toggleFalse = new ToggleMenuFlyoutItem();
                toggleFalse.Text = Strings.VoipGroupAllCanSpeak;
                toggleFalse.IsChecked = !_call.MuteNewParticipants;
                toggleFalse.Click += (s, args) =>
                {
                    _call.ClientService.Send(new ToggleVideoChatMuteNewParticipants(_call.Id, false));
                };

                var toggleTrue = new ToggleMenuFlyoutItem();
                toggleTrue.Text = Strings.VoipGroupOnlyAdminsCanSpeak;
                toggleTrue.IsChecked = _call.MuteNewParticipants;
                toggleTrue.Click += (s, args) =>
                {
                    _call.ClientService.Send(new ToggleVideoChatMuteNewParticipants(_call.Id, true));
                };

                var settings = new MenuFlyoutSubItem();
                settings.Text = Strings.VoipGroupEditPermissions;
                settings.Icon = MenuFlyoutHelper.CreateIcon(Icons.Key);
                settings.Items.Add(toggleFalse);
                settings.Items.Add(toggleTrue);

                flyout.Items.Add(settings);
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

            if (_call.CanEnableVideo && VoipScreenCapture.IsSupported())
            {
                if (_call.IsScreenSharing)
                {
                    flyout.CreateFlyoutItem(_call.EndScreenSharing, Strings.VoipChatStopScreenCapture, Icons.ShareScreenStop);
                }
                else
                {
                    void StartScreenSharing()
                    {
                        _call.StartScreenSharing(XamlRoot);
                    }

                    flyout.CreateFlyoutItem(StartScreenSharing, Strings.VoipChatStartScreenCapture, Icons.ShareScreenStart);
                }
            }

            if (_call.CanToggleAreMessagesAllowed)
            {
                flyout.CreateFlyoutItem(ToggleAreMessagesAllowed, _call.AreMessagesAllowed ? Strings.VoipChannelDisableComments : Strings.VoipChannelEnableComments, _call.AreMessagesAllowed ? Icons.ChatOff : Icons.Chat);
            }

            if (_call.ScheduledStartDate == 0)
            {
                flyout.CreateFlyoutSeparator();

                var videoId = _call.VideoInputId;
                var inputId = _call.AudioInputId;
                var outputId = _call.AudioOutputId;

                var video = new MenuFlyoutSubItem();
                video.Text = Strings.VoipDeviceCamera;
                video.Icon = MenuFlyoutHelper.CreateIcon(Icons.Camera);

                var input = new MenuFlyoutSubItem();
                input.Text = Strings.VoipDeviceInput;
                input.Icon = MenuFlyoutHelper.CreateIcon(Icons.MicOn);

                var output = new MenuFlyoutSubItem();
                output.Text = Strings.VoipDeviceOutput;
                output.Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3);

                flyout.Items.Add(video);
                flyout.Items.Add(input);
                flyout.Items.Add(output);

                var hasVideo = _call.Devices.HasDevices(MediaDeviceClass.VideoInput);
                if (hasVideo is true)
                {
                    foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.VideoInput))
                    {
                        var deviceItem = new ToggleMenuFlyoutItem();
                        deviceItem.Text = device.Name;
                        deviceItem.IsChecked = videoId == device.Id;
                        deviceItem.Click += (s, args) =>
                        {
                            _call.VideoInputId = device.Id;
                        };

                        video.Items.Add(deviceItem);
                    }
                }
                else
                {
                    video.CreateFlyoutItem(null, hasVideo.HasValue ? Strings.NotFoundCamera : Strings.Loading);
                }

                var hasInput = _call.Devices.HasDevices(MediaDeviceClass.AudioInput);
                if (hasInput is true)
                {
                    foreach (var device in _call.Devices.GetDevices(MediaDeviceClass.AudioInput))
                    {
                        var deviceItem = new ToggleMenuFlyoutItem();
                        deviceItem.Text = device.Name;
                        deviceItem.IsChecked = inputId == device.Id;
                        deviceItem.Click += (s, args) =>
                        {
                            _call.AudioInputId = device.Id;
                        };

                        input.Items.Add(deviceItem);
                    }
                }
                else
                {
                    input.CreateFlyoutItem(null, hasInput.HasValue ? Strings.NotFoundMicrophone : Strings.Loading);
                }

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

                flyout.CreateFlyoutItem(() => _call.IsNoiseSuppressionEnabled = !_call.IsNoiseSuppressionEnabled, Strings.VoipNoiseCancellation, _call.IsNoiseSuppressionEnabled ? Icons.Checkmark : null);
            }

            //flyout.CreateFlyoutItem(ShareInviteLink, Strings.VoipGroupShareInviteLink, Icons.Link);

            if (_call.CanBeManaged)
            {
                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(Discard, _call.IsChannel ? Strings.VoipChannelEndChat : Strings.VoipGroupEndChat, Icons.Dismiss, destructive: true);
            }

            if (flyout.Items.Count > 0)
            {
                flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedLeft);
            }
        }

        private void ToggleAreMessagesAllowed()
        {
            _call.ClientService.Send(new ToggleGroupCallAreMessagesAllowed(_call.Id, !_call.AreMessagesAllowed));
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
                _call.ClientService.Send(new SetVideoChatTitle(_call.Id, input.Text));
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

        private void ShareInviteLink()
        {
            this.ShowPopup(_call.ClientService.Session, new ChooseChatsPopup(), new ChooseChatsConfigurationGroupCall(_call.Id, false));
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Participant_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var participant = args.Item as GroupCallParticipant;

            if (args.InRecycleQueue)
            {
                return;
            }

            UpdateGroupCallParticipant(args.ItemContainer, content, participant, true);
            args.Handled = true;
        }

        public void UpdateGroupCallParticipant(GroupCallParticipant participant)
        {
            foreach (var videoInfo in participant.GetVideoInfo())
            {
                if (_gridCells.TryGetValue(videoInfo.EndpointId, out var cell))
                {
                    cell.UpdateGroupCallParticipant(_call.ClientService, participant, videoInfo);
                }
            }

            var container = ScrollingHost.ContainerFromItem(participant) as SelectorItem;
            var content = container?.ContentTemplateRoot as Grid;

            if (content == null)
            {
                return;
            }

            UpdateGroupCallParticipant(container, content, participant, false);
        }

        private void UpdateGroupCallParticipant(SelectorItem container, Grid content, GroupCallParticipant participant, bool containerContentChanging)
        {
            var wave = content.Children[0] as Border;
            var photo = content.Children[1] as ProfilePicture;
            var title = content.Children[2] as TextBlock;
            var subtitle = content.Children[3] as Grid;
            var glyph = content.Children[4] as TextBlock;

            var status = subtitle.Children[0] as TextBlock;
            var speaking = subtitle.Children[1] as TextBlock;

            if (containerContentChanging)
            {
                var element = ElementComposition.GetElementVisual(wave);
                element.CenterPoint = new Vector3(36 / 2);
                element.Scale = new Vector3(0.9f);
            }

            if (_call.ClientService.TryGetUser(participant.ParticipantId, out Telegram.Td.Api.User user))
            {
                photo.Source = ProfilePictureSource.User(_call.ClientService, user);
                title.Text = user.FullName();
            }
            else if (_call.ClientService.TryGetChat(participant.ParticipantId, out Chat chat))
            {
                photo.Source = ProfilePictureSource.Chat(_call.ClientService, chat);
                title.Text = _call.ClientService.GetTitle(chat);
            }

            if (participant.HasVideoInfo())
            {
                if (_mode != ParticipantsGridMode.Compact && _selectedEndpointId != null)
                {
                    container.IsEnabled = false;
                    container.Visibility = Visibility.Collapsed;
                }
                else
                {
                    container.IsEnabled = true;
                    container.Visibility = Visibility.Visible;
                }

                if (participant.ScreenSharingVideoInfo != null && participant.VideoInfo != null)
                {
                    status.Text = Icons.SmallScreencastFilled + Icons.SmallVideoFilled;
                }
                else if (participant.ScreenSharingVideoInfo != null && participant.VideoInfo != null)
                {
                    status.Text = Icons.SmallScreencastFilled;
                }
                else if (participant.VideoInfo != null)
                {
                    status.Text = Icons.SmallVideoFilled;
                }

                status.Margin = new Thickness(0, 0, 4, 0);
            }
            else
            {
                container.IsEnabled = true;
                container.Visibility = Visibility.Visible;

                status.Text = string.Empty;
                status.Margin = new Thickness(0);
            }

            if (participant.IsHandRaised)
            {
                speaking.Text = Strings.WantsToSpeak;
                speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xff));
                glyph.Text = Icons.HandRight;
                glyph.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x00, 0x78, 0xff));
            }
            else if (participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser)
            {
                var permanent = participant.IsMutedForCurrentUser || (participant.IsMutedForAllUsers && !participant.CanUnmuteSelf);

                if (participant.IsCurrentUser)
                {
                    speaking.Text = Strings.ThisIsYou;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x4D, 0xB8, 0xFF));
                }
                else if (participant.IsMutedForCurrentUser)
                {
                    speaking.Text = Strings.VoipGroupMutedForMe;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    speaking.Text = participant.Bio.Length > 0 ? participant.Bio : Strings.Listening;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x85, 0x85, 0x85));
                }

                wave.Background = new SolidColorBrush(permanent ? Color.FromArgb(0xDD, 0xFF, 0x00, 0x00) : Color.FromArgb(0xDD, 0x4D, 0xB8, 0xFF));
                glyph.Text = Icons.MicOff;
                glyph.Foreground = new SolidColorBrush(permanent ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85));
            }
            else
            {
                if (participant.IsSpeaking && participant.VolumeLevel != 10000)
                {
                    speaking.Text = string.Format(Strings.SpeakingWithVolume, (participant.VolumeLevel / 100d).ToString("N0"));
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0xc6, 0x59));
                }
                else if (participant.IsSpeaking)
                {
                    speaking.Text = Strings.Speaking;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x33, 0xc6, 0x59));
                }
                else if (participant.IsCurrentUser)
                {
                    speaking.Text = Strings.ThisIsYou;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x4D, 0xB8, 0xFF));
                }
                else
                {
                    speaking.Text = Strings.Listening;
                    speaking.Foreground = status.Foreground = new SolidColorBrush(Color.FromArgb(0xFF, 0x85, 0x85, 0x85));
                }

                wave.Background = new SolidColorBrush(participant.IsSpeaking ? Color.FromArgb(0xDD, 0x33, 0xc6, 0x59) : Color.FromArgb(0xDD, 0x4D, 0xB8, 0xFF));
                glyph.Text = Icons.MicOn;
                glyph.Foreground = new SolidColorBrush(participant.IsSpeaking ? Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) : Color.FromArgb(0xFF, 0x85, 0x85, 0x85));
            }
        }

        private void AddGridItem(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo, bool screenSharing)
        {
            AddItem(participant, videoInfo, screenSharing, false);
        }

        private void AddListItem(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo, bool screenSharing)
        {
            AddItem(participant, videoInfo, screenSharing, true);
        }

        private void AddItem(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo, bool screenSharing, bool list)
        {
            var cells = list ? _listCells : _gridCells;
            var viewport = list ? ListViewport.Children : Viewport.Children;

            if (cells.ContainsKey(videoInfo.EndpointId))
            {
                return;
            }

            var child = new GroupCallParticipantGridCell(_call.ClientService, participant, videoInfo, screenSharing);
            child.Click += Participant_Click;
            child.ToggleDocked += Participant_ToggleDocked;
            child.ContextRequested += Participant_ContextRequested;
            child.IsList = list;

            cells[videoInfo.EndpointId] = child;
            viewport.Add(child);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.MaxWidth = 324;
                ViewportAspect.Margin = new Thickness(-4, -2, -4, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else if (_mode == ParticipantsGridMode.Docked)
            {
                ViewportAspect.MaxWidth = double.PositiveInfinity;
                ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
            }
            else
            {
                ViewportAspect.MaxWidth = double.PositiveInfinity;
                ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
            }

            ListViewport.Margin = new Thickness(-4, -2, -4, ListViewport.Children.Count > 0 ? 4 : 0);

            UpdateLayout(ActualSize, ActualSize, true);
        }

        private void RemoveGridItem(GroupCallParticipantGridCell cell)
        {
            RemoveItem(cell, false);
        }

        private void RemoveListItem(GroupCallParticipantGridCell cell)
        {
            RemoveItem(cell, true);
        }

        private void RemoveItem(GroupCallParticipantGridCell cell, bool list)
        {
            var cells = list ? _listCells : _gridCells;
            var viewport = list ? ListViewport.Children : Viewport.Children;

            if (_selectedEndpointId == cell.EndpointId && !list)
            {
                _selectedEndpointId = null;
            }

            cells.Remove(cell.EndpointId);
            viewport.Remove(cell);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.MaxWidth = 324;
                ViewportAspect.Margin = new Thickness(-4, -2, -4, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else if (_mode == ParticipantsGridMode.Docked)
            {
                ViewportAspect.MaxWidth = double.PositiveInfinity;
                ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
            }
            else
            {
                ViewportAspect.MaxWidth = double.PositiveInfinity;
                ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
            }

            ListViewport.Margin = new Thickness(-4, -2, -4, ListViewport.Children.Count > 0 ? 4 : 0);

            UpdateLayout(ActualSize, ActualSize, true);
        }

        private async void Participant_Click(object sender, RoutedEventArgs e)
        {
            //var view = ApplicationView.GetForCurrentView();
            //var size = view.VisibleBounds;

            //if (size.Width < 500)
            //{
            //    view.TryResizeView(new Size(780, size.Height + 1));
            //}

            var cell = sender as GroupCallParticipantGridCell;
            if (cell == null)
            {
                return;
            }

            if (cell.Parent == ListViewport && !_gridCells.TryGetValue(cell.EndpointId, out cell))
            {
                return;
            }

            cell.IsSelected = !cell.IsSelected;

            foreach (var child in Viewport.Cells)
            {
                if (child == cell)
                {
                    Canvas.SetZIndex(child, 1);
                    child.ShowHideHeader(cell.IsSelected, _mode == ParticipantsGridMode.Compact ? null : _docked);
                    continue;
                }

                Canvas.SetZIndex(child, 0);
                child.ShowHideHeader(false, null);
                child.IsSelected = false;
            }

            _selectedEndpointId = cell.IsSelected ? cell.EndpointId : null;
            Viewport.InvalidateMeasure();

            TransformList(ActualSize, ActualSize, _mode, _mode);

            // Wait for the UI to update to calculate correct quality
            await this.UpdateLayoutAsync();
            UpdateVisibleParticipants(false);

            if (_mode == ParticipantsGridMode.Compact)
            {
                var scrollingHost = ScrollingHost.GetScrollViewer();
                scrollingHost?.TryChangeView(null, 0, null, false);
            }
        }

        private void Participant_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = (element.Tag ?? ScrollingHost.ItemFromContainer(sender)) as GroupCallParticipant;

            var call = _call;

            if (participant.IsCurrentUser)
            {
                if (participant.IsHandRaised)
                {
                    flyout.CreateFlyoutItem(() => _call.ClientService.Send(new ToggleGroupCallParticipantIsHandRaised(_call.Id, participant.ParticipantId, false)), Strings.VoipGroupCancelRaiseHand, Icons.HandRight);
                }

                if (participant.HasVideoInfo())
                {
                    //if (participant.ParticipantId.IsEqual(_pinnedParticipant?.ParticipantId))
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(null), "Unpin Video", Icons.PinOff);
                    //}
                    //else
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(participant), "Pin Video", Icons.Pin);
                    //}
                }
            }
            else
            {
                var slider = new MenuFlyoutSlider
                {
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.Speaker3),
                    TextValueConverter = new TextValueProvider(newValue => string.Format("{0:P0}", newValue / 100)),
                    IconValueConverter = new IconValueProvider(newValue => newValue switch
                    {
                        double n when n > 66 => Icons.Speaker3,
                        double n when n > 33 => Icons.Speaker2,
                        double n when n > 0 => Icons.Speaker1,
                        _ => Icons.SpeakerOff
                    }),
                    FontWeight = FontWeights.SemiBold,
                    Value = participant.VolumeLevel / 100d,
                    Minimum = 0,
                    Maximum = 200
                };

                var debounder = new EventDebouncer<RangeBaseValueChangedEventArgs>(Constants.HoldingThrottle, handler => slider.ValueChanged += new RangeBaseValueChangedEventHandler(handler), handler => slider.ValueChanged -= new RangeBaseValueChangedEventHandler(handler));
                debounder.Invoked += (s, args) =>
                {
                    if (args.NewValue > 0)
                    {
                        _call.ClientService.Send(new SetGroupCallParticipantVolumeLevel(call.Id, participant.ParticipantId, (int)(args.NewValue * 100)));
                    }
                    else
                    {
                        _call.ClientService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, participant.ParticipantId, true));
                    }
                };

                flyout.Items.Add(slider);

                if (participant.HasVideoInfo())
                {
                    //if (participant.ParticipantId.IsEqual(_pinnedParticipant?.ParticipantId))
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(null), "Unpin Video", Icons.PinOff);
                    //}
                    //else
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(participant), "Pin Video", Icons.Pin);
                    //}
                }

                if (participant.CanBeUnmutedForAllUsers)
                {
                    flyout.CreateFlyoutItem(() => _call.ClientService.Send(new ToggleGroupCallParticipantIsMuted(_call.Id, participant.ParticipantId, false)), Strings.VoipGroupAllowToSpeak, Icons.MicOn);
                }
                else if (participant.CanBeUnmutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _call.ClientService.Send(new ToggleGroupCallParticipantIsMuted(_call.Id, participant.ParticipantId, false)), Strings.VoipGroupUnmuteForMe, Icons.MicOn);
                }
                else if (participant.CanBeMutedForAllUsers)
                {
                    flyout.CreateFlyoutItem(() => _call.ClientService.Send(new ToggleGroupCallParticipantIsMuted(_call.Id, participant.ParticipantId, true)), Strings.VoipGroupMute, Icons.MicOff);
                }
                else if (participant.CanBeMutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _call.ClientService.Send(new ToggleGroupCallParticipantIsMuted(_call.Id, participant.ParticipantId, true)), Strings.VoipGroupMuteForMe, Icons.MicOff);
                }

                //if (_clientService.TryGetUser(participant.ParticipantId, out User user))
                //{
                //    flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.VoipGroupOpenProfile, Icons.Person);
                //}
                //else if (_clientService.TryGetChat(participant.ParticipantId, out Chat chat))
                //{
                //    if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                //    {
                //        flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.VoipGroupOpenChannel, Icons.Megaphone);
                //    }
                //    else
                //    {
                //        flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.VoipGroupOpenGroup, Icons.People);
                //    }
                //}

                if (_call.ClientService.TryGetSupergroup(_call.Chat, out Supergroup supergroup))
                {
                    if (supergroup.CanRestrictMembers())
                    {
                        flyout.CreateFlyoutItem(RemoveParticipant, participant, Strings.VoipGroupUserRemove, Icons.Delete, destructive: true);
                    }
                }
            }

            flyout.ShowAt(element, args);
        }

        private async void RemoveParticipant(GroupCallParticipant participant)
        {
            var title = _call.ClientService.GetTitle(participant.ParticipantId);

            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = Strings.VoipGroupRemoveMemberAlertTitle2;
            popup.Message = string.Format(_call.IsChannel ? Strings.VoipChannelRemoveMemberAlertText2 : Strings.VoipGroupRemoveMemberAlertText2, title, _call.Chat.Title);
            popup.PrimaryButtonText = Strings.VoipGroupUserRemove;
            popup.SecondaryButtonText = Strings.Cancel;

            var confirm = await popup.ShowQueuedAsync(XamlRoot);
            if (confirm == ContentDialogResult.Primary)
            {
                _call.ClientService.Send(new SetChatMemberStatus(_call.Chat.Id, participant.ParticipantId, new ChatMemberStatusBanned()));
            }
        }

        private ScrollViewer _scrollingHost;

        private void List_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = ScrollingHost.GetScrollViewer();
            if (scrollingHost != null)
            {
                _scrollingHost = scrollingHost;
                _scrollingHost.ViewChanged += OnParticipantsViewChanged;
            }

            var panel = ScrollingHost.ItemsPanelRoot;
            if (panel != null)
            {
                panel.SizeChanged += OnParticipantsSizeChanged;
            }
        }

        private void OnParticipantsViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            UpdateVisibleParticipants(e.IsIntermediate);
        }

        private void OnParticipantsSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisibleParticipants(false);
        }

        private void UpdateVisibleParticipants(bool intermediate)
        {
            if (_scrollingHost == null || _disposed)
            {
                return;
            }

            int gridFirst = 0;
            int gridLast = -1;
            int listFirst = 0;
            int listLast = -1;

            if (_selectedEndpointId != null || Viewport.Mode != ParticipantsGridMode.Compact)
            {
                if (_selectedEndpointId != null && _gridCells.TryGetValue(_selectedEndpointId, out GroupCallParticipantGridCell cell))
                {
                    gridFirst = Viewport.Children.IndexOf(cell);
                    gridLast = gridFirst;
                }
                else
                {
                    gridFirst = 0;
                    gridLast = Viewport.Children.Count - 1;
                }

                listFirst = (int)Math.Truncate(_scrollingHost.VerticalOffset / (ListViewport.ActualWidth / 16 * 9));
                listLast = (int)Math.Ceiling((_scrollingHost.VerticalOffset + _scrollingHost.ViewportHeight) / (ListViewport.ActualWidth / 16 * 9));

                listLast = Math.Min(listLast, ListViewport.Children.Count - 1);
            }
            else if (Viewport.Mode == ParticipantsGridMode.Compact)
            {
                gridFirst = (int)Math.Truncate(_scrollingHost.VerticalOffset / (Viewport.ActualWidth / 2));
                gridLast = (int)Math.Ceiling((_scrollingHost.VerticalOffset + _scrollingHost.ViewportHeight) / (Viewport.ActualWidth / 2));

                gridLast *= 2;
                gridLast = Math.Min(gridLast - 1, Viewport.Children.Count - 1);
            }

            var descriptions = new Dictionary<string, VoipVideoChannelInfo>();

            UpdateVisibleParticipants(gridFirst, gridLast, false, descriptions);
            UpdateVisibleParticipants(listFirst, listLast, true, descriptions);

            foreach (var sink in _sinks.ToArray())
            {
                if (descriptions.ContainsKey(sink.Key))
                {
                    continue;
                }

                _sinks.Remove(sink.Key);
                sink.Value.Stop();
            }

            _call?.SetRequestedVideoChannels(descriptions.Values.ToArray());
        }

        private void ConnectVisual(GroupCallParticipantGridCell cell, string endpointId, bool mirrored, Action<string, VoipVideoOutputSink> connect)
        {
            if (_sinks.TryGetValue(endpointId, out VoipVideoOutputReference reference))
            {
                cell.Visual.Brush = reference.Brush;
            }
            else
            {
                var sink = new VoipVideoOutputSink(PlaceholderHelper.Foreground.Device, cell.Visual, mirrored, false);
                reference = new VoipVideoOutputReference(sink, cell.Visual.Brush as CompositionSurfaceBrush);

                _sinks[endpointId] = reference;
                connect(endpointId, sink);
            }
        }

        private void UpdateVisibleParticipants(int first, int last, bool list, Dictionary<string, VoipVideoChannelInfo> descriptions)
        {
            var viewport = list ? ListViewport.Children : Viewport.Children;

            if (last < viewport.Count && first <= last && first >= 0)
            {
                for (int i = 0; i < viewport.Count; i++)
                {
                    var child = viewport[i] as GroupCallParticipantGridCell;
                    var participant = child.Participant;

                    if (i >= first && i <= last)
                    {
                        if (descriptions.TryGetValue(child.EndpointId, out VoipVideoChannelInfo info))
                        {
                            // TODO
                        }
                        else
                        {
                            descriptions[child.EndpointId] =
                                new VoipVideoChannelInfo(
                                    child.Participant.AudioSourceId,
                                    child.Participant.ParticipantId.ToId(),
                                    child.EndpointId,
                                    child.VideoInfo.SourceGroups.ToCalls(),
                                    child.Quality,
                                    child.Quality);
                        }

                        if (participant.ScreenSharingVideoInfo?.EndpointId == child.EndpointId && participant.IsCurrentUser && _call.IsScreenSharing)
                        {
                            //ConnectVisual(child, child.EndpointId, false, _call.AddScreenSharingVideoOutput);
                        }
                        else
                        {
                            ConnectVisual(child, child.EndpointId, participant.IsCurrentUser, _call.AddIncomingVideoOutput);
                        }
                    }
                    else
                    {
                        child.Visual.Brush = null;
                    }
                }
            }
        }

        private void Viewport_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PointerListener);
            if (point.Position.X > PointerListener.ActualWidth - ColumnWidth && Viewport.Mode == ParticipantsGridMode.Docked)
            {
                ShowHideInfo(false);
            }
            else if (point.Position.Y > PointerListener.ActualHeight - BottomPanel.ActualHeight && Viewport.Mode == ParticipantsGridMode.Compact)
            {
                ShowHideInfo(false);
            }
            else
            {
                ShowHideInfo(point.Position.Y >= 0);
            }
        }

        private void Viewport_PointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            ShowHideInfo(false);
        }

        private bool _infoCollapsed;

        private void ShowHideInfo(bool show)
        {
            if (_infoCollapsed == !show)
            {
                return;
            }

            if (!_messageCollapsed && !show)
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

            _infoCollapsed = !show;

            foreach (var child in Viewport.Cells)
            {
                child.ShowHideInfo(show, _mode == ParticipantsGridMode.Compact ? null : _docked);
            }

            ShowHideBottomRoot(show || Viewport.Mode == ParticipantsGridMode.Compact);
        }

        private bool _bottomRootCollapsed;

        private void ShowHideBottomRoot(bool show)
        {
            if (_bottomRootCollapsed == !show)
            {
                return;
            }

            _bottomRootCollapsed = !show;

            if (show)
            {
                StartAnimating();
            }
            else
            {
                _visual.StopAnimating();
            }

            var anim = BootStrapper.Current.Compositor.CreateScalarKeyFrameAnimation();
            anim.InsertKeyFrame(0, show ? 0 : 1);
            anim.InsertKeyFrame(1, show ? 1 : 0);

            var root = ElementComposition.GetElementVisual(BottomPanel);

            root.StartAnimation("Opacity", anim);
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (ParticipantsPanel.Children.Contains(ViewportAspect) || e.NewSize.Height == e.PreviousSize.Height)
            {
                return;
            }

            var scrollingHost = ScrollingHost.GetScrollViewer();
            if (scrollingHost == null || scrollingHost.VerticalOffset > Math.Max(e.NewSize.Height, e.PreviousSize.Height))
            {
                return;
            }

            var panel = ScrollingHost.ItemsPanelRoot;
            if (panel == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(panel);
            ElementCompositionPreview.SetIsTranslationEnabled(panel, true);

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var animation = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(0, prev.Y - next.Y, 0));
            animation.InsertKeyFrame(1, Vector3.Zero);

            visual.StartAnimation("Translation", animation);

            UpdateVisibleParticipants(false);
        }

        private void OnItemsPanelSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var element = sender as UIElement;
            var geometry = BootStrapper.Current.Compositor.CreateRoundedRectangleGeometry();
            var visual = ElementComposition.GetElementVisual(element);
            visual.Clip = BootStrapper.Current.Compositor.CreateGeometricClip(geometry);

            geometry.Size = e.NewSize.ToVector2();
            geometry.CornerRadius = new Vector2(8);
        }

        private void MessagesHost_ContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.ItemContainer.ContentTemplateRoot is GroupCallMessageCell content && args.Item is GroupCallMessage message)
            {
                content.Update(_call.ClientService, message);
            }
        }

        private void MessageField_TextChanged(object sender, RoutedEventArgs e)
        {
            ShowHideReactions(MessageField.IsEmpty && !_messageCollapsed);
            SendMessage.IsEnabled = !MessageField.IsEmpty;
        }

        private bool _reactionsCollapsed = true;

        private void ShowHideReactions(bool show)
        {
            if (_reactionsCollapsed != show)
            {
                return;
            }

            _reactionsCollapsed = !show;
            MessageReactions.Visibility = Visibility.Visible;
            MessageReactionsPlaceholder.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;

            var visual = ElementComposition.GetElementVisual(MessageReactions);
            visual.CenterPoint = new Vector3(264 / 2, 112 - 20, 0);

            var compositor = visual.Compositor;
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_reactionsCollapsed)
                {
                    MessageReactions.Visibility = Visibility.Collapsed;
                }
            };

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);
            scale.Duration = Constants.SoftAnimation;

            var opacity = compositor.CreateScalarKeyFrameAnimation();
            opacity.InsertKeyFrame(show ? 0 : 1, 0);
            opacity.InsertKeyFrame(show ? 1 : 0, 1);
            opacity.Duration = Constants.SoftAnimation;

            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Opacity", opacity);
            batch.End();
        }

        private bool _messageCollapsed = true;

        private void ShowHideMessage(bool show)
        {
            if (_messageCollapsed != show)
            {
                return;
            }

            ShowHideReactions(show);

            _messageCollapsed = !show;
            MessagePanel.Visibility = Visibility.Visible;
            MessagePlaceholder.Visibility = show
                ? Visibility.Visible
                : Visibility.Collapsed;

            Message.Glyph = _messageCollapsed
                ? Icons.ChatFilled
                : Icons.DismissFilled;

            var width = Math.Min(MessagesHost.ActualSize.X - 48, 320);
            var transform = Message.TransformToVector2(MessageRoot);
            var offset = transform.X;

            var visual = ElementComposition.GetElementVisual(MessagePanel);
            ElementCompositionPreview.SetIsTranslationEnabled(MessagePanel, true);

            visual.CenterPoint = new Vector3(MessageRoot.ActualSize.X - 24, 48 + 16, 0);

            var compositor = visual.Compositor;
            var batch = compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                visual.Clip = null;

                if (_messageCollapsed)
                {
                    MessagePanel.Visibility = Visibility.Collapsed;
                    MessageField.ClearText();
                }
                else
                {
                    MessageField.Focus(FocusState.Keyboard);
                }
            };

            var rectangle = compositor.CreateRoundedRectangleGeometry();
            rectangle.CornerRadius = new Vector2(24);
            rectangle.Size = new Vector2(48);

            visual.Clip = compositor.CreateGeometricClip(rectangle);

            var resize = compositor.CreateVector2KeyFrameAnimation();
            resize.InsertKeyFrame(show ? 0 : 1, new Vector2(48));
            resize.InsertKeyFrame(show ? 1 : 0, new Vector2(width, 48));
            resize.Duration = Constants.SoftAnimation;

            var remove = compositor.CreateScalarKeyFrameAnimation();
            remove.InsertKeyFrame(show ? 0 : 1, MessageRoot.ActualSize.X - 48);
            remove.InsertKeyFrame(show ? 1 : 0, 0);
            remove.Duration = Constants.SoftAnimation;

            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(show ? 0 : 1, offset - MessageRoot.ActualSize.X + 48);
            translation.InsertKeyFrame(show ? 1 : 0, 0);
            translation.Duration = Constants.SoftAnimation;

            var scale = compositor.CreateVector3KeyFrameAnimation();
            scale.InsertKeyFrame(show ? 0 : 1, Vector3.Zero);
            scale.InsertKeyFrame(show ? 1 : 0, Vector3.One);

            //if (!show)
            //{
            //    //scale.DelayTime = show ? TimeSpan.Zero : Constants.FastAnimation;
            //    scale.Duration = Constants.SoftAnimation;
            //}
            //else
            {
                //scale.DelayTime = scale.DelayTime = show ? TimeSpan.Zero : Constants.FastAnimation;
                scale.Duration = Constants.SoftAnimation;
            }

            rectangle.StartAnimation("Size", resize);
            rectangle.StartAnimation("Offset.X", remove);
            visual.StartAnimation("Scale", scale);
            visual.StartAnimation("Translation.X", translation);
            batch.End();
        }

        private void MessageField_Accept(FormattedTextBox sender, EventArgs args)
        {
            _call.SendMessage(sender.GetFormattedText(true));
        }

        private void Emoji_Click(object sender, RoutedEventArgs e)
        {
            // We don't want to unfocus the text are when the context menu gets opened
            EmojiPanel.ViewModel.Update();
            EmojiFlyout.ShowAt(MessagePanel, new FlyoutShowOptions { ShowMode = FlyoutShowMode.Transient });
        }

        private void Emoji_ItemClick(object sender, EmojiDrawerItemClickEventArgs e)
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

        private void Send_Click(object sender, RoutedEventArgs e)
        {
            _call.SendMessage(MessageField.GetFormattedText(true));
        }

        private void MessageField_GotFocus(object sender, RoutedEventArgs e)
        {


        }

        private void MessageField_LostFocus(object sender, RoutedEventArgs e)
        {
            if (MessageField.IsEmpty)
            {
                //MessagePanel.Visibility = Visibility.Collapsed;
            }
        }

        private void Message_Click(object sender, RoutedEventArgs e)
        {
            ShowHideMessage(_messageCollapsed);
        }

        private void MessageReactions_ItemClick(object sender, AvailableReaction e)
        {
            if (e.Type is ReactionTypeEmoji emoji)
            {
                _call.SendMessage(emoji.Emoji.AsFormattedText());
            }
            else if (e.Type is ReactionTypeCustomEmoji customEmoji)
            {
                _call.SendMessage(ClientEx.CustomEmoji(customEmoji.CustomEmojiId));
            }

            ShowHideMessage(false);
        }

        private void MessagePill_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var transform = Message.TransformToPoint(MessageRoot);
            var offset = transform.X + Message.ActualWidth;

            var width = e.NewSize.Width;
            var height = e.NewSize.Height;
            var haheight = 24;

            var figure = new PathFigure();
            figure.StartPoint = new Point(haheight, 0);
            figure.Segments.Add(new LineSegment { Point = new Point(width - haheight, 0) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width, haheight), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(width, height - haheight) });
            figure.Segments.Add(new ArcSegment { Point = new Point(width - haheight, height), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            figure.Segments.Add(new LineSegment { Point = new Point(offset, height) });
            //figure.Segments.Add(new LineSegment { Point = new Point(offset + 7, height + 7) });

            figure.Segments.Add(new ArcSegment { Point = new Point(offset - 14, height), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            //figure.Segments.Add(new ArcSegment { Point = new Point(width - haheight - 14, height), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            figure.Segments.Add(new LineSegment { Point = new Point(haheight, height) });
            figure.Segments.Add(new ArcSegment { Point = new Point(0, height - haheight), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });
            figure.Segments.Add(new LineSegment { Point = new Point(0, haheight) });
            figure.Segments.Add(new ArcSegment { Point = new Point(haheight, 0), Size = new Size(haheight, haheight), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            var path = new PathGeometry();
            path.Figures.Add(figure);

            var data = new GeometryGroup();
            data.FillRule = FillRule.Nonzero;
            data.Children.Add(path);

            //figure.Segments.Add(new ArcSegment { Point = new Point(width - haheight - 14, height), Size = new Size(7, 7), RotationAngle = 180, SweepDirection = SweepDirection.Clockwise });

            data.Children.Add(new EllipseGeometry { Center = new Point(offset - 8 + 5, height + 20 - 7), RadiusX = 3.5f, RadiusY = 3.5f });
            //data.Children.Add(new EllipseGeometry { Center = new Point(width - haheight - 8 + 5, height + 20 - 7), RadiusX = 3.5f, RadiusY = 3.5f });

            MessagePill.Data = data;
        }

        private void MessageRoot_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var next = e.NewSize.ToVector2();
            var prev = e.PreviousSize.ToVector2();

            var panel = MessagesHost.ItemsPanelRoot;
            if (panel == null)
            {
                return;
            }

            var visual = ElementComposition.GetElementVisual(panel);
            ElementCompositionPreview.SetIsTranslationEnabled(panel, true);

            var compositor = visual.Compositor;
            var translation = compositor.CreateScalarKeyFrameAnimation();
            translation.InsertKeyFrame(0, next.Y - prev.Y);
            translation.InsertKeyFrame(1, 0);
            translation.Duration = Constants.SoftAnimation;

            visual.StartAnimation("Translation.Y", translation);

        }

        private void MessagePanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MessagePlaceholder.Height = e.NewSize.Height + MessagePanel.Margin.Top + MessagePanel.Margin.Bottom;
        }

        private void MessageReactions_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MessageReactionsPlaceholder.Height = e.NewSize.Height + MessageReactions.Margin.Top + MessageReactions.Margin.Bottom;
        }

        private void MessagesHost_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = MessagesHost.GetScrollViewer();
            scrollingHost?.VerticalAnchorRatio = 1.0;

            AddHandler(PointerPressedEvent, new PointerEventHandler(Test), true);
        }

        private void Test(object sender, PointerRoutedEventArgs e)
        {
            var children = VisualTreeHelper.FindElementsInHostCoordinates(e.GetCurrentPoint(this).Position, this).ToList();
            var a = 1 + 2;
        }
    }

    public partial class BottomPanel : Grid
    {
        private int _columns;

        protected override Size MeasureOverride(Size availableSize)
        {
            var columns = 0;
            var availableWidth = Math.Min(availableSize.Width - Padding.Left - Padding.Right, 288);

            for (int i = 0; i < Children.Count; i += 2)
            {
                var child = Children[i];
                if (child.Visibility == Visibility.Collapsed)
                {
                    continue;
                }

                columns++;
            }

            var column = new Size(Math.Max(0, (availableWidth - ColumnSpacing * (columns - 1)) / columns), availableSize.Height);
            var height = 0d;

            for (int i = 0; i < Children.Count; i += 2)
            {
                var child0 = Children[i];
                var child1 = Children[i + 1];
                child0.Measure(column);
                child1.Measure(availableSize);

                if (child0.Visibility == Visibility.Visible)
                {
                    height = Math.Max(height, child0.DesiredSize.Height + child1.DesiredSize.Height);
                }
            }

            _columns = columns;
            return new Size(availableWidth + Padding.Left + Padding.Right, height + Padding.Top + Padding.Bottom);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var x = Padding.Left;
            var y = Padding.Top;

            var availableWidth = finalSize.Width - Padding.Left - Padding.Right;
            var column = (availableWidth - ColumnSpacing * (_columns - 1)) / _columns;

            for (int i = 0; i < Children.Count; i += 2)
            {
                var child0 = Children[i];
                var child1 = Children[i + 1];

                var offset0 = (x + column / 2) - child0.DesiredSize.Width / 2;
                var offset1 = (x + column / 2) - child1.DesiredSize.Width / 2;

                child0.Arrange(new Rect(offset0, y, child0.DesiredSize.Width, child0.DesiredSize.Height));
                child1.Arrange(new Rect(offset1, y + child0.DesiredSize.Height, child1.DesiredSize.Width, child1.DesiredSize.Height));

                if (child0.Visibility == Visibility.Visible)
                {
                    x += column + ColumnSpacing;
                }
            }

            return finalSize;
        }
    }

    public enum ParticipantsGridMode
    {
        Compact,
        Docked,
        Expanded
    }

    public partial class ParticipantsGrid : Panel
    {
        private ParticipantsGridMode _mode = ParticipantsGridMode.Compact;
        public ParticipantsGridMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    InvalidateMeasure();
                }
            }
        }

        public IEnumerable<GroupCallParticipantGridCell> Cells => Children.OfType<GroupCallParticipantGridCell>();

        private readonly List<Rect> _prev = new();
        private ParticipantsGridMode _prevMode = ParticipantsGridMode.Compact;
        private bool _prevPinned;
        private int _prevCount;

        protected override Size MeasureOverride(Size availableSize)
        {
            var index = 0;
            var count = Children.Count;

            var rows = Math.Ceiling(Math.Sqrt(count));
            var columns = Math.Ceiling(count / rows);

            var finalWidth = availableSize.Width;
            var finalHeight = availableSize.Height;

            if (_mode == ParticipantsGridMode.Compact)
            {
                rows = Math.Ceiling(count / 2d);
                columns = 2;

                finalHeight = finalWidth / 2 * rows;
            }
            else
            {
                var tail = columns - (rows * columns - count);
                if (tail > 0 && rows >= columns + tail && rows > 1)
                {
                    columns++;
                    rows--;
                }
                else if (tail > 0 && columns >= columns - 1 + tail && columns > 1)
                {
                    //columns--;
                }

                if (count == 2)
                {
                    rows = 2;
                    columns = 1;
                }

                if (_mode == ParticipantsGridMode.Docked)
                {
                    finalWidth -= GroupCallPage.ColumnWidth;
                }
            }

            finalWidth = Math.Max(0, finalWidth);

            static Size SafeSize(double width, double height)
            {
                return new Size(Math.Max(0, width), Math.Max(0, height));
            }

            for (int row = 0; row < rows; row++)
            {
                var rowColumns = columns;
                if (row == rows - 1)
                {
                    rowColumns = count - index;
                }

                for (int column = 0; column < rowColumns; column++)
                {
                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsSelected)
                    {
                        if (_mode == ParticipantsGridMode.Compact)
                        {
                            finalHeight = finalWidth / 4 * 2;
                        }

                        Children[index].Measure(SafeSize(finalWidth, finalHeight));
                    }
                    else
                    {
                        Children[index].Measure(SafeSize(finalWidth / (_mode == ParticipantsGridMode.Compact ? rowColumns : columns), finalHeight / rows));
                    }

                    index++;
                }
            }

            availableSize.Height = finalHeight;
            return availableSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var index = 0;
            var count = Children.Count;

            var rows = Math.Ceiling(Math.Sqrt(count));
            var columns = Math.Ceiling(count / rows);

            var finalWidth = finalSize.Width;
            var finalHeight = finalSize.Height;

            if (_mode == ParticipantsGridMode.Compact)
            {
                rows = Math.Ceiling(count / 2d);
                columns = 2;

                finalHeight = finalWidth / 2 * rows;
            }
            else
            {
                var tail = columns - (rows * columns - count);
                if (tail > 0 && rows >= columns + tail && rows > 1)
                {
                    columns++;
                    rows--;
                }
                else if (tail > 0 && columns >= columns - 1 + tail && columns > 1)
                {
                    //columns--;
                }

                if (count == 2)
                {
                    rows = 2;
                    columns = 1;
                }

                if (_mode == ParticipantsGridMode.Docked)
                {
                    finalWidth -= GroupCallPage.ColumnWidth;
                }
            }

            var animate = _prevMode != _mode || _prevCount != Children.Count;
            var pinned = false;

            var batch = BootStrapper.Current.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);

            for (int row = 0; row < rows; row++)
            {
                var rowColumns = columns;
                if (row == rows - 1)
                {
                    rowColumns = count - index;
                }

                var rowWidth = rowColumns * (finalWidth / columns);
                var x = (finalWidth - rowWidth) / 2;

                if (_mode == ParticipantsGridMode.Compact)
                {
                    x = 0;
                }

                static Size SafeSize(double width, double height)
                {
                    return new Size(Math.Max(0, width), Math.Max(0, height));
                }

                for (int column = 0; column < rowColumns; column++)
                {
                    var cell = Children[index] as GroupCallParticipantGridCell;
                    if (cell == null)
                    {
                        continue;
                    }

                    var size = SafeSize(finalWidth / (_mode == ParticipantsGridMode.Compact ? rowColumns : columns), finalHeight / rows);
                    var point = new Point(x + column * size.Width, row * size.Height);

                    if (cell.IsSelected)
                    {
                        size = SafeSize(finalWidth, _mode == ParticipantsGridMode.Compact ? finalWidth / 4 * 2 : finalHeight);
                        point = new Point(0, 0);
                        pinned = true;
                    }

                    cell.Arrange(new Rect(point, size));

                    Rect prev;
                    if (index < _prev.Count)
                    {
                        prev = _prev[index];
                    }
                    else
                    {
                        prev = new Rect(point, new Size(0, 0));
                    }

                    if (PowerSavingPolicy.AreCallsAnimated && (animate || _prevPinned != pinned))
                    {
                        if (prev.X != point.X || prev.Y != point.Y)
                        {
                            ElementCompositionPreview.SetIsTranslationEnabled(cell, true);

                            var visual = ElementComposition.GetElementVisual(cell);
                            var offset = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                            offset.InsertKeyFrame(0, new Vector3((float)(prev.X - point.X), (float)(prev.Y - point.Y), 0));
                            offset.InsertKeyFrame(1, Vector3.Zero);
                            offset.Duration = TimeSpan.FromMilliseconds(300);
                            visual.StartAnimation("Translation", offset);
                        }

                        if (prev.Width != size.Width || prev.Height != size.Height)
                        {
                            var visual = ElementComposition.GetElementVisual(cell);
                            var scale = BootStrapper.Current.Compositor.CreateVector3KeyFrameAnimation();
                            scale.InsertKeyFrame(0, new Vector3((float)(prev.Width / size.Width), (float)(prev.Height / size.Height), 0));
                            scale.InsertKeyFrame(1, Vector3.One);
                            scale.Duration = TimeSpan.FromMilliseconds(300);
                            visual.StartAnimation("Scale", scale);

                            var factor = BootStrapper.Current.Compositor.CreateExpressionAnimation("Vector3(1 / content.Scale.X, 1 / content.Scale.Y, 1)");
                            factor.SetReferenceParameter("content", visual);
                            cell.StartAnimation(factor);
                        }

                        // Save previous position only when there's an animation-
                        // This is needed because the page state is updated on SizeChanged,
                        // and this causes layout measure/arrange to be invalidated.
                        if (index < _prev.Count)
                        {
                            _prev[index] = new Rect(point, size);
                        }
                        else
                        {
                            _prev.Add(new Rect(point, size));
                        }
                    }

                    index++;
                }
            }

            batch.End();

            for (int i = _prev.Count - 1; i >= Children.Count; i--)
            {
                _prev.RemoveAt(i);
            }

            _prevMode = _mode;
            _prevPinned = pinned;
            _prevCount = Children.Count;

            return finalSize;
        }
    }
}
