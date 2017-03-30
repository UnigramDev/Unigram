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
using Telegram.Api.TL.Methods.Contacts;
using Unigram.Collections;
using Unigram.Common;
using Unigram.Converters;
using Unigram.Core.Notifications;
using Unigram.Core.Services;
using Unigram.Views;
using Windows.ApplicationModel.Background;
using Windows.Globalization.DateTimeFormatting;
using Windows.Networking.PushNotifications;
using Windows.Security.Authentication.Web;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
    public class MainViewModel : UnigramViewModelBase
    {
        private readonly IPushService _pushService;

        public MainViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator, IPushService pushService, IContactsService contactsService)
            : base(protoService, cacheService, aggregator)
        {
            _pushService = pushService;

            //Dialogs = new DialogCollection(protoService, cacheService);
            SearchDialogs = new ObservableCollection<TLDialog>();
            Dialogs = new DialogsViewModel(protoService, cacheService, aggregator);
            Contacts = new ContactsViewModel(protoService, cacheService, aggregator, contactsService);
            Calls = new CallsViewModel(protoService, cacheService, aggregator);
        }

        public override Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            Task.Run(() => _pushService.RegisterAsync());

            Execute.BeginOnUIThread(() => Calls.OnNavigatedToAsync(parameter, mode, state));
            //Execute.BeginOnUIThread(() => Dialogs.LoadFirstSlice());
            //Execute.BeginOnUIThread(() => Contacts.getTLContacts());
            //Execute.BeginOnUIThread(() => Contacts.GetSelfAsync());

            return Task.CompletedTask;
        }

        //END OF EXPERIMENTS
        //public DialogCollection Dialogs { get; private set; }

        public ObservableCollection<TLDialog> SearchDialogs { get; private set; }

        public DialogsViewModel Dialogs { get; private set; }

        public ContactsViewModel Contacts { get; private set; }

        public CallsViewModel Calls { get; private set; }
    }
}
