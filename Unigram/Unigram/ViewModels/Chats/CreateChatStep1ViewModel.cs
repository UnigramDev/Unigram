using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;
using Unigram.Views.Chats;

namespace Unigram.ViewModels.Chats
{
    public class CreateChatStep1ViewModel : UnigramViewModelBase
    {
        public CreateChatStep1ViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute, () => !string.IsNullOrWhiteSpace(Title));
        }

        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                Set(ref _title, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            NavigationService.Navigate(typeof(CreateChatStep2Page), _title);
        }
    }
}
