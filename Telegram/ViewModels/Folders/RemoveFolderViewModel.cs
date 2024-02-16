//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public class RemoveFolderViewModel : ViewModelBase
    {
        public RemoveFolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(SelectedCount));
            RaisePropertyChanged(nameof(PrimaryButtonText));
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is Tuple<ChatFolder, Td.Api.Chats> tuple)
            {
                Items.ReplaceWith(ClientService.GetChats(tuple.Item1.IncludedChatIds));

                foreach (var item in Items)
                {
                    if (tuple.Item2.ChatIds.Contains(item.Id))
                    {
                        SelectedItems.Add(item);
                    }
                }
            }

            return Task.CompletedTask;
        }

        public MvxObservableCollection<Chat> Items { get; private set; } = new();

        public MvxObservableCollection<Chat> SelectedItems { get; private set; } = new();

        public string PrimaryButtonText => SelectedItems.Count > 0
            ? Strings.FolderLinkButtonRemoveChats
            : Strings.FolderLinkButtonRemove;

        public int TotalCount => Items.Count;

        public int SelectedCount => SelectedItems.Count;

        public void SelectAll()
        {
            if (SelectedItems.Count >= TotalCount)
            {
                SelectedItems.Clear();
            }
            else
            {
                List<Chat> temp = null;

                foreach (var chat in Items)
                {
                    if (!SelectedItems.Contains(chat))
                    {
                        temp ??= new();
                        temp.Add(chat);
                    }
                }

                if (temp != null)
                {
                    SelectedItems.AddRange(temp);
                }
            }
        }
    }
}
