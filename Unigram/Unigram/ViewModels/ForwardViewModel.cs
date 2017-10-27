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
using Unigram.Core.Common;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class ForwardViewModel : UnigramViewModelBase
    {
        public ForwardViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            Items = new MvxObservableCollection<TLDialog>();
            GroupedItems = new MvxObservableCollection<ForwardViewModel> { this };

            SendCommand = new RelayCommand(SendExecute, () => SelectedItem != null);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var dialogs = CacheService.GetDialogs();
            var self = dialogs.FirstOrDefault(x => x.With is TLUser user && user.IsSelf);
            if (self == null)
            {
                var user = CacheService.GetUser(SettingsHelper.UserId);
                if (user == null)
                {
                    var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                    if (response.IsSucceeded)
                    {
                        user = response.Result.FirstOrDefault() as TLUser;
                    }
                }

                if (user != null)
                {
                    self = new TLDialog { With = user, Peer = user.ToPeer() };
                }
            }

            if (self != null)
            {
                dialogs.Remove(self);
                dialogs.Insert(0, self);
            }

            Items.ReplaceWith(dialogs);
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

        public List<TLMessage> Messages { get; set; }

        public TLKeyboardButtonSwitchInline SwitchInline { get; set; }
        public TLUser SwitchInlineBot { get; set; }

        public string SendMessage { get; set; }
        public bool SendMessageUrl { get; set; }

        //public DialogsViewModel Dialogs { get; private set; }

        public MvxObservableCollection<TLDialog> Items { get; private set; }
        public MvxObservableCollection<ForwardViewModel> GroupedItems { get; private set; }



        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            var messages = Messages?.ToList();
            var switchInline = SwitchInline;
            var switchInlineBot = SwitchInlineBot;
            var sendMessage = SendMessage;
            var sendMessageUrl = SendMessageUrl;

            NavigationService.GoBack();

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null)
            {
                App.InMemoryState.ForwardMessages = messages;
                App.InMemoryState.SwitchInline = switchInline;
                App.InMemoryState.SwitchInlineBot = switchInlineBot;
                App.InMemoryState.SendMessage = sendMessage;
                App.InMemoryState.SendMessageUrl = sendMessageUrl;
                service.NavigateToDialog(_selectedItem.With);
            }
        }
    }
}
