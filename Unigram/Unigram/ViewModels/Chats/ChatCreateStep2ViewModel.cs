using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Template10.Common;
using Template10.Services.NavigationService;
using Unigram.Common;
using Unigram.Views;
using Windows.UI.Xaml.Navigation;
using Windows.Storage;
using ChatCreateStep2Tuple = System.Tuple<string, object>;
using System.Runtime.InteropServices.WindowsRuntime;
using Telegram.Td.Api;
using Unigram.Services;

namespace Unigram.ViewModels.Chats
{
    public class ChatCreateStep2ViewModel : UsersSelectionViewModel
    {
        private string _title;

        public ChatCreateStep2ViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            _maximum = CacheService.Options.SupergroupSizeMax;
        }

        public override string Title => _title;

        private int _maximum;
        public override int Maximum { get { return _maximum; } }

        public override int Minimum { get { return 1; } }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            if (parameter is ChatCreateStep2Tuple tuple)
            {
                _title = tuple.Item1;
                //_photo = tuple.Item2;
            }


            RaisePropertyChanged(() => Title);
            return base.OnNavigatedToAsync(parameter, mode, state);
        }

        protected override async void SendExecute()
        {
            var maxSize = CacheService.Options.BasicGroupSizeMax;

            var peers = SelectedItems.Select(x => x.Id).ToList();
            if (peers.Count <= maxSize)
            {
                // Classic chat
                var response = await ProtoService.SendAsync(new CreateNewBasicGroupChat(peers, _title));
                if (response is Chat chat)
                {
                    // TODO: photo

                    NavigationService.NavigateToChat(chat);
                    NavigationService.RemoveLast();
                    NavigationService.RemoveLast();
                }
                else if (response is Error error)
                {
                    AlertsService.ShowAddUserAlert(Dispatcher, error.Message, false);
                }
            }
            else
            {
            }
        }
    }
}
