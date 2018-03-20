using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Td.Api;
using Template10.Common;
using Unigram.Common;
using Unigram.Core.Common;
using Windows.UI.Xaml.Navigation;
using Unigram.Services;

namespace Unigram.ViewModels
{
    public class ForwardViewModel : UnigramViewModelBase
    {
        public ForwardViewModel(IProtoService protoService, ICacheService cacheService, ISettingsService settingsService, IEventAggregator aggregator)
            : base(protoService, cacheService, settingsService, aggregator)
        {
            Items = new MvxObservableCollection<Chat>();

            SendCommand = new RelayCommand(SendExecute, () => SelectedItem != null);
        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            //var dialogs = CacheService.GetDialogs().Select(x => x.With).ToList();
            //if (dialogs.IsEmpty())
            //{
            //    // TODO: request
            //}

            //for (int i = 0; i < dialogs.Count; i++)
            //{
            //    if (dialogs[i] is TLChannel channel && (channel.IsBroadcast && !(channel.IsCreator || (channel.HasAdminRights && channel.AdminRights != null && channel.AdminRights.IsPostMessages))))
            //    {
            //        dialogs.RemoveAt(i);
            //        i--;
            //    }
            //}

            //var self = dialogs.FirstOrDefault(x => x is TLUser user && user.IsSelf);
            //if (self == null)
            //{
            //    var user = CacheService.GetUser(SettingsHelper.UserId);
            //    if (user == null)
            //    {
            //        var response = await LegacyService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
            //        if (response.IsSucceeded)
            //        {
            //            user = response.Result.FirstOrDefault() as TLUser;
            //        }
            //    }

            //    if (user != null)
            //    {
            //        self = user;
            //    }
            //}

            //if (self != null)
            //{
            //    dialogs.Remove(self);
            //    dialogs.Insert(0, self);
            //}

            //Items.ReplaceWith(dialogs);
        }

        private Chat _selectedItem;
        public Chat SelectedItem
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

        public InlineKeyboardButtonTypeSwitchInline SwitchInline { get; set; }
        public User SwitchInlineBot { get; set; }

        public string SendMessage { get; set; }
        public bool SendMessageUrl { get; set; }

        //public DialogsViewModel Dialogs { get; private set; }

        public MvxObservableCollection<Chat> Items { get; private set; }



        public RelayCommand SendCommand { get; }
        private void SendExecute()
        {
            var switchInline = SwitchInline;
            var switchInlineBot = SwitchInlineBot;
            var sendMessage = SendMessage;
            var sendMessageUrl = SendMessageUrl;

            NavigationService.GoBack();

            var service = WindowWrapper.Current().NavigationServices.GetByFrameId("Main");
            if (service != null)
            {
                App.InMemoryState.SwitchInline = switchInline;
                App.InMemoryState.SwitchInlineBot = switchInlineBot;
                App.InMemoryState.SendMessage = sendMessage;
                App.InMemoryState.SendMessageUrl = sendMessageUrl;
                //service.NavigateToChat(ProtoService, _selectedItem);
            }
        }
    }
}
