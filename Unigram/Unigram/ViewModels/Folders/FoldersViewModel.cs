using System.Collections.Generic;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Services;
using Unigram.Views.Folders;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Folders
{
    public class FoldersViewModel : TLViewModelBase, IHandle<UpdateChatFilters>
    {
        public FoldersViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<ChatFilterInfo>();
            Recommended = new MvxObservableCollection<RecommendedChatFilter>();

            RecommendCommand = new RelayCommand<RecommendedChatFilter>(RecommendExecute);
            EditCommand = new RelayCommand<ChatFilterInfo>(EditExecute);
            DeleteCommand = new RelayCommand<ChatFilterInfo>(DeleteExecute);

            CreateCommand = new RelayCommand(CreateExecute);
        }

        public MvxObservableCollection<ChatFilterInfo> Items { get; private set; }
        public MvxObservableCollection<RecommendedChatFilter> Recommended { get; private set; }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Items.ReplaceWith(CacheService.ChatFilters);

            var response = await ProtoService.SendAsync(new GetRecommendedChatFilters());
            if (response is RecommendedChatFilters filters)
            {
                Recommended.ReplaceWith(filters.ChatFilters);
            }

            Aggregator.Subscribe(this);
        }

        public override Task OnNavigatedFromAsync(IDictionary<string, object> pageState, bool suspending)
        {
            Aggregator.Unsubscribe(this);
            return base.OnNavigatedFromAsync(pageState, suspending);
        }

        public void Handle(UpdateChatFilters filters)
        {
            BeginOnUIThread(async () =>
            {
                Items.ReplaceWith(CacheService.ChatFilters);

                var response = await ProtoService.SendAsync(new GetRecommendedChatFilters());
                if (response is RecommendedChatFilters filters)
                {
                    Recommended.ReplaceWith(filters.ChatFilters);
                }
            });
        }

        public RelayCommand<RecommendedChatFilter> RecommendCommand { get; }
        private void RecommendExecute(RecommendedChatFilter filter)
        {
            Recommended.Remove(filter);
            ProtoService.Send(new CreateChatFilter(filter.Filter));
        }

        public RelayCommand<ChatFilterInfo> EditCommand { get; }
        private void EditExecute(ChatFilterInfo filter)
        {
            NavigationService.Navigate(typeof(FolderPage), filter.ChatFilterId);
        }

        public RelayCommand<ChatFilterInfo> DeleteCommand { get; }
        private async void DeleteExecute(ChatFilterInfo filter)
        {
            var confirm = await TLMessageDialog.ShowAsync(Strings.Resources.FilterDeleteAlert, Strings.Resources.FilterDelete, Strings.Resources.Delete, Strings.Resources.Cancel);
            if (confirm != ContentDialogResult.Primary)
            {
                return;
            }

            ProtoService.Send(new DeleteChatFilter(filter.ChatFilterId));
        }

        public RelayCommand CreateCommand { get; }
        private void CreateExecute()
        {
            NavigationService.Navigate(typeof(FolderPage));
        }
    }
}
