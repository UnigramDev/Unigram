using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Template10.Common;
using Unigram.Common;
using Windows.ApplicationModel.DataTransfer;

namespace Unigram.ViewModels
{
    public class ForwardViewModel : UnigramViewModelBase
    {
        public ForwardViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, DialogsViewModel dialogs)
            : base(protoService, cacheService, aggregator)
        {
            Dialogs = dialogs;
            GroupedItems = new ObservableCollection<ForwardViewModel> { this };
        }

        private TLDialog _selectedItem;
        public TLDialog SelectedItem
        {
            get
            {
                return _selectedItem;
            }
            set
            {
                Set(ref _selectedItem, value);
                SendCommand.RaiseCanExecuteChanged();
            }
        }

        private List<TLMessage> _messages;
        public List<TLMessage> Messages
        {
            get
            {
                return _messages;
            }
            set
            {
                Set(ref _messages, value);
            }
        }

        private TLKeyboardButtonSwitchInline _switchInline;
        public TLKeyboardButtonSwitchInline SwitchInline
        {
            get
            {
                return _switchInline;
            }
            set
            {
                Set(ref _switchInline, value);
            }
        }

        private TLUser _switchInlineBot;
        public TLUser SwitchInlineBot
        {
            get
            {
                return _switchInlineBot;
            }
            set
            {
                Set(ref _switchInlineBot, value);
            }
        }

        public DialogsViewModel Dialogs { get; private set; }

        public ObservableCollection<ForwardViewModel> GroupedItems { get; private set; }



        private RelayCommand _sendCommand;
        public RelayCommand SendCommand => _sendCommand = (_sendCommand ?? new RelayCommand(SendExecute, () => SelectedItem != null));

        private void SendExecute()
        {
            var messages = _messages?.ToList();
            var switchInline = _switchInline;
            var switchInlineBot = _switchInlineBot;

            NavigationService.GoBack();

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null)
            {
                App.InMemoryState.ForwardMessages = messages;
                App.InMemoryState.SwitchInline = switchInline;
                App.InMemoryState.SwitchInlineBot = switchInlineBot;
                service.NavigateToDialog(_selectedItem.With);
            }
        }
    }
}
