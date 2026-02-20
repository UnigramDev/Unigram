//
// Copyright (c) Fela Ameghino 2015-2026
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//

using Rg.DiffUtils;
using System;
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Controls;
using Telegram.Navigation;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Td.Api;
using Telegram.Views.Stars.Popups;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Chats
{
    public partial class ChatAffiliateViewModel : ViewModelBase, IHandle, IIncrementalCollectionOwner, IDiffHandler<ConnectedAffiliateProgram>, IDiffHandler<FoundAffiliateProgram>
    {
        private AffiliateType _affiliateType;

        private ChatProgramsCollection _programs;
        private FoundProgramsCollection _foundPrograms;

        public ChatAffiliateViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            Programs = new SearchCollection<ConnectedAffiliateProgram, IncrementalCollection<ConnectedAffiliateProgram>>(UpdatePrograms, this);
            FoundPrograms = new SearchCollection<FoundAffiliateProgram, IncrementalCollection<FoundAffiliateProgram>>(UpdateFoundPrograms, this);
        }

        protected override Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState _)
        {
            if (parameter is AffiliateType affiliateType)
            {
                _affiliateType = affiliateType;

                Programs.Reload();
                FoundPrograms.UpdateSender(new AffiliateProgramSortOrderProfitability());
            }

            return Task.CompletedTask;
        }

        public override void Subscribe()
        {
            Aggregator.Subscribe<UpdateChatAffiliatePrograms>(this, Handle);
        }

        private void Handle(UpdateChatAffiliatePrograms update)
        {
            if (update.AffiliateType.AreTheSame(_affiliateType))
            {
                BeginOnUIThread(() =>
                {
                    Programs.Reload();
                    FoundPrograms.Reload();
                });
            }
        }

        public SearchCollection<ConnectedAffiliateProgram, IncrementalCollection<ConnectedAffiliateProgram>> Programs { get; }

        public SearchCollection<FoundAffiliateProgram, IncrementalCollection<FoundAffiliateProgram>> FoundPrograms { get; }

        private IncrementalCollection<ConnectedAffiliateProgram> UpdatePrograms(object arg1, string arg2)
        {
            _programs = new ChatProgramsCollection(ClientService, _affiliateType);
            return _programs?.Items;
        }

        private IncrementalCollection<FoundAffiliateProgram> UpdateFoundPrograms(object arg1, string arg2)
        {
            if (arg1 is AffiliateProgramSortOrder sortOrder)
            {
                _foundPrograms = new FoundProgramsCollection(ClientService, _affiliateType, this, sortOrder);
            }

            return _foundPrograms?.Items;
        }

        public AffiliateProgramSortOrder SortOrder
        {
            get => _foundPrograms?.SortOrder;
            set
            {
                FoundPrograms.UpdateSender(value);
                RaisePropertyChanged(nameof(SortOrder));
            }
        }

        public bool CompareItems(ConnectedAffiliateProgram oldItem, ConnectedAffiliateProgram newItem)
        {
            return oldItem?.BotUserId == newItem?.BotUserId;
        }

        public bool CompareItems(FoundAffiliateProgram oldItem, FoundAffiliateProgram newItem)
        {
            return oldItem?.BotUserId == newItem?.BotUserId;
        }

        public void UpdateItem(ConnectedAffiliateProgram oldItem, ConnectedAffiliateProgram newItem)
        {
            //throw new System.NotImplementedException();
        }

        public void UpdateItem(FoundAffiliateProgram oldItem, FoundAffiliateProgram newItem)
        {
            //throw new NotImplementedException();
        }

        public Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
        {
            if (Programs.HasMoreItems)
            {
                return Programs.LoadMoreItemsAsync(count).AsTask();
            }

            return _foundPrograms.LoadMoreItemsAsync(count);
        }

        public bool HasMoreItems => _foundPrograms.HasMoreItems || _programs.HasMoreItems;

        class ChatProgramsCollection : IIncrementalCollectionOwner
        {
            private readonly IClientService _clientService;
            private readonly AffiliateType _affiliateType;

            private string _nextOffset = string.Empty;

            public ChatProgramsCollection(IClientService clientService, AffiliateType affiliateType)
            {
                _clientService = clientService;
                _affiliateType = affiliateType;

                Items = new IncrementalCollection<ConnectedAffiliateProgram>(this);
            }

            public IncrementalCollection<ConnectedAffiliateProgram> Items { get; }

            public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;

                var response = await _clientService.SendAsync(new GetConnectedAffiliatePrograms(_affiliateType, _nextOffset, 20));
                if (response is ConnectedAffiliatePrograms programs)
                {
                    foreach (var item in programs.Programs)
                    {
                        Items.Add(item);
                        totalCount++;
                    }

                    _nextOffset = programs.NextOffset.Length > 0
                        ? programs.NextOffset
                        : null;
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItems => _nextOffset != null;
        }

        class FoundProgramsCollection : IIncrementalCollectionOwner
        {
            private readonly IClientService _clientService;
            private readonly AffiliateType _affiliateType;
            private readonly AffiliateProgramSortOrder _sortOrder;

            private string _nextOffset = string.Empty;

            public FoundProgramsCollection(IClientService clientService, AffiliateType affiliateType, IIncrementalCollectionOwner owner, AffiliateProgramSortOrder sortOrder)
            {
                _clientService = clientService;
                _affiliateType = affiliateType;
                _sortOrder = sortOrder;

                Items = new IncrementalCollection<FoundAffiliateProgram>(owner);
            }

            public IncrementalCollection<FoundAffiliateProgram> Items { get; }

            public AffiliateProgramSortOrder SortOrder => _sortOrder;

            public async Task<LoadMoreItemsResult> LoadMoreItemsAsync(uint count)
            {
                var totalCount = 0u;

                var response = await _clientService.SendAsync(new SearchAffiliatePrograms(_affiliateType, _sortOrder, _nextOffset, 20));
                if (response is FoundAffiliatePrograms programs)
                {
                    foreach (var item in programs.Programs)
                    {
                        Items.Add(item);
                        totalCount++;
                    }

                    _nextOffset = programs.NextOffset.Length > 0
                        ? programs.NextOffset
                        : null;
                }

                return new LoadMoreItemsResult
                {
                    Count = totalCount
                };
            }

            public bool HasMoreItems => _nextOffset != null;
        }

        public void OpenProgram(object item)
        {
            if (item is FoundAffiliateProgram foundProgram)
            {
                ShowPopup(new FoundAffiliateProgramPopup(ClientService, NavigationService, foundProgram, _affiliateType));
            }
            else if (item is ConnectedAffiliateProgram program)
            {
                ShowPopup(new ConnectedAffiliateProgramPopup(ClientService, NavigationService, program, _affiliateType));
            }
        }

        public void LaunchProgram(ConnectedAffiliateProgram program)
        {
            if (program == null || !ClientService.TryGetUser(program.BotUserId, out User user))
            {
                return;
            }

            if (user.Type is UserTypeBot { HasMainWebApp: true })
            {
                MessageHelper.NavigateToMainWebApp(ClientService, NavigationService, user, string.Empty, new WebAppOpenModeFullSize());
            }
            else
            {
                NavigationService.NavigateToUser(program.BotUserId);
            }
        }

        public void CopyProgram(ConnectedAffiliateProgram program)
        {
            MessageHelper.CopyLink(XamlRoot, program.Url);
        }

        public async void DisconnectProgram(ConnectedAffiliateProgram program)
        {
            if (program == null || !ClientService.TryGetUser(program.BotUserId, out User user))
            {
                return;
            }

            var confirm = await ShowPopupAsync(string.Format(Strings.LeaveAffiliateLinkAlert, user.FirstName), Strings.LeaveAffiliateLink, Strings.LeaveAffiliateLinkButton, Strings.Cancel, destructive: true);
            if (confirm == ContentDialogResult.Primary)
            {
                var response = await ClientService.SendAsync(new DisconnectAffiliateProgram(_affiliateType, program.Url));
                if (response is ConnectedAffiliateProgram)
                {
                    Programs.Reload();
                    FoundPrograms.Reload();
                }
                else if (response is Error error)
                {
                    ToastPopup.ShowError(XamlRoot, error);
                }
            }
        }
    }
}
