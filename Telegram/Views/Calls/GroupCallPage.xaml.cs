//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Composition;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Native.Calls;
using Telegram.Navigation;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Delegates;
using Telegram.Views.Host;
using Telegram.Views.Popups;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.System;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Media;
using Point = Windows.Foundation.Point;

namespace Telegram.Views.Calls
{
    public sealed partial class GroupCallPage : Page, IGroupCallDelegate
    {
        private readonly IClientService _clientService;
        private readonly IEventAggregator _aggregator;

        private IVoipGroupService _service;

        private VoipGroupManager _manager;
        private bool _disposed;

        private readonly ConcurrentDictionary<string, VoipVideoRendererToken> _listTokens = new();
        private readonly ConcurrentDictionary<string, VoipVideoRendererToken> _gridTokens = new();

        private readonly Dictionary<string, GroupCallParticipantGridCell> _prevGrid = new();
        private readonly Dictionary<string, GroupCallParticipantGridCell> _gridCells = new();
        private readonly Dictionary<string, GroupCallParticipantGridCell> _prevList = new();
        private readonly Dictionary<string, GroupCallParticipantGridCell> _listCells = new();

        private readonly CompositionVoiceBlobVisual _visual;

        private readonly DispatcherTimer _scheduledTimer;
        private readonly DispatcherTimer _debouncerTimer;

        private readonly DisplayRequest _displayRequest = new();

        private readonly DispatcherQueue _dispatcherQueue;

        private ParticipantsGridMode _mode = ParticipantsGridMode.Compact;
        private bool _docked = true;

        public GroupCallPage(IClientService clientService, IEventAggregator aggregator, IVoipGroupService voipService)
        {
            InitializeComponent();

            _clientService = clientService;
            _aggregator = aggregator;

            _visual = new CompositionVoiceBlobVisual(AudioBlob, 300, 300, 1.5f);

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

            _service = voipService;
            _service.MutedChanged += OnMutedChanged;
            _service.AudioLevelsUpdated += OnAudioLevelsUpdated;
            _service.PropertyChanged += OnParticipantsChanged;

            if (_service.Participants != null)
            {
                _service.Participants.Delegate = this;
                _service.Participants.LoadVideoInfo();
                ScrollingHost.ItemsSource = _service.Participants;
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
                    cell.UpdateGroupCallParticipant(_clientService, participant, item);
                }
                else
                {
                    AddGridItem(participant, item, item == participant.ScreenSharingVideoInfo);
                }

                if (_listCells.TryGetValue(item.EndpointId, out cell))
                {
                    cell.UpdateGroupCallParticipant(_clientService, participant, item);
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

        private void OnMutedChanged(object sender, EventArgs e)
        {
            this.BeginOnUIThread(() => UpdateNetworkState(_service?.Call, _service?.CurrentUser, _service?.IsConnected));
        }

        private void OnParticipantsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_disposed || _service?.Participants == null)
            {
                return;
            }

            _service.Participants.Delegate = this;
            _service.Participants.LoadVideoInfo();
            this.BeginOnUIThread(() => ScrollingHost.ItemsSource = _service.Participants);
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
                _manager = null;
            }

            if (_service != null)
            {
                _service.MutedChanged -= OnMutedChanged;
                _service.AudioLevelsUpdated -= OnAudioLevelsUpdated;
                _service.PropertyChanged -= OnParticipantsChanged;
                _service = null;
            }

            _prevGrid.Clear();
            _prevList.Clear();

            _listTokens.Values.ForEach(x => x.Stop());
            _gridTokens.Values.ForEach(x => x.Stop());

            _listTokens.Clear();
            _gridTokens.Clear();

            _listCells.Clear();
            _gridCells.Clear();

            RemoveChildren(false);
            RemoveChildren(true);

            _visual.StopAnimating();
        }

        private void RemoveChildren(bool list)
        {
            var collection = list ? ListViewport.Children : Viewport.Children;

            for (int i = 0; i < collection.Count; i++)
            {
                if (collection[i] is GroupCallParticipantGridCell cell)
                {
                    cell.Surface = null;
                }
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
            }

            controller.NetworkStateUpdated += OnNetworkStateUpdated;

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
            var service = _service;
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
                Resize.Glyph = Icons.ArrowMinimize;

                Grid.SetRowSpan(ParticipantsPanel, 2);

                Grid.SetColumn(ScrollingHost, 1);

                Viewport.Mode = mode;
                ParticipantsPanel.ColumnDefinitions[1].Width = new GridLength(224, GridUnitType.Pixel);
                ParticipantsPanel.Margin = new Thickness();
                ScrollingHost.Padding = new Thickness(8, 0, 12, 8);

                if (ListHeader.Children.Contains(ViewportAspect))
                {
                    ListHeader.Children.Remove(ViewportAspect);
                    ParticipantsPanel.Children.Add(ViewportAspect);
                }

                if (mode == ParticipantsGridMode.Docked)
                {
                    ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
                    ScrollingHost.Margin = new Thickness();
                    BottomPanel.Padding = new Thickness(8, 8, 224, 42);
                }
                else
                {
                    ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
                    ScrollingHost.Margin = new Thickness(216, 0, -216, 0);
                    BottomPanel.Padding = new Thickness(8, 8, 8, 42);
                }

                BottomShadow.Visibility = Visibility.Collapsed;
                BottomRoot.Padding = new Thickness(4, 8, 4, 8);
                BottomBackground.Background = new AcrylicBrush { TintColor = Colors.Black, TintOpacity = 0, FallbackColor = Color.FromArgb(0xDD, 0, 0, 0) /*TintLuminosityOpacity = 0.5*/ }; //new SolidColorBrush(Color.FromArgb(0x99, 0x33, 0x33, 0x33));
                BottomRoot.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Auto);

                foreach (var column in BottomRoot.ColumnDefinitions)
                {
                    column.Width = new GridLength(48 + 12 + 12, GridUnitType.Pixel);
                }

                _visual.Scale = new Vector3(0.5f);
                AudioBlob.Margin = new Thickness(-126, -126, -126, -126);

                Audio.Width = Lottie.Width = 48;
                Audio.Height = Lottie.Height = 48;
                Audio.Margin = Lottie.Margin = new Thickness(12, 0, 12, 0);
                Audio.CornerRadius = new CornerRadius(24);

                AudioInfo.Margin = new Thickness(0, 4, 0, 0);

                Grid.SetColumn(Screen, 0);
                Grid.SetColumn(ScreenInfo, 0);

                Grid.SetColumn(Video, 1);
                Grid.SetColumn(VideoInfo, 1);

                Grid.SetColumn(AudioBlob, 2);
                Grid.SetRow(AudioBlob, 1);
                Grid.SetRowSpan(AudioBlob, 1);

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
                Menu.Visibility = service.CanEnableVideo ? Visibility.Visible : Visibility.Collapsed;
                Resize.Glyph = Icons.ArrowMaximize;

                Grid.SetRowSpan(ParticipantsPanel, 1);

                Grid.SetColumn(ScrollingHost, 0);

                Viewport.Mode = mode;
                ViewportAspect.Margin = new Thickness(-2, -2, -2, Viewport.Children.Count > 0 ? 4 : 0);
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
                BottomPanel.Padding = new Thickness(0, 0, 0, 0);
                BottomRoot.Padding = new Thickness(0, 0, 0, 0);
                BottomBackground.Background = null;
                BottomRoot.RowDefinitions[0].Height = new GridLength(24, GridUnitType.Pixel);

                foreach (var column in BottomRoot.ColumnDefinitions)
                {
                    column.Width = new GridLength(1, GridUnitType.Auto);
                }

                _visual.Scale = new Vector3(1);
                AudioBlob.Margin = new Thickness(-102, -102, -102, -102);

                Audio.Width = Lottie.Width = 96;
                Audio.Height = Lottie.Height = 96;
                Audio.Margin = Lottie.Margin = new Thickness(48, 0, 48, 0);
                Audio.CornerRadius = new CornerRadius(48);

                AudioInfo.Margin = new Thickness(0, 8, 0, 24);

                Grid.SetColumn(Screen, 0);
                Grid.SetColumn(ScreenInfo, 0);

                Grid.SetColumn(Video, 0);
                Grid.SetColumn(VideoInfo, 0);

                Grid.SetColumn(AudioBlob, 1);
                Grid.SetRow(AudioBlob, 0);
                Grid.SetRowSpan(AudioBlob, 3);

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
                Settings.Visibility = SettingsInfo.Visibility = service.CanEnableVideo ? Visibility.Collapsed : Visibility.Visible;
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
                _prevList.Clear();

                _listTokens.Values.ForEach(x => x.Stop());
                _listTokens.Clear();

                _listCells.Clear();

                RemoveChildren(true);
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

            // ScrollingHost offset
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
            var service = _service;
            if (service == null)
            {
                return;
            }

            var root = ElementComposition.GetElementVisual(BottomRoot);
            var list = ElementComposition.GetElementVisual(ScrollingHost);
            var audio1 = ElementComposition.GetElementVisual(AudioBlob);
            var audio2 = ElementComposition.GetElementVisual(Lottie);
            var audioInfo = ElementComposition.GetElementVisual(AudioInfo);
            var video = ElementComposition.GetElementVisual(Video);
            var videoInfo = ElementComposition.GetElementVisual(VideoInfo);
            var screen = ElementComposition.GetElementVisual(Screen);
            var screenInfo = ElementComposition.GetElementVisual(ScreenInfo);
            var settings = ElementComposition.GetElementVisual(Settings);
            var settingsInfo = ElementComposition.GetElementVisual(SettingsInfo);
            var leave = ElementComposition.GetElementVisual(Leave);
            var leaveInfo = ElementComposition.GetElementVisual(LeaveInfo);

            var expanded = next is ParticipantsGridMode.Expanded or ParticipantsGridMode.Docked;

            audio1.CenterPoint = new Vector3(150, 150, 0);
            audio2.CenterPoint = new Vector3(expanded ? 24 : 48, expanded ? 24 : 48, 0);

            screen.CenterPoint = new Vector3(24, 24, 0);
            screenInfo.CenterPoint = new Vector3(ScreenInfo.ActualSize / 2, 0);

            settings.CenterPoint = new Vector3(24, 24, 0);
            settingsInfo.CenterPoint = new Vector3(SettingsInfo.ActualSize / 2, 0);

            ElementCompositionPreview.SetIsTranslationEnabled(BottomRoot, true);
            ElementCompositionPreview.SetIsTranslationEnabled(ScrollingHost, true);
            ElementCompositionPreview.SetIsTranslationEnabled(AudioBlob, true);
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

            if (service.CanEnableVideo || next != ParticipantsGridMode.Compact)
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
                        Menu.Visibility = _service.CanEnableVideo ? Visibility.Visible : Visibility.Collapsed;
                        Settings.Visibility = SettingsInfo.Visibility = _service.CanEnableVideo ? Visibility.Collapsed : Visibility.Visible;
                    }

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

                    UpdateVideo();
                    UpdateScreen();
                }

                UpdateNetworkState(call, currentUser, _service.IsConnected);
                UpdateLayout(ActualSize, ActualSize, true);
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

        private void OnAudioLevelsUpdated(object sender, IList<VoipGroupParticipant> levels)
        {
            var participants = _service?.Participants;
            if (participants == null)
            {
                return;
            }

            var validLevels = new Dictionary<GroupCallParticipant, float>();

            foreach (var level in levels)
            {
                var value = level.Level;
                if (level.AudioSource == 0 && _service.IsMuted)
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

            var outer = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            outer.InsertKeyFrame(1, new Vector3(0.9f + 0.5f * amplitude));

            var inner = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            inner.InsertKeyFrame(1, new Vector3(1f + 0.15f * amplitude));

            wave.CenterPoint = new Vector3(18, 18, 0);
            wave.StartAnimation("Scale", outer);

            photo.CenterPoint = new Vector3(18, 18, 0);
            photo.StartAnimation("Scale", inner);
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

        private async void Audio_Click(object sender, RoutedEventArgs e)
        {
            if (_service == null)
            {
                await ConsolidateAsync();
                return;
            }

            var call = _service.Call;
            var currentUser = _service.CurrentUser;

            if (call != null && call.ScheduledStartDate > 0)
            {
                if (call.CanBeManaged)
                {
                    _clientService.Send(new StartScheduledGroupCall(call.Id));
                }
                else
                {
                    _clientService.Send(new ToggleGroupCallEnabledStartNotification(call.Id, !call.EnabledStartNotification));
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
            else if (currentUser != null && currentUser.CanUnmuteSelf && _service.IsMuted)
            {
                var permissions = await MediaDeviceWatcher.CheckAccessAsync(false, false, ElementTheme.Dark);
                if (permissions == false || _service == null)
                {
                    return;
                }

                _service.IsMuted = false;
            }
            else if (currentUser != null && !_service.IsMuted)
            {
                _service.IsMuted = true;
            }
            else if (currentUser != null)
            {
                _clientService.Send(new ToggleGroupCallParticipantIsHandRaised(call.Id, _service.CurrentUser.ParticipantId, true));
            }
        }

        private async void Video_Click(object sender, RoutedEventArgs e)
        {
            if (_service?.IsVideoEnabled == false)
            {
                var permissions = await MediaDeviceWatcher.CheckAccessAsync(true, false, ElementTheme.Dark);
                if (permissions == false || _service == null)
                {
                    return;
                }
            }

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
            else if (currentUser != null && (_service.IsMuted || (currentUser.IsMutedForAllUsers && currentUser.CanUnmuteSelf)))
            {
                SetButtonState(ButtonState.Mute);
            }
            //else if (currentUser != null && currentUser.CanBeMutedForAllUsers)
            else if (currentUser != null && !_service.IsMuted)
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
                    AudioInfo.Text = Strings.VoipGroupCancelReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceCancelReminder.tgs");
                    break;
                case ButtonState.SetReminder:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = Strings.VoipGroupSetReminder;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceSetReminder.tgs");
                    break;
                case ButtonState.Start:
                    colors = ButtonColors.Disabled;
                    AudioInfo.Text = Strings.VoipGroupStartNow;
                    Lottie.AutoPlay = false;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceStart.tgs");
                    break;
                case ButtonState.Unmute:
                    colors = ButtonColors.Unmute;
                    AudioInfo.Text = Strings.VoipTapToMute;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new LocalFileSource("ms-appx:///Assets/Animations/VoiceUnmute.tgs");
                    break;
                case ButtonState.Mute:
                    colors = ButtonColors.Mute;
                    AudioInfo.Text = Strings.VoipGroupUnmute;
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
                    AudioInfo.Text = state == ButtonState.HandRaised
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

            if (colors == _prevColors)
            {
                return;
            }

            switch (colors)
            {
                case ButtonColors.Disabled:
                    _visual.SetColorStops(0xff57A4FE, 0xffF05459, 0xff766EE9);
                    StartAnimating();
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                    break;
                case ButtonColors.Unmute:
                    _visual.SetColorStops(0xFF0078ff, 0xFF33c659);
                    StartAnimating();
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x33, 0xc6, 0x59) };
                    break;
                case ButtonColors.Mute:
                    _visual.SetColorStops(0xFF59c7f8, 0xFF0078ff);
                    StartAnimating();
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x00, 0x78, 0xff) };
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
            var service = _service;
            if (service == null)
            {
                return;
            }

            if (service.CanEnableVideo && service.Call?.ScheduledStartDate == 0)
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                        break;
                    case ButtonColors.Unmute:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsVideoEnabled ? 0xFF : 0x66), 0x33, 0xc6, 0x59) };
                        break;
                    case ButtonColors.Mute:
                        Video.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(_service.IsVideoEnabled ? 0xFF : 0x66), 0x00, 0x78, 0xff) };
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
            var service = _service;
            if (service == null)
            {
                return;
            }

            if (_mode != ParticipantsGridMode.Compact && service.CanEnableVideo && VoipScreenCapture.IsSupported())
            {
                switch (_prevColors)
                {
                    case ButtonColors.Disabled:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x76, 0x6E, 0xE9) };
                        break;
                    case ButtonColors.Unmute:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(service.IsScreenSharing ? 0xFF : 0x66), 0x33, 0xc6, 0x59) };
                        break;
                    case ButtonColors.Mute:
                        Screen.Background = new SolidColorBrush { Color = Color.FromArgb((byte)(service.IsScreenSharing ? 0xFF : 0x66), 0x00, 0x78, 0xff) };
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

            var chat = _service?.Chat;
            var call = _service?.Call;

            if (chat == null || call == null)
            {
                return;
            }

            var aliases = await _service.CanChooseAliasAsync(chat.Id);
            if (aliases)
            {
                flyout.CreateFlyoutItem(async () => await _service.RejoinAsync(), Strings.VoipGroupDisplayAs, Icons.Person);
                flyout.CreateFlyoutSeparator();
            }

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(SetTitle, _service.IsChannel ? Strings.VoipChannelEditTitle : Strings.VoipGroupEditTitle, Icons.Edit);
            }

            if (call.CanToggleMuteNewParticipants)
            {
                var toggleFalse = new ToggleMenuFlyoutItem();
                toggleFalse.Text = Strings.VoipGroupAllCanSpeak;
                toggleFalse.IsChecked = !call.MuteNewParticipants;
                toggleFalse.Click += (s, args) =>
                {
                    _clientService.Send(new ToggleGroupCallMuteNewParticipants(call.Id, false));
                };

                var toggleTrue = new ToggleMenuFlyoutItem();
                toggleTrue.Text = Strings.VoipGroupOnlyAdminsCanSpeak;
                toggleTrue.IsChecked = call.MuteNewParticipants;
                toggleTrue.Click += (s, args) =>
                {
                    _clientService.Send(new ToggleGroupCallMuteNewParticipants(call.Id, true));
                };

                var settings = new MenuFlyoutSubItem();
                settings.Text = Strings.VoipGroupEditPermissions;
                settings.Icon = MenuFlyoutHelper.CreateIcon(Icons.Key);
                settings.Items.Add(toggleFalse);
                settings.Items.Add(toggleTrue);

                flyout.Items.Add(settings);
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

            if (_service.CanEnableVideo && VoipScreenCapture.IsSupported())
            {
                if (_service.IsScreenSharing)
                {
                    flyout.CreateFlyoutItem(_service.EndScreenSharing, Strings.VoipChatStopScreenCapture, Icons.ShareScreenStop);
                }
                else
                {
                    flyout.CreateFlyoutItem(_service.StartScreenSharing, Strings.VoipChatStartScreenCapture, Icons.ShareScreenStart);
                }
            }

            if (call.ScheduledStartDate == 0)
            {
                flyout.CreateFlyoutSeparator();

                var videoId = _service.CurrentVideoInput;
                var inputId = _service.CurrentAudioInput;
                var outputId = _service.CurrentAudioOutput;

                var video = new MenuFlyoutSubItem();
                video.Text = Strings.VoipDeviceCamera;
                video.Icon = MenuFlyoutHelper.CreateIcon(Icons.Camera);

                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
                    var videoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
                    foreach (var device in videoDevices)
                    {
                        var deviceItem = new ToggleMenuFlyoutItem();
                        deviceItem.Text = device.Name;
                        deviceItem.IsChecked = videoId == device.Id;
                        deviceItem.Click += (s, args) =>
                        {
                            _service.CurrentVideoInput = device.Id;
                        };

                        video.Items.Add(deviceItem);
                    }
                });

                var defaultInput = new ToggleMenuFlyoutItem();
                defaultInput.Text = Strings.Default;
                defaultInput.IsChecked = inputId == string.Empty;
                defaultInput.Click += (s, args) =>
                {
                    _service.CurrentAudioInput = string.Empty;
                };

                var input = new MenuFlyoutSubItem();
                input.Text = Strings.VoipDeviceInput;
                input.Icon = MenuFlyoutHelper.CreateIcon(Icons.MicOn);
                input.Items.Add(defaultInput);

                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
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
                });

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

                _dispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                {
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
                });

                flyout.Items.Add(video);
                flyout.Items.Add(input);
                flyout.Items.Add(output);

                flyout.CreateFlyoutItem(() => _service.IsNoiseSuppressionEnabled = !_service.IsNoiseSuppressionEnabled, Strings.VoipNoiseCancellation, _service.IsNoiseSuppressionEnabled ? Icons.Checkmark : null);
            }

            //flyout.CreateFlyoutItem(ShareInviteLink, Strings.VoipGroupShareInviteLink, Icons.Link);

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutSeparator();

                var discard = flyout.CreateFlyoutItem(Discard, _service.IsChannel ? Strings.VoipChannelEndChat : Strings.VoipGroupEndChat, Icons.Dismiss);
                discard.Foreground = new SolidColorBrush(Colors.IndianRed);
            }

            if (flyout.Items.Count > 0)
            {
                if (sender == Settings)
                {
                    flyout.ShowAt(sender as Button, FlyoutPlacementMode.TopEdgeAlignedLeft);
                }
                else
                {
                    flyout.ShowAt(sender as Button, FlyoutPlacementMode.BottomEdgeAlignedLeft);
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
                    cell.UpdateGroupCallParticipant(_clientService, participant, videoInfo);
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
                element.Scale = new Vector3(0.9f);
            }

            if (_clientService.TryGetUser(participant.ParticipantId, out Telegram.Td.Api.User user))
            {
                photo.SetUser(_clientService, user, 36);
                title.Text = user.FullName();
            }
            else if (_clientService.TryGetChat(participant.ParticipantId, out Chat chat))
            {
                photo.SetChat(_clientService, chat, 36);
                title.Text = _clientService.GetTitle(chat);
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
                speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
                glyph.Text = Icons.EmojiHand;
                glyph.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
            }
            else if (participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser)
            {
                var permanent = participant.IsMutedForCurrentUser || (participant.IsMutedForAllUsers && !participant.CanUnmuteSelf);

                if (participant.IsCurrentUser)
                {
                    speaking.Text = Strings.ThisIsYou;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x4D, 0xB8, 0xFF) };
                }
                else if (participant.IsMutedForCurrentUser)
                {
                    speaking.Text = Strings.VoipGroupMutedForMe;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Colors.Red };
                }
                else
                {
                    speaking.Text = participant.Bio.Length > 0 ? participant.Bio : Strings.Listening;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                }

                wave.Background = new SolidColorBrush { Color = permanent ? Color.FromArgb(0xDD, 0xFF, 0x00, 0x00) : Color.FromArgb(0xDD, 0x4D, 0xB8, 0xFF) };
                glyph.Text = Icons.MicOff;
                glyph.Foreground = new SolidColorBrush { Color = permanent ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
            }
            else
            {
                if (participant.IsSpeaking && participant.VolumeLevel != 10000)
                {
                    speaking.Text = string.Format(Strings.SpeakingWithVolume, (participant.VolumeLevel / 100d).ToString("N0"));
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                }
                else if (participant.IsSpeaking)
                {
                    speaking.Text = Strings.Speaking;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                }
                else if (participant.IsCurrentUser)
                {
                    speaking.Text = Strings.ThisIsYou;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x4D, 0xB8, 0xFF) };
                }
                else
                {
                    speaking.Text = Strings.Listening;
                    speaking.Foreground = status.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                }


                wave.Background = new SolidColorBrush { Color = participant.IsSpeaking ? Color.FromArgb(0xDD, 0x33, 0xc6, 0x59) : Color.FromArgb(0xDD, 0x4D, 0xB8, 0xFF) };
                glyph.Text = Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = participant.IsSpeaking ? Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
            }
        }

        private void UpdateRequestedVideos()
        {
            var descriptions = new Dictionary<string, VoipVideoChannelInfo>();

            foreach (var cell in _gridCells.Values)
            {
                descriptions[cell.EndpointId] =
                    new VoipVideoChannelInfo(cell.Participant.AudioSourceId, cell.EndpointId, cell.VideoInfo.SourceGroups, cell.Quality, cell.Quality);
            }

            foreach (var cell in _listCells.Values)
            {
                if (descriptions.ContainsKey(cell.EndpointId))
                {
                    continue;
                }

                descriptions[cell.EndpointId] =
                    new VoipVideoChannelInfo(cell.Participant.AudioSourceId, cell.EndpointId, cell.VideoInfo.SourceGroups, cell.Quality, cell.Quality);
            }

            _manager?.SetRequestedVideoChannels(descriptions.Values.ToArray());
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

            var child = new GroupCallParticipantGridCell(_clientService, participant, videoInfo, screenSharing);
            child.Click += Participant_Click;
            child.ToggleDocked += Participant_ToggleDocked;
            child.ContextRequested += Participant_ContextRequested;
            child.IsList = list;

            cells[videoInfo.EndpointId] = child;
            viewport.Add(child);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.Margin = new Thickness(-2, -2, -2, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else if (_mode == ParticipantsGridMode.Docked)
            {
                ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
            }
            else
            {
                ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
            }

            ListViewport.Margin = new Thickness(-2, -2, -2, ListViewport.Children.Count > 0 ? 4 : 0);

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
            var tokens = list ? _listTokens : _gridTokens;
            var prev = list ? _prevList : _prevGrid;
            var cells = list ? _listCells : _gridCells;
            var viewport = list ? ListViewport.Children : Viewport.Children;

            if (tokens.TryRemove(cell.EndpointId, out var token))
            {
                token.Stop();
            }

            if (_selectedEndpointId == cell.EndpointId && !list)
            {
                _selectedEndpointId = null;
            }

            prev.Remove(cell.EndpointId);
            cells.Remove(cell.EndpointId);

            cell.Surface = null;
            viewport.Remove(cell);

            if (_mode == ParticipantsGridMode.Compact)
            {
                ViewportAspect.Margin = new Thickness(-2, -2, -2, Viewport.Children.Count > 0 ? 4 : 0);
            }
            else if (_mode == ParticipantsGridMode.Docked)
            {
                ViewportAspect.Margin = new Thickness(10, -2, 0, 8);
            }
            else
            {
                ViewportAspect.Margin = new Thickness(10, -2, 10, 8);
            }

            ListViewport.Margin = new Thickness(-2, -2, -2, ListViewport.Children.Count > 0 ? 4 : 0);

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
                scrollingHost?.ChangeView(null, 0, null, false);
            }
        }

        private void Participant_ContextRequested(UIElement sender, Windows.UI.Xaml.Input.ContextRequestedEventArgs args)
        {
            var flyout = new MenuFlyout();

            var element = sender as FrameworkElement;
            var participant = (element.Tag ?? ScrollingHost.ItemFromContainer(sender)) as GroupCallParticipant;

            var call = _service.Call;

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
                    _clientService.Send(new SetGroupCallParticipantVolumeLevel(call.Id, participant.ParticipantId, (int)(args.NewValue * 100)));
                }
                else
                {
                    _clientService.Send(new ToggleGroupCallParticipantIsMuted(call.Id, participant.ParticipantId, true));
                }
            };

            flyout.Items.Add(slider);

            if (participant.IsCurrentUser)
            {
                if (participant.IsHandRaised)
                {
                    flyout.CreateFlyoutItem(() => _clientService.Send(new ToggleGroupCallParticipantIsHandRaised(_service.Call.Id, participant.ParticipantId, false)), Strings.VoipGroupCancelRaiseHand, Icons.EmojiHand);
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
                    flyout.CreateFlyoutItem(() => _clientService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, false)), Strings.VoipGroupAllowToSpeak, Icons.MicOn);
                }
                else if (participant.CanBeUnmutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _clientService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, false)), Strings.VoipGroupUnmuteForMe, Icons.MicOn);
                }
                else if (participant.CanBeMutedForAllUsers)
                {
                    flyout.CreateFlyoutItem(() => _clientService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, true)), Strings.VoipGroupMute, Icons.MicOff);
                }
                else if (participant.CanBeMutedForCurrentUser)
                {
                    flyout.CreateFlyoutItem(() => _clientService.Send(new ToggleGroupCallParticipantIsMuted(_service.Call.Id, participant.ParticipantId, true)), Strings.VoipGroupMuteForMe, Icons.MicOff);
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
            }

            flyout.ShowAt(element, args);
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
                gridFirst = 0;
                gridLast = Viewport.Children.Count - 1;

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

            UpdateVisibleParticipants(gridFirst, gridLast, false);
            UpdateVisibleParticipants(listFirst, listLast, true);

            UpdateRequestedVideos();
        }

        private void UpdateVisibleParticipants(int first, int last, bool list)
        {
            var tokens = list ? _listTokens : _gridTokens;
            var prev = list ? _prevList : _prevGrid;
            var viewport = list ? ListViewport.Children : Viewport.Children;

            var next = new Dictionary<string, GroupCallParticipantGridCell>();

            if (last < viewport.Count && first <= last && first >= 0)
            {
                for (int i = first; i <= last; i++)
                {
                    var child = viewport[i] as GroupCallParticipantGridCell;
                    var participant = child.Participant;

                    if (_selectedEndpointId != null && _selectedEndpointId != child.EndpointId && !list)
                    {
                        continue;
                    }

                    next[child.EndpointId] = child;

                    // Check if already playing
                    if (tokens.TryGetValue(child.EndpointId, out var token))
                    {
                        if (token.IsMatch(child.EndpointId, child.Surface))
                        {
                            token.Stretch = child.GetStretch(_mode, list);
                            continue;
                        }
                    }

                    child.Surface = new CanvasControl();

                    VoipVideoRendererToken future;
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
                        future.Stretch = child.GetStretch(_mode, list);
                        future.IsMirrored = participant.IsCurrentUser && participant.ScreenSharingVideoInfo?.EndpointId != child.EndpointId;
                        tokens[child.EndpointId] = future;
                    }
                    else
                    {
                        next.Remove(child.EndpointId);
                    }
                }
            }

            foreach (var item in prev.Keys.ToArray())
            {
                if (next.ContainsKey(item))
                {
                    continue;
                }

                if (tokens.TryRemove(item, out var token))
                {
                    // Wait for token to be disposed to avoid
                    // a race condition in CanvasControl.
                    token.Stop();
                }

                prev[item].Surface = null;
                prev.Remove(item);
            }

            foreach (var item in next)
            {
                prev[item.Key] = item.Value;
            }
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

            var anim = Window.Current.Compositor.CreateScalarKeyFrameAnimation();
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

            var animation = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
            animation.InsertKeyFrame(0, new Vector3(0, prev.Y - next.Y, 0));
            animation.InsertKeyFrame(1, Vector3.Zero);

            visual.StartAnimation("Translation", animation);

            UpdateVisibleParticipants(false);
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
                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsSelected)
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

                    if (Children[index] is GroupCallParticipantGridCell cell && cell.IsSelected)
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

                    if (PowerSavingPolicy.AreCallsAnimated && (animate || _prevPinned != pinned))
                    {
                        if (prev.X != point.X || prev.Y != point.Y)
                        {
                            ElementCompositionPreview.SetIsTranslationEnabled(Children[index], true);

                            var visual = ElementComposition.GetElementVisual(Children[index]);
                            var offset = Window.Current.Compositor.CreateVector3KeyFrameAnimation();
                            offset.InsertKeyFrame(0, new Vector3((float)(prev.X - point.X), (float)(prev.Y - point.Y), 0));
                            offset.InsertKeyFrame(1, Vector3.Zero);
                            offset.Duration = TimeSpan.FromMilliseconds(300);
                            visual.StartAnimation("Translation", offset);
                        }

                        if (prev.Width != size.Width || prev.Height != size.Height)
                        {
                            var visual = ElementComposition.GetElementVisual(Children[index]);
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
