using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Telegram.Api.Aggregator;
using Telegram.Api.Services;
using Telegram.Api.Services.Cache;
using Unigram.Common;

namespace Unigram.ViewModels.Settings
{
    public class SettingsSecurityChangePasswordViewModel : UnigramViewModelBase
    {
        public SettingsSecurityChangePasswordViewModel(IMTProtoService protoService, ICacheService cacheService, ITelegramEventAggregator aggregator)
            : base(protoService, cacheService, aggregator)
        {
            SendCommand = new RelayCommand(SendExecute);
        }

        private string _password;
        public string Password
        {
            get
            {
                return _password;
            }
            set
            {
                Set(ref _password, value);
            }
        }

        private string _passwordAgain;
        public string PasswordAgain
        {
            get
            {
                return _passwordAgain;
            }
            set
            {
                Set(ref _passwordAgain, value);
            }
        }

        private string _passwordHint;
        public string PasswordHint
        {
            get
            {
                return _passwordHint;
            }
            set
            {
                Set(ref _passwordHint, value);
            }
        }

        private string _recoveryEmail;
        public string RecoveryEmail
        {
            get
            {
                return _recoveryEmail;
            }
            set
            {
                Set(ref _recoveryEmail, value);
            }
        }

        public RelayCommand SendCommand { get; }
        private async void SendExecute()
        {
            //ProtoService.UpdatePasswordSettingsAsync()
        }
    }
}
