﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Popups;

namespace Telegram.ViewModels.Folders
{
    public partial class ShareFolderViewModel : ViewModelBase
    {
        public ShareFolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(SelectedCount));
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is Tuple<int, ChatFolderInviteLink> data)
            {
                InviteLink = data.Item2?.InviteLink;

                var response = await ClientService.SendAsync(new GetChatFolder(data.Item1));
                if (response is ChatFolder folder)
                {
                    Title = folder.Title;

                    var ids = new List<long>(data.Item2?.ChatIds ?? Array.Empty<long>());

                    foreach (var id in folder.IncludedChatIds)
                    {
                        if (ids.Contains(id))
                        {
                            continue;
                        }

                        ids.Add(id);
                    }

                    _shareableItems = new HashSet<long>(ids);

                    var chats = ClientService.GetChats(ids);

                    var selected = new List<Chat>(ClientService.GetChats(data.Item2?.ChatIds ?? folder.IncludedChatIds));
                    var sorted = new List<Chat>(chats);

                    foreach (var chat in chats)
                    {
                        if (ClientService.TryGetSupergroup(chat, out Supergroup supergroup))
                        {
                            if (supergroup.CanInviteUsers())
                            {
                                continue;
                            }
                            else if (supergroup.HasActiveUsername() && !supergroup.JoinByRequest)
                            {
                                continue;
                            }
                        }
                        else if (ClientService.TryGetBasicGroup(chat, out BasicGroup basicGroup))
                        {
                            if (basicGroup.CanInviteUsers())
                            {
                                continue;
                            }
                        }

                        _shareableItems.Remove(chat.Id);

                        selected.Remove(chat);
                        sorted.Remove(chat);
                        sorted.Add(chat);
                    }

                    Items.ReplaceWith(sorted);
                    SelectedItems.ReplaceWith(selected);
                }
            }
        }

        private HashSet<long> _shareableItems = new();

        public MvxObservableCollection<Chat> Items { get; private set; } = new();

        public MvxObservableCollection<Chat> SelectedItems { get; private set; } = new();

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _inviteLink;
        public string InviteLink
        {
            get => _inviteLink?.Replace("https://", string.Empty);
            set => Set(ref _inviteLink, value);
        }

        public int TotalCount => _shareableItems.Count;

        public int SelectedCount => SelectedItems.Count;

        public bool CanBeShared(Chat chat)
        {
            return _shareableItems.Contains(chat.Id);
        }

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
                    if (_shareableItems.Contains(chat.Id) && !SelectedItems.Contains(chat))
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

        public void Copy()
        {
            MessageHelper.CopyLink(NavigationService.XamlRoot, _inviteLink);
        }

        public async void Share()
        {
            await ShowPopupAsync(typeof(ChooseChatsPopup), new ChooseChatsConfigurationPostLink(new HttpUrl(_inviteLink)));
        }
    }
}
