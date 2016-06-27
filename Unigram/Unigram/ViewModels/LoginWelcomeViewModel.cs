using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Views;

namespace Unigram.ViewModels
{
    public class LoginWelcomeViewModel : UnigramViewModelBase
    {

        public LoginWelcomeViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator) : base(protoService, cacheService, aggregator)
        {
        }

        public RelayCommand ContinueCommand => new RelayCommand(ContinueExecute);
        private void ContinueExecute()
        {
                NavigationService.Navigate(typeof(LoginPhoneNumberPage));
        }
    }
}
