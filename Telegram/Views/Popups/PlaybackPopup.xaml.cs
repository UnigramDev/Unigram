//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Converters;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Composition;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class PlaybackPopup : ContentPopup, IDiffHandler<PlaybackItem>
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private readonly DiffObservableCollection<PlaybackItem> _items;

        private int _lastAudioId;

        public PlaybackPopup(IClientService clientService, INavigationService navigationService)
        {
            InitializeComponent();

            _clientService = clientService;
            _navigationService = navigationService;

            // TODO: consider creating a collection specifically for the playback session
            // rather than using the playlist provided by PlaybackService

            _items = new DiffObservableCollection<PlaybackItem>(this, Constants.DiffOptions);
            _items.AddRange(LifetimeService.Current.Playback.Items);

            Slider.AddHandler(KeyDownEvent, new KeyEventHandler(Slider_KeyDown), true);
            Slider.PositionChanged += Slider_PositionChanged;

            LifetimeService.Current.Playback.SourceChanged += OnPlaybackStateChanged;
            LifetimeService.Current.Playback.StateChanged += OnPlaybackStateChanged;
            LifetimeService.Current.Playback.PositionChanged += OnPositionChanged;
            LifetimeService.Current.Playback.PlaylistChanged += OnPlaylistChanged;

            ScrollingHost.ItemsSource = _items;

            UpdateGlyph();
        }

        public bool CompareItems(PlaybackItem oldItem, PlaybackItem newItem)
        {
            return oldItem.AreTheSame(newItem);
        }

        public void UpdateItem(PlaybackItem oldItem, PlaybackItem newItem)
        {
            // Do nothing
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = ScrollingHost.ScrollToItem2(LifetimeService.Current.Playback?.CurrentItem, VerticalAlignment.Center);
        }

        private void OnPlaybackStateChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(UpdateGlyph);
        }

        private void OnPositionChanged(IPlaybackService sender, PlaybackPositionChangedEventArgs args)
        {
            var position = args.Position;
            var duration = args.Duration;
            var playing = sender.IsPlaying;

            this.BeginOnUIThread(() => UpdatePosition(position, duration, playing));
        }

        private void OnPlaylistChanged(IPlaybackService sender, object args)
        {
            this.BeginOnUIThread(() =>
            {
                _items.ReplaceDiff(LifetimeService.Current.Playback.Items);
            });
        }

        private void UpdatePosition(TimeSpan position, TimeSpan duration, bool playing)
        {
            Position.Text = position.ToDuration(duration.TotalHours >= 1);
            Duration.Text = duration.ToDuration(duration.TotalHours >= 1);

            if (Slider.IsScrubbing)
            {
                return;
            }

            Slider.UpdateValue(position, duration, playing);
        }

        private void UpdateGlyph()
        {
            UpdatePosition(LifetimeService.Current.Playback.Position, LifetimeService.Current.Playback.Duration, LifetimeService.Current.Playback.IsPlaying);

            var item = LifetimeService.Current.Playback.CurrentItem;
            if (item == null)
            {
                TitleLabel.Text = string.Empty;
                SubtitleLabel.Text = string.Empty;

                Hide();
                return;
            }

            if (_lastAudioId != item.Document.Id)
            {
                _lastAudioId = item.Document.Id;
                UpdateIsProfileAudio(item);
            }

            VolumeButton.Glyph = LifetimeService.Current.Playback.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };

            PlaybackButton.Glyph = LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused ? Icons.PlayFilled24 : Icons.PauseFilled24;
            Automation.SetToolTip(PlaybackButton, LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused ? Strings.AccActionPlay : Strings.AccActionPause);

            HeaderLabel.Visibility = Visibility.Collapsed;

            if (item is PlaybackItemMessage message)
            {
                var linkPreview = message.Message.Content is MessageText text ? text.LinkPreview : null;

                if (message.Message.Content is MessageVoiceNote || message.Message.Content is MessageVideoNote || linkPreview?.Type is LinkPreviewTypeVoiceNote or LinkPreviewTypeVideoNote)
                {
                    var title = string.Empty;
                    var date = Formatter.DateAt(message.Message.Date);

                    if (_clientService.TryGetUser(message.Message.SenderId, out Telegram.Td.Api.User senderUser))
                    {
                        title = senderUser.Id == _clientService.Options.MyId ? Strings.ChatYourSelfName : senderUser.FullName();
                    }
                    else if (_clientService.TryGetChat(message.Message.SenderId, out Chat senderChat))
                    {
                        title = _clientService.GetTitle(senderChat);
                    }

                    UpdateText(message.ChatId, message.Id, title, date);

                    PreviousButton.Visibility = Visibility.Collapsed;
                    NextButton.Visibility = Visibility.Collapsed;

                    RepeatButton.Visibility = Visibility.Collapsed;
                    //ShuffleButton.Visibility = Visibility.Collapsed;

                    //UpdateSpeed(int.MaxValue);
                }
                else if (message.Message.Content is MessageAudio || linkPreview?.Type is LinkPreviewTypeAudio)
                {
                    var audio = message.Message.Content is MessageAudio messageAudio ? messageAudio.Audio : (linkPreview?.Type is LinkPreviewTypeAudio previewAudio ? previewAudio.Audio : null);
                    if (audio == null)
                    {
                        return;
                    }

                    if (string.IsNullOrEmpty(item.Performer))
                    {
                        UpdateText(message.ChatId, message.Id, item.Title, Strings.AudioUnknownArtist);
                    }
                    else
                    {
                        UpdateText(message.ChatId, message.Id, item.Title, item.Performer);
                    }

                    PreviousButton.Visibility = Visibility.Visible;
                    NextButton.Visibility = Visibility.Visible;

                    RepeatButton.Visibility = Visibility.Visible;
                    //ShuffleButton.Visibility = Visibility.Visible;

                    //UpdateSpeed(audio.Duration);
                    UpdateRepeat();
                }
            }
            else if (item is PlaybackItemProfileAudio audio)
            {
                if (_clientService.Options.MyId == audio.UserId)
                {
                    HeaderLabel.Text = Strings.ProfilePlaylistTitleMine;
                    HeaderLabel.Visibility = Visibility.Visible;
                }
                else if (_clientService.TryGetUser(audio.UserId, out User user))
                {
                    HeaderLabel.Text = string.Format(Strings.ProfilePlaylistTitle, user.FullName(true));
                    HeaderLabel.Visibility = Visibility.Visible;
                }
                else
                {
                    HeaderLabel.Visibility = Visibility.Collapsed;
                }

                if (string.IsNullOrEmpty(item.Performer))
                {
                    UpdateText(audio.UserId, audio.Audio.AudioValue.Id, item.Title, Strings.AudioUnknownArtist);
                }
                else
                {
                    UpdateText(audio.UserId, audio.Audio.AudioValue.Id, item.Title, item.Performer);
                }

                PreviousButton.Visibility = Visibility.Visible;
                NextButton.Visibility = Visibility.Visible;

                RepeatButton.Visibility = Visibility.Visible;
                //ShuffleButton.Visibility = Visibility.Visible;

                //UpdateSpeed(audio.Audio.Duration);
                UpdateRepeat();
            }
        }

        private void UpdateRepeat()
        {
            RepeatButton.IsChecked = LifetimeService.Current.Playback.IsRepeatEnabled;
            Automation.SetToolTip(RepeatButton, LifetimeService.Current.Playback.IsRepeatEnabled == null
                ? Strings.AccDescrRepeatOne
                : LifetimeService.Current.Playback.IsRepeatEnabled == true
                ? Strings.AccDescrRepeatList
                : Strings.AccDescrRepeatOff);
        }

        private async void UpdateIsProfileAudio(PlaybackItem item)
        {
            //if (item is PlaybackItemProfileAudio profileAudio && profileAudio.UserId == _clientService.Options.MyId)
            //{
            //    ShowHideRemove(true);
            //    return;
            //}

            AddToProfileText.Visibility = Visibility.Collapsed;
            AddToProfileRing.Visibility = Visibility.Visible;
            RemoveFromProfileText.Visibility = Visibility.Collapsed;
            RemoveFromProfileRing.Visibility = Visibility.Visible;

            var response = await _clientService.SendAsync(new IsProfileAudio(item.Document.Id));

            if (!item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                return;
            }

            if (response is Ok)
            {
                ShowHideRemove(true);
            }
            else
            {
                ShowHideRemove(false);
            }

            AddToProfileText.Visibility = Visibility.Visible;
            AddToProfileRing.Visibility = Visibility.Collapsed;
            RemoveFromProfileText.Visibility = Visibility.Visible;
            RemoveFromProfileRing.Visibility = Visibility.Collapsed;
        }

        private bool _removeCollapsed;

        private void ShowHideRemove(bool show)
        {
            if (_removeCollapsed != show)
            {
                return;
            }

            _removeCollapsed = !show;
            AddToProfile.Visibility = Visibility.Visible;
            RemoveFromProfile.Visibility = Visibility.Visible;

            var visualShow = ElementComposition.GetElementVisual(RemoveFromProfile);
            var visualHide = ElementComposition.GetElementVisual(AddToProfile);

            var batch = visualShow.Compositor.CreateScopedBatch(CompositionBatchTypes.Animation);
            batch.Completed += (s, args) =>
            {
                if (_removeCollapsed)
                {
                    RemoveFromProfile.Visibility = Visibility.Collapsed;
                }
                else
                {
                    AddToProfile.Visibility = Visibility.Collapsed;
                }
            };

            var hide1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            hide1.InsertKeyFrame(show ? 0 : 1, new Vector3(1));
            hide1.InsertKeyFrame(show ? 1 : 0, new Vector3(0));

            var hide2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            hide2.InsertKeyFrame(show ? 0 : 1, 1);
            hide2.InsertKeyFrame(show ? 1 : 0, 0);

            visualHide.StartAnimation("Scale", hide1);
            visualHide.StartAnimation("Opacity", hide2);

            var show1 = visualShow.Compositor.CreateVector3KeyFrameAnimation();
            show1.InsertKeyFrame(show ? 1 : 0, new Vector3(1));
            show1.InsertKeyFrame(show ? 0 : 1, new Vector3(0));

            var show2 = visualShow.Compositor.CreateScalarKeyFrameAnimation();
            show2.InsertKeyFrame(show ? 1 : 0, 1);
            show2.InsertKeyFrame(show ? 0 : 1, 0);

            visualShow.StartAnimation("Scale", show1);
            visualShow.StartAnimation("Opacity", show2);

            batch.End();
        }

        private void UpdateText(long chatId, long messageId, string title, string subtitle)
        {
            TitleLabel.Text = title;
            SubtitleLabel.Text = subtitle;
            //if (_chatId == chatId && _messageId == messageId)
            //{
            //    return;
            //}

            //var prev = _chatId == chatId && _messageId > messageId;

            //_chatId = chatId;
            //_messageId = messageId;

            //var visualShow = _visual == _visual1 ? _visual2 : _visual1;
            //var visualHide = _visual == _visual1 ? _visual1 : _visual2;

            //var titleShow = _visual == _visual1 ? TitleLabel2 : TitleLabel1;
            //var subtitleShow = _visual == _visual1 ? SubtitleLabel2 : SubtitleLabel1;

            //var hide1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            //hide1.InsertKeyFrame(0, new Vector3(0));
            //hide1.InsertKeyFrame(1, new Vector3(prev ? -12 : 12, 0, 0));

            //var hide2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            //hide2.InsertKeyFrame(0, 1);
            //hide2.InsertKeyFrame(1, 0);

            //visualHide.StartAnimation("Offset", hide1);
            //visualHide.StartAnimation("Opacity", hide2);

            //titleShow.Text = title;
            //subtitleShow.Text = subtitle;

            //var show1 = _visual.Compositor.CreateVector3KeyFrameAnimation();
            //show1.InsertKeyFrame(0, new Vector3(prev ? 12 : -12, 0, 0));
            //show1.InsertKeyFrame(1, new Vector3(0));

            //var show2 = _visual.Compositor.CreateScalarKeyFrameAnimation();
            //show2.InsertKeyFrame(0, 0);
            //show2.InsertKeyFrame(1, 1);

            //visualShow.StartAnimation("Offset", show1);
            //visualShow.StartAnimation("Opacity", show2);

            //_visual = visualShow;
        }

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            var container = ScrollingHost.ContainerFromItem(e.ClickedItem) as SelectorItem;
            var cell = container?.ContentTemplateRoot as PlaybackItemCell;
            cell?.Click();
        }

        private void OnChoosingItemContainer(ListViewBase sender, ChoosingItemContainerEventArgs args)
        {
            if (args.ItemContainer == null)
            {
                args.ItemContainer = new ListViewItem
                {
                    ContentTemplate = sender.ItemTemplate,
                    Style = sender.ItemContainerStyle
                };

                args.ItemContainer.ContextRequested += OnContextRequested;
            }

            args.IsContainerPrepared = true;
        }

        private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
        {
            if (args.InRecycleQueue)
            {
                return;
            }
            else if (args.Item is PlaybackItem item && args.ItemContainer.ContentTemplateRoot is PlaybackItemCell cell)
            {
                if (item is PlaybackItemMessage message)
                {
                    AutomationProperties.SetName(args.ItemContainer, Automation.GetSummary(message.Message, true, false));
                }
                else if (item is PlaybackItemProfileAudio audio)
                {
                    AutomationProperties.SetName(args.ItemContainer, Automation.GetSummary(audio.Audio, true));
                }

                cell.UpdateItem(item);
                args.Handled = true;
            }

        }

        private async void OnContextRequested(UIElement sender, ContextRequestedEventArgs args)
        {
            var item = ScrollingHost.ItemFromContainer(sender) as PlaybackItem;
            if (item != null)
            {
                var flyout = new MenuFlyout();

                await PopulateContextMenuAsync(flyout, item);

                flyout.ShowAt(sender, args);
            }
        }

        private async Task PopulateContextMenuAsync(MenuFlyout flyout, PlaybackItem item)
        {
            var canAddToProfile = await _clientService.SendAsync(new IsProfileAudio(item.Document.Id)) is Error;

            if (item is PlaybackItemMessage message)
            {
                var properties = await _clientService.SendAsync(new GetMessageProperties(message.ChatId, message.Id)) as MessageProperties;

                LoadSaveTo(flyout, item, properties, canAddToProfile);

                flyout.CreateFlyoutSeparator();
                flyout.CreateFlyoutItem(ShowInChat, message, Strings.ShowInChat, Icons.ChatEmpty);

                if (properties != null && properties.CanBeForwarded)
                {
                    flyout.CreateFlyoutItem(Forward, (message, properties), Strings.Forward, Icons.Share);
                }
            }
            else if (item is PlaybackItemProfileAudio audio)
            {
                LoadSaveTo(flyout, item, null, canAddToProfile);

                flyout.CreateFlyoutItem(Forward, audio, Strings.Forward, Icons.Share);
            }
        }

        private void LoadSaveTo(MenuFlyout flyout, PlaybackItem item, MessageProperties properties, bool canAddToProfile)
        {
            if (properties == null || properties.CanBeForwarded || properties.CanBeSaved)
            {
                var menu = new MenuFlyoutSubItem
                {
                    Text = Strings.AudioSaveTo,
                    Icon = MenuFlyoutHelper.CreateIcon(Icons.SaveAs)
                };

                flyout.Items.Add(menu);

                if (canAddToProfile)
                {
                    menu.CreateFlyoutItem(SaveToProfile, item, Strings.AudioSaveToMyProfile, Icons.Person);
                }

                if (properties == null || properties.CanBeForwarded)
                {
                    menu.CreateFlyoutItem(SaveToSavedMessages, item, Strings.AudioSaveToSavedMessages, Icons.Bookmark);
                }

                if (properties == null || properties.CanBeSaved)
                {
                    menu.CreateFlyoutItem(SaveToFiles, item, Strings.AudioSaveToFiles, Icons.Folder);
                }
            }
            else if (canAddToProfile)
            {
                flyout.CreateFlyoutItem(SaveToProfile, item, Strings.AudioAddToProfile, Icons.Person);
            }
        }

        private void ShowInChat(PlaybackItemMessage message)
        {
            Hide();
            _navigationService.NavigateToChat(message.ChatId, message.Id);
        }

        private void Forward((PlaybackItemMessage message, MessageProperties properties) param)
        {
            Hide();
            _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationShareMessages(new MessageToShare(param.message.Message, param.properties)));
        }

        private void Forward(PlaybackItemProfileAudio audio)
        {
            Hide();
            _navigationService.ShowPopup(new ChooseChatsPopup(), new ChooseChatsConfigurationPostMessage(audio.ToInputMessage()));
        }

        private void SaveToProfile(PlaybackItem item)
        {
            _clientService.Send(new AddProfileAudio(item.Document.Id));
            _navigationService.ShowToast(Strings.AudioSaveToMyProfileSaved, ToastPopupIcon.SavedMessages);

            if (item.AreTheSame(LifetimeService.Current.Playback.CurrentItem))
            {
                ShowHideRemove(true);
            }
        }

        private void SaveToSavedMessages(PlaybackItem item)
        {
            if (item is PlaybackItemMessage message)
            {
                _clientService.Send(new ForwardMessages(_clientService.Options.MyId, null, message.ChatId, new[] { message.Id }, null, false, false));
            }
            else if (item is PlaybackItemProfileAudio audio)
            {
                _clientService.Send(new SendMessage(_clientService.Options.MyId, null, null, null, audio.ToInputMessage()));
            }

            _navigationService.ShowToast(Strings.AudioSaveToSavedMessagesSaved, ToastPopupIcon.SavedMessages);
        }

        private void SaveToFiles(PlaybackItem item)
        {
            var service = _clientService.Session.Resolve<IStorageService>();
            _ = service.SaveFileAsAsync(item.Document);
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (LifetimeService.Current.Playback.PlaybackState == PlaybackState.Paused)
            {
                LifetimeService.Current.Playback.Play();
            }
            else
            {
                LifetimeService.Current.Playback.Pause();
            }
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            LifetimeService.Current.Playback.MoveNext();
        }

        private void Previous_Click(object sender, RoutedEventArgs e)
        {
            if (LifetimeService.Current.Playback.Position.TotalSeconds > 5)
            {
                LifetimeService.Current.Playback.Seek(TimeSpan.Zero);
            }
            else
            {
                LifetimeService.Current.Playback.MovePrevious();
            }
        }

        private void VolumeButton_Click(object sender, RoutedEventArgs e)
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
                Value = LifetimeService.Current.Playback.Volume * 100
            };

            slider.ValueChanged += VolumeSlider_ValueChanged;

            var flyout = new MenuFlyout();
            flyout.Items.Add(slider);
            flyout.ShowAt(VolumeButton, FlyoutPlacementMode.BottomEdgeAlignedLeft);
        }

        private void VolumeSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            LifetimeService.Current.Playback.Volume = e.NewValue / 100;

            VolumeButton.Glyph = LifetimeService.Current.Playback.Volume switch
            {
                double n when n > 0.66 => Icons.Speaker3,
                double n when n > 0.33 => Icons.Speaker2,
                double n when n > 0 => Icons.Speaker1,
                _ => Icons.SpeakerOff
            };
        }

        private void Repeat_Click(object sender, RoutedEventArgs e)
        {
            LifetimeService.Current.Playback.IsRepeatEnabled = RepeatButton.IsChecked;
            UpdateRepeat();
        }

        private void Shuffle_Click(object sender, RoutedEventArgs e)
        {
            //LifetimeService.Current.Playback.IsShuffleEnabled = ShuffleButton.IsChecked == true;
            LifetimeService.Current.Playback.IsReversed = ShuffleButton.IsChecked == true;
        }

        private void Slider_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.Right || e.Key == VirtualKey.Up)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position + TimeSpan.FromSeconds(5));
            }
            else if (e.Key == VirtualKey.Left || e.Key == VirtualKey.Down)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position - TimeSpan.FromSeconds(5));
            }
            else if (e.Key == VirtualKey.PageUp)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position + TimeSpan.FromSeconds(30));
            }
            else if (e.Key == VirtualKey.PageDown)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Position - TimeSpan.FromSeconds(30));
            }
            else if (e.Key == VirtualKey.Home)
            {
                LifetimeService.Current.Playback?.Seek(TimeSpan.Zero);
            }
            else if (e.Key == VirtualKey.End)
            {
                LifetimeService.Current.Playback?.Seek(Slider.Duration);
            }
        }

        private void Slider_PositionChanged(object sender, PlaybackSliderPositionChanged e)
        {
            LifetimeService.Current.Playback?.Seek(e.NewPosition);
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            if (e.Items.Count > 1 || e.Items[0] is not PlaybackItemProfileAudio)
            {
                ScrollingHost.CanReorderItems = false;
                e.Cancel = true;
            }
            else
            {
                if (ScrollingHost.Items.Count < 2)
                {
                    ScrollingHost.CanReorderItems = false;
                    e.Cancel = true;
                }
                else
                {
                    ScrollingHost.CanReorderItems = true;
                }
            }
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            sender.CanReorderItems = false;

            if (args.DropResult == DataPackageOperation.Move && args.Items.Count == 1 && args.Items[0] is PlaybackItemProfileAudio item)
            {
                var index = ScrollingHost.Items.IndexOf(item);
                if (index == -1)
                {
                    return;
                }

                if (index > 0 && ScrollingHost.Items[index - 1] is PlaybackItemProfileAudio previous)
                {
                    _clientService.Send(new SetProfileAudioPosition(item.Id, previous.Id));
                }
                else
                {
                    _clientService.Send(new SetProfileAudioPosition(item.Id, 0));
                }

                LifetimeService.Current.Playback.MoveTo(item, index);
            }
        }

        private void AddToProfile_Click(object sender, RoutedEventArgs e)
        {
            if (LifetimeService.Current.Playback.CurrentItem is PlaybackItem item)
            {
                _clientService.Send(new AddProfileAudio(item.Document.Id));
                ShowHideRemove(true);
            }
        }

        private void RemoveFromProfile_Click(object sender, TextUrlClickEventArgs e)
        {
            if (LifetimeService.Current.Playback.CurrentItem is PlaybackItem item)
            {
                _clientService.Send(new RemoveProfileAudio(item.Document.Id));
                ShowHideRemove(false);
            }
        }

        private void Visual_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (sender is UIElement element)
            {
                var visual = ElementComposition.GetElementVisual(element);
                visual.CenterPoint = new Vector3(element.ActualSize / 2, 0);
            }
        }

        private async void More_ContextRequested(object sender, RoutedEventArgs e)
        {
            var item = LifetimeService.Current.Playback.CurrentItem;
            if (item != null)
            {
                var flyout = new MenuFlyout();

                await PopulateContextMenuAsync(flyout, item);

                flyout.ShowAt(sender as UIElement, FlyoutPlacementMode.BottomEdgeAlignedRight);
            }
        }
    }
}
