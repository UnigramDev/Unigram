using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Streams;
using Telegram.Td.Api;
using Telegram.ViewModels.Stories;
using Telegram.Views;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public class StoryInteractionsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private string _nextOffset = string.Empty;

        public StoryInteractionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Items = new IncrementalCollection<object>(this);
        }

        private StoryViewModel _story;
        public StoryViewModel Story
        {
            get => _story;
            set => Set(ref _story, value);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is StoryViewModel story)
            {
                Story = story;
            }

            return Task.CompletedTask;
        }

        public async void BlockUser(StoryViewer viewer, DependencyObject container)
        {
            if (ClientService.TryGetUser(viewer.UserId, out User user))
            {
                var confirm = await MessagePopup.ShowAsync(
                    container as FrameworkElement,
                    string.Format(Strings.AreYouSureBlockContact2, user.FirstName),
                    Strings.BlockUser,
                    Strings.BlockUser,
                    Strings.Cancel,
                    true);

                if (confirm == ContentDialogResult.Primary)
                {
                    viewer.BlockList = new BlockListMain();
                    ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(viewer.UserId), new BlockListMain()));
                }
            }
        }

        public async void DeleteContact(StoryViewer viewer, DependencyObject container)
        {
            if (ClientService.TryGetUser(viewer.UserId, out User user))
            {
                var confirm = await MessagePopup.ShowAsync(
                    container as FrameworkElement,
                    Strings.AreYouSureDeleteContact,
                    Strings.DeleteContact,
                    Strings.Delete,
                    Strings.Cancel,
                    true);

                if (confirm == ContentDialogResult.Primary)
                {
                    ClientService.Send(new RemoveContacts(new[] { viewer.UserId }));
                }
            }
        }

        public void HideStories(StoryViewer viewer)
        {
            if (ClientService.TryGetUser(viewer.UserId, out User user))
            {
                viewer.BlockList = new BlockListStories();

                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), new BlockListStories()));
                Window.Current.ShowTeachingTip(string.Format(Strings.StoryHiddenHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public void ShowStories(StoryViewer viewer)
        {
            if (ClientService.TryGetUser(viewer.UserId, out User user))
            {
                viewer.BlockList = null;

                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), null));
                Window.Current.ShowTeachingTip(string.Format(Strings.StoryShownHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public void UnblockUser(StoryViewer viewer)
        {
            if (ClientService.TryGetUser(viewer.UserId, out User user))
            {
                viewer.BlockList = null;

                ClientService.Send(new SetMessageSenderBlockList(new MessageSenderUser(user.Id), null));
                Window.Current.ShowTeachingTip(string.Format(Strings.StoryShownHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public IncrementalCollection<object> Items { get; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            var totalCount = 0u;

            var response = await ClientService.SendAsync(new GetStoryViewers(_story.StoryId, string.Empty, false, false, _nextOffset, 50));
            if (response is StoryViewers viewers)
            {
                _nextOffset = viewers.NextOffset;
                HasMoreItems = viewers.NextOffset.Length > 0;

                foreach (var item in viewers.Viewers)
                {
                    totalCount++;
                    Items.Add(item);
                }
            }

            return new LoadMoreItemsResult
            {
                Count = totalCount
            };
        }

        public bool HasMoreItems { get; private set; } = true;

        public async void OpenChat(object clickedItem)
        {
            if (clickedItem is AddedReaction addedReaction)
            {
                if (addedReaction.SenderId is MessageSenderChat senderChat)
                {
                    NavigationService.Navigate(typeof(ProfilePage), senderChat.ChatId);
                }
                else if (addedReaction.SenderId is MessageSenderUser senderUser)
                {
                    var response = await ClientService.SendAsync(new CreatePrivateChat(senderUser.UserId, true));
                    if (response is Chat chat)
                    {
                        NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                    }
                }
            }
            else if (clickedItem is MessageViewer messageViewer)
            {
                var response = await ClientService.SendAsync(new CreatePrivateChat(messageViewer.UserId, true));
                if (response is Chat chat)
                {
                    NavigationService.Navigate(typeof(ProfilePage), chat.Id);
                }
            }
        }
    }

}
