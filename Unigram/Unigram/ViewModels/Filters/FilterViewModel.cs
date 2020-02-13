using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Controls.Views;
using Unigram.Services;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels.Filters
{
    public class FilterViewModel : TLViewModelBase
    {
        public FilterViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            IncludeChats = new MvxObservableCollection<Chat>();

            AddChatsCommand = new RelayCommand(AddChatsExecute);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is int id)
            {
                var response = await ProtoService.SendAsync(new GetChatFilters());
                if (response is ChatFilters filters)
                {
                    var filter = filters.FiltersValue.FirstOrDefault(x => x.Id == id);
                    if (filter == null)
                    {
                        return;
                    }

                    Id = filter.Id;
                    Title = filter.Title;

                    IncludePrivate = filter.IncludePrivate;
                    IncludeSecret = filter.IncludeSecret;
                    IncludePrivateGroups = filter.IncludePrivateGroups;
                    IncludePublicGroups = filter.IncludePublicGroups;
                    IncludeChannels = filter.IncludeChannels;
                    IncludeBots = filter.IncludeBots;

                    ExcludeMuted = filter.ExcludeMuted;
                    ExcludeRead = filter.ExcludeRead;

                    IncludeChats.Clear();

                    foreach (var chatId in filter.IncludeChats)
                    {
                        var chat = CacheService.GetChat(chatId);
                        if (chat == null)
                        {
                            continue;
                        }

                        IncludeChats.Add(chat);
                    }
                }
            }
        }

        public int? Id { get; set; }
        public string Title { get; set; }

        public bool IncludePrivate { get; set; } = true;
        public bool IncludeSecret { get; set; } = true;
        public bool IncludePrivateGroups { get; set; } = true;
        public bool IncludePublicGroups { get; set; } = true;
        public bool IncludeChannels { get; set; } = true;
        public bool IncludeBots { get; set; } = true;

        public bool ExcludeMuted { get; set; }
        public bool ExcludeRead { get; set; }

        public MvxObservableCollection<Chat> IncludeChats { get; private set; }

        

        public RelayCommand AddChatsCommand { get; }
        private async void AddChatsExecute()
        {
            var dialog = ShareView.GetForCurrentView();
            var confirm = await dialog.PickAsync(IncludeChats.Select(x => x.Id).ToArray(), SearchChatsType.All);
            if (confirm != Windows.UI.Xaml.Controls.ContentDialogResult.Primary)
            {
                return;
            }

            foreach (var chat in dialog.ViewModel.SelectedItems)
            {
                var already = IncludeChats.FirstOrDefault(x => x.Id == chat.Id);
                if (already != null)
                {
                    continue;
                }

                IncludeChats.Add(chat);
            }
        }
    }
}
