//
// Copyright Fela Ameghino 2015-2023
//
// Distributed under the GNU General Public License v3.0. (See accompanying
// file LICENSE or copy at https://www.gnu.org/licenses/gpl-3.0.txt)
//
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Navigation.Services;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views.Folders;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Folders
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

            RecommendCommand = new RelayCommand<RecommendedChatFilter>(RecommendExecute);
            EditCommand = new RelayCommand<ChatFilterInfo>(EditExecute);
            DeleteCommand = new RelayCommand<ChatFilterInfo>(DeleteExecute);

            CreateCommand = new RelayCommand(CreateExecute);
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

        public RelayCommand<RecommendedChatFilter> RecommendCommand { get; }
        private void RecommendExecute(RecommendedChatFilter filter)
        {
            Recommended.Remove(filter);
            ClientService.Send(new CreateChatFilter(filter.Filter));
        }

        public RelayCommand<ChatFilterInfo> EditCommand { get; }
        private void EditExecute(ChatFilterInfo filter)
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

        public RelayCommand<ChatFilterInfo> DeleteCommand { get; }
        private async void DeleteExecute(ChatFilterInfo filter)
        {
            var confirm = await ShowPopupAsync(Strings.Resources.FilterDeleteAlert, Strings.Resources.FilterDelete, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ClientService.Send(new DeleteChatFilter(filter.Id));
        }

        public RelayCommand CreateCommand { get; }
        private void CreateExecute()
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
