//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading;
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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels
{
    public enum StoryInteractionsSortBy
    {
        Reaction,
        Time
    }

    public class StoryInteractionsViewModel : ViewModelBase, IIncrementalCollectionOwner
    {
        private string _nextOffset = string.Empty;
        private CancellationTokenSource _nextToken;

        public StoryInteractionsViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            _query = new DebouncedProperty<string>(Constants.TypingTimeout, UpdateQuery);
            Items = new IncrementalCollection<object>(this);
        }

        private StoryViewModel _story;
        public StoryViewModel Story
        {
            get => _story;
            set => Set(ref _story, value);
        }

        public StoryInteractionsSortBy SortBy { get; private set; }

        private readonly DebouncedProperty<string> _query;
        public string Query
        {
            get => _query ?? string.Empty;
            set => _query.Set(value);
        }

        public void UpdateQuery(string value)
        {
            _query.Value = value;

            UpdateSortBy();
            RaisePropertyChanged(nameof(Query));
        }

        private int _onlyContacts;
        public int OnlyContacts
        {
            get => _onlyContacts;
            set
            {
                if (Set(ref _onlyContacts, value))
                {
                    UpdateSortBy();
                }
            }
        }

        public void SortByReaction()
        {
            SortBy = StoryInteractionsSortBy.Reaction;
            RaisePropertyChanged(nameof(SortBy));
            UpdateSortBy();
        }

        public void SortByTime()
        {
            SortBy = StoryInteractionsSortBy.Time;
            RaisePropertyChanged(nameof(SortBy));
            UpdateSortBy();
        }

        private void UpdateSortBy()
        {
            _nextOffset = string.Empty;
            _ = LoadMoreItemsAsync(0);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is StoryViewModel story)
            {
                Story = story;
            }

            return Task.CompletedTask;
        }

        public async void BlockUser(StoryInteraction interaction, DependencyObject container)
        {
            if (ClientService.TryGetUser(interaction.ActorId, out User user))
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
                    interaction.BlockList = new BlockListMain();
                    ClientService.Send(new SetMessageSenderBlockList(interaction.ActorId, new BlockListMain()));
                }
            }
        }

        public async void DeleteContact(StoryInteraction interaction, DependencyObject container)
        {
            if (ClientService.TryGetUser(interaction.ActorId, out User user))
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
                    ClientService.Send(new RemoveContacts(new[] { user.Id }));
                }
            }
        }

        public void HideStories(StoryInteraction interaction)
        {
            if (ClientService.TryGetUser(interaction.ActorId, out User user))
            {
                interaction.BlockList = new BlockListStories();

                ClientService.Send(new SetMessageSenderBlockList(interaction.ActorId, new BlockListStories()));
                ToastPopup.Show(string.Format(Strings.StoryHiddenHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public void ShowStories(StoryInteraction interaction)
        {
            if (ClientService.TryGetUser(interaction.ActorId, out User user))
            {
                interaction.BlockList = null;

                ClientService.Send(new SetMessageSenderBlockList(interaction.ActorId, null));
                ToastPopup.Show(string.Format(Strings.StoryShownHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public void UnblockUser(StoryInteraction interaction)
        {
            if (ClientService.TryGetUser(interaction.ActorId, out User user))
            {
                interaction.BlockList = null;

                ClientService.Send(new SetMessageSenderBlockList(interaction.ActorId, null));
                ToastPopup.Show(string.Format(Strings.StoryShownHint, user.FirstName), new LocalFileSource("ms-appx:///Assets/Toasts/Info.tgs"));
            }
        }

        public IncrementalCollection<object> Items { get; }

        public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            _nextToken?.Cancel();

            if (count == 0 && Items.Count > 0)
            {
                Items.Clear();
            }

            var totalCount = 0u;
            var token = _nextToken = new CancellationTokenSource();

            var response = await ClientService.SendAsync(new GetStoryInteractions(_story.StoryId, Query ?? string.Empty, OnlyContacts > 0, false, SortBy == StoryInteractionsSortBy.Reaction, _nextOffset, 50));
            if (response is StoryInteractions interactions && !token.IsCancellationRequested)
            {
                _nextOffset = interactions.NextOffset;
                HasMoreItems = interactions.NextOffset.Length > 0;

                foreach (var item in interactions.Interactions)
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

        public void OpenChat(object clickedItem)
        {
            if (clickedItem is AddedReaction addedReaction)
            {
                NavigationService.NavigateToSender(addedReaction.SenderId);
            }
            else if (clickedItem is MessageViewer messageViewer)
            {
                NavigationService.NavigateToUser(messageViewer.UserId);
            }
            else if (clickedItem is StoryInteraction interaction)
            {
                NavigationService.NavigateToSender(interaction.ActorId);
            }
        }
    }
}
