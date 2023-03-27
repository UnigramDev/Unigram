//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Collections;
using Telegram.Common;
using Telegram.Navigation.Services;
using Telegram.Services;
using Telegram.Services.Updates;
using Telegram.Td.Api;
using Telegram.Views.Folders;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Telegram.ViewModels.Folders
{
    public class FoldersViewModel : TLViewModelBase
        , IHandle
    //, IHandle<UpdateChatFilters>
    {
        public FoldersViewModel(IClientService clientService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(clientService, settingsService, aggregator)
        {
            UseLeftLayout = settingsService.IsLeftTabsEnabled;
            UseTopLayout = !settingsService.IsLeftTabsEnabled;

            Items = new MvxObservableCollection<ChatFilterInfo>();
            Recommended = new MvxObservableCollection<RecommendedChatFilter>();
        }

        public MvxObservableCollection<ChatFilterInfo> Items { get; private set; }
        public MvxObservableCollection<RecommendedChatFilter> Recommended { get; private set; }

        private bool _canCreateNew;
        public bool CanCreateNew
        {
            get => _canCreateNew;
            set => Set(ref _canCreateNew, value);
        }

        protected override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, NavigationState state)
        {
            Items.ReplaceWith(ClientService.ChatFilters);

            if (ClientService.IsPremiumAvailable)
            {
                var limit = await ClientService.SendAsync(new GetPremiumLimit(new PremiumLimitTypeChatFilterCount())) as PremiumLimit;
                CanCreateNew = Items.Count < limit.PremiumValue;
            }
            else
            {
                CanCreateNew = Items.Count < ClientService.Options.ChatFilterCountMax;
            }

            if (ClientService.Options.ChatFilterCountMax > Items.Count)
            {
                var response = await ClientService.SendAsync(new GetRecommendedChatFilters());
                if (response is RecommendedChatFilters filters)
                {
                    Recommended.ReplaceWith(filters.ChatFilters);
                }
            }
            else
            {
                Recommended.Clear();
            }

            Aggregator.Subscribe<UpdateChatFilters>(this, Handle);
        }

        private bool _useLeftLayout;
        public bool UseLeftLayout
        {
            get => _useLeftLayout;
            set
            {
                if (_useLeftLayout != value)
                {
                    _useLeftLayout = value;
                    Settings.IsLeftTabsEnabled = value;

                    RaisePropertyChanged(nameof(UseLeftLayout));

                    Aggregator.Publish(new UpdateChatFiltersLayout());
                }
            }
        }

        private bool _useTopLayout;
        public bool UseTopLayout
        {
            get => _useTopLayout;
            set => Set(ref _useTopLayout, value);
        }

        public void Handle(UpdateChatFilters update)
        {
            BeginOnUIThread(async () =>
            {
                Items.ReplaceWith(update.ChatFilters);

                if (Items.Count < 10)
                {
                    var response = await ClientService.SendAsync(new GetRecommendedChatFilters());
                    if (response is RecommendedChatFilters recommended)
                    {
                        Recommended.ReplaceWith(recommended.ChatFilters);
                    }
                }
                else
                {
                    Recommended.Clear();
                }
            });
        }

        public void AddRecommended(RecommendedChatFilter filter)
        {
            Recommended.Remove(filter);
            ClientService.Send(new CreateChatFilter(filter.Filter));
        }

        public void Edit(ChatFilterInfo filter)
        {
            var index = Items.IndexOf(filter);
            if (index < ClientService.Options.ChatFilterCountMax)
            {
                NavigationService.Navigate(typeof(FolderPage), filter.Id);
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeChatFilterCount());
            }
        }

        public async void Delete(ChatFilterInfo filter)
        {
            var confirm = await ShowPopupAsync(Strings.FilterDeleteAlert, Strings.FilterDelete, Strings.Delete, Strings.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new DeleteChatFilter(filter.Id));
        }

        public void Create()
        {
            if (Items.Count < ClientService.Options.ChatFilterCountMax)
            {
                NavigationService.Navigate(typeof(FolderPage));
            }
            else
            {
                NavigationService.ShowLimitReached(new PremiumLimitTypeChatFilterCount());
            }
        }
    }
}
