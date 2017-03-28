using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Helpers;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Telegram.Api.TL;
using Unigram.Common;
using Unigram.Controls;
using Unigram.Converters;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace Unigram.ViewModels
{
   public class SettingsViewModel : UnigramViewModelBase
    {
        public SettingsViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) 
            : base(protoService, cacheService, aggregator)
        {

        }

        public override async Task OnNavigatedToAsync(object parameter, NavigationMode mode, IDictionary<string, object> state)
        {
            var cached = CacheService.GetUser(SettingsHelper.UserId) as TLUser;
            if (cached != null)
            {
                Self = cached;
            }
            else
            {
                var response = await ProtoService.GetUsersAsync(new TLVector<TLInputUserBase> { new TLInputUserSelf() });
                if (response.IsSucceeded)
                {
                    var user = response.Result.FirstOrDefault() as TLUser;
                    if (user != null)
                    {
                        Self = user;
                    }
                }
            }
        }

        private TLUser _self;
        public TLUser Self
        {
            get
            {
                return _self;
            }
            set
            {
                Set(ref _self, value);
            }
        }

#if DEBUG

        public RelayCommand DeleteAccountCommand => new RelayCommand(DeleteAccountExecute);
        private async void DeleteAccountExecute()
        {
            // THIS CODE WILL RUN ONLY IF FIRST CONFIGURED SERVER IP IS TEST SERVER
            if (Telegram.Api.Constants.FirstServerIpAddress.Equals("149.154.167.40"))
            {
                var dialog = new InputDialog();
                var confirm = await dialog.ShowAsync();
                if (confirm == ContentDialogResult.Primary && dialog.Text.Equals(Self.Phone) && Self.Username != "frayxrulez")
                {
                    var really = await TLMessageDialog.ShowAsync("REAAAALLY???", "REALLYYYY???", "YES", "NO I DON'T WANT TO");
                    if (really == ContentDialogResult.Primary)
                    {
                        await ProtoService.DeleteAccountAsync("Testing registration");
                        App.Current.Exit();
                    }
                }
            }
        }

#endif
    }
}
