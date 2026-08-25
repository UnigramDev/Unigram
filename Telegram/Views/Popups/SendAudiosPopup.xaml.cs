//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Controls.Cells;
using Telegram.Controls.Media;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Automation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;

namespace Telegram.Views.Popups
{
    public sealed partial class SendAudiosPopup : ContentPopup, IDiffHandler<PlaybackItem>
    {
        private readonly IClientService _clientService;
        private readonly INavigationService _navigationService;

        private int _lastAudioId;

        public SendAudiosPopup(IClientService clientService, INavigationService navigationService)
        {
            InitializeComponent();

            Title = Strings.AttachMusic;

            _clientService = clientService;
            _navigationService = navigationService;

            var child = new UserProfileAudioCollection(clientService);

            ScrollingHost.ItemsSource = new SharedAudioCollection(clientService, child);
            ScrollingHost2.ItemsSource = child;
        }

        public partial class UserProfileAudioCollection : ObservableCollection<PlaybackItem>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private bool _hasMoreItems = true;

            public UserProfileAudioCollection(IClientService clientService)
            {
                _clientService = clientService;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                return IncrementalLoading.Run(async token =>
                {
                    var totalCount = 0u;

                    var response = await _clientService.SendAsync(new GetUserProfileAudios(_clientService.Options.MyId, Count, 20));
                    if (response is Audios audios)
                    {
                        foreach (var audio in audios.AudiosValue)
                        {
                            Add(new PlaybackItemProfileAudio(null, new AudioWithOwner(_clientService, _clientService.Options.MyId, audio)));
                            totalCount++;
                        }

                        _hasMoreItems = audios.AudiosValue.Count > 0;
                    }

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                });
            }

            public bool HasMoreItems => _hasMoreItems;
        }

        public partial class SharedAudioCollection : ObservableCollection<PlaybackItem>, ISupportIncrementalLoading
        {
            private readonly IClientService _clientService;
            private readonly UserProfileAudioCollection _child;

            private string _nextOffset = string.Empty;
            private bool _hasMoreItems = true;

            public SharedAudioCollection(IClientService clientService, UserProfileAudioCollection child)
            {
                _clientService = clientService;
                _child = child;
            }

            public IAsyncOperation<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                if (_child.HasMoreItems)
                {
                    return _child.LoadMoreItemsAsync(count);
                }

                return IncrementalLoading.Run(async token =>
                {
                    var totalCount = 0u;

                    var response = await _clientService.SendAsync(new SearchMessages(new ChatListMain(), string.Empty, _nextOffset, 20, new SearchMessagesFilterAudio(), null, 0, 0));
                    if (response is FoundMessages audios)
                    {
                        foreach (var audio in audios.Messages)
                        {
                            Add(new PlaybackItemMessage(null, new MessageWithOwner(_clientService, audio), null));
                            totalCount++;
                        }

                        _nextOffset = audios.NextOffset;
                        _hasMoreItems = audios.NextOffset.Length > 0;
                    }

                    return new LoadMoreItemsResult
                    {
                        Count = totalCount
                    };
                });
            }

            public bool HasMoreItems => _child.HasMoreItems || _hasMoreItems;
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

        private void OnItemClick(object sender, ItemClickEventArgs e)
        {
            SelectedItems = [e.ClickedItem as PlaybackItem];
            Hide(ContentDialogResult.Primary);
        }

        public IList<PlaybackItem> SelectedItems { get; private set; }

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
                flyout.CreateFlyoutItem(ShowInChat, message, Strings.ShowInChat2, Icons.ChatEmpty);

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

                if (properties == null || properties.CanBeForwarded)
                {
                    menu.CreateFlyoutItem(SaveToSavedMessages, item, Strings.AudioSaveToSavedMessages, Icons.Bookmark);
                }

                if (properties == null || properties.CanBeSaved)
                {
                    menu.CreateFlyoutItem(SaveToFiles, item, Strings.AudioSaveToFiles, Icons.Folder);
                }
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
            _ = service.SaveFileAsAsync(XamlRoot, item.Document);
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

        private void Local_Click(object sender, RoutedEventArgs e)
        {
            SelectedItems = null;
            Hide(ContentDialogResult.Primary);
        }
    }
}
