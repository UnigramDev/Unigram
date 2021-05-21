using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Controls.Cells;
using Unigram.Converters;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Graphics.Capture;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Unigram.Views
{
    public sealed partial class GroupCallPage : Page, IDisposable, IGroupCallDelegate
    {
        private readonly IProtoService _protoService;
        private readonly ICacheService _cacheService;
        private readonly IEventAggregator _aggregator;

        private IGroupCallService _service;

        private VoipGroupManager _manager;
        private bool _disposed;

        private readonly ConcurrentDictionary<MessageSender, VoipVideoRendererToken> _listTokens = new(new MessageSenderEqualityComparer());
        private readonly ConcurrentDictionary<string, VoipVideoRendererToken> _gridTokens = new();

        private readonly ButtonWavesDrawable _drawable = new ButtonWavesDrawable();

        private readonly DispatcherTimer _scheduledTimer;
        private readonly DispatcherTimer _debouncerTimer;

        public GroupCallPage(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, IGroupCallService voipService)
        {
            InitializeComponent();

            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

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

            _service = voipService;
            _service.PropertyChanged += OnParticipantsChanged;

            if (_service.Participants != null)
            {
                _service.Participants.Delegate = this;
                List.ItemsSource = _service.Participants;
            }

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
            Window.Current.Closed += OnClosed;

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
        }

        public void VideoInfoAdded(GroupCallParticipant participant, GroupCallParticipantVideoInfo[] videoInfos)
        {
            this.BeginOnUIThread(() => VideoInfoAddedInternal(participant, videoInfos));
        }

        public void VideoInfoRemoved(GroupCallParticipant participant, string[] endpointIds)
        {
            this.BeginOnUIThread(() => VideoInfoRemovedInternal(participant, endpointIds));
        }

        public void VideoInfoAddedInternal(GroupCallParticipant participant, GroupCallParticipantVideoInfo[] videoInfos)
        {
            foreach (var item in videoInfos)
            {
                if (item == null)
                {
                    continue;
                }

                if (_gridCells.TryGetValue(item.EndpointId, out var cell))
                {
                    cell.UpdateGroupCallParticipant(_cacheService, participant, item);
                }
                else
                {
                    AddCell(participant, item);
                }
            }

            _debouncerTimer.Stop();
            _debouncerTimer.Start();
        }

        public void VideoInfoRemovedInternal(GroupCallParticipant participant, string[] endpointIds)
        {
            foreach (var item in endpointIds)
            {
                if (item == null)
                {
                    continue;
                }

                if (_gridCells.TryGetValue(item, out var cell))
                {
                    RemoveCell(cell);
                }
            }

            _debouncerTimer.Stop();
            _debouncerTimer.Start();
        }

        enum ButtonState
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

        enum ButtonColors
        {
            None,
            Disabled,
            Unmute,
            Mute
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

        private void OnParticipantsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_disposed || _service?.Participants == null)
            {
                return;
            }

            _service.Participants.Delegate = this;
            this.BeginOnUIThread(() => List.ItemsSource = _service.Participants);
        }

        private void OnClosed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
        {
            Window.Current.Content = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Dispose();
            AudioCanvas.RemoveFromVisualTree();

            _prev.Clear();

            _listTokens.Values.ForEach(x => x.Stop());
            _gridTokens.Values.ForEach(x => x.Stop());

            _gridCells.Clear();

            for (int i = 0; i < Viewport.Children.Count; i++)
            {
                if (Viewport.Children[i] is GroupCallParticipantGridCell cell)
                {
                    cell.Surface = null;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_manager != null)
            {
                _manager.NetworkStateUpdated -= OnNetworkStateUpdated;
                _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
                _manager = null;
            }

            if (_service != null)
            {
                _service.PropertyChanged -= OnParticipantsChanged;
                _service = null;
            }
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
                _manager.AudioLevelsUpdated -= OnAudioLevelsUpdated;
            }

            controller.NetworkStateUpdated += OnNetworkStateUpdated;
            controller.AudioLevelsUpdated += OnAudioLevelsUpdated;

            _manager = controller;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            var prevSize = e.PreviousSize.ToVector2();
            var nextSize = e.NewSize.ToVector2();

            UpdateLayout(prevSize, nextSize, prevSize.X > 0 && prevSize.Y > 0);
        }

        private async void UpdateLayout(Vector2 prevSize, Vector2 nextSize, bool animated)
        {
            var call = _service?.Call;
            if (call == null)
            {
                return;
            }

            var expanded = nextSize.X >= 500;
            var docked = Mode.IsChecked == true;

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
                Mode.Visibility = Visibility.Visible;
                Resize.Glyph = Icons.ArrowMinimize;

                Grid.SetRowSpan(ParticipantsPanel, 2);

                Grid.SetColumn(List, 1);

                Viewport.Mode = mode;
                ViewportAspect.Padding = new Thickness(0, 0, 8, 0);
                ViewportAspect.Margin = new Thickness(8, 0, -4, 4);
                ParticipantsPanel.ColumnDefinitions[1].Width = new GridLength(224, GridUnitType.Pixel);
                ParticipantsPanel.Margin = new Thickness();
                List.Padding = new Thickness(8, 4, 12, 12);

                if (!ParticipantsPanel.Children.Contains(ViewportAspect))
                {
                    List.Header = null;
                    ParticipantsPanel.Children.Add(ViewportAspect);
                }

                if (mode == ParticipantsGridMode.Docked)
                {
                    List.Margin = new Thickness();
                    BottomPanel.Padding = new Thickness(8, 8, 224, 42);
                }
                else
                {
                    List.Margin = new Thickness(216, 0, -216, 0);
                    BottomPanel.Padding = new Thickness(8, 8, 8, 42);
                }

                BottomRoot.Padding = new Thickness(4, 8, 4, 8);
                //BottomRoot.CornerRadius = new CornerRadius(8);
                BottomRoot.Background = new AcrylicBrush { TintColor = Colors.Black, TintOpacity = 0, TintLuminosityOpacity = 0.5 }; //new SolidColorBrush(Color.FromArgb(0x99, 0x33, 0x33, 0x33));
                BottomRoot.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Auto);

                foreach (var column in BottomRoot.ColumnDefinitions)
                {
                    column.Width = new GridLength(48 + 12 + 12, GridUnitType.Pixel);
                }

                _drawable.SetSize(true);
                AudioCanvas.Margin = new Thickness(-126, -126, -126, -126);

                Audio.Width = Lottie.Width = 48;
                Audio.Height = Lottie.Height = 48;
                Audio.Margin = Lottie.Margin = new Thickness(12, 0, 12, 0);
                Audio.CornerRadius = new CornerRadius(24);

                AudioInfo.Margin = new Thickness(0, 4, 0, 0);

                Grid.SetColumn(Screen, 0);
                Grid.SetColumn(ScreenInfo, 0);

                Grid.SetColumn(Video, 1);
                Grid.SetColumn(VideoInfo, 1);

                Grid.SetColumn(AudioCanvas, 2);
                Grid.SetRow(AudioCanvas, 1);
                Grid.SetRowSpan(AudioCanvas, 1);

                Grid.SetColumn(Audio, 2);
                Grid.SetRow(Audio, 1);
                Grid.SetRowSpan(Audio, 1);

                Grid.SetColumn(Lottie, 2);
                Grid.SetRow(Lottie, 1);
                Grid.SetRowSpan(Lottie, 1);

                Grid.SetColumn(AudioInfo, 2);
                Grid.SetRow(AudioInfo, 2);

                Grid.SetColumn(Settings, 3);
                Grid.SetColumn(SettingsInfo, 3);

                UpdateVideo();
                UpdateScreen();
                Settings.Visibility = SettingsInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Menu.Visibility = call.CanStartVideo ? Visibility.Visible : Visibility.Collapsed;
                Mode.Visibility = Visibility.Collapsed;
                Resize.Glyph = Icons.ArrowMaximize;

                Grid.SetRowSpan(ParticipantsPanel, 1);

                Grid.SetColumn(List, 0);

                Viewport.Mode = mode;
                ViewportAspect.Padding = new Thickness(0, 0, 0, 0);
                ViewportAspect.Margin = new Thickness(-4, 0, -4, Viewport.Children.Count > 0 ? 4 : 0);
                ParticipantsPanel.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Auto);
                ParticipantsPanel.Margin = new Thickness(0, 0, 0, -24);
                List.Padding = new Thickness(12, 0, 12, 36);
                List.Margin = new Thickness();

                if (ParticipantsPanel.Children.Contains(ViewportAspect))
                {
                    ParticipantsPanel.Children.Remove(ViewportAspect);
                    List.Header = ViewportAspect;
                }

                BottomPanel.Padding = new Thickness(0, 0, 0, 0);
                BottomRoot.Padding = new Thickness(0, 0, 0, 0);
                BottomRoot.CornerRadius = new CornerRadius(0);
                BottomRoot.Background = null;
                BottomRoot.RowDefinitions[0].Height = new GridLength(24, GridUnitType.Pixel);

                foreach (var column in BottomRoot.ColumnDefinitions)
                {
                    column.Width = new GridLength(1, GridUnitType.Auto);
                }

                _drawable.SetSize(false);
                AudioCanvas.Margin = new Thickness(-102, -102, -102, -102);

                Audio.Width = Lottie.Width = 96;
                Audio.Height = Lottie.Height = 96;
                Audio.Margin = Lottie.Margin = new Thickness(48, 0, 48, 0);
                Audio.CornerRadius = new CornerRadius(48);

                AudioInfo.Margin = new Thickness(0, 8, 0, 24);

                Grid.SetColumn(Screen, 0);
                Grid.SetColumn(ScreenInfo, 0);

                Grid.SetColumn(Video, 0);
                Grid.SetColumn(VideoInfo, 0);

                Grid.SetColumn(AudioCanvas, 1);
                Grid.SetRow(AudioCanvas, 0);
                Grid.SetRowSpan(AudioCanvas, 3);

                Grid.SetColumn(Audio, 1);
                Grid.SetRow(Audio, 0);
                Grid.SetRowSpan(Audio, 3);

                Grid.SetColumn(Lottie, 1);
                Grid.SetRow(Lottie, 0);
                Grid.SetRowSpan(Lottie, 3);

                Grid.SetColumn(AudioInfo, 1);
                Grid.SetRow(AudioInfo, 3);

                Grid.SetColumn(Settings, 0);
                Grid.SetColumn(SettingsInfo, 0);

                UpdateVideo();
                UpdateScreen();
                Settings.Visibility = SettingsInfo.Visibility = call.CanStartVideo ? Visibility.Collapsed : Visibility.Visible;
            }

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

            await this.UpdateLayoutAsync();

            ShowHideBottomRoot(true);

            if (animated)
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

            UpdateVisibleParticipants(false);
        }

        private void TransformDocked()
        {
            var root = ElementCompositionPreview.GetElementVisual(BottomRoot);
            var list = ElementCompositionPreview.GetElementVisual(List);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(List, true);

            // Root offset
            var rootOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_mode == ParticipantsGridMode.Docked)
            {
                rootOffset.InsertKeyFrame(0, new Vector3(108, 0, 0));
            }
            else
            {
                rootOffset.InsertKeyFrame(0, new Vector3(-108, 0, 0));
            }

            rootOffset.InsertKeyFrame(1, Vector3.Zero);

            // List offset
            var listOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_mode == ParticipantsGridMode.Docked)
            {
                listOffset.InsertKeyFrame(0, new Vector3(224, 0, 0));
            }
            else
            {
                listOffset.InsertKeyFrame(0, new Vector3(-224, 0, 0));
            }

            listOffset.InsertKeyFrame(1, Vector3.Zero);

            listOffset.Duration =
                rootOffset.Duration = TimeSpan.FromMilliseconds(300);

            root.StartAnimation("Translation", rootOffset);
            list.StartAnimation("Translation", listOffset);
        }

        private ParticipantsGridMode _mode = ParticipantsGridMode.Compact;
        private string _pinnedEndpointId;

        private void Resize_Click(object sender, RoutedEventArgs e)
        {
            var view = ApplicationView.GetForCurrentView();
            var size = view.VisibleBounds;

            if (size.Width >= 500)
            {
                view.TryResizeView(new Size(380, size.Height + 1));
            }
            else
            {
                view.TryResizeView(new Size(780, size.Height + 1));
            }
        }

        private void Mode_Click(object sender, RoutedEventArgs e)
        {
            UpdateLayout(this.GetActualSize(), this.GetActualSize(), true);
        }

        private void TransformBottomRoot(Vector2 prevSize, Vector2 nextSize, ParticipantsGridMode prev, ParticipantsGridMode next)
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            var root = ElementCompositionPreview.GetElementVisual(BottomRoot);
            var list = ElementCompositionPreview.GetElementVisual(List);
            var audio1 = ElementCompositionPreview.GetElementVisual(AudioCanvas);
            var audio2 = ElementCompositionPreview.GetElementVisual(Lottie);
            var audioInfo = ElementCompositionPreview.GetElementVisual(AudioInfo);
            var video = ElementCompositionPreview.GetElementVisual(Video);
            var videoInfo = ElementCompositionPreview.GetElementVisual(VideoInfo);
            var screen = ElementCompositionPreview.GetElementVisual(Screen);
            var screenInfo = ElementCompositionPreview.GetElementVisual(ScreenInfo);
            var settings = ElementCompositionPreview.GetElementVisual(Settings);
            var settingsInfo = ElementCompositionPreview.GetElementVisual(SettingsInfo);
            var leave = ElementCompositionPreview.GetElementVisual(Leave);
            var leaveInfo = ElementCompositionPreview.GetElementVisual(LeaveInfo);

            var expanded = next == ParticipantsGridMode.Expanded || next == ParticipantsGridMode.Docked;

            audio1.CenterPoint = new Vector3(150, 150, 0);
            audio2.CenterPoint = new Vector3(expanded ? 24 : 48, expanded ? 24 : 48, 0);

            screen.CenterPoint = new Vector3(24, 24, 0);
            screenInfo.CenterPoint = new Vector3((float)ScreenInfo.ActualWidth / 2, (float)ScreenInfo.ActualHeight / 2, 0);

            settings.CenterPoint = new Vector3(24, 24, 0);
            settingsInfo.CenterPoint = new Vector3((float)SettingsInfo.ActualWidth / 2, (float)SettingsInfo.ActualHeight / 2, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(List, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioCanvas, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Lottie, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Video, true);
            ElementCompositionPreview.SetIsTranslationEnabled(VideoInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Screen, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ScreenInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Settings, true);
            ElementCompositionPreview.SetIsTranslationEnabled(SettingsInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Leave, true);
            ElementCompositionPreview.SetIsTranslationEnabled(LeaveInfo, true);

            // Root
            var rootOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            var prevCenter = prevSize.X / 2 - (float)BottomRoot.ActualWidth / 2;
            var nextCenter = nextSize.X / 2 - (float)BottomRoot.ActualWidth / 2;

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

            // List offset
            var listOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (next == ParticipantsGridMode.Docked)
            {
                listOffset.InsertKeyFrame(0, new Vector3(224, 0, 0));
            }
            else
            {
                //listOffset.InsertKeyFrame(0, new Vector3(-224, 0, 0));
            }

            listOffset.InsertKeyFrame(1, Vector3.Zero);

            // Audio scale
            var audioScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (expanded)
            {
                audioScale.InsertKeyFrame(0, new Vector3(2, 2, 1));
            }
            else
            {
                audioScale.InsertKeyFrame(0, new Vector3(0.5f, 0.5f, 0));
            }

            audioScale.InsertKeyFrame(1, Vector3.One);

            // Audio info offset
            var audioInfoOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (expanded)
            {
                audioInfoOffset.InsertKeyFrame(0, new Vector3(0, 26, 0));
            }
            else
            {
                audioInfoOffset.InsertKeyFrame(0, new Vector3(0, -26, 0));
            }

            audioInfoOffset.InsertKeyFrame(1, Vector3.Zero);

            // Other offset
            var otherOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

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
            var otherScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

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
                audioScale.Duration =
                otherScale.Duration =
                otherOffset.Duration =
                audioInfoOffset.Duration = TimeSpan.FromMilliseconds(300);
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
            audio1.StartAnimation("Scale", audioScale);
            audio2.StartAnimation("Scale", audioScale);
            audioInfo.StartAnimation("Translation", audioInfoOffset);
            screen.StartAnimation("Scale", otherScale);
            screenInfo.StartAnimation("Scale", otherScale);
            screen.StartAnimation("Translation", otherOffset);
            screenInfo.StartAnimation("Translation", otherOffset);
            leave.StartAnimation("Translation", otherOffset);
            leaveInfo.StartAnimation("Translation", otherOffset);

            if (call.CanStartVideo || next != ParticipantsGridMode.Compact)
            {
                video.StartAnimation("Translation", otherOffset);
                videoInfo.StartAnimation("Translation", otherOffset);
                settings.StartAnimation("Scale", otherScale);
                settingsInfo.StartAnimation("Scale", otherScale);
                settings.StartAnimation("Translation", otherOffset);
                settingsInfo.StartAnimation("Translation", otherOffset);
            }
            else
            {
                settings.Scale = Vector3.One;
                settingsInfo.Scale = Vector3.One;
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
                TitleInfo.Text = call.Title.Length > 0 ? call.Title : _cacheService.GetTitle(_service.Chat);

                RecordingInfo.Visibility = call.RecordDuration > 0 ? Visibility.Visible : Visibility.Collapsed;

                if (call.ScheduledStartDate != 0)
                {
                    var date = Converter.DateTime(call.ScheduledStartDate);
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
                    SubtitleInfo.Text = Strings.Resources.VoipGroupScheduledVoiceChat;
                    ParticipantsPanel.Visibility = Visibility.Collapsed;
                    ScheduledInfo.Text = duration < TimeSpan.Zero ? Strings.Resources.VoipChatLateBy : Strings.Resources.VoipChatStartsIn;
                    StartsAt.Text = call.GetStartsAt();
                    StartsIn.Text = call.GetStartsIn();

                    Menu.Visibility = Visibility.Collapsed;
                    TitlePanel.Margin = new Thickness(0, 0, 0, 0);
                    SubtitleInfo.Margin = new Thickness(12, -8, 0, 12);
                    Settings.Visibility = SettingsInfo.Visibility = Visibility.Visible;
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
                        Settings.Visibility = SettingsInfo.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Menu.Visibility = call.CanStartVideo ? Visibility.Visible : Visibility.Collapsed;
                        Settings.Visibility = SettingsInfo.Visibility = call.CanStartVideo ? Visibility.Collapsed : Visibility.Visible;
                    }

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

                    SubtitleInfo.Text = Locale.Declension("Participants", call.ParticipantCount);
                    ParticipantsPanel.Visibility = Visibility.Visible;

                    UpdateVideo();
                    UpdateScreen();
                }

                UpdateNetworkState(call, currentUser, _service.IsConnected);
            });
        }

        private void OnNetworkStateUpdated(VoipGroupManager sender, GroupNetworkStateChangedEventArgs args)
        {
            this.BeginOnUIThread(() => UpdateNetworkState(_service.Call, _service.CurrentUser, args.IsConnected));
        }

        private void OnAudioLevelsUpdated(VoipGroupManager sender, IReadOnlyDictionary<int, KeyValuePair<float, bool>> levels)
        {
            if (levels.TryGetValue(0, out var level))
            {
                _drawable.SetAmplitude(MathF.Max(0, MathF.Log(level.Key, short.MaxValue / 4000)));
            }
        }

        private async void Leave_Click(object sender, RoutedEventArgs e)
        {
            var chat = _service.Chat;
            var call = _service.Call;

            if (chat == null || call == null)
            {
                return;
            }

            if (call.CanBeManaged)
            {
                var popup = new MessagePopup();
                popup.RequestedTheme = ElementTheme.Dark;
                popup.Title = Strings.Resources.VoipGroupLeaveAlertTitle;
                popup.Message = Strings.Resources.VoipGroupLeaveAlertText;
                popup.PrimaryButtonText = Strings.Resources.VoipGroupLeave;
                popup.SecondaryButtonText = Strings.Resources.Cancel;
                popup.CheckBoxLabel = Strings.Resources.VoipGroupLeaveAlertEndChat;

                var confirm = await popup.ShowQueuedAsync();
                if (confirm == ContentDialogResult.Primary)
                {
                    if (popup.IsChecked == true)
                    {
                        _service.Discard();
                    }
                    else
                    {
                        _service.Leave();
                    }

                    await ApplicationView.GetForCurrentView().ConsolidateAsync();
                }
            }
            else
            {
                _service.Leave();
                await ApplicationView.GetForCurrentView().ConsolidateAsync();
            }
        }

        private async void Discard()
        {
            var chat = _service.Chat;
            var call = _service.Call;

            if (chat == null || call == null)
            {
                return;
            }

            var popup = new MessagePopup();
            popup.RequestedTheme = ElementTheme.Dark;
            popup.Title = Strings.Resources.VoipGroupEndAlertTitle;
            popup.Message = Strings.Resources.VoipGroupEndAlertText;
            popup.PrimaryButtonText = Strings.Resources.VoipGroupEnd;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _service.Discard();
                await ApplicationView.GetForCurrentView().ConsolidateAsync();
            }
        }

        private void Audio_Click(object sender, RoutedEventArgs e)
        {
            var call = _service.Call;
            var currentUser = _service.CurrentUser;

            if (call != null && call.ScheduledStartDate > 0)
            {
                if (call.CanBeManaged)
                {
                    _protoService.Send(new StartScheduledGroupCall(call.Id));
                }
                else
                {
                    _protoService.Send(new ToggleGroupCallEnabledStartNotification(call.Id, !call.EnabledStartNotification));
                }
            }
            //else if (currentUser != null && (currentUser.CanBeMutedForAllUsers || currentUser.CanBeUnmutedForAllUsers))
            //{
            //    if (_service.Manager != null)
            //    {
            //        _service.Manager.IsMuted = !currentUser.IsMutedForAllUsers;
            //        _protoService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, currentUser.ParticipantId, _service.Manager.IsMuted));
            //    }
            //}
            else if (currentUser != null && currentUser.CanUnmuteSelf && _service.Manager.IsMuted)
            {
                _service.Manager.IsMuted = false;
                _protoService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, currentUser.ParticipantId, false));
            }
            else if (currentUser != null && !_service.Manager.IsMuted)
            {
                _service.Manager.IsMuted = true;
                _protoService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, currentUser.ParticipantId, true));
            }
            else if (currentUser != null)
            {
                _protoService.Send(new ToggleGroupCallParticipantIsHandRaised(call.Id, _service.CurrentUser.ParticipantId, true));
            }
        }

        private void Video_Click(object sender, RoutedEventArgs e)
        {
            _service.ToggleCapturing();
            UpdateVideo();
        }

        private void Screen_Click(object sender, RoutedEventArgs e)
        {
            if (_service.IsScreenSharing)
            {
                _service.EndScreenSharing();
            }
            else
            {
                _service.StartScreenSharing();
            }

            UpdateScreen();
        }

        private void UpdateNetworkState(GroupCall call, GroupCallParticipant currentUser, bool? connected = null)
        {
            if (call != null && call.ScheduledStartDate > 0)
            {
                if (call.CanBeManaged)
                {
                    SetButtonState(ButtonState.Start);
                }
                else
                {
                    SetButtonState(call.EnabledStartNotification ? ButtonState.SetReminder : ButtonState.CancelReminder);
                }
            }
            //else if (currentUser != null && currentUser.CanBeUnmutedForAllUsers)
            else if (currentUser != null && currentUser.CanUnmuteSelf && _service.Manager.IsMuted)
            {
                SetButtonState(ButtonState.Mute);
            }
            //else if (currentUser != null && currentUser.CanBeMutedForAllUsers)
            else if (currentUser != null && !_service.Manager.IsMuted)
            {
                SetButtonState(ButtonState.Unmute);
            }
            else if (currentUser != null && currentUser.IsHandRaised)
            {
                SetButtonState(ButtonState.HandRaised);
            }
            else if (call != null && call.IsJoined)
            {
                SetButtonState(ButtonState.RaiseHand);
            }
        }

        private ButtonState _prevState;
        private ButtonColors _prevColors;

        private void SetButtonState(ButtonState state)
        {
            if (state == _prevState)
            {
                return;
            }

            ButtonColors colors = _prevColors;

            switch (state)
            {
                case ButtonState.CancelReminder:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = Strings.Resources.VoipGroupCancelReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceCancelReminder.tgs");
                    break;
                case ButtonState.SetReminder:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = Strings.Resources.VoipGroupSetReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceSetReminder.tgs");
                    break;
                case ButtonState.Start:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = Strings.Resources.VoipGroupStartNow;
                    Lottie.AutoPlay = false;
                    Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceStart.tgs");
                    break;
                case ButtonState.Unmute:
                    colors = ButtonColors.Unmute;
                    AudioInfo.Text = Strings.Resources.VoipTapToMute;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceUnmute.tgs");
                    break;
                case ButtonState.Mute:
                    colors = ButtonColors.Mute;
                    AudioInfo.Text = Strings.Resources.VoipGroupUnmute;
                    switch (_prevState)
                    {
                        case ButtonState.CancelReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceCancelReminderToMute.tgs");
                            break;
                        case ButtonState.RaiseHand:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceRaiseHandToMute.tgs");
                            break;
                        case ButtonState.SetReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceSetReminderToMute.tgs");
                            break;
                        case ButtonState.Start:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceStart.tgs");
                            Lottie.Play();
                            break;
                        default:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceMute.tgs");
                            break;
                    }
                    break;
                case ButtonState.RaiseHand:
                case ButtonState.HandRaised:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = state == ButtonState.HandRaised
                        ? Strings.Resources.VoipMutedTapedForSpeak
                        : Strings.Resources.VoipMutedByAdmin;
                    switch (_prevState)
                    {
                        case ButtonState.CancelReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceCancelReminderToRaiseHand.tgs");
                            break;
                        case ButtonState.Mute:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceMuteToRaiseHand.tgs");
                            break;
                        case ButtonState.Unmute:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceUnmuteToRaiseHand.tgs");
                            break;
                        case ButtonState.SetReminder:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceSetReminderToRaiseHand.tgs");
                            break;
                        default:
                            Lottie.AutoPlay = true;
                            Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceHand_1.tgs");
                            break;
                    }
                    break;
            }

            _prevState = state;

            if (colors == _prevColors)
            {
                return;
            }

            switch (colors)
            {
                case ButtonColors.Disabled:
                    _drawable.SetColors(0xff57A4FE, 0xffF05459, 0xff766EE9);
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                    break;
                case ButtonColors.Unmute:
                    _drawable.SetColors(0xFF0078ff, 0xFF33c659);
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x33, 0xc6, 0x59) };
                    break;
                case ButtonColors.Mute:
                    _drawable.SetColors(0xFF59c7f8, 0xFF0078ff);
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x00, 0x78, 0xff) };
                    break;
            }

            _prevColors = colors;

            UpdateVideo();
            UpdateScreen();
        }

        private void UpdateVideo()
        {
            if (_service.Call.CanStartVideo)
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                        break;
                    case ButtonColors.Unmute:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsCapturing ? 0xFF : 0x66), 0x33, 0xc6, 0x59) };
                        break;
                    case ButtonColors.Mute:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsCapturing ? 0xFF : 0x66), 0x00, 0x78, 0xff) };
                        break;
                }

                Video.Glyph = _service.IsCapturing ? Icons.VideoFilled : Icons.VideoOffFilled;
                Video.Visibility = VideoInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Video.Visibility = VideoInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateScreen()
        {
            if (_mode != ParticipantsGridMode.Compact && _service.Call.CanStartVideo && GraphicsCaptureSession.IsSupported())
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                        break;
                    case ButtonColors.Unmute:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsScreenSharing ? 0xBB : 0x66), 0x33, 0xc6, 0x59) };
                        break;
                    case ButtonColors.Mute:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsScreenSharing ? 0xBB : 0x66), 0x00, 0x78, 0xff) };
                        break;
                }

                Screen.Glyph = _service.IsScreenSharing ? Icons.ShareScreenStopFilled : Icons.ShareScreenFilled;
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

            var chat = _service.Chat;
            var call = _service.Call;

            if (chat == null || call == null)
            {
                return;
            }

            var aliases = await _service.CanChooseAliasAsync(chat.Id);
            if (aliases)
            {
                flyout.CreateFlyoutItem(async () => await _service.RejoinAsync(), Strings.Resources.VoipGroupDisplayAs, new FontIcon { Glyph = Icons.Person });
                flyout.CreateFlyoutSeparator();
            }

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(SetTitle, Strings.Resources.VoipGroupEditTitle, new FontIcon { Glyph = Icons.Edit });
            }

            if (call.CanChangeMuteNewParticipants)
            {
                var toggleFalse = new ToggleMenuFlyoutItem();
                toggleFalse.Text = Strings.Resources.VoipGroupAllCanSpeak;
                toggleFalse.IsChecked = !call.MuteNewParticipants;
                toggleFalse.Click += (s, args) =>
                {
                    _protoService.Send(new ToggleGroupCallMuteNewParticipants(call.Id, false));
                };

                var toggleTrue = new ToggleMenuFlyoutItem();
                toggleTrue.Text = Strings.Resources.VoipGroupOnlyAdminsCanSpeak;
                toggleTrue.IsChecked = call.MuteNewParticipants;
                toggleTrue.Click += (s, args) =>
                {
                    _protoService.Send(new ToggleGroupCallMuteNewParticipants(call.Id, true));
                };

                var settings = new MenuFlyoutSubItem();
                settings.Text = Strings.Resources.VoipGroupEditPermissions;
                settings.Icon = new FontIcon { Glyph = Icons.Key, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
                settings.Items.Add(toggleFalse);
                settings.Items.Add(toggleTrue);

                flyout.Items.Add(settings);
            }

            if (call.CanBeManaged && call.ScheduledStartDate == 0)
            {
                if (call.RecordDuration > 0)
                {
                    flyout.CreateFlyoutItem(StopRecording, Strings.Resources.VoipGroupStopRecordCall, new FontIcon { Glyph = Icons.Record });
                }
                else
                {
                    flyout.CreateFlyoutItem(StartRecording, Strings.Resources.VoipGroupRecordCall, new FontIcon { Glyph = Icons.Record });
                }
            }

            if (call.CanStartVideo && GraphicsCaptureSession.IsSupported())
            {
                if (_service.IsScreenSharing)
                {
                    flyout.CreateFlyoutItem(_service.EndScreenSharing, "Stop screen sharing", new FontIcon { Glyph = Icons.ShareScreenStop });
                }
                else
                {
                    flyout.CreateFlyoutItem(_service.StartScreenSharing, "Share screen", new FontIcon { Glyph = Icons.ShareScreenStart });
                }
            }

            if (call.ScheduledStartDate == 0)
            {
                flyout.CreateFlyoutSeparator();

                var inputId = _service.CurrentAudioInput;
                var outputId = _service.CurrentAudioOutput;

                var defaultInput = new ToggleMenuFlyoutItem();
                defaultInput.Text = Strings.Resources.Default;
                defaultInput.IsChecked = inputId == string.Empty;
                defaultInput.Click += (s, args) =>
                {
                    _service.CurrentAudioInput = string.Empty;
                };

                var input = new MenuFlyoutSubItem();
                input.Text = "Microphone";
                input.Icon = new FontIcon { Glyph = Icons.MicOn, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
                input.Items.Add(defaultInput);

                var inputDevices = await DeviceInformation.FindAllAsync(DeviceClass.AudioCapture);
                foreach (var device in inputDevices)
                {
                    var deviceItem = new ToggleMenuFlyoutItem();
                    deviceItem.Text = device.Name;
                    deviceItem.IsChecked = inputId == device.Id;
                    deviceItem.Click += (s, args) =>
                    {
                        _service.CurrentAudioInput = device.Id;
                    };

                    input.Items.Add(deviceItem);
                }

                var defaultOutput = new ToggleMenuFlyoutItem();
                defaultOutput.Text = Strings.Resources.Default;
                defaultOutput.IsChecked = outputId == string.Empty;
                defaultOutput.Click += (s, args) =>
                {
                    _service.CurrentAudioOutput = string.Empty;
                };

                var output = new MenuFlyoutSubItem();
                output.Text = "Speaker";
                output.Icon = new FontIcon { Glyph = Icons.Speaker, FontFamily = App.Current.Resources["TelegramThemeFontFamily"] as FontFamily };
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

                flyout.Items.Add(input);
                flyout.Items.Add(output);
            }

            //flyout.CreateFlyoutItem(ShareInviteLink, Strings.Resources.VoipGroupShareInviteLink, new FontIcon { Glyph = Icons.Link });

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutSeparator();

                var discard = flyout.CreateFlyoutItem(Discard, Strings.Resources.VoipGroupEndChat, new FontIcon { Glyph = Icons.Dismiss });
                discard.Foreground = new SolidColorBrush(Colors.IndianRed);
            }

            if (flyout.Items.Count > 0)
            {
                if (sender == Settings)
                {
                    flyout.ShowAt(sender as Button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedLeft });
                }
                else
                {
                    flyout.ShowAt(sender as Button, new FlyoutShowOptions { Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft });
                }
            }
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
            input.Title = Strings.Resources.VoipGroupTitle;
            input.PrimaryButtonText = Strings.Resources.Save;
            input.SecondaryButtonText = Strings.Resources.Cancel;
            input.PlaceholderText = chat.Title;
            input.Text = call.Title;
            input.MaxLength = 64;
            input.CanBeEmpty = true;

            var confirm = await input.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _protoService.Send(new SetGroupCallTitle(call.Id, input.Text));
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

            var input = new InputPopup();
            input.RequestedTheme = ElementTheme.Dark;
            input.Title = Strings.Resources.VoipGroupStartRecordingTitle;
            input.Header = Strings.Resources.VoipGroupStartRecordingText;
            input.PrimaryButtonText = Strings.Resources.Start;
            input.SecondaryButtonText = Strings.Resources.Cancel;
            input.PlaceholderText = Strings.Resources.VoipGroupSaveFileHint;
            input.Text = call.Title;
            input.MaxLength = 64;
            input.CanBeEmpty = true;

            var confirm = await input.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _protoService.Send(new StartGroupCallRecording(call.Id, input.Text));
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
            popup.Title = Strings.Resources.VoipGroupStopRecordingTitle;
            popup.Message = Strings.Resources.VoipGroupStopRecordingText;
            popup.PrimaryButtonText = Strings.Resources.Stop;
            popup.SecondaryButtonText = Strings.Resources.Cancel;

            var confirm = await popup.ShowQueuedAsync();
            if (confirm == ContentDialogResult.Primary)
            {
                _protoService.Send(new EndGroupCallRecording(call.Id));
            }
        }

        private async void ShareInviteLink()
        {
            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            await SharePopup.GetForCurrentView().ShowAsync(call);
        }

        private void AudioCanvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            _drawable.Draw(sender, args.DrawingSession);
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new TextListViewItem();
                args.ItemContainer.Style = sender.ItemContainerStyle;
                args.ItemContainer.ContentTemplate = sender.ItemTemplate;
                args.ItemContainer.ContextRequested += Member_ContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var participant = args.Item as GroupCallParticipant;

            if (args.InRecycleQueue)
            {
                if (content?.Children[1] is Border viewport)
                {
                    viewport.Child = null;
                }

                RemoveIncomingVideoOutput(participant);
                return;
            }

            UpdateGroupCallParticipant(content, participant);
            args.Handled = true;
        }

        public void UpdateGroupCallParticipant(GroupCallParticipant participant)
        {
            this.BeginOnUIThread(() =>
            {
                var container = List.ContainerFromItem(participant) as SelectorItem;
                var content = container?.ContentTemplateRoot as Grid;

                if (content == null)
                {
                    return;
                }

                UpdateGroupCallParticipant(content, participant);
            });
        }

        private void UpdateGroupCallParticipant(Grid content, GroupCallParticipant participant)
        {
            var photo = content.Children[0] as ProfilePicture;
            var viewport = content.Children[1] as Border;
            var title = content.Children[2] as TextBlock;
            var subtitle = content.Children[3] as Grid;
            var glyph = content.Children[4] as TextBlock;

            var status = subtitle.Children[0] as TextBlock;
            var speaking = subtitle.Children[1] as TextBlock;

            if (_cacheService.TryGetUser(participant.ParticipantId, out User user))
            {
                photo.Source = PlaceholderHelper.GetUser(_protoService, user, 36);
                title.Text = user.GetFullName();
            }
            else if (_cacheService.TryGetChat(participant.ParticipantId, out Chat chat))
            {
                photo.Source = PlaceholderHelper.GetChat(_protoService, chat, 36);
                title.Text = _protoService.GetTitle(chat);
            }

            void SetIncomingVideoOutput(GroupCallParticipantVideoInfo videoInfo, bool screencast, string glyph)
            {
                if (HasIncomingVideoOutput(participant.ParticipantId, videoInfo, viewport.Child as CanvasControl))
                {
                    return;
                }

                status.Text = glyph;
                status.Margin = new Thickness(0, 0, 4, 0);

                //viewport.Child = new CanvasControl();
                //AddIncomingVideoOutput(participant, videoInfo, viewport.Child as CanvasControl, screencast);
            }

            if (participant.ScreenSharingVideoInfo != null)
            {
                if (participant.VideoInfo != null)
                {
                    SetIncomingVideoOutput(participant.ScreenSharingVideoInfo, true, Icons.SmallScreencastFilled + Icons.SmallVideoFilled);
                }
                else
                {
                    SetIncomingVideoOutput(participant.ScreenSharingVideoInfo, true, Icons.SmallScreencastFilled);
                }
            }
            else if (participant.VideoInfo != null)
            {
                SetIncomingVideoOutput(participant.VideoInfo, false, Icons.SmallVideoFilled);
            }
            else
            {
                status.Text = string.Empty;
                status.Margin = new Thickness(0);

                viewport.Child = null;
                RemoveIncomingVideoOutput(participant);
            }

            if (participant.IsHandRaised)
            {
                speaking.Text = Strings.Resources.WantsToSpeak;
                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
                glyph.Text = Icons.EmojiHand;
                glyph.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
            }
            else if (participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser)
            {
                if (participant.IsCurrentUser)
                {
                    speaking.Text = Strings.Resources.ThisIsYou;
                }
                else
                {
                    speaking.Text = participant.Bio.Length > 0 ? participant.Bio : Strings.Resources.Listening;
                }

                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                glyph.Text = Icons.MicOff;
                glyph.Foreground = new SolidColorBrush { Color = !participant.CanUnmuteSelf ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
            }
            else
            {
                if (participant.IsSpeaking && participant.VolumeLevel != 10000)
                {
                    speaking.Text = string.Format(Strings.Resources.SpeakingWithVolume, (participant.VolumeLevel / 100d).ToString("N0"));
                }
                else if (participant.IsSpeaking)
                {
                    speaking.Text = Strings.Resources.Speaking;
                }
                else
                {
                    speaking.Text = Strings.Resources.Listening;
                }

                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = participant.IsSpeaking ? Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                glyph.Text = Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = participant.IsSpeaking ? Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
            }
        }

        private void UpdateRequestedVideos()
        {
            var descriptions = new Dictionary<string, VoipVideoChannelInfo>();

            if (_pinnedEndpointId != null && _gridTokens.TryGetValue(_pinnedEndpointId, out var token))
            {
                descriptions[token.EndpointId] = new VoipVideoChannelInfo(token.AudioSource, token.Description, VoipVideoChannelQuality.Full);
            }

            foreach (var item in _gridTokens)
            {
                if (descriptions.ContainsKey(item.Key))
                {
                    continue;
                }

                if (_gridTokens.Count > 1)
                {
                    descriptions[item.Value.EndpointId] = new VoipVideoChannelInfo(item.Value.AudioSource, item.Value.Description, VoipVideoChannelQuality.Medium);
                }
                else
                {
                    descriptions[item.Value.EndpointId] = new VoipVideoChannelInfo(item.Value.AudioSource, item.Value.Description, VoipVideoChannelQuality.Full);
                }
            }

            foreach (var item in _listTokens)
            {
                if (descriptions.ContainsKey(item.Value.EndpointId))
                {
                    continue;
                }

                descriptions[item.Value.EndpointId] = new VoipVideoChannelInfo(item.Value.AudioSource, item.Value.Description, VoipVideoChannelQuality.Thumbnail);
            }

            _manager.SetRequestedVideoChannels(descriptions.Values.ToArray());
        }

        private void AddCell(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            var child = new GroupCallParticipantGridCell(_cacheService, participant, videoInfo);
            child.TogglePinned += Canvas_TogglePinned;

            _gridCells[videoInfo.EndpointId] = child;
            Viewport.Children.Add(child);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.Margin = new Thickness(-4, 0, -4, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else
            {
                ViewportAspect.Margin = new Thickness(-4, 0, -4, 0);
            }
        }

        private void RemoveCell(GroupCallParticipantGridCell cell)
        {
            if (_gridTokens.TryRemove(cell.EndpointId, out var token))
            {
                token.Stop();
            }

            if (_pinnedEndpointId == cell.EndpointId)
            {
                _pinnedEndpointId = null;
            }

            _prev.Remove(cell.EndpointId);
            _gridCells.Remove(cell.EndpointId);

            cell.Surface = null;
            Viewport.Children.Remove(cell);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.Margin = new Thickness(-4, 0, -4, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else
            {
                ViewportAspect.Margin = new Thickness(-4, 0, -4, 0);
            }
        }

        private void Canvas_TogglePinned(object sender, EventArgs e)
        {
            if (sender is GroupCallParticipantGridCell cell)
            {
                foreach (var child in Viewport.Cells)
                {
                    if (child == cell)
                    {
                        Canvas.SetZIndex(child, 1);
                        continue;
                    }

                    Canvas.SetZIndex(child, 0);
                    child.IsPinned = false;
                }

                _pinnedEndpointId = cell.IsPinned ? cell.EndpointId : null;
                UpdateVisibleParticipants(false);

                Viewport.InvalidateMeasure();
            }

            if (_mode == ParticipantsGridMode.Compact)
            {
                var scrollingHost = List.GetScrollViewer();
                if (scrollingHost != null)
                {
                    scrollingHost.ChangeView(null, 0, null, false);
                }
            }
        }

        public bool HasIncomingVideoOutput(MessageSender sender, GroupCallParticipantVideoInfo videoInfo, CanvasControl canvas)
        {
            if (_listTokens.TryGetValue(sender, out var token))
            {
                return token.IsMatch(videoInfo.EndpointId, canvas);
            }

            return false;
        }

        private void RemoveIncomingVideoOutput(GroupCallParticipant participant)
        {
            AddIncomingVideoOutput(participant, null, null, false);
        }

        private void AddIncomingVideoOutput(GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo, CanvasControl canvas, bool screencast)
        {
            if (videoInfo == null || canvas == null)
            {
                _listTokens.TryRemove(participant.ParticipantId, out _);
            }
            else if (screencast && participant.IsCurrentUser && _service.IsScreenSharing)
            {
                _listTokens[participant.ParticipantId] = _service.ScreenSharing.AddIncomingVideoOutput(participant.AudioSourceId, videoInfo, canvas);
            }
            else
            {
                _listTokens[participant.ParticipantId] = _manager.AddIncomingVideoOutput(participant.AudioSourceId, videoInfo, canvas);
            }
        }

        private void Member_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = List.ItemFromContainer(sender) as GroupCallParticipant;

            var call = _service.Call;

            if (participant.IsCurrentUser)
            {
                if (participant.IsHandRaised)
                {
                    flyout.CreateFlyoutItem(() => _protoService.Send(new ToggleGroupCallParticipantIsHandRaised(_service.Call.Id, participant.ParticipantId, false)), Strings.Resources.VoipGroupCancelRaiseHand, new FontIcon { Glyph = Icons.EmojiHand });
                }

                if (participant.HasVideoInfo())
                {
                    //if (participant.ParticipantId.IsEqual(_pinnedParticipant?.ParticipantId))
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(null), "Unpin Video", new FontIcon { Glyph = Icons.PinOff });
                    //}
                    //else
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(participant), "Pin Video", new FontIcon { Glyph = Icons.Pin });
                    //}
                }
            }
            else
            {
                var slider = new Slider
                {
                    Value = participant.VolumeLevel / 100d,
                    Minimum = 0,
                    Maximum = 200,
                    MinWidth = 200,
                    TickFrequency = 100,
                    TickPlacement = TickPlacement.Outside
                };

                var debounder = new EventDebouncer<RangeBaseValueChangedEventArgs>(Constants.HoldingThrottle, handler => slider.ValueChanged += new RangeBaseValueChangedEventHandler(handler), handler => slider.ValueChanged -= new RangeBaseValueChangedEventHandler(handler));
                debounder.Invoked += (s, args) =>
                {
                    if (args.NewValue > 0)
                    {
                        _protoService.Send(new SetGroupCallParticipantVolumeLevel(call.Id, participant.ParticipantId, (int)(args.NewValue * 100)));
                    }
                    else
                    {
                        _protoService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, participant.ParticipantId, true));
                    }
                };

                flyout.Items.Add(new ContentMenuFlyoutItem { Content = slider });

                if (participant.HasVideoInfo())
                {
                    //if (participant.ParticipantId.IsEqual(_pinnedParticipant?.ParticipantId))
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(null), "Unpin Video", new FontIcon { Glyph = Icons.PinOff });
                    //}
                    //else
                    //{
                    //    flyout.CreateFlyoutItem(() => UpdateGridParticipant(participant), "Pin Video", new FontIcon { Glyph = Icons.Pin });
                    //}
                }

                if (participant.CanBeUnmutedForAllUsers && participant.IsMutedForAllUsers)
                {
                    flyout.CreateFlyoutItem(() => _protoService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, false)), Strings.Resources.VoipGroupAllowToSpeak, new FontIcon { Glyph = Icons.MicOn });
                }
                else if (participant.CanBeMutedForAllUsers && !participant.IsMutedForAllUsers)
                {
                    flyout.CreateFlyoutItem(() => _protoService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, true)), Strings.Resources.VoipGroupMute, new FontIcon { Glyph = Icons.MicOff });
                }
                else if (participant.CanBeUnmutedForCurrentUser && participant.IsMutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _protoService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, false)), Strings.Resources.VoipGroupUnmuteForMe, new FontIcon { Glyph = Icons.MicOn });
                }
                else if (participant.CanBeMutedForCurrentUser && !participant.IsMutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _protoService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, true)), Strings.Resources.VoipGroupMuteForMe, new FontIcon { Glyph = Icons.MicOff });
                }

                //if (_cacheService.TryGetUser(participant.ParticipantId, out User user))
                //{
                //    flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.Resources.VoipGroupOpenProfile, new FontIcon { Glyph = Icons.Person });
                //}
                //else if (_cacheService.TryGetChat(participant.ParticipantId, out Chat chat))
                //{
                //    if (chat.Type is ChatTypeSupergroup supergroup && supergroup.IsChannel)
                //    {
                //        flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.Resources.VoipGroupOpenChannel, new FontIcon { Glyph = Icons.Megaphone });
                //    }
                //    else
                //    {
                //        flyout.CreateFlyoutItem(() => _aggregator.Publish(new UpdateSwitchToSender(participant.ParticipantId)), Strings.Resources.VoipGroupOpenGroup, new FontIcon { Glyph = Icons.People });
                //    }
                //}
            }

            args.ShowAt(flyout, element);
        }

        private ScrollViewer _scrollingHost;
        private Dictionary<string, GroupCallParticipantGridCell> _prev = new();
        private Dictionary<string, GroupCallParticipantGridCell> _gridCells = new();

        private void List_Loaded(object sender, RoutedEventArgs e)
        {
            var scrollingHost = List.GetScrollViewer();
            if (scrollingHost != null)
            {
                _scrollingHost = scrollingHost;
                _scrollingHost.ViewChanged += OnParticipantsViewChanged;
            }

            var panel = List.ItemsPanelRoot;
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
            int first = 0;
            int last = -1;

            if (_pinnedEndpointId != null || Viewport.Mode != ParticipantsGridMode.Compact)
            {
                first = 0;
                last = Viewport.Children.Count - 1;
            }
            else if (Viewport.Mode == ParticipantsGridMode.Compact)
            {
                first = (int)Math.Truncate(_scrollingHost.VerticalOffset / (Viewport.ActualWidth / 2));
                last = (int)Math.Ceiling((_scrollingHost.VerticalOffset + _scrollingHost.ViewportHeight) / (Viewport.ActualWidth / 2));

                last *= 2;
                last = Math.Min(last - 1, Viewport.Children.Count - 1);
            }

            var next = new Dictionary<string, GroupCallParticipantGridCell>();

            if (last < Viewport.Children.Count && first <= last && first >= 0)
            {
                for (int i = first; i <= last; i++)
                {
                    var child = Viewport.Children[i] as GroupCallParticipantGridCell;
                    var participant = child.Participant;

                    if (_pinnedEndpointId != null && _pinnedEndpointId != child.EndpointId)
                    {
                        continue;
                    }

                    next[child.EndpointId] = child;

                    // Check if already playing
                    if (_gridTokens.TryGetValue(child.EndpointId, out var token))
                    {
                        if (token.IsMatch(child.EndpointId, child.Surface))
                        {
                            continue;
                        }
                    }

                    child.Surface = new CanvasControl();

                    VoipVideoRendererToken future = null;
                    if (participant.ScreenSharingVideoInfo?.EndpointId == child.EndpointId && participant.IsCurrentUser && _service.IsScreenSharing)
                    {
                        future = _service.ScreenSharing.AddIncomingVideoOutput(participant.AudioSourceId, participant.ScreenSharingVideoInfo, child.Surface);
                    }
                    else
                    {
                        future = _manager.AddIncomingVideoOutput(participant.AudioSourceId, child.VideoInfo, child.Surface);
                    }

                    if (future != null)
                    {
                        _gridTokens[child.EndpointId] = future;
                    }
                    else
                    {
                        next.Remove(child.EndpointId);
                    }
                }
            }

            foreach (var item in _prev.Keys.ToImmutableArray())
            {
                if (next.ContainsKey(item))
                {
                    continue;
                }

                if (_gridTokens.TryRemove(item, out var token))
                {
                    // Wait for token to be disposed to avoid a
                    // race condition in CanvasControl.
                    token.Stop();
                }

                _prev[item].Surface = null;
                _prev.Remove(item);
            }

            foreach (var item in next)
            {
                _prev[item.Key] = item.Value;
            }

            UpdateRequestedVideos();
        }

        private void Viewport_PointerEntered(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(PointerListener);
            if (point.Position.X > PointerListener.ActualWidth - 224 && Viewport.Mode == ParticipantsGridMode.Docked)
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

            _infoCollapsed = !show;

            foreach (var child in Viewport.Cells)
            {
                child.ShowHideInfo(show);
            }

            ShowHideBottomRoot(show ? true : Viewport.Mode == ParticipantsGridMode.Compact);
        }

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

            var root = ElementCompositionPreview.GetElementVisual(BottomPanel);

            root.StartAnimation("Opacity", anim);
        }

        private void OnViewportSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (List.Header != ViewportAspect || e.NewSize.Height == e.PreviousSize.Height)
            {
                return;
            }

            var scrollingHost = List.GetScrollViewer();
            if (scrollingHost == null || scrollingHost.VerticalOffset > Math.Max(e.NewSize.Height, e.PreviousSize.Height))
            {
                return;
            }

            var panel = List.ItemsPanelRoot;
            if (panel == null)
            {
                return;
            }

            var visual = ElementCompositionPreview.GetElementVisual(panel);
            ElementCompositionPreview.SetIsTranslationEnabled(panel, true);

            var prev = e.PreviousSize.ToVector2();
            var next = e.NewSize.ToVector2();

            var animation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(0, prev.Y - next.Y, 0));
            animation.InsertKeyFrame(1, Vector3.Zero);

            visual.StartAnimation("Translation", animation);
        }
    }

    public class ButtonWavesDrawable
    {
        private readonly BlobDrawable _buttonWaveDrawable = new BlobDrawable(3);
        private readonly BlobDrawable _tinyWaveDrawable = new BlobDrawable(6);
        private readonly BlobDrawable _bigWaveDrawable = new BlobDrawable(9);

        private float _amplitude;
        private float _animateAmplitudeDiff;
        private float _animateToAmplitude;

        private bool _small;

        private uint[] _stops;

        public ButtonWavesDrawable()
        {
            //this.buttonWaveDrawable.minRadius = 57.0f;
            //this.buttonWaveDrawable.maxRadius = 63.0f;
            //this.buttonWaveDrawable.generateBlob();
            //this.tinyWaveDrawable.minRadius = 62.0f;
            //this.tinyWaveDrawable.maxRadius = 72.0f;
            //this.tinyWaveDrawable.generateBlob();
            //this.bigWaveDrawable.minRadius = 65.0f;
            //this.bigWaveDrawable.maxRadius = 75.0f;
            //this.bigWaveDrawable.generateBlob();
            _buttonWaveDrawable.minRadius = 48.0f;
            _buttonWaveDrawable.maxRadius = 48.0f;
            _buttonWaveDrawable.GenerateBlob();
            _tinyWaveDrawable.minRadius = 50.0f;
            _tinyWaveDrawable.maxRadius = 50.0f;
            _tinyWaveDrawable.GenerateBlob();
            _bigWaveDrawable.minRadius = 52.0f;
            _bigWaveDrawable.maxRadius = 52.0f;
            _bigWaveDrawable.GenerateBlob();
            _tinyWaveDrawable.paint.A = 38;
            _bigWaveDrawable.paint.A = 76;
        }

        public void SetSize(bool small)
        {
            _small = small;
            _buttonWaveDrawable.minRadius = small ? 24 : 48.0f;
            _buttonWaveDrawable.maxRadius = small ? 24 : 48.0f;
            _buttonWaveDrawable.GenerateBlob();
            _tinyWaveDrawable.minRadius = small ? 25 : 50.0f;
            _tinyWaveDrawable.maxRadius = small ? 25 : 50.0f;
            _tinyWaveDrawable.GenerateBlob();
            _bigWaveDrawable.minRadius = small ? 26 : 52.0f;
            _bigWaveDrawable.maxRadius = small ? 26 : 52.0f;
            _bigWaveDrawable.GenerateBlob();
        }

        public void SetAmplitude(float amplitude)
        {
            _animateToAmplitude = amplitude;
            _animateAmplitudeDiff = (amplitude - _amplitude) / ((BlobDrawable.AMPLITUDE_SPEED * 500.0f) + 100.0f);
        }

        public void SetColors(params uint[] stops)
        {
            if (_stops != null && stops.SequenceEqual(_stops))
            {
                return;
            }

            _stops = stops;
            _layerGradient = null;
        }

        private int _lastUpdateTime;

        private CanvasRenderTarget _target;
        private CanvasRadialGradientBrush _layerGradient;
        private CanvasRadialGradientBrush _maskGradient;

        public void Draw(CanvasControl view, CanvasDrawingSession canvas)
        {
            int elapsedRealtime = Environment.TickCount;
            int access = elapsedRealtime - _lastUpdateTime;
            int unused = _lastUpdateTime = elapsedRealtime;
            if (access > 20)
            {
                access = 17;
            }
            long j = access;

            //this.tinyWaveDrawable.minRadius = 62.0f;
            //this.tinyWaveDrawable.maxRadius = 62.0f + (20.0f * BlobDrawable.FORM_SMALL_MAX);
            //this.bigWaveDrawable.minRadius = 65.0f;
            //this.bigWaveDrawable.maxRadius = 65.0f + (20.0f * BlobDrawable.FORM_BIG_MAX);
            //this.buttonWaveDrawable.minRadius = 57.0f;
            //this.buttonWaveDrawable.maxRadius = 57.0f + (12.0f * BlobDrawable.FORM_BUTTON_MAX);
            _tinyWaveDrawable.minRadius = 50.0f;
            _tinyWaveDrawable.maxRadius = 50.0f + (20.0f * BlobDrawable.FORM_SMALL_MAX);
            _bigWaveDrawable.minRadius = 52.0f;
            _bigWaveDrawable.maxRadius = 52.0f + (20.0f * BlobDrawable.FORM_BIG_MAX);
            _buttonWaveDrawable.minRadius = 48.0f;
            _buttonWaveDrawable.maxRadius = 48.0f + (12.0f * BlobDrawable.FORM_BUTTON_MAX);

            if (_small)
            {
                _tinyWaveDrawable.minRadius /= 2;
                _tinyWaveDrawable.maxRadius /= 2;
                _bigWaveDrawable.minRadius /= 2;
                _bigWaveDrawable.maxRadius /= 2;
                _buttonWaveDrawable.minRadius /= 2;
                _buttonWaveDrawable.maxRadius /= 2;
            }

            if (_animateToAmplitude != _amplitude)
            {
                _amplitude = _amplitude + (_animateAmplitudeDiff * j);

                if (_animateAmplitudeDiff > 0.0f)
                {
                    if (_amplitude > _animateToAmplitude)
                    {
                        _amplitude = _animateToAmplitude;
                    }
                }
                else if (_amplitude < _animateToAmplitude)
                {
                    _amplitude = _animateToAmplitude;
                }
                view.Invalidate();
            }

            _bigWaveDrawable.Update(_amplitude, 1.0f);
            _tinyWaveDrawable.Update(_amplitude, 1.0f);
            _buttonWaveDrawable.Update(_amplitude, 0.4f);

            if (_target == null)
            {
                _target = new CanvasRenderTarget(canvas, 300, 300);
            }

            using (var session = _target.CreateDrawingSession())
            {
                if (_maskGradient == null)
                {
                    _maskGradient = new CanvasRadialGradientBrush(session, Color.FromArgb(0x77, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
                    _maskGradient.Center = new Vector2(150, 150);
                }

                float bigScale = BlobDrawable.SCALE_BIG_MIN + (BlobDrawable.SCALE_BIG * _amplitude);
                float tinyScale = BlobDrawable.SCALE_SMALL_MIN + (BlobDrawable.SCALE_SMALL * _amplitude);
                float glowScale = bigScale * BlobDrawable.LIGHT_GRADIENT_SIZE + 0.7f;

                _maskGradient.RadiusX = 84 * glowScale;
                _maskGradient.RadiusY = 84 * glowScale;

                if (_small)
                {
                    _maskGradient.RadiusX /= 2;
                    _maskGradient.RadiusY /= 2;
                }

                session.Clear(Colors.Transparent);
                session.FillEllipse(new Vector2(150, 150), _maskGradient.RadiusX, _maskGradient.RadiusY, _maskGradient);
                session.Transform = Matrix3x2.CreateScale(bigScale, new Vector2(150, 150));
                _bigWaveDrawable.Draw(session, 150, 150);
                session.Transform = Matrix3x2.CreateScale(tinyScale, new Vector2(150, 150));
                _tinyWaveDrawable.Draw(session, 150, 150);
                session.Transform = Matrix3x2.Identity;
                _buttonWaveDrawable.Draw(session, 150, 150);
            }
            using (var layer = canvas.CreateLayer(new CanvasImageBrush(canvas, _target)))
            {
                if (_layerGradient == null && _stops != null)
                {
                    var stops = new CanvasGradientStop[_stops.Length];

                    for (int i = 0; i < _stops.Length; i++)
                    {
                        stops[i] = new CanvasGradientStop
                        {
                            Color = ColorEx.FromHex(_stops[i]),
                            Position = i / (_stops.Length - 1f)
                        };
                    }

                    _layerGradient = new CanvasRadialGradientBrush(canvas, stops);
                    _layerGradient.RadiusX = MathF.Sqrt(200 * 200 + 200 * 200);
                    _layerGradient.RadiusY = MathF.Sqrt(200 * 200 + 200 * 200);
                    _layerGradient.Center = new Vector2(300, 0);
                    //_layerGradient.RadiusX = 120;
                    //_layerGradient.RadiusY = 120;
                    //_layerGradient.Center = new Vector2(150 + 70, 150 - 70);
                }

                if (_layerGradient != null)
                {
                    canvas.FillRectangle(0, 0, 300, 300, _layerGradient);
                }
            }

            view.Invalidate();
        }
    }

    public class MessageSenderEqualityComparer : IEqualityComparer<MessageSender>
    {
        public bool Equals(MessageSender x, MessageSender y)
        {
            return x.IsEqual(y);
        }

        public int GetHashCode(MessageSender obj)
        {
            if (obj is MessageSenderUser user)
            {
                return user.UserId.GetHashCode();
            }
            else if (obj is MessageSenderChat chat)
            {
                return chat.ChatId.GetHashCode();
            }

            return obj.GetHashCode();
        }
    }

    public enum ParticipantsGridMode
    {
        Compact,
        Docked,
        Expanded
    }

    public class ParticipantsGrid : Windows.UI.Xaml.Controls.Panel
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
                if (count == 2)
                {
                    rows = 1;
                    columns = 2;
                }

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

                if (_mode == ParticipantsGridMode.Docked)
                {
                    finalWidth -= 224;
                }
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
                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsPinned)
                    {
                        if (_mode == ParticipantsGridMode.Compact)
                        {
                            finalHeight = finalWidth / 4 * 2;
                        }

                        Children[index].Measure(new Size(finalWidth, finalHeight));
                    }
                    else
                    {
                        Children[index].Measure(new Size(finalWidth / (_mode == ParticipantsGridMode.Compact ? rowColumns : columns), finalHeight / rows));
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
                if (count == 2)
                {
                    rows = 1;
                    columns = 2;
                }

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

                if (_mode == ParticipantsGridMode.Docked)
                {
                    finalWidth -= 224;
                }
            }

            var animate = _prevMode != _mode || _prevCount != Children.Count;
            var pinned = false;

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

                for (int column = 0; column < rowColumns; column++)
                {
                    var size = new Size(finalWidth / (_mode == ParticipantsGridMode.Compact ? rowColumns : columns), finalHeight / rows);
                    var point = new Point(x + column * size.Width, row * size.Height);

                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsPinned)
                    {
                        size = new Size(finalWidth, _mode == ParticipantsGridMode.Compact ? finalWidth / 4 * 2 : finalHeight);
                        point = new Point(0, 0);
                        pinned = true;
                    }

                    Children[index].Arrange(new Rect(point, size));

                    Rect prev;
                    if (index < _prev.Count)
                    {
                        prev = _prev[index];
                    }
                    else
                    {
                        prev = new Rect(point, new Size(0, 0));
                    }

                    if (animate || _prevPinned != pinned)
                    {
                        if (prev.X != point.X || prev.Y != point.Y)
                        {
                            ElementCompositionPreview.SetIsTranslationEnabled(Children[index], true);

                            var visual = ElementCompositionPreview.GetElementVisual(Children[index]);
                            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                            offset.InsertKeyFrame(0, new Vector3((float)(prev.X - point.X), (float)(prev.Y - point.Y), 0));
                            offset.InsertKeyFrame(1, Vector3.Zero);
                            offset.Duration = TimeSpan.FromMilliseconds(300);
                            visual.StartAnimation("Translation", offset);
                        }

                        if (prev.Width != size.Width || prev.Height != size.Height)
                        {
                            var visual = ElementCompositionPreview.GetElementVisual(Children[index]);
                            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                            scale.InsertKeyFrame(0, new Vector3((float)(prev.Width / size.Width), (float)(prev.Height / size.Height), 0));
                            scale.InsertKeyFrame(1, Vector3.One);
                            scale.Duration = TimeSpan.FromMilliseconds(300);
                            visual.StartAnimation("Scale", scale);
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
