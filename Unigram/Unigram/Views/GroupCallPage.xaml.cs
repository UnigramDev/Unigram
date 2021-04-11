using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Telegram.Td.Api;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Unigram.Native.Calls;
using Unigram.Services;
using Unigram.ViewModels.Delegates;
using Unigram.Views.Popups;
using Windows.Devices.Enumeration;
using Windows.Foundation.Metadata;
using Windows.UI;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;

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

        private readonly ButtonWavesDrawable _drawable = new ButtonWavesDrawable();

        private readonly DispatcherTimer _scheduledTimer;

        public GroupCallPage(IProtoService protoService, ICacheService cacheService, IEventAggregator aggregator, IGroupCallService voipService)
        {
            InitializeComponent();

            _protoService = protoService;
            _cacheService = cacheService;
            _aggregator = aggregator;

            _scheduledTimer = new DispatcherTimer();
            _scheduledTimer.Interval = TimeSpan.FromSeconds(1);
            _scheduledTimer.Tick += OnTick;

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

            Window.Current.Closed += OnClosed;

            //Window.Current.SetTitleBar(BlurPanel);
            PhotoInfo.Source = PlaceholderHelper.GetChat(protoService, voipService.Chat, 36);

            if (voipService.Call != null)
            {
                Update(voipService.Call, voipService.CurrentUser);
            }

            if (voipService.Manager != null)
            {
                Connect(voipService.Manager);
            }
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
            Mute,
            Unmute
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

        private async void OnParticipantsChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (_disposed || _service?.Participants == null)
            {
                return;
            }

            _service.Participants.Delegate = this;

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                List.ItemsSource = _service.Participants;
            });
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
                SubtitleInfo.Text = Locale.Declension("Participants", call.ParticipantCount);

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
                    ParticipantsPanel.Visibility = Visibility.Collapsed;
                    StartsAt.Text = call.GetStartsAt();
                    StartsIn.Text = call.GetStartsIn();
                }
                else
                {
                    _scheduledTimer.Stop();

                    if (ScheduledPanel != null)
                    {
                        UnloadObject(ScheduledPanel);
                    }

                    ParticipantsPanel.Visibility = Visibility.Visible;
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
                    Window.Current.Close();

                    if (popup.IsChecked == true)
                    {
                        _service.Discard();
                    }
                    else
                    {
                        _service.Leave();
                    }
                }
            }
            else
            {
                Window.Current.Close();
                _service.Leave();
            }
        }

        private async void Discard_Click(object sender, RoutedEventArgs e)
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
                Window.Current.Close();
                _service.Discard();
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

        private void UpdateNetworkState(GroupCall call, GroupCallParticipant currentUser, bool? connected = null)
        {
            if (call != null && currentUser != null && call.CanUnmuteSelf != currentUser.CanUnmuteSelf)
            {
                return;
            }

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
                    colors = ButtonColors.Mute;
                    AudioInfo.Text = Strings.Resources.VoipGroupUnmute;
                    Lottie.AutoPlay = true;
                    Lottie.Source = new Uri("ms-appx:///Assets/Animations/VoiceUnmute.tgs");
                    break;
                case ButtonState.Mute:
                    colors = ButtonColors.Unmute;
                    AudioInfo.Text = Strings.Resources.VoipTapToMute;
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
                case ButtonColors.Mute:
                    _drawable.SetColors(0xFF0078ff, 0xFF33c659);
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x33, 0xc6, 0x59) };
                    break;
                case ButtonColors.Unmute:
                    _drawable.SetColors(0xFF59c7f8, 0xFF0078ff);
                    Settings.Background = new SolidColorBrush { Color = Color.FromArgb(0x66, 0x00, 0x78, 0xff) };
                    break;
            }

            _prevColors = colors;
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

            flyout.CreateFlyoutSeparator();

            if (call.ScheduledStartDate == 0)
            {

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

                flyout.CreateFlyoutSeparator();
            }

            flyout.CreateFlyoutItem(ShareInviteLink, Strings.Resources.VoipGroupShareInviteLink, new FontIcon { Glyph = Icons.Link });

            if (call.CanBeManaged)
            {
                flyout.CreateFlyoutItem(() => Discard_Click(null, null), Strings.Resources.VoipGroupEndChat, new FontIcon { Glyph = Icons.Delete });
            }

            if (flyout.Items.Count > 0)
            {
                if (ApiInformation.IsEnumNamedValuePresent("Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode", "TopEdgeAlignedLeft"))
                {
                    flyout.Placement = FlyoutPlacementMode.TopEdgeAlignedLeft;
                }

                flyout.ShowAt((Button)sender);
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
            if (args.InRecycleQueue)
            {
                return;
            }

            var content = args.ItemContainer.ContentTemplateRoot as Grid;
            var participant = args.Item as GroupCallParticipant;

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
            var title = content.Children[1] as TextBlock;
            var speaking = content.Children[2] as TextBlock;
            var glyph = content.Children[3] as TextBlock;

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

            if (participant.IsHandRaised)
            {
                speaking.Text = Strings.Resources.WantsToSpeak;
                speaking.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x00, 0x78, 0xff) };
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

                speaking.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
                glyph.Text = Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x33, 0xc6, 0x59) };
            }
            else
            {
                var muted = participant.IsMutedForAllUsers || participant.IsMutedForCurrentUser;

                speaking.Text = participant.Bio.Length > 0 ? participant.Bio : Strings.Resources.Listening;
                speaking.Foreground = new SolidColorBrush { Color = Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
                glyph.Text = muted ? Icons.MicOff : Icons.MicOn;
                glyph.Foreground = new SolidColorBrush { Color = muted && !participant.CanUnmuteSelf ? Colors.Red : Color.FromArgb(0xFF, 0x85, 0x85, 0x85) };
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
                    TickPlacement = TickPlacement.Inline
                };

                var debounder = new EventDebouncer<RangeBaseValueChangedEventArgs>(Constants.HoldingThrottle, handler => slider.ValueChanged += new RangeBaseValueChangedEventHandler(handler), handler => slider.ValueChanged -= new RangeBaseValueChangedEventHandler(handler));
                debounder.Invoked += (s, args) =>
                {
                    _protoService.Send(new SetGroupCallParticipantVolumeLevel(call.Id, participant.ParticipantId, (int)(args.NewValue * 100)));
                };

                flyout.Items.Add(new ContentMenuFlyoutItem { Content = slider });

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
                    _maskGradient = new CanvasRadialGradientBrush(session, Color.FromArgb(0x99, 0xFF, 0xFF, 0xFF), Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF));
                    _maskGradient.Center = new Vector2(150, 150);
                }

                float bigScale = BlobDrawable.SCALE_BIG_MIN + (BlobDrawable.SCALE_BIG * _amplitude);
                float tinyScale = BlobDrawable.SCALE_SMALL_MIN + (BlobDrawable.SCALE_SMALL * _amplitude);
                float glowScale = bigScale * BlobDrawable.LIGHT_GRADIENT_SIZE + 0.7f;

                _maskGradient.RadiusX = 84 * glowScale;
                _maskGradient.RadiusY = 84 * glowScale;

                session.Clear(Colors.Transparent);
                session.FillEllipse(new Vector2(150, 150), 84 * glowScale, 84 * glowScale, _maskGradient);
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
}
