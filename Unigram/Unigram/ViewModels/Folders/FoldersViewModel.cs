using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Services.Updates;
using Unigram.Views.Folders;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Folders
{
    public class FoldersViewModel : TLViewModelBase
    {
        public FoldersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ChatListFilter>();
            Suggestions = new MvxObservableCollection<ChatListFilterSuggestion>();

            EditCommand = new RelayCommand<ChatListFilter>(EditExecute);
            AddCommand = new RelayCommand(AddExecute);
        }

        public MvxObservableCollection<ChatListFilter> Items { get; private set; }
        public MvxObservableCollection<ChatListFilterSuggestion> Suggestions { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetChatListFilters());
            if (response is ChatListFilters filters)
            {
                Items.ReplaceWith(filters.Filters);
            }

            response = await ProtoService.SendAsync(new GetChatListFilterSuggestons());
            if (response is ChatListFilterSuggestions suggestions)
            {
                Suggestions.ReplaceWith(suggestions.Suggestions);
            }
        }

        public RelayCommand<ChatListFilter> EditCommand { get; }
        private void EditExecute(ChatListFilter filter)
        {
            NavigationService.Navigate(typeof(FolderPage), filter.Id);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            NavigationService.Navigate(typeof(FolderPage));
        }
    }
}
