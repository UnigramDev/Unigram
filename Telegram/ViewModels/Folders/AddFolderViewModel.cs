﻿//
// Copyright Fela Ameghino 2015-2024
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.ViewModels.Settings;

namespace Telegram.ViewModels.Folders
{
    public class AddFolderViewModel : ViewModelBase
    {
        public AddFolderViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            SelectedItems.CollectionChanged += OnCollectionChanged;
        }

        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RaisePropertyChanged(nameof(SelectedCount));

            if (_bindButtonToSelection)
            {
                PrimaryButtonText = Locale.Declension(Strings.R.FolderLinkButtonJoinPlural, SelectedCount);
            }
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            if (parameter is ChatFolderInviteLinkInfo inviteLink)
            {
                if (inviteLink.ChatFolderInfo.Id == 0)
                {
                    Title = Strings.FolderLinkTitleAdd;
                    Subtitle = string.Format(Strings.FolderLinkSubtitle, inviteLink.ChatFolderInfo.Title);

                    _bindButtonToSelection = false;
                    PrimaryButtonText = string.Format(Strings.FolderLinkButtonAdd, inviteLink.ChatFolderInfo.Title);
                }
                else if (inviteLink.MissingChatIds.Count == 0)
                {
                    Title = Strings.FolderLinkTitleAlready;
                    Subtitle = string.Format(Strings.FolderLinkSubtitleAlready, inviteLink.ChatFolderInfo.Title);

                    _bindButtonToSelection = false;
                    PrimaryButtonText = Strings.OK;
                }
                else
                {
                    Title = Strings.FolderLinkTitleAddChats;
                    Subtitle = Locale.Declension(Strings.R.FolderLinkSubtitleChats, inviteLink.MissingChatIds.Count, inviteLink.ChatFolderInfo.Title);

                    _bindButtonToSelection = true;
                    PrimaryButtonText = Locale.Declension(Strings.R.FolderLinkButtonJoinPlural, inviteLink.MissingChatIds.Count);
                }

                var missing = ClientService.GetChats(inviteLink.MissingChatIds);
                var added = ClientService.GetChats(inviteLink.AddedChatIds);

                if (inviteLink.MissingChatIds.Count == 0)
                {
                    Items.Add(new KeyedList<KeyedGroup, Chat>(new KeyedGroup { Title = Strings.FolderLinkHeaderAlready }, added));
                    SelectedItems.Clear();
                }
                else
                {
                    Items.Add(new KeyedList<KeyedGroup, Chat>(null, missing));
                    Items.Add(new KeyedList<KeyedGroup, Chat>(new KeyedGroup { Title = Strings.FolderLinkHeaderAlready, Footer = Strings.FolderLinkHint }, added));
                    SelectedItems.ReplaceWith(Items[0]);
                }

                _joinedChats.Clear();
                _shareableChats = new(inviteLink.MissingChatIds);

                foreach (var chat in missing)
                {
                    if (ClientService.TryGetSupergroup(chat, out var supergroup))
                    {
                        if (supergroup.IsMember())
                        {
                            _joinedChats.Add(chat.Id);
                        }
                    }
                }
            }

            return Task.CompletedTask;
        }

        private readonly HashSet<long> _joinedChats = new();

        private HashSet<long> _shareableChats = new();

        private bool _bindButtonToSelection;

        public MvxObservableCollection<KeyedList<KeyedGroup, Chat>> Items { get; private set; } = new();

        public MvxObservableCollection<Chat> SelectedItems { get; private set; } = new();

        private string _title;
        public string Title
        {
            get => _title;
            set => Set(ref _title, value);
        }

        private string _subtitle;
        public string Subtitle
        {
            get => _subtitle;
            set => Set(ref _subtitle, value);
        }

        private string _primaryButtonText;
        public string PrimaryButtonText
        {
            get => _primaryButtonText;
            set => Set(ref _primaryButtonText, value);
        }

        public int TotalCount => _shareableChats.Count;

        public int SelectedCount => SelectedItems.Count;

        public bool CanBeAdded(Chat chat)
        {
            return _shareableChats.Contains(chat.Id)
                && !_joinedChats.Contains(chat.Id);
        }

        public void SelectAll()
        {
            if (SelectedItems.Count >= TotalCount)
            {
                for (int i = 0; i < SelectedItems.Count; i++)
                {
                    if (_joinedChats.Contains(SelectedItems[i].Id))
                    {
                        continue;
                    }

                    SelectedItems.RemoveAt(i);
                    i--;
                }
            }
            else
            {
                List<Chat> temp = null;

                foreach (var chat in Items[0])
                {
                    if (_shareableChats.Contains(chat.Id) && !SelectedItems.Contains(chat))
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
