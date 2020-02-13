using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Services;
using Unigram.Views.Filters;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Filters
{
    public class FiltersViewModel : TLViewModelBase
    {
        public FiltersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ChatFilter>();

            EditCommand = new RelayCommand<ChatFilter>(EditExecute);
            AddCommand = new RelayCommand(AddExecute);
        }

        public MvxObservableCollection<ChatFilter> Items { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var response = await ProtoService.SendAsync(new GetChatFilters());
            if (response is ChatFilters filters)
            {
                Items.ReplaceWith(filters.FiltersValue);
            }
        }

        public RelayCommand<ChatFilter> EditCommand { get; }
        private void EditExecute(ChatFilter filter)
        {
            NavigationService.Navigate(typeof(FilterPage), filter.Id);
        }

        public RelayCommand AddCommand { get; }
        private void AddExecute()
        {
            NavigationService.Navigate(typeof(FilterPage));
        }
    }
}
