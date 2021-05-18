using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
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
using Windows.UI.Composition;
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
                    AddCell(_cacheService, participant, item);
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
            
            _listTokens.Clear();
            _gridTokens.Clear();

            _gridCells.Clear();

            for (int i = 0; i < Viewport.Children.Count; i++)
            {
                if (Viewport.Children[i] is GroupCallParticipantGridCell cell)
                {
                    cell.RemoveFromVisualTree();
                    i--;
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

            UpdateLayout(prevSize, nextSize, true);

        }

        private async void UpdateLayout(Vector2 prevSize, Vector2 nextSize, bool animated)
        {
            var expanded = nextSize.X >= 500;
            if (expanded == _pinnedExpanded)
            {
                return;
            }

            _pinnedExpanded = expanded;

            var call = _service.Call;
            if (call == null)
            {
                return;
            }

            if (expanded)
            {
                Menu.Visibility = Visibility.Collapsed;
                TitlePanel.Margin = new Thickness(0, 0, 0, 0);
                SubtitleInfo.Visibility = Visibility.Collapsed;

                Grid.SetRowSpan(ParticipantsPanel, 2);

                Grid.SetColumn(ListPanel, 0);

                Grid.SetColumn(ViewportAspect, 0);
                Grid.SetColumnSpan(ViewportAspect, 2);

                Viewport.IsCompact = false;
                ViewportAspect.Padding = new Thickness(0, 0, 8, 0);
                ParticipantsPanel.ColumnDefinitions[0].Width = new GridLength(224, GridUnitType.Pixel);
                ParticipantsPanel.VerticalAlignment = VerticalAlignment.Stretch;
                ParticipantsPanel.Margin = new Thickness();
                ListPanel.Margin = new Thickness(12, 4, 8, 12);

                List.Header = null;
                ParticipantsPanel.Children.Add(ViewportAspect);

                BottomPanel.Padding = new Thickness(224, 8, 8, 8);
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

                Grid.SetColumn(AudioCanvas, 0);
                Grid.SetRow(AudioCanvas, 1);
                Grid.SetRowSpan(AudioCanvas, 1);

                Grid.SetColumn(Audio, 0);
                Grid.SetRow(Audio, 1);
                Grid.SetRowSpan(Audio, 1);

                Grid.SetColumn(Lottie, 0);
                Grid.SetRow(Lottie, 1);
                Grid.SetRowSpan(Lottie, 1);

                Grid.SetColumn(AudioInfo, 0);
                Grid.SetRow(AudioInfo, 2);

                Grid.SetColumn(Video, 1);
                Grid.SetColumn(VideoInfo, 1);

                Grid.SetColumn(Screen, 2);
                Grid.SetColumn(ScreenInfo, 2);

                Grid.SetColumn(Settings, 3);
                Grid.SetColumn(SettingsInfo, 3);

                UpdateVideo();
                UpdateScreen();
                Settings.Visibility = SettingsInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Menu.Visibility = call.CanStartVideo ? Visibility.Visible : Visibility.Collapsed;
                TitlePanel.Margin = new Thickness(call.CanStartVideo ? 32 : 0, 0, 0, 0);
                SubtitleInfo.Margin = new Thickness(call.CanStartVideo ? 44 : 12, -8, 0, 16);
                SubtitleInfo.Visibility = Visibility.Visible;

                Grid.SetRowSpan(ParticipantsPanel, 1);

                Grid.SetColumn(ListPanel, 1);

                Viewport.IsCompact = true;
                ViewportAspect.Padding = new Thickness(0, 0, 0, 0);
                ParticipantsPanel.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Auto);
                ParticipantsPanel.VerticalAlignment = VerticalAlignment.Top;
                ParticipantsPanel.Margin = new Thickness(0, 0, 0, 16);
                ListPanel.Margin = new Thickness(12, 0, 12, 0);

                ParticipantsPanel.Children.Remove(ViewportAspect);
                List.Header = ViewportAspect;

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

                Grid.SetColumn(Video, 0);
                Grid.SetColumn(VideoInfo, 0);

                Grid.SetColumn(Screen, 0);
                Grid.SetColumn(ScreenInfo, 0);

                Grid.SetColumn(Settings, 0);
                Grid.SetColumn(SettingsInfo, 0);

                UpdateVideo();
                UpdateScreen();
                Settings.Visibility = SettingsInfo.Visibility = call.CanStartVideo ? Visibility.Collapsed : Visibility.Visible;
            }

            await this.UpdateLayoutAsync();

            ShowHideBottomRoot(true);

            if (animated)
            {
                TransformBottomRoot();
            }

            UpdateVisibleParticipants(false);
        }

        private bool _pinnedExpanded = false;
        private string _pinnedEndpointId;

        [ComImport, Guid("45D64A29-A63E-4CB6-B498-5781D298CB4F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ICoreWindowInterop
        {
            IntPtr WindowHandle { get; }
            bool MessageHandled { set; }
        }

        [DllImport("user32.dll", ExactSpelling = true, CharSet = CharSet.Auto)]
        public static extern IntPtr GetParent(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;        // x position of upper-left corner
            public int Top;         // y position of upper-left corner
            public int Right;       // x position of lower-right corner
            public int Bottom;      // y position of lower-right corner
        }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        private void Viewport_Click(object sender, RoutedEventArgs e)
        {
            //object corewin = Windows.UI.Core.CoreWindow.GetForCurrentThread();
            //var interop = (ICoreWindowInterop)corewin;
            //var handle = GetParent(interop.WindowHandle);

            //if (GetWindowRect(handle, out RECT lpRect))
            //{
            //    var width = lpRect.Right - lpRect.Left;
            //    var height = lpRect.Bottom - lpRect.Top;

            //    var a = MoveWindow(handle, lpRect.Left -100, lpRect.Top, width, height, true);
            //}

            if (_pinnedExpanded)
            {
                ApplicationView.GetForCurrentView().TryResizeView(new Size(380, 580 + 33));
            }
            else
            {
                ApplicationView.GetForCurrentView().TryResizeView(new Size(780, 580 + 33));
            }
        }

        private void TransformBottomRoot()
        {
            var root = ElementCompositionPreview.GetElementVisual(BottomRoot);
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

            audio1.CenterPoint = new Vector3(150, 150, 0);
            audio2.CenterPoint = new Vector3(_pinnedExpanded ? 24 : 48, _pinnedExpanded ? 24 : 48, 0);

            screen.CenterPoint = new Vector3(24, 24, 0);
            screenInfo.CenterPoint = new Vector3(ScreenInfo.ActualSize.X / 2, ScreenInfo.ActualSize.Y / 2, 0);

            settings.CenterPoint = new Vector3(24, 24, 0);
            settingsInfo.CenterPoint = new Vector3(SettingsInfo.ActualSize.X / 2, SettingsInfo.ActualSize.Y / 2, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioCanvas, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Lottie, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Video, true);
            ElementCompositionPreview.SetIsTranslationEnabled(VideoInfo, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(Screen, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(ScreenInfo, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(Settings, true);
            //ElementCompositionPreview.SetIsTranslationEnabled(SettingsInfo, true);
            ElementCompositionPreview.SetIsTranslationEnabled(Leave, true);
            ElementCompositionPreview.SetIsTranslationEnabled(LeaveInfo, true);

            // Root
            var rootOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                rootOffset.InsertKeyFrame(0, new Vector3(-224 - 12, -34, 0));
            }
            else
            {
                rootOffset.InsertKeyFrame(0, new Vector3(224 + 12, 34, 0));
            }

            rootOffset.InsertKeyFrame(1, Vector3.Zero);

            // Audio scale
            var audioScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                audioScale.InsertKeyFrame(0, new Vector3(2, 2, 1));
            }
            else
            {
                audioScale.InsertKeyFrame(0, new Vector3(0.5f, 0.5f, 0));
            }

            audioScale.InsertKeyFrame(1, Vector3.One);

            // Audio offset
            var audioOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                audioOffset.InsertKeyFrame(0, new Vector3(72, 0, 0));
            }
            else
            {
                audioOffset.InsertKeyFrame(0, new Vector3(-72, 0, 0));
            }

            audioOffset.InsertKeyFrame(1, Vector3.Zero);

            // Audio info offset
            var audioInfoOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                audioInfoOffset.InsertKeyFrame(0, new Vector3(72, 26, 0));
            }
            else
            {
                audioInfoOffset.InsertKeyFrame(0, new Vector3(-72, -26, 0));
            }

            audioInfoOffset.InsertKeyFrame(1, Vector3.Zero);

            // Video offset
            var videoOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                videoOffset.InsertKeyFrame(0, new Vector3(-120, 0, 0));
            }
            else
            {
                videoOffset.InsertKeyFrame(0, new Vector3(120, 0, 0));
            }

            videoOffset.InsertKeyFrame(1, Vector3.Zero);

            // Other scales
            var otherScale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                otherScale.InsertKeyFrame(0, Vector3.Zero);
                otherScale.InsertKeyFrame(1, Vector3.One);
            }
            else
            {
                otherScale.InsertKeyFrame(0, Vector3.One);
                otherScale.InsertKeyFrame(1, Vector3.Zero);
            }

            // Leave offset
            var leaveOffset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();

            if (_pinnedExpanded)
            {
                leaveOffset.InsertKeyFrame(0, new Vector3(-96, 0, 0));
            }
            else
            {
                leaveOffset.InsertKeyFrame(0, new Vector3(96, 0, 0));
            }

            leaveOffset.InsertKeyFrame(1, Vector3.Zero);

            rootOffset.Duration =
                audioScale.Duration =
                leaveOffset.Duration =
                otherScale.Duration =
                videoOffset.Duration =
                audioInfoOffset.Duration =
                audioOffset.Duration = TimeSpan.FromSeconds(.4);
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
            audio1.StartAnimation("Scale", audioScale);
            audio2.StartAnimation("Scale", audioScale);
            audio1.StartAnimation("Translation", audioOffset);
            audio2.StartAnimation("Translation", audioOffset);
            audioInfo.StartAnimation("Translation", audioInfoOffset);
            video.StartAnimation("Translation", videoOffset);
            videoInfo.StartAnimation("Translation", videoOffset);
            screen.StartAnimation("Scale", otherScale);
            screenInfo.StartAnimation("Scale", otherScale);
            settings.StartAnimation("Scale", otherScale);
            settingsInfo.StartAnimation("Scale", otherScale);
            leave.StartAnimation("Translation", leaveOffset);
            leaveInfo.StartAnimation("Translation", leaveOffset);
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
                    SubtitleInfo.Margin = new Thickness(12, -8, 0, 16);
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

                    if (_pinnedExpanded)
                    {
                        Menu.Visibility = Visibility.Collapsed;
                        TitlePanel.Margin = new Thickness(0, 0, 0, 0);
                        SubtitleInfo.Visibility = Visibility.Collapsed;
                        Settings.Visibility = SettingsInfo.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Menu.Visibility = call.CanStartVideo ? Visibility.Visible : Visibility.Collapsed;
                        TitlePanel.Margin = new Thickness(call.CanStartVideo ? 32 : 0, 0, 0, 0);
                        SubtitleInfo.Margin = new Thickness(call.CanStartVideo ? 44 : 12, -8, 0, 16);
                        Settings.Visibility = SettingsInfo.Visibility = call.CanStartVideo ? Visibility.Collapsed : Visibility.Visible;
                    }

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

                Video.Glyph = _service.IsCapturing ? Icons.VideoOffFilled : Icons.VideoFilled;
                Video.Visibility = VideoInfo.Visibility = Visibility.Visible;
            }
            else
            {
                Video.Visibility = VideoInfo.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateScreen()
        {
            if (_pinnedExpanded && _service.Call.CanStartVideo && GraphicsCaptureSession.IsSupported())
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
            else if (participant.IsSpeaking)
            {
                if (participant.VolumeLevel != 10000)
                {
                    speaking.Text = string.Format("{0:N0}% {1}", participant.VolumeLevel / 100d, Strings.Resources.Speaking);
                }
                else
                {
                    speaking.Text = Strings.Resources.Speaking;
                }

                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                glyph.Text = Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
            }
            else if (participant.IsCurrentUser)
            {
                var muted = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser;

                speaking.Text = Strings.Resources.ThisIsYou;
                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
                glyph.Text = muted ? Icons.MicOff : Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = muted && !participant.CanUnmuteSelf ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
            }
            else
            {
                var muted = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser;

                speaking.Text = participant.Bio.Length > 0 ? participant.Bio : Strings.Resources.Listening;
                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                glyph.Text = muted ? Icons.MicOff : Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = muted && !participant.CanUnmuteSelf ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
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

                descriptions[item.Value.EndpointId] = new VoipVideoChannelInfo(item.Value.AudioSource, item.Value.Description, VoipVideoChannelQuality.Medium);
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

        private void AddCell(ICacheService cacheService, GroupCallParticipant participant, GroupCallParticipantVideoInfo videoInfo)
        {
            var child = new GroupCallParticipantGridCell(_cacheService, participant, videoInfo);
            child.TogglePinned += Canvas_TogglePinned;

            _gridCells[videoInfo.EndpointId] = child;
            Viewport.Children.Add(child);
        }

        private void RemoveCell(GroupCallParticipantGridCell cell)
        {
            if (_gridTokens.TryRemove(cell.EndpointId, out var token))
            {
                token.Stop();
            }

            _prev.Remove(cell.EndpointId);
            _gridCells.Remove(cell.EndpointId);

            cell.RemoveFromVisualTree();
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
        }

        private bool _pinnedCollapsed = true;

        private async void ShowHidePinnedParticipant(bool show)
        {
            if ((show && ViewportAspect.Visibility == Visibility.Visible) || (!show && (ViewportAspect.Visibility == Visibility.Collapsed || _pinnedCollapsed)))
            {
                return;
            }

            if (show)
            {
                _pinnedCollapsed = false;
            }
            else
            {
                _pinnedCollapsed = true;
            }

            ViewportAspect.Visibility = Visibility.Visible;

            await ViewportAspect.UpdateLayoutAsync();

            var aspect = ElementCompositionPreview.GetElementVisual(ViewportAspect);
            var visual = ElementCompositionPreview.GetElementVisual(Viewport);
            //var pinned = ElementCompositionPreview.GetElementVisual(PinnedInfo);
            //var glyph = ElementCompositionPreview.GetElementVisual(PinnedGlyph);

            var clip = aspect.Clip as CompositionGeometricClip;
            var rectangle = clip.Geometry as CompositionRoundedRectangleGeometry;

            var participants = ElementCompositionPreview.GetElementVisual(ListRoot);

            var batch = aspect.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                //aspect.Clip = null;
                participants.Offset = new Vector3();

                if (show)
                {
                    _pinnedCollapsed = false;
                }
                else
                {
                    ViewportAspect.Visibility = Visibility.Collapsed;
                }
            };

            var actualSize = ViewportAspect.ActualSize.Y + 8;
            var duration = TimeSpan.FromSeconds(0.5);

            var clipSize = aspect.Compositor.CreateVector2KeyFrameAnimation();
            clipSize.InsertKeyFrame(0, new Vector2(ViewportAspect.ActualSize.X - 24, show ? 0 : ViewportAspect.ActualSize.Y));
            clipSize.InsertKeyFrame(1, new Vector2(ViewportAspect.ActualSize.X - 24, show ? ViewportAspect.ActualSize.Y : 0));
            clipSize.Duration = duration;

            if (_pinnedExpanded)
            {
                var clipOffset = Window.Current.Compositor.CreateVector2KeyFrameAnimation();
                clipOffset.InsertKeyFrame(1, new Vector2(12, 0));
                clipOffset.Duration = duration;

                var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                offset.InsertKeyFrame(1, new Vector3(12, -(Viewport.ActualSize.Y - ViewportAspect.ActualSize.Y) / 2, 0));
                offset.Duration = duration;

                var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                scale.InsertKeyFrame(1, new Vector3((ViewportAspect.ActualSize.X - 24) / Viewport.ActualSize.X));
                scale.Duration = duration;

                var translateInfo = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                translateInfo.InsertKeyFrame(1, new Vector3(12, 0, 0));
                translateInfo.Duration = duration;

                var translateGlyph = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                translateGlyph.InsertKeyFrame(1, new Vector3(-24, 0, 0));
                translateGlyph.Duration = duration;

                rectangle.StartAnimation("Size", clipSize);
                rectangle.StartAnimation("Offset", clipOffset);
                visual.StartAnimation("Translation", offset);
                visual.StartAnimation("Scale", scale);
                //pinned.StartAnimation("Translation", translateInfo);
                //glyph.StartAnimation("Translation", translateGlyph);

                _pinnedExpanded = false;
            }

            var participantsOffset = aspect.Compositor.CreateVector3KeyFrameAnimation();
            participantsOffset.InsertKeyFrame(0, new Vector3(0, show ? -actualSize : 0, 0));
            participantsOffset.InsertKeyFrame(1, new Vector3(0, show ? 0 : -actualSize, 0));
            participantsOffset.Duration = TimeSpan.FromSeconds(0.5);

            rectangle.StartAnimation("Size", clipSize);
            participants.StartAnimation("Offset", participantsOffset);

            batch.End();
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
                    _protoService.Send(new SetGroupCallParticipantVolumeLevel(call.Id, participant.ParticipantId, (int)(args.NewValue * 100)));
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

            if (_pinnedEndpointId != null || !Viewport.IsCompact)
            {
                first = 0;
                last = Viewport.Children.Count - 1;
            }
            else if (Viewport.IsCompact)
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

                    if (participant.ScreenSharingVideoInfo?.EndpointId == child.EndpointId && participant.IsCurrentUser && _service.IsScreenSharing)
                    {
                        _gridTokens[child.EndpointId] = _service.ScreenSharing.AddIncomingVideoOutput(participant.AudioSourceId, participant.ScreenSharingVideoInfo, child.Surface);
                    }
                    else
                    {
                        _gridTokens[child.EndpointId] = _manager.AddIncomingVideoOutput(participant.AudioSourceId, child.VideoInfo, child.Surface);
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
            if (point.Position.X < 224 && !Viewport.IsCompact)
            {
                ShowHideInfo(false);
            }
            else if (point.Position.Y > PointerListener.ActualHeight - BottomPanel.ActualHeight && Viewport.IsCompact)
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

            ShowHideBottomRoot(show ? true : Viewport.IsCompact);
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

            var root = ElementCompositionPreview.GetElementVisual(BottomRoot);

            root.StartAnimation("Opacity", anim);
        }
    }

    public class ButtonWavesDrawable
    {
        //private BlobDrawable buttonWaveDrawable = new BlobDrawable(6);
        //private BlobDrawable tinyWaveDrawable = new BlobDrawable(9);
        //private BlobDrawable bigWaveDrawable = new BlobDrawable(12);

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

    public class ParticipantId
    {
        public MessageSender Sender { get; set; }
        public bool IsScreenSharing { get; set; }

        public ParticipantId(MessageSender sender, bool screen)
        {
            Sender = sender;
            IsScreenSharing = screen;
        }

        public override bool Equals(object obj)
        {
            if (obj is ParticipantId y)
            {
                return Sender.IsEqual(y.Sender) && IsScreenSharing == y.IsScreenSharing;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (Sender is MessageSenderUser user)
            {
                return user.UserId.GetHashCode() ^ IsScreenSharing.GetHashCode();
            }
            else if (Sender is MessageSenderChat chat)
            {
                return chat.ChatId.GetHashCode() ^ IsScreenSharing.GetHashCode();
            }

            return base.GetHashCode();
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

    public class ParticipantsGrid : Windows.UI.Xaml.Controls.Panel
    {
        private bool _compact = true;
        public bool IsCompact
        {
            get => _compact;
            set
            {
                if (_compact != value)
                {
                    _compact = value;
                    InvalidateMeasure();
                }
            }
        }

        public IEnumerable<GroupCallParticipantGridCell> Cells => Children.OfType<GroupCallParticipantGridCell>();

        private readonly List<Rect> _prev = new();
        private bool _prevCompact = true;
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

            if (_compact)
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

                finalWidth -= 224;
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
                        Children[index].Measure(new Size(finalWidth, finalHeight));
                    }
                    else
                    {
                        Children[index].Measure(new Size(finalWidth / (_compact ? rowColumns : columns), finalHeight / rows));
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

            if (_compact)
            {
                rows = Math.Ceiling(count / 2d);
                columns = 2;
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

                finalWidth -= 224;
            }

            var animate = _prevCompact != _compact || _prevCount != Children.Count;
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

                if (_compact)
                {
                    x = 0;
                }
                else
                {
                    x += 224;
                }

                for (int column = 0; column < rowColumns; column++)
                {
                    var size = new Size(finalWidth / (_compact ? rowColumns : columns), finalHeight / rows);
                    var point = new Point(x + column * size.Width, row * size.Height);

                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsPinned)
                    {
                        size = new Size(finalWidth, finalHeight);
                        point = new Point(_compact ? 0 : 224, 0);
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

                    if (_prevCompact != _compact)
                    {
                        if (_prevCompact)
                        {
                            prev.Y += 16;
                        }
                        else
                        {
                            prev.Y -= 16;
                        }
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
                            offset.Duration = TimeSpan.FromMilliseconds(400);
                            visual.StartAnimation("Translation", offset);
                        }

                        if (prev.Width != size.Width || prev.Height != size.Height)
                        {
                            var visual = ElementCompositionPreview.GetElementVisual(Children[index]);
                            var scale = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                            scale.InsertKeyFrame(0, new Vector3((float)(prev.Width / size.Width), (float)(prev.Height / size.Height), 0));
                            scale.InsertKeyFrame(1, Vector3.One);
                            scale.Duration = TimeSpan.FromMilliseconds(400);
                            visual.StartAnimation("Scale", scale);
                        }
                    }

                    if (index < _prev.Count)
                    {
                        _prev[index] = new Rect(point, size);
                    }
                    else
                    {
                        _prev.Add(new Rect(point, size));
                    }

                    index++;
                }
            }

            for (int i = _prev.Count - 1; i >= Children.Count; i--)
            {
                _prev.RemoveAt(i);
            }

            _prevCompact = _compact;
            _prevPinned = pinned;
            _prevCount = Children.Count;

            return finalSize;
        }
    }
}
